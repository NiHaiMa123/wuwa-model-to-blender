using System.Text.Json.Serialization;

namespace Wuwa.Core;

public sealed class ExportManifest
{
    public string SchemaVersion { get; init; } = Wuwa.Core.ToolVersions.ManifestSchema;
    public string JobId { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string GameVersion { get; init; } = "";
    public string UeVersion { get; init; } = "";
    public string SourceObjectPath { get; init; } = "";
    public string UnrealObjectPath { get; init; } = "";
    public Dictionary<string, string> ToolVersions { get; init; } = new();
    public ExportMeshInfo? Mesh { get; init; }
    public ExportSkeletonInfo? Skeleton { get; init; }
    public List<ExportMaterialInfo> Materials { get; init; } = [];
    public List<ExportTextureInfo> Textures { get; init; } = [];
    public List<ExportAnimationInfo> Animations { get; init; } = [];
    public List<MaterialParameterSnapshot> MaterialParameters { get; init; } = [];
    public List<string> Files { get; init; } = [];
    public SourceHashInfo SourceHashes { get; init; } = new();
    public List<string> Warnings { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GoldenComparison? Golden { get; set; }
}

public sealed class ExportMeshInfo
{
    public string ObjectPath { get; init; } = "";
    public string ExportType { get; init; } = "SkeletalMesh";
    public string? UeModel { get; set; }
    public int LodCount { get; init; }
    public List<ExportLodInfo> Lods { get; init; } = [];
    public int MaterialSlotCount { get; init; }
    public int MorphTargetCount { get; init; }
    public bool HasVertexColors { get; init; }
    public int UvChannels { get; init; }
}

public sealed class ExportLodInfo
{
    public int Index { get; init; }
    public int Vertices { get; init; }
    public int Triangles { get; init; }
    public int Indices { get; init; }
    public int Sections { get; init; }
    public int UvChannels { get; init; }
}

public sealed class ExportSkeletonInfo
{
    public string? ObjectPath { get; init; }
    public int BoneCount { get; init; }
    public int SocketCount { get; init; }
    public string? RootBone { get; init; }
    public List<string> Bones { get; init; } = [];
}

public sealed class ExportMaterialInfo
{
    public int SlotIndex { get; init; }
    public string SlotName { get; init; } = "";
    public string? ObjectPath { get; init; }
    public string? Parent { get; init; }
    public string? JsonFile { get; set; }
}

public sealed class ExportTextureInfo
{
    public string ObjectPath { get; init; } = "";
    public string? ParameterName { get; init; }
    public string? File { get; set; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? PixelFormat { get; init; }
}

public sealed class ExportAnimationInfo
{
    public string ObjectPath { get; init; } = "";
    public string? File { get; init; }
}

public sealed class MaterialParameterSnapshot
{
    public string MaterialObjectPath { get; init; } = "";
    public string? SlotName { get; init; }
    public Dictionary<string, string> Textures { get; init; } = new();
    public Dictionary<string, float> Scalars { get; init; } = new();
    public Dictionary<string, string> Vectors { get; init; } = new();
}

public sealed class SourceHashInfo
{
    public string? AesSource { get; init; }
    public string? AesContentHash { get; init; }
    public string? MappingsSource { get; init; }
    public string? MappingsContentHash { get; init; }
    public Dictionary<string, string> Files { get; init; } = new();
}

public sealed class GoldenComparison
{
    public bool Matched { get; init; }
    public List<string> Mismatches { get; init; } = [];
}

public sealed class ExportRequest
{
    public required string Asset { get; init; }
    public string? OutputDirectory { get; init; }
    public bool IncludeAnimations { get; init; }
    public string MeshQuality { get; init; } = "highest";
}
