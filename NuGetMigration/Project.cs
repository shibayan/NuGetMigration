using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Microsoft.VisualStudio.GraphModel;

namespace NuGetMigration
{
    public class Project
    {
        public Project(string path)
        {
            _path = path;
            _project = XDocument.Load(path);
        }

        private readonly string _path;
        private readonly XDocument _project;

        private readonly Graph _graph = new Graph();

        private static readonly XNamespace xmlns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");

        public void MigrateToMSBuild()
        {
            var installedPackages = GetInstalledPackages();

            BuildDependencyGraph(installedPackages);

            RemoveOldReference(installedPackages);

            AddPackageReference(installedPackages);
            
            RemovePackageConfig();

            _project.Save(_path);
        }

        private List<PackageReference> GetInstalledPackages()
        {
            return XDocument.Load(Path.Combine(Path.GetDirectoryName(_path), "packages.config"))
                            .Descendants("package")
                            .Select(x => new PackageReference
                            {
                                Id = (string)x.Attribute("id"),
                                Version = (string)x.Attribute("version"),
                                TargetFramework = (string)x.Attribute("targetFramework")
                            })
                            .ToList();
        }

        private string FindPackageRepository(string path)
        {
            var directory = Path.GetDirectoryName(path);

            while (true)
            {
                var packagePath = Path.Combine(directory, "packages");

                if (Directory.Exists(packagePath))
                {
                    return packagePath;
                }

                var dirInfo = Directory.GetParent(directory);

                if (dirInfo == null)
                {
                    break;
                }

                directory = dirInfo.FullName;
            }

            throw new DirectoryNotFoundException();
        }

        private void BuildDependencyGraph(List<PackageReference> installedPackages)
        {
            // NuGet Package の依存関係グラフを作成
            var repository = Directory.EnumerateDirectories(FindPackageRepository(_path))
                                      .Select(x => NuGetPackage.Load(Path.Combine(x, Path.GetFileName(x) + ".nupkg")))
                                      .Where(x => installedPackages.Any(xs => xs.Id == x.Id && xs.Version == x.Version))
                                      .ToList();

            repository.ForEach(x => _graph.Nodes.GetOrCreate(x.Id));

            foreach (var package in repository.Where(x => x.Dependencies.Count > 0))
            {
                var node = _graph.Nodes.Get(package.Id);

                package.Dependencies.ForEach(x => _graph.Links.GetOrCreate(node, _graph.Nodes.GetOrCreate(x.Id)));
            }
        }

        private void RemoveOldReference(List<PackageReference> installedPackages)
        {
            // 既存の NuGet Reference を削除
            var references = _project.Descendants(xmlns + "Reference")
                                     .Where(x => x.Element(xmlns + "HintPath") != null)
                                     .ToArray();

            foreach (var reference in references)
            {
                var hintPath = (string)reference.Element(xmlns + "HintPath");

                if (installedPackages.Any(x => hintPath.IndexOf(x.Id, StringComparison.OrdinalIgnoreCase) != -1))
                {
                    reference.Remove();
                }
            }
        }

        private void AddPackageReference(List<PackageReference> installedPackages)
        {
            // 新しく PackageReference を追加
            var packages = _graph.Nodes
                                 .Where(x => x.IncomingLinkCount == 0)
                                 .Select(x => installedPackages.First(xs => xs.Id == x.Label))
                                 .ToArray();

            var itemGroup = new XElement(xmlns + "ItemGroup");

            foreach (var package in packages)
            {
                itemGroup.Add(new XElement(xmlns + "PackageReference",
                    new XAttribute("Include", package.Id),
                    new XElement(xmlns + "Version", package.Version)
                ));
            }

            _project.Root.Add(itemGroup);
        }

        private void RemovePackageConfig()
        {
            // packages.config の参照を削除
            _project.Descendants(xmlns + "None")
                    .FirstOrDefault(x => (string)x.Attribute("Include") == "packages.config")
                    ?.Remove();

            _project.Descendants(xmlns + "Content")
                    .FirstOrDefault(x => (string)x.Attribute("Include") == "packages.config")
                    ?.Remove();

            // packages.config 自体を削除
            File.Delete(Path.Combine(Path.GetDirectoryName(_path), "packages.config"));
        }
    }
}