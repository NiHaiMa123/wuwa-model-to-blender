namespace Wuwa.Core;

public sealed class GoldenInvariants
{
    public const string Character = "Jinhsi / 今汐";
    public const string GameVersion = "3.6.0";
    public const string ObjectPath =
        "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011.R2T1JinxiMd10011";
    public const string UnrealObjectPath =
        "/Game/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011";
    public const int LodCount = 5;
    public const int Lod0Sections = 7;
    public const int Lod0Vertices = 40662;
    public const int Lod0Triangles = 56483;
    public const int Lod0Indices = 169449;
    public const int MaterialSlots = 17;
    public const int MorphTargets = 86;
    public const int UniqueTextures = 26;
    public const int CookedBones = 196;
    public const int BlenderBones = 204;
    public const int UvChannels = 4;
    public const bool HasVertexColors = true;

    public static bool Matches(string objectPath)
    {
        var archive = PathNormalization.ToArchiveObjectPath(objectPath);
        var unreal = PathNormalization.ToUnrealObjectPath(objectPath);
        return archive.Equals(ObjectPath, StringComparison.OrdinalIgnoreCase) ||
               archive.Equals(ObjectPath + ".uasset", StringComparison.OrdinalIgnoreCase) ||
               unreal.StartsWith(UnrealObjectPath, StringComparison.OrdinalIgnoreCase) ||
               archive.Contains("R2T1JinxiMd10011.R2T1JinxiMd10011", StringComparison.OrdinalIgnoreCase);
    }

    public static GoldenComparison Compare(ExportManifest manifest)
    {
        var mismatches = new List<string>();
        var mesh = manifest.Mesh;
        if (mesh is null)
        {
            mismatches.Add("mesh is missing");
            return new GoldenComparison { Matched = false, Mismatches = mismatches };
        }

        Check(mismatches, "lodCount", LodCount, mesh.LodCount);
        var lod0 = mesh.Lods.FirstOrDefault(l => l.Index == 0) ?? mesh.Lods.FirstOrDefault();
        if (lod0 is null)
        {
            mismatches.Add("LOD0 is missing");
        }
        else
        {
            Check(mismatches, "lod0.vertices", Lod0Vertices, lod0.Vertices);
            Check(mismatches, "lod0.triangles", Lod0Triangles, lod0.Triangles);
            Check(mismatches, "lod0.indices", Lod0Indices, lod0.Indices);
            Check(mismatches, "lod0.sections", Lod0Sections, lod0.Sections);
            Check(mismatches, "lod0.uvChannels", UvChannels, lod0.UvChannels);
        }

        Check(mismatches, "materialSlots", MaterialSlots, mesh.MaterialSlotCount);
        Check(mismatches, "morphTargets", MorphTargets, mesh.MorphTargetCount);
        Check(mismatches, "hasVertexColors", HasVertexColors ? 1 : 0, mesh.HasVertexColors ? 1 : 0);
        Check(mismatches, "uniqueTextures", UniqueTextures, manifest.Textures.Select(t => t.ObjectPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        if (manifest.Skeleton is not null)
        {
            Check(mismatches, "cookedBones", CookedBones, manifest.Skeleton.BoneCount);
        }
        else
        {
            mismatches.Add("skeleton is missing");
        }

        return new GoldenComparison { Matched = mismatches.Count == 0, Mismatches = mismatches };
    }

    public static GoldenComparison CompareBlend(BlendSceneStats scene)
    {
        var mismatches = new List<string>();
        Check(mismatches, "lod0.vertices", Lod0Vertices, scene.Vertices);
        Check(mismatches, "lod0.triangles", Lod0Triangles, scene.Faces);
        Check(mismatches, "lod0.indices", Lod0Indices, scene.Loops);
        Check(mismatches, "lod0.sections", Lod0Sections, scene.MaterialSlots);
        Check(mismatches, "morphTargets", MorphTargets, scene.MorphTargets);
        Check(mismatches, "blenderBones", BlenderBones, scene.Bones);
        Check(mismatches, "uvChannels", UvChannels, scene.UvChannels);
        Check(mismatches, "hasVertexColors", HasVertexColors ? 1 : 0, scene.HasVertexColors ? 1 : 0);
        Check(mismatches, "armatureModifier", 1, scene.HasArmatureModifier ? 1 : 0);
        if (scene.MissingImages > 0)
        {
            mismatches.Add($"missingImages: expected 0, got {scene.MissingImages}");
        }

        return new GoldenComparison { Matched = mismatches.Count == 0, Mismatches = mismatches };
    }

    private static void Check(List<string> mismatches, string name, int expected, int actual)
    {
        if (expected != actual)
        {
            mismatches.Add($"{name}: expected {expected}, got {actual}");
        }
    }
}
