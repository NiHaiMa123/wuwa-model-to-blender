using Wuwa.Core;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class BlenderBridgeTests
{
    [Fact]
    public void MaterialProfile_3xMapsWuWaParameterAliases()
    {
        var profile = MaterialProfile.Load(Path.Combine(RepoRoot(), "config", "material-profiles", "3x.json"));
        Assert.Equal("wuwa-3x", profile.ProfileId);
        Assert.Equal("baseColor", profile.RoleForParameter("MainTex"));
        Assert.Equal("baseColor", profile.RoleForParameter("PM_Diffuse"));
        Assert.Equal("normal", profile.RoleForParameter("PM_Normals"));
        Assert.Equal("emission", profile.RoleForParameter("EM"));
        Assert.Equal("hair", profile.KindForName("MI_R2T1JinxiMd10011Bangs").Kind);
        Assert.True(profile.KindForName("MI_R2T1JinxiMd10011Hair").UseBaseColorAlpha);
        Assert.Equal("face", profile.KindForName("MI_R2T1JinxiMd10011Face").Kind);
        Assert.True(profile.KindForName("MI_R2T1JinxiMd10011Hair_OL").Skip);
        Assert.True(profile.NormalYFlip);
        Assert.Equal(0, profile.Import.TargetLod);
        Assert.Equal(0.01, profile.Import.ScaleFactor);
    }

    [Fact]
    public void BlenderLaunch_BuildsHeadlessArgumentList()
    {
        var args = BlenderLaunch.BuildArguments(
            @"D:\repo\blender\scripts\batch_import.py",
            @"D:\repo\work\exports\Jinhsi\manifest.json",
            @"D:\repo\work\blend\Jinhsi.blend",
            @"D:\repo\config\material-profiles\3x.json",
            @"D:\repo\work\blend\Jinhsi.validation.json",
            packImages: true);
        Assert.Equal("--background", args[0]);
        Assert.Equal("--python", args[1]);
        Assert.Contains("--manifest", args);
        Assert.Contains("--save", args);
        Assert.Contains("--profile", args);
        Assert.Contains("--report", args);
        Assert.Contains("--pack", args);
        Assert.DoesNotContain("--factory-startup", args);
        Assert.Equal("Jinhsi.blend", Path.GetFileName(BlenderLaunch.DefaultSavePath(@"D:\repo", "Jinhsi")));
        Assert.True(File.Exists(BlenderLaunch.DefaultScriptPath(RepoRoot())));
        Assert.True(File.Exists(BlenderLaunch.DefaultProfilePath(RepoRoot())));
    }

    [Fact]
    public void GoldenInvariants_CompareBlendDetectsBoneAndImageFailures()
    {
        var good = SampleScene(GoldenInvariants.BlenderBones, missingImages: 0);
        Assert.True(GoldenInvariants.CompareBlend(good).Matched);

        var bones = SampleScene(GoldenInvariants.CookedBones, missingImages: 0);
        var boneCompare = GoldenInvariants.CompareBlend(bones);
        Assert.False(boneCompare.Matched);
        Assert.Contains(boneCompare.Mismatches, m => m.Contains("blenderBones", StringComparison.Ordinal));

        var missing = SampleScene(GoldenInvariants.BlenderBones, missingImages: 3);
        Assert.Contains(
            GoldenInvariants.CompareBlend(missing).Mismatches,
            m => m.Contains("missingImages", StringComparison.Ordinal));
    }

    private static BlendSceneStats SampleScene(int bones, int missingImages)
        => new()
        {
            MeshName = "R2T1JinxiMd10011_LOD0",
            ArmatureName = "R2T1JinxiMd10011_LOD0_Skeleton",
            Vertices = GoldenInvariants.Lod0Vertices,
            Faces = GoldenInvariants.Lod0Triangles,
            Loops = GoldenInvariants.Lod0Indices,
            MaterialSlots = GoldenInvariants.Lod0Sections,
            MorphTargets = GoldenInvariants.MorphTargets,
            Bones = bones,
            UvChannels = GoldenInvariants.UvChannels,
            HasVertexColors = true,
            HasArmatureModifier = true,
            BoundImages = GoldenInvariants.UniqueTextures,
            MissingImages = missingImages
        };

    private static string RepoRoot()
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
}
