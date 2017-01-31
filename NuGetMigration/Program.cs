namespace NuGetMigration
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                return;
            }

            var project = new Project(args[0]);

            project.MigrateToMSBuild();
        }
    }
}
