using Wuwa.Core;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class BlenderSmokeTests
{
    [BlenderSmokeFact]
    public async Task HeadlessImport_SelfAuthoredUeFormatFixture()
    {
        var configPath = RepoPaths.LocalConfig();
        Assert.True(File.Exists(configPath), $"Blender smoke needs {configPath} with blender.executable.");
        var config = ConfigLoader.Load(configPath);
        Assert.True(
            File.Exists(config.Blender.Executable),
            $"Blender executable not found: {config.Blender.Executable}");

        var repo = RepoPaths.Root();
        var fixture = RepoPaths.SmokeFixtureDir();
        Assert.True(File.Exists(Path.Combine(fixture, SmokeFixture.ManifestFile)));

        var outDir = Path.Combine(repo, "work", "blend");
        var savePath = Path.Combine(outDir, "ueformat-smoke.blend");
        var reportPath = Path.Combine(outDir, "ueformat-smoke.validation.json");
        var logPath = Path.Combine(repo, "work", "logs", "blender-smoke.log");

        var result = await BlenderProcess.RunAsync(
            config.Blender.Executable,
            repo,
            Path.Combine(fixture, SmokeFixture.ManifestFile),
            savePath,
            RepoPaths.MaterialProfile(),
            reportPath,
            logPath);

        Assert.True(File.Exists(result.LogPath), result.LogPath);
        Assert.Equal(0, result.BlenderExitCode);
        Assert.True(result.Report.Saved);
        Assert.True(result.Report.ReopenedClean);
        Assert.Empty(result.Report.Errors);
        Assert.Equal("wuwa-3x", result.Report.ProfileId);
        Assert.Equal(ToolVersions.Tool, result.Report.ToolVersion);

        var scene = result.Report.Scene;
        Assert.NotNull(scene);
        Assert.Equal("SmokeCube_LOD0", scene.MeshName);
        Assert.Equal(SyntheticUeModelWriter.VertexCount, scene.Vertices);
        Assert.Equal(SyntheticUeModelWriter.TriangleCount, scene.Faces);
        Assert.Equal(SyntheticUeModelWriter.IndexCount, scene.Loops);
        Assert.Equal(SyntheticUeModelWriter.SectionCount, scene.MaterialSlots);
        Assert.Equal(SyntheticUeModelWriter.MorphCount, scene.MorphTargets);
        Assert.Equal(SyntheticUeModelWriter.BoneCount, scene.Bones);
        Assert.True(scene.HasArmatureModifier);
        Assert.True(scene.HasVertexColors);
        Assert.True(scene.UvChannels >= SyntheticUeModelWriter.UvChannels);
        Assert.Equal(0, scene.MissingImages);
        Assert.True(scene.BoundImages >= 2);
        Assert.Contains(scene.MaterialNames, name => name.Contains("Hair", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(scene.MaterialNames, name => name.Contains("Body", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(savePath));
    }
}
