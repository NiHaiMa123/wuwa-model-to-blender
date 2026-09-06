using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using Wuwa.Core;

namespace Wuwa.Extractor;

public sealed class DependencyGraph
{
    public required USkeletalMesh Mesh { get; init; }
    public required string MeshObjectPath { get; init; }
    public required ExportMeshInfo MeshInfo { get; init; }
    public required ExportSkeletonInfo Skeleton { get; init; }
    public List<ExportMaterialInfo> Materials { get; } = [];
    public List<ExportTextureInfo> Textures { get; } = [];
    public List<MaterialParameterSnapshot> MaterialParameters { get; } = [];
    public List<UObject> ExportRoots { get; } = [];
    public List<string> Warnings { get; } = [];
}

public static class DependencyWalker
{
    public static DependencyGraph Walk(USkeletalMesh mesh, bool includePhysics, IList<string>? extraWarnings = null)
    {
        var meshPath = MeshStats.ObjectPathOf(mesh);
        var warnings = extraWarnings ?? new List<string>();
        string? skeletonPath = null;
        if (!mesh.Skeleton.IsNull)
        {
            var skeleton = mesh.Skeleton.Load();
            if (skeleton is not null)
            {
                skeletonPath = MeshStats.ObjectPathOf(skeleton);
            }
            else
            {
                warnings.Add($"Skeleton reference could not be loaded: {mesh.Skeleton}");
            }
        }

        var graph = new DependencyGraph
        {
            Mesh = mesh,
            MeshObjectPath = meshPath,
            MeshInfo = MeshStats.FromSkinnedAsset(mesh, meshPath),
            Skeleton = MeshStats.FromReferenceSkeleton(mesh, skeletonPath)
        };
        graph.ExportRoots.Add(mesh);
        graph.Warnings.AddRange(warnings);

        var visitedMaterials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var texturesByPath = new Dictionary<string, ExportTextureInfo>(StringComparer.OrdinalIgnoreCase);

        var slots = mesh.SkeletalMaterials ?? [];
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var slotName = SlotName(slot, i);
            UMaterialInterface? material = null;
            try
            {
                var index = slot.Material;
                if (index is { IsNull: false })
                {
                    material = index.Load<UMaterialInterface>();
                }
            }
            catch (Exception ex)
            {
                graph.Warnings.Add($"Material slot {i} ({slotName}) failed to load: {ex.GetType().Name}: {ex.Message}");
            }

            if (material is null)
            {
                graph.Materials.Add(new ExportMaterialInfo
                {
                    SlotIndex = i,
                    SlotName = slotName
                });
                graph.Warnings.Add($"Material slot {i} ({slotName}) is unresolved.");
                continue;
            }

            var materialPath = MeshStats.ObjectPathOf(material);
            graph.Materials.Add(new ExportMaterialInfo
            {
                SlotIndex = i,
                SlotName = slotName,
                ObjectPath = materialPath,
                Parent = ParentPath(material)
            });
            graph.ExportRoots.Add(material);
            CollectMaterial(material, slotName, visitedMaterials, texturesByPath, graph, collectTextures: true, depth: 0);
        }

        if (mesh.MorphTargets is { Length: > 0 })
        {
            var unresolved = 0;
            foreach (var morph in mesh.MorphTargets)
            {
                if (morph.IsNull || morph.Load() is null)
                {
                    unresolved++;
                }
            }

            if (unresolved > 0)
            {
                graph.Warnings.Add($"{unresolved} morph target references failed to load.");
            }
        }

        if (includePhysics && mesh.PhysicsAsset is { IsNull: false })
        {
            if (mesh.PhysicsAsset.Load() is null)
            {
                graph.Warnings.Add($"Physics asset could not be loaded: {mesh.PhysicsAsset}");
            }
        }

        graph.Textures.AddRange(texturesByPath.Values.OrderBy(t => t.ObjectPath, StringComparer.OrdinalIgnoreCase));
        return graph;
    }

    private static void CollectMaterial(
        UMaterialInterface material,
        string? slotName,
        HashSet<string> visited,
        Dictionary<string, ExportTextureInfo> textures,
        DependencyGraph graph,
        bool collectTextures,
        int depth)
    {
        var path = MeshStats.ObjectPathOf(material);
        if (!visited.Add(path) || depth > 8)
        {
            return;
        }

        var snapshot = new MaterialParameterSnapshot
        {
            MaterialObjectPath = path,
            SlotName = slotName
        };

        if (material is UMaterialInstanceConstant instance)
        {
            foreach (var parameter in instance.TextureParameterValues)
            {
                var texture = parameter.ParameterValue.Load<UTexture>();
                if (texture is null)
                {
                    if (!parameter.ParameterValue.IsNull)
                    {
                        graph.Warnings.Add($"Texture parameter '{parameter.Name}' on {path} failed to load.");
                    }

                    continue;
                }

                if (collectTextures)
                {
                    AddTexture(texture, parameter.Name, textures, graph);
                }

                snapshot.Textures[parameter.Name] = MeshStats.ObjectPathOf(texture);
            }

            foreach (var parameter in instance.ScalarParameterValues)
            {
                snapshot.Scalars[parameter.Name] = parameter.ParameterValue;
            }

            foreach (var parameter in instance.VectorParameterValues)
            {
                if (parameter.ParameterValue is FLinearColor color)
                {
                    snapshot.Vectors[parameter.Name] = $"{color.R:G4},{color.G:G4},{color.B:G4},{color.A:G4}";
                }
            }
        }

        try
        {
            var parameters = new CMaterialParams2();
            material.GetParams(parameters, EMaterialDepth.TopLayerOnly);

            foreach (var (name, value) in parameters.Scalars)
            {
                snapshot.Scalars.TryAdd(name, value);
            }

            foreach (var (name, color) in parameters.Colors)
            {
                snapshot.Vectors.TryAdd(name, $"{color.R:G4},{color.G:G4},{color.B:G4},{color.A:G4}");
            }
        }
        catch (Exception ex)
        {
            graph.Warnings.Add($"GetParams failed for {path}: {ex.GetType().Name}: {ex.Message}");
        }

        graph.MaterialParameters.Add(snapshot);

        if (material is UMaterialInstance instanceWithParent &&
            instanceWithParent.Parent is UMaterialInterface parent &&
            parent != material)
        {
            CollectMaterial(parent, slotName: null, visited, textures, graph, collectTextures: false, depth + 1);
        }
    }

    private static void AddTexture(
        UTexture texture,
        string? parameterName,
        Dictionary<string, ExportTextureInfo> textures,
        DependencyGraph graph)
    {
        var path = MeshStats.ObjectPathOf(texture);
        if (textures.ContainsKey(path))
        {
            return;
        }

        graph.ExportRoots.Add(texture);
        var info = new ExportTextureInfo
        {
            ObjectPath = path,
            ParameterName = parameterName,
            Width = texture.PlatformData?.SizeX,
            Height = texture.PlatformData?.SizeY,
            PixelFormat = texture.Format.ToString()
        };
        textures[path] = info;
    }

    private static string SlotName(FSkeletalMaterial slot, int index)
    {
        var name = slot.MaterialSlotName.Text;
        if (!string.IsNullOrWhiteSpace(name) && !name.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return $"Slot_{index}";
    }

    private static string? ParentPath(UMaterialInterface material)
        => material is UMaterialInstance instance && instance.Parent is UObject parent
            ? MeshStats.ObjectPathOf(parent)
            : null;
}
