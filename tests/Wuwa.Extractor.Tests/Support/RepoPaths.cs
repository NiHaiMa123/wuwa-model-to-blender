namespace Wuwa.Extractor.Tests;

internal static class RepoPaths
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PLAN.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output.");
    }

    public static string SmokeFixtureDir()
        => Path.Combine(Root(), "tests", "fixtures", "ueformat-smoke");

    public static string LocalConfig()
        => Path.Combine(Root(), "config", "wuwa.local.json");

    public static string MaterialProfile()
        => Path.Combine(Root(), "config", "material-profiles", "3x.json");

    public static string SearchAliases()
        => Path.Combine(Root(), "config", "search-aliases.json");

    public static string PythonManifestTests()
        => Path.Combine(Root(), "tests", "python", "test_manifest_io.py");

    public static string PythonTestsDir()
        => Path.Combine(Root(), "tests", "python");
}
