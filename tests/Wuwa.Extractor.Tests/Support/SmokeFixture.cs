using System.Text.Json;
using Wuwa.Core;

namespace Wuwa.Extractor.Tests;

internal static class SmokeFixture
{
    public const string JobId = "ueformat-smoke";
    public const string UeModelFile = "SmokeCube.uemodel";
    public const string DiffuseFile = "T_SmokeDiffuse.png";
    public const string NormalFile = "T_SmokeNormal.png";
    public const string ManifestFile = "manifest.json";

    public static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, UeModelFile), SyntheticUeModelWriter.Write());
        File.WriteAllBytes(Path.Combine(directory, DiffuseFile), TinyPng.SolidRgb(220, 40, 40));
        File.WriteAllBytes(Path.Combine(directory, NormalFile), TinyPng.SolidRgb(128, 128, 255));
        var manifest = CreateManifest();
        File.WriteAllText(
            Path.Combine(directory, ManifestFile),
            JsonSerializer.Serialize(manifest, ConfigLoader.JsonOptions));
    }

    public static ExportManifest CreateManifest()
        => new()
        {
            SchemaVersion = ToolVersions.ManifestSchema,
            JobId = JobId,
            Timestamp = DateTimeOffset.Parse("2026-09-06T00:00:00Z"),
            GameVersion = "fixture",
            UeVersion = "GAME_WutheringWaves",
            SourceObjectPath = SyntheticUeModelWriter.ObjectPath,
            UnrealObjectPath = SyntheticUeModelWriter.ObjectPath,
            ToolVersions = new Dictionary<string, string>
            {
                ["wuwa2blender"] = ToolVersions.Tool,
                ["ueformat"] = "synthetic-v10"
            },
            Mesh = new ExportMeshInfo
            {
                ObjectPath = SyntheticUeModelWriter.ObjectPath,
                ExportType = "SkeletalMesh",
                UeModel = UeModelFile,
                LodCount = 1,
                MaterialSlotCount = SyntheticUeModelWriter.SectionCount,
                MorphTargetCount = SyntheticUeModelWriter.MorphCount,
                HasVertexColors = true,
                UvChannels = SyntheticUeModelWriter.UvChannels,
                Lods =
                [
                    new ExportLodInfo
                    {
                        Index = 0,
                        Vertices = SyntheticUeModelWriter.VertexCount,
                        Triangles = SyntheticUeModelWriter.TriangleCount,
                        Indices = SyntheticUeModelWriter.IndexCount,
                        Sections = SyntheticUeModelWriter.SectionCount,
                        UvChannels = SyntheticUeModelWriter.UvChannels
                    }
                ]
            },
            Skeleton = new ExportSkeletonInfo
            {
                ObjectPath = SyntheticUeModelWriter.SkeletonPath,
                BoneCount = SyntheticUeModelWriter.BoneCount,
                RootBone = "Root",
                Bones = ["Root", "Spine"]
            },
            Materials =
            [
                new ExportMaterialInfo
                {
                    SlotIndex = 0,
                    SlotName = "Hair",
                    ObjectPath = SyntheticUeModelWriter.HairPath
                },
                new ExportMaterialInfo
                {
                    SlotIndex = 1,
                    SlotName = "Body",
                    ObjectPath = SyntheticUeModelWriter.BodyPath
                }
            ],
            Textures =
            [
                new ExportTextureInfo
                {
                    ObjectPath = SyntheticUeModelWriter.DiffusePath,
                    ParameterName = "MainTex",
                    File = DiffuseFile,
                    Width = 1,
                    Height = 1,
                    PixelFormat = "PF_B8G8R8A8"
                },
                new ExportTextureInfo
                {
                    ObjectPath = SyntheticUeModelWriter.NormalPath,
                    ParameterName = "PM_Normals",
                    File = NormalFile,
                    Width = 1,
                    Height = 1,
                    PixelFormat = "PF_B8G8R8A8"
                }
            ],
            MaterialParameters =
            [
                new MaterialParameterSnapshot
                {
                    MaterialObjectPath = SyntheticUeModelWriter.HairPath,
                    SlotName = "Hair",
                    Textures = new Dictionary<string, string>
                    {
                        ["MainTex"] = SyntheticUeModelWriter.DiffusePath
                    }
                },
                new MaterialParameterSnapshot
                {
                    MaterialObjectPath = SyntheticUeModelWriter.BodyPath,
                    SlotName = "Body",
                    Textures = new Dictionary<string, string>
                    {
                        ["PM_Diffuse"] = SyntheticUeModelWriter.DiffusePath,
                        ["PM_Normals"] = SyntheticUeModelWriter.NormalPath
                    }
                }
            ],
            Files = [UeModelFile, DiffuseFile, NormalFile],
            SourceHashes = new SourceHashInfo
            {
                AesSource = "fixture",
                AesContentHash = "synthetic",
                MappingsSource = "none"
            },
            Warnings = ["Self-authored P6 smoke fixture; not a game asset."]
        };
}
