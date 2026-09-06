using System.Text.Json;
using Wuwa.Core;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class RunJobTests
{
    private const string Golden =
        "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011.R2T1JinxiMd10011";

    [Fact]
    public void DeriveJobId_PrefersSaveNameThenMeshLeaf()
    {
        Assert.Equal("Jinhsi", RunJobLayout.DeriveJobId(Golden, @"work\blend\Jinhsi.blend", null));
        Assert.Equal("custom", RunJobLayout.DeriveJobId(Golden, @"work\blend\Jinhsi.blend", "custom"));
        Assert.Equal("R2T1JinxiMd10011", RunJobLayout.DeriveJobId(Golden, null, null));
        Assert.Equal("Jinhsi", RunJobLayout.DeriveJobId("Jinhsi", null, null));
        Assert.Equal("now_slash", RunJobLayout.Sanitize("now/slash"));
    }

    [Fact]
    public void CanonicalAsset_UnifiesGameAndArchiveObjectPaths()
    {
        Assert.Equal(
            Golden,
            RunJobLayout.CanonicalAsset(Golden),
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            Golden,
            RunJobLayout.CanonicalAsset(GoldenInvariants.UnrealObjectPath),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RunIndex_NeedsSearchForAliasesOnly()
    {
        Assert.True(RunIndex.NeedsSearch("Jinhsi"));
        Assert.True(RunIndex.NeedsSearch("今汐"));
        Assert.False(RunIndex.NeedsSearch(Golden));
        Assert.False(RunIndex.NeedsSearch("/Game/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011"));
    }

    [Fact]
    public void Fingerprints_AreStableAndLayered()
    {
        var input = SampleFingerprint(Golden, "aes-1");
        var first = RunFingerprint.Compute(input);
        var second = RunFingerprint.Compute(input);
        Assert.Equal(first.Config, second.Config);
        Assert.Equal(first.Source, second.Source);
        Assert.Equal(first.Profile, second.Profile);
        Assert.Equal(first.Combined, second.Combined);

        var otherAsset = RunFingerprint.Compute(SampleFingerprint(Golden + "_Other", "aes-1"));
        Assert.Equal(first.Config, otherAsset.Config);
        Assert.NotEqual(first.Source, otherAsset.Source);

        var otherAes = RunFingerprint.Compute(SampleFingerprint(Golden, "aes-2"));
        Assert.NotEqual(first.Source, otherAes.Source);

        var otherProfile = RunFingerprint.Compute(SampleFingerprint(Golden, "aes-1") with { ProfileHash = "other" });
        Assert.Equal(first.Source, otherProfile.Source);
        Assert.NotEqual(first.Profile, otherProfile.Profile);
    }

    [Fact]
    public void CanSkipDoctor_RequiresMatchingConfigAndCompletedStage()
    {
        var previous = RunJob.Create("Jinhsi");
        previous.Fingerprints.Config = "cfg";
        previous.Stage(RunStages.Doctor).Status = RunStageStatus.Warn;
        var blender = Path.GetTempFileName();
        try
        {
            Assert.True(RunCache.CanSkipDoctor(previous, "cfg", blender, force: false));
            Assert.False(RunCache.CanSkipDoctor(previous, "other", blender, force: false));
            Assert.False(RunCache.CanSkipDoctor(previous, "cfg", blender, force: true));
            Assert.False(RunCache.CanSkipDoctor(null, "cfg", blender, force: false));
        }
        finally
        {
            File.Delete(blender);
        }
    }

    [Fact]
    public void CanSkipIndex_AcceptsSameQueryOrCanonicalObjectPath()
    {
        var previous = RunJob.Create("Jinhsi");
        previous.AssetInput = "Jinhsi";
        previous.ResolvedAsset = Golden;
        previous.Stage(RunStages.Index).Status = RunStageStatus.Pass;

        Assert.True(RunCache.CanSkipIndex(previous, "Jinhsi", null, force: false));
        Assert.True(RunCache.CanSkipIndex(previous, Golden, null, force: false));
        Assert.False(RunCache.CanSkipIndex(previous, "Rover", null, force: false));
        Assert.False(RunCache.CanSkipIndex(previous, "Jinhsi", null, force: true));
    }

    [Fact]
    public void ExportStaging_ReusesCompleteManifestAndRejectsMissingFiles()
    {
        var dir = CreateStaging(Golden, "aes-1", writeUeModel: true);
        try
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            Assert.True(ExportStaging.IsReusableFor(manifestPath, dir, RunJobLayout.CanonicalAsset(Golden), "aes-1", out _));
            Assert.False(ExportStaging.IsReusableFor(manifestPath, dir, RunJobLayout.CanonicalAsset(Golden), "aes-other", out var aesReason));
            Assert.Contains("AES", aesReason, StringComparison.OrdinalIgnoreCase);

            File.Delete(Path.Combine(dir, "mesh.uemodel"));
            Assert.False(ExportStaging.IsReusableFor(manifestPath, dir, RunJobLayout.CanonicalAsset(Golden), "aes-1", out var missingReason));
            Assert.Contains("missing", missingReason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CanSkipExport_AllowsFirstRunReuseWithoutPreviousJob()
    {
        var dir = CreateStaging(Golden, "aes-1", writeUeModel: true);
        try
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            var canonical = RunJobLayout.CanonicalAsset(Golden);
            Assert.True(RunCache.CanSkipExport(null, "source-fp", manifestPath, dir, canonical, "aes-1", force: false));
            Assert.False(RunCache.CanSkipExport(null, "source-fp", manifestPath, dir, canonical, "aes-1", force: true));

            var previous = RunJob.Create("Jinhsi");
            previous.Fingerprints.Source = "source-fp";
            previous.Stage(RunStages.Export).Status = RunStageStatus.Pass;
            Assert.True(RunCache.CanSkipExport(previous, "source-fp", manifestPath, dir, canonical, "aes-1", force: false));
            Assert.False(RunCache.CanSkipExport(previous, "other-fp", manifestPath, dir, canonical, "aes-1", force: false));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CanSkipBlender_RequiresPreviousSuccessAndExistingFiles()
    {
        var blend = Path.GetTempFileName();
        var report = Path.GetTempFileName();
        try
        {
            var previous = RunJob.Create("Jinhsi");
            previous.Fingerprints.Source = "src";
            previous.Fingerprints.Profile = "prof";
            previous.Stage(RunStages.LaunchBlender).Status = RunStageStatus.Pass;
            previous.Stage(RunStages.ValidateBlend).Status = RunStageStatus.Pass;
            Assert.True(RunCache.CanSkipBlender(previous, "prof", "src", blend, report, force: false));
            Assert.False(RunCache.CanSkipBlender(previous, "other", "src", blend, report, force: false));
            Assert.False(RunCache.CanSkipBlender(null, "prof", "src", blend, report, force: false));
        }
        finally
        {
            File.Delete(blend);
            File.Delete(report);
        }
    }

    [Fact]
    public void FromStage_RunsRequestedStageAndLater()
    {
        Assert.False(RunCache.MustRunFrom(RunStages.Doctor, RunStages.LaunchBlender));
        Assert.True(RunCache.MustRunFrom(RunStages.LaunchBlender, RunStages.LaunchBlender));
        Assert.True(RunCache.MustRunFrom(RunStages.ValidateBlend, RunStages.LaunchBlender));

        var previous = RunJob.Create("Jinhsi");
        previous.Stage(RunStages.Export).Status = RunStageStatus.Pass;
        Assert.True(RunCache.CanSkipBefore(RunStages.Export, RunStages.LaunchBlender, previous));
        Assert.False(RunCache.CanSkipBefore(RunStages.LaunchBlender, RunStages.LaunchBlender, previous));
        Assert.Null(RunStages.Canonical("NotAStage"));
        Assert.Equal(RunStages.LaunchBlender, RunStages.Canonical("launchblender"));
    }

    [Fact]
    public async Task RunJobStore_RoundTripsStageStatus()
    {
        var path = Path.Combine(Path.GetTempPath(), "wuwa-job-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var job = RunJob.Create("Jinhsi");
            job.AssetInput = "Jinhsi";
            job.ResolvedAsset = Golden;
            job.Stage(RunStages.Export).Status = RunStageStatus.Pass;
            job.Stage(RunStages.Export).Summary = "44 files";
            await RunJobStore.SaveAsync(job, path);
            var loaded = RunJobStore.TryLoad(path);
            Assert.NotNull(loaded);
            Assert.Equal("Jinhsi", loaded.JobId);
            Assert.Equal(Golden, loaded.ResolvedAsset);
            Assert.Equal(RunStageStatus.Pass, loaded.Stage(RunStages.Export).Status);
            Assert.Equal(RunStages.Order.Length, loaded.Stages.Count);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static RunFingerprintInput SampleFingerprint(string asset, string aesHash)
        => new()
        {
            ToolVersion = ToolVersions.Tool,
            CanonicalAsset = RunJobLayout.CanonicalAsset(asset),
            SavePath = @"D:\repo\work\blend\Jinhsi.blend",
            ProfilePath = @"D:\repo\config\material-profiles\3x.json",
            ProfileHash = "profile-hash",
            PackImages = true,
            MeshQuality = "highest",
            UeVersion = "GAME_WutheringWaves",
            GameVersion = "3.6.0",
            PaksDir = @"D:\game\Paks",
            InstallDir = @"D:\game",
            BlenderExe = @"D:\software\blender.exe",
            AesMode = "endpoint",
            AesEndpoint = "https://example.invalid/keys.json",
            AesHash = aesHash,
            MappingsMode = "none",
            MappingsHash = ""
        };

    private static string CreateStaging(string objectPath, string aesHash, bool writeUeModel)
    {
        var dir = Path.Combine(Path.GetTempPath(), "wuwa-staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        if (writeUeModel)
        {
            File.WriteAllText(Path.Combine(dir, "mesh.uemodel"), "uemodel");
        }

        var manifest = new ExportManifest
        {
            JobId = "Jinhsi",
            SourceObjectPath = objectPath,
            UnrealObjectPath = PathNormalization.ToUnrealObjectPath(objectPath),
            Mesh = new ExportMeshInfo { UeModel = "mesh.uemodel", LodCount = 5 },
            Files = ["mesh.uemodel"],
            SourceHashes = new SourceHashInfo { AesContentHash = aesHash }
        };
        File.WriteAllText(
            Path.Combine(dir, "manifest.json"),
            JsonSerializer.Serialize(manifest, ConfigLoader.JsonOptions));
        return dir;
    }
}
