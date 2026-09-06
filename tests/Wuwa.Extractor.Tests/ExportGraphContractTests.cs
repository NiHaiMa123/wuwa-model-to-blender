using System.Text.Json;
using Wuwa.Core;
using Wuwa.Export;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class ExportGraphContractTests
{
    [Fact]
    public void Manifest_PreservesMaterialTextureGraphAndParent()
    {
        var manifest = new ExportManifest
        {
            SchemaVersion = "1",
            JobId = "graph",
            SourceObjectPath = "/Game/WuwaSmoke/SmokeCube.SmokeCube",
            Mesh = new ExportMeshInfo
            {
                ObjectPath = "/Game/WuwaSmoke/SmokeCube.SmokeCube",
                MaterialSlotCount = 2,
                MorphTargetCount = 1
            },
            Materials =
            [
                new ExportMaterialInfo
                {
                    SlotIndex = 0,
                    SlotName = "Hair",
                    ObjectPath = "/Game/WuwaSmoke/MI_SmokeHair",
                    Parent = "/Game/WuwaSmoke/M_Character"
                },
                new ExportMaterialInfo
                {
                    SlotIndex = 1,
                    SlotName = "Body",
                    ObjectPath = "/Game/WuwaSmoke/MI_SmokeBody",
                    Parent = "/Game/WuwaSmoke/M_Character"
                }
            ],
            Textures =
            [
                new ExportTextureInfo { ObjectPath = "/Game/WuwaSmoke/T_SmokeDiffuse", ParameterName = "MainTex" },
                new ExportTextureInfo { ObjectPath = "/Game/WuwaSmoke/T_SmokeNormal", ParameterName = "PM_Normals" }
            ],
            MaterialParameters =
            [
                new MaterialParameterSnapshot
                {
                    MaterialObjectPath = "/Game/WuwaSmoke/MI_SmokeHair",
                    SlotName = "Hair",
                    Textures = new Dictionary<string, string> { ["MainTex"] = "/Game/WuwaSmoke/T_SmokeDiffuse" },
                    Scalars = new Dictionary<string, float> { ["Opacity"] = 0.8f }
                }
            ],
            Files = ["SmokeCube.uemodel"],
            Warnings = []
        };

        var json = JsonSerializer.Serialize(manifest, ConfigLoader.JsonOptions);
        var loaded = JsonSerializer.Deserialize<ExportManifest>(json, ConfigLoader.JsonOptions);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Materials.Count);
        Assert.Equal("/Game/WuwaSmoke/M_Character", loaded.Materials[0].Parent);
        Assert.Equal(2, loaded.Textures.Select(t => t.ObjectPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("/Game/WuwaSmoke/T_SmokeDiffuse", loaded.MaterialParameters[0].Textures["MainTex"]);
        Assert.Equal(0.8f, loaded.MaterialParameters[0].Scalars["Opacity"]);
        Assert.DoesNotContain("0x", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManifestWriter_DoesNotEmbedAesKeyMaterial()
    {
        var dir = Directory.CreateTempSubdirectory("wuwa-graph-");
        try
        {
            var path = Path.Combine(dir.FullName, "manifest.json");
            var manifest = new ExportManifest
            {
                SchemaVersion = "1",
                JobId = "graph-hash",
                SourceObjectPath = "/Game/WuwaSmoke/SmokeCube.SmokeCube",
                Files = ["SmokeCube.uemodel"],
                Warnings = [],
                SourceHashes = new SourceHashInfo
                {
                    AesSource = "endpoint:example.invalid",
                    AesContentHash = "abc123",
                    MappingsSource = "none"
                }
            };
            await ManifestWriter.WriteAsync(path, manifest);
            var json = await File.ReadAllTextAsync(path);
            Assert.Contains("abc123", json, StringComparison.Ordinal);
            Assert.DoesNotContain("mainKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("0x", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
