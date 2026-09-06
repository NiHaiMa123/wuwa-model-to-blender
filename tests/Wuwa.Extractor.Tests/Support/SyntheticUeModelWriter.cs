using System.Text;

namespace Wuwa.Extractor.Tests;

/// <summary>
/// Minimal self-authored UEFormat writer for the P6 Blender smoke fixture.
/// Matches the v10 layout documented by UEFormat and consumed by io_scene_ueformat 0.10.0.
/// Not a production exporter — CUE4Parse-Conversion remains the only game-asset writer.
/// </summary>
internal static class SyntheticUeModelWriter
{
    public const byte FileVersion = 10; // EUEFormatVersion.AttributeFormatRestructure
    public const string ObjectName = "SmokeCube";
    public const string ObjectPath = "/Game/WuwaSmoke/SmokeCube.SmokeCube";
    public const string SkeletonPath = "/Game/WuwaSmoke/SmokeCube_Skeleton";
    public const string HairMaterial = "MI_SmokeHair";
    public const string BodyMaterial = "MI_SmokeBody";
    public const string HairPath = "/Game/WuwaSmoke/MI_SmokeHair";
    public const string BodyPath = "/Game/WuwaSmoke/MI_SmokeBody";
    public const string DiffusePath = "/Game/WuwaSmoke/T_SmokeDiffuse";
    public const string NormalPath = "/Game/WuwaSmoke/T_SmokeNormal";
    public const int VertexCount = 4;
    public const int TriangleCount = 2;
    public const int IndexCount = 6;
    public const int SectionCount = 2;
    public const int BoneCount = 2;
    public const int MorphCount = 1;
    public const int UvChannels = 1;

    public static byte[] Write()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("UEFORMAT"));
        WriteFString(writer, "UEMODEL");
        writer.Write(FileVersion);
        WriteFString(writer, ObjectName);
        WriteFString(writer, ObjectPath);
        writer.Write(false);

        WriteAttributes(writer,
            ("LODS", WriteLods),
            ("SKELETON", WriteSkeleton));
        return stream.ToArray();
    }

    private static void WriteLods(BinaryWriter writer)
    {
        writer.Write(1);
        WriteFString(writer, "LOD0");
        WriteAttributes(writer,
            ("VERTICES", w => WriteVectorArray(w, Vertices, 3)),
            ("NORMALS", WriteNormals),
            ("TEXCOORDS", WriteTexCoords),
            ("INDICES", WriteIndices),
            ("VERTEXCOLORS", WriteVertexColors),
            ("MATERIALS", WriteMaterials),
            ("WEIGHTS", WriteWeights),
            ("MORPHTARGETS", WriteMorphs));
    }

    private static readonly float[] Vertices =
    [
        0, 0, 0,
        100, 0, 0,
        100, 100, 0,
        0, 100, 0
    ];

    private static void WriteNormals(BinaryWriter writer)
    {
        writer.Write(VertexCount);
        for (var i = 0; i < VertexCount; i++)
        {
            writer.Write(1f);
            writer.Write(0f);
            writer.Write(0f);
            writer.Write(1f);
        }
    }

    private static void WriteTexCoords(BinaryWriter writer)
    {
        writer.Write(1);
        WriteFString(writer, "UV0");
        writer.Write(VertexCount);
        writer.Write(0f); writer.Write(0f);
        writer.Write(1f); writer.Write(0f);
        writer.Write(1f); writer.Write(1f);
        writer.Write(0f); writer.Write(1f);
    }

    private static void WriteIndices(BinaryWriter writer)
    {
        writer.Write(IndexCount);
        writer.Write(0u);
        writer.Write(1u);
        writer.Write(2u);
        writer.Write(0u);
        writer.Write(2u);
        writer.Write(3u);
    }

    private static void WriteVertexColors(BinaryWriter writer)
    {
        writer.Write(1);
        WriteFString(writer, "COL0");
        writer.Write(VertexCount);
        for (var i = 0; i < VertexCount; i++)
        {
            writer.Write((byte)255);
            writer.Write((byte)255);
            writer.Write((byte)255);
            writer.Write((byte)255);
        }
    }

    private static void WriteMaterials(BinaryWriter writer)
    {
        writer.Write(2);
        WriteFString(writer, HairMaterial);
        WriteFString(writer, HairPath);
        writer.Write(0);
        writer.Write(1);
        WriteFString(writer, BodyMaterial);
        WriteFString(writer, BodyPath);
        writer.Write(3);
        writer.Write(1);
    }

    private static void WriteWeights(BinaryWriter writer)
    {
        writer.Write(VertexCount);
        for (var vertex = 0; vertex < VertexCount; vertex++)
        {
            writer.Write((ushort)0);
            writer.Write(vertex);
            writer.Write(1f);
        }
    }

    private static void WriteMorphs(BinaryWriter writer)
    {
        writer.Write(1);
        WriteFString(writer, "Smile");
        writer.Write(1);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(10f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);
        writer.Write(2);
    }

    private static void WriteSkeleton(BinaryWriter writer)
    {
        WriteAttributes(writer,
            ("METADATA", w => WriteFString(w, SkeletonPath)),
            ("BONES", WriteBones));
    }

    private static void WriteBones(BinaryWriter writer)
    {
        writer.Write(BoneCount);
        WriteBone(writer, "Root", -1, 0, 0, 0);
        WriteBone(writer, "Spine", 0, 0, 50, 0);
    }

    private static void WriteBone(BinaryWriter writer, string name, int parent, float x, float y, float z)
    {
        WriteFString(writer, name);
        writer.Write(parent);
        writer.Write(x); writer.Write(y); writer.Write(z);
        writer.Write(0f); writer.Write(0f); writer.Write(0f); writer.Write(1f);
        writer.Write(1f); writer.Write(1f); writer.Write(1f);
    }

    private static void WriteVectorArray(BinaryWriter writer, float[] values, int width)
    {
        writer.Write(values.Length / width);
        foreach (var value in values)
        {
            writer.Write(value);
        }
    }

    private static void WriteAttributes(BinaryWriter writer, params (string Name, Action<BinaryWriter> Write)[] attributes)
    {
        writer.Write(attributes.Length);
        foreach (var (name, write) in attributes)
        {
            WriteFString(writer, name);
            using var payload = new MemoryStream();
            using (var inner = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
            {
                write(inner);
            }

            var bytes = payload.ToArray();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }

    private static void WriteFString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
