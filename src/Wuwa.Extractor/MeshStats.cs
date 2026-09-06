using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using Wuwa.Core;

namespace Wuwa.Extractor;

public static class MeshStats
{
    public static ExportMeshInfo FromSkinnedAsset(USkeletalMesh mesh, string objectPath)
    {
        var lods = new List<ExportLodInfo>();
        var models = mesh.LODModels ?? [];
        for (var i = 0; i < models.Length; i++)
        {
            lods.Add(FromLod(i, models[i]));
        }

        var lod0 = lods.FirstOrDefault(l => l.Index == 0) ?? lods.FirstOrDefault();
        return new ExportMeshInfo
        {
            ObjectPath = objectPath,
            ExportType = ExportTypeNames.ShortName(mesh.ExportType ?? mesh.GetType().Name),
            LodCount = models.Length,
            Lods = lods,
            MaterialSlotCount = mesh.SkeletalMaterials?.Length ?? 0,
            MorphTargetCount = mesh.MorphTargets?.Length ?? 0,
            HasVertexColors = mesh.bHasVertexColors,
            UvChannels = lod0?.UvChannels ?? 0
        };
    }

    public static ExportLodInfo FromLod(int index, FStaticLODModel lod)
    {
        var indexCount = lod.Indices?.Buffer?.Length ?? 0;
        var trianglesFromSections = lod.Sections?.Sum(s => s.NumTriangles) ?? 0;
        var triangles = trianglesFromSections > 0 ? trianglesFromSections : indexCount / 3;
        return new ExportLodInfo
        {
            Index = index,
            Vertices = lod.NumVertices,
            Triangles = triangles,
            Indices = indexCount > 0 ? indexCount : triangles * 3,
            Sections = lod.Sections?.Length ?? 0,
            UvChannels = lod.NumTexCoords
        };
    }

    public static ExportSkeletonInfo FromReferenceSkeleton(USkeletalMesh mesh, string? skeletonPath)
    {
        var bones = mesh.ReferenceSkeleton?.FinalRefBoneInfo ?? [];
        var names = bones.Select(b => b.Name.Text).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        return new ExportSkeletonInfo
        {
            ObjectPath = skeletonPath,
            BoneCount = bones.Length,
            SocketCount = mesh.Sockets?.Length ?? 0,
            RootBone = names.Count > 0 ? names[0] : null,
            Bones = names
        };
    }

    public static string ObjectPathOf(CUE4Parse.UE4.Assets.Exports.UObject obj)
        => PathNormalization.NormalizeLocal(obj.GetPathName());
}
