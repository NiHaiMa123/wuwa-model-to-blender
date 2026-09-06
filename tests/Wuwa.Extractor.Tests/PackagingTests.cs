using System.Text.RegularExpressions;
using Wuwa.Core;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class PackagingTests
{
    private static readonly string[] AddonModules =
    [
        "__init__.py",
        "importer.py",
        "manifest_io.py",
        "materials.py",
        "operators.py",
        "pipeline.py",
        "rigging.py",
        "ui.py",
        "validation.py"
    ];

    [Fact]
    public void ToolVersion_MatchesDirectoryBuildProps()
    {
        var props = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Directory.Build.props"));
        Assert.Contains($"<InformationalVersion>{ToolVersions.Tool}</InformationalVersion>", props, StringComparison.Ordinal);
        var pipeline = File.ReadAllText(Path.Combine(RepoPaths.Root(), "blender", "addon", "wuwa_model_tools", "pipeline.py"));
        Assert.Contains($"TOOL_VERSION = \"{ToolVersions.Tool}\"", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void PackScript_PublishesWinX64SelfContainedAndForbidsGameAssets()
    {
        var pack = File.ReadAllText(Path.Combine(RepoPaths.Root(), "tools", "pack.ps1"));
        Assert.Contains("dotnet publish", pack, StringComparison.Ordinal);
        Assert.Contains("-r win-x64", pack, StringComparison.Ordinal);
        Assert.Contains("--self-contained true", pack, StringComparison.Ordinal);
        Assert.Contains("wuwa2blender-win-x64", pack, StringComparison.Ordinal);
        Assert.Contains("$cliName.zip", pack, StringComparison.Ordinal);
        Assert.Contains("wuwa_model_tools.zip", pack, StringComparison.Ordinal);
        Assert.Contains("wuwa.local.json", pack, StringComparison.Ordinal);
        Assert.Contains("work/", pack, StringComparison.Ordinal);
        Assert.Contains(".pak", pack, StringComparison.Ordinal);
        Assert.Contains(".usmap", pack, StringComparison.Ordinal);
        Assert.DoesNotContain("WUWA_INTEGRATION_TESTS", pack, StringComparison.Ordinal);
    }

    [Fact]
    public void CiWorkflow_RunsBlenderIndependentChecksAndDoesNotUploadWork()
    {
        var yaml = File.ReadAllText(Path.Combine(RepoPaths.Root(), ".github", "workflows", "ci.yml"));
        Assert.Contains("dotnet restore", yaml, StringComparison.Ordinal);
        Assert.Contains("dotnet build", yaml, StringComparison.Ordinal);
        Assert.Contains("dotnet test", yaml, StringComparison.Ordinal);
        Assert.Contains("compileall", yaml, StringComparison.Ordinal);
        Assert.Contains("ruff check", yaml, StringComparison.Ordinal);
        Assert.Contains("test_schemas.py", yaml, StringComparison.Ordinal);
        Assert.Contains("tools/pack.ps1", yaml, StringComparison.Ordinal);
        Assert.Contains("dist/wuwa2blender-win-x64.zip", yaml, StringComparison.Ordinal);
        Assert.Contains("dist/wuwa_model_tools.zip", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("WUWA_INTEGRATION_TESTS", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("WUWA_BLENDER_SMOKE", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("path: work", yaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wuwa.local.json", yaml, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"D:\\\\game", RegexOptions.IgnoreCase), yaml);
    }

    [Fact]
    public void AddonSources_AreCompleteAndHaveNoGameAssets()
    {
        var addon = Path.Combine(RepoPaths.Root(), "blender", "addon", "wuwa_model_tools");
        foreach (var module in AddonModules)
        {
            Assert.True(File.Exists(Path.Combine(addon, module)), module);
        }

        var forbidden = Directory.EnumerateFiles(addon, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var ext = Path.GetExtension(path);
                return ext is ".pak" or ".uemodel" or ".ueanim" or ".usmap" or ".blend" or ".png";
            })
            .ToArray();
        Assert.Empty(forbidden);
        Assert.True(File.Exists(BlenderLaunch.DefaultScriptPath(RepoPaths.Root())));
        Assert.True(File.Exists(BlenderLaunch.DefaultProfilePath(RepoPaths.Root())));
        Assert.True(File.Exists(Path.Combine(RepoPaths.Root(), "config", "wuwa.example.json")));
        Assert.False(Path.GetFileName(Path.Combine(RepoPaths.Root(), "config", "wuwa.example.json"))
            .Equals("wuwa.local.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CommittedSmokeManifest_UsesCurrentToolVersion()
    {
        var json = File.ReadAllText(Path.Combine(RepoPaths.SmokeFixtureDir(), "manifest.json"));
        Assert.Contains($"\"wuwa2blender\": \"{ToolVersions.Tool}\"", json, StringComparison.Ordinal);
    }
}
