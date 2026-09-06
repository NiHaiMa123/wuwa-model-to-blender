using Wuwa.Core;
using Wuwa.Export;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class ObjectPathAndManifestTests
{
    private const string Golden =
        "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011.R2T1JinxiMd10011";

    [Fact]
    public void Candidates_MapGamePathToClientContent()
    {
        var candidates = ObjectPathResolver.Candidates(
            "/Game/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011");
        Assert.Contains(Golden, candidates, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011",
            candidates,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Split_SeparatesPackageAndExport()
    {
        var (package, export) = ObjectPathResolver.Split(Golden);
        Assert.Equal(
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011",
            package);
        Assert.Equal("R2T1JinxiMd10011", export);
    }

    [Fact]
    public void LooksLikeObjectPath_RejectsBareAlias()
    {
        Assert.False(ObjectPathResolver.LooksLikeObjectPath("Jinhsi"));
        Assert.True(ObjectPathResolver.LooksLikeObjectPath(Golden));
        Assert.True(ObjectPathResolver.LooksLikeObjectPath("/Game/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011"));
    }

    [Fact]
    public void PathNormalization_RoundTripsGameAndArchive()
    {
        var unreal = PathNormalization.ToUnrealObjectPath(Golden);
        Assert.Equal(GoldenInvariants.UnrealObjectPath + ".R2T1JinxiMd10011", unreal);
        Assert.Equal(
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011",
            PathNormalization.ToArchiveObjectPath(GoldenInvariants.UnrealObjectPath));
    }

    [Fact]
    public void GoldenInvariants_CompareDetectsMismatchAndMatch()
    {
        var bad = SampleManifest(vertices: 1);
        var badCompare = GoldenInvariants.Compare(bad);
        Assert.False(badCompare.Matched);
        Assert.Contains(badCompare.Mismatches, m => m.Contains("lod0.vertices", StringComparison.Ordinal));

        var good = SampleManifest(vertices: GoldenInvariants.Lod0Vertices);
        Assert.True(GoldenInvariants.Compare(good).Matched);
        Assert.True(GoldenInvariants.Matches(Golden));
        Assert.True(GoldenInvariants.Matches(GoldenInvariants.UnrealObjectPath));
    }

    [Fact]
    public async Task ManifestWriter_RoundTripsRequiredFields()
    {
        var dir = Directory.CreateTempSubdirectory("wuwa-manifest-");
        try
        {
            var path = Path.Combine(dir.FullName, "manifest.json");
            var original = SampleManifest(GoldenInvariants.Lod0Vertices);
            original.Golden = GoldenInvariants.Compare(original);
            await ManifestWriter.WriteAsync(path, original);
            var json = await File.ReadAllTextAsync(path);
            var loaded = System.Text.Json.JsonSerializer.Deserialize<ExportManifest>(json, ConfigLoader.JsonOptions);
            Assert.NotNull(loaded);
            Assert.Equal("job-test", loaded.JobId);
            Assert.Equal(Golden, loaded.SourceObjectPath);
            Assert.Equal(GoldenInvariants.Lod0Vertices, loaded.Mesh!.Lods[0].Vertices);
            Assert.Equal(GoldenInvariants.UniqueTextures, loaded.Textures.Count);
            Assert.True(loaded.Golden!.Matched);
            Assert.Contains("schemaVersion", json, StringComparison.Ordinal);
            Assert.Contains("sourceObjectPath", json, StringComparison.Ordinal);
            Assert.DoesNotContain("0x", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static ExportManifest SampleManifest(int vertices)
        => new()
        {
            SchemaVersion = "1",
            JobId = "job-test",
            GameVersion = "3.6.0",
            UeVersion = "GAME_WutheringWaves",
            SourceObjectPath = Golden,
            UnrealObjectPath = GoldenInvariants.UnrealObjectPath,
            ToolVersions = new Dictionary<string, string> { ["wuwa2blender"] = ToolVersions.Tool },
            Mesh = new ExportMeshInfo
            {
                ObjectPath = Golden,
                LodCount = GoldenInvariants.LodCount,
                MaterialSlotCount = GoldenInvariants.MaterialSlots,
                MorphTargetCount = GoldenInvariants.MorphTargets,
                HasVertexColors = true,
                UvChannels = GoldenInvariants.UvChannels,
                Lods =
                [
                    new ExportLodInfo
                    {
                        Index = 0,
                        Vertices = vertices,
                        Triangles = GoldenInvariants.Lod0Triangles,
                        Indices = GoldenInvariants.Lod0Indices,
                        Sections = GoldenInvariants.Lod0Sections,
                        UvChannels = GoldenInvariants.UvChannels
                    }
                ]
            },
            Skeleton = new ExportSkeletonInfo
            {
                ObjectPath = Golden + "_Skeleton",
                BoneCount = GoldenInvariants.CookedBones,
                RootBone = "Root"
            },
            Textures = Enumerable.Range(0, GoldenInvariants.UniqueTextures)
                .Select(i => new ExportTextureInfo { ObjectPath = $"T_Dummy_{i}" })
                .ToList(),
            Files = ["Game/dummy.uemodel"],
            Warnings = []
        };
}
