using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace NuGetMigration
{
    public class NuGetPackage
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public List<NuGetPackageDependency> Dependencies { get; set; }

        public static NuGetPackage Load(string path)
        {
            using (var zipFile = ZipFile.Open(path, ZipArchiveMode.Read))
            {
                var nuspecEntry = zipFile.Entries.First(xs => xs.FullName.EndsWith(".nuspec"));

                var document = XDocument.Load(nuspecEntry.Open());

                var xmlns = XNamespace.Get((string)document.Root.Attribute("xmlns") ?? (string)((XElement)document.Root.FirstNode).Attribute("xmlns"));
                var metadata = document.Root.Element(xmlns + "metadata");

                return new NuGetPackage
                {
                    Id = (string)metadata.Element(xmlns + "id"),
                    Version = (string)metadata.Element(xmlns + "version"),
                    Dependencies = metadata.Descendants(xmlns + "dependency")
                                           .Select(x => new NuGetPackageDependency
                                           {
                                               Id = (string)x.Attribute("id"),
                                               Version = (string)x.Attribute("version")
                                           })
                                           .ToList()
                };
            }
        }
    }
}