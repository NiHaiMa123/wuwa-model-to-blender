using System.Text.Json;
using Wuwa.Core;
using Wuwa.Export;
using Wuwa.Extractor;

namespace Wuwa.Cli;

public static class ExportRunner
{
    public static async Task<ExportPipelineResult> RunAsync(
        string configPath,
        ExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = ConfigLoader.Load(configPath);
        var aes = await AesKeyProviderFactory.Create(config.Decryption.Aes).GetAsync(cancellationToken).ConfigureAwait(false);
        var mappings = await MappingsProviderFactory.Create(config.Decryption.Mappings).GetAsync(cancellationToken).ConfigureAwait(false);
        using var session = Cue4ParseMount.Open(config.Game.PaksDir, config.Game.UeVersion, aes, mappings);
        return await ExportPipeline.RunAsync(config, session, aes, mappings, request, cancellationToken).ConfigureAwait(false);
    }

    public static void WriteHuman(ExportPipelineResult result, TextWriter writer)
    {
        var manifest = result.Manifest;
        writer.WriteLine($"wuwa2blender export  tool={ToolVersions.Tool}  job={manifest.JobId}");
        writer.WriteLine($"source  {manifest.SourceObjectPath}");
        writer.WriteLine($"unreal  {manifest.UnrealObjectPath}");
        writer.WriteLine($"ue      {manifest.UeVersion}  game={manifest.GameVersion}");
        if (manifest.Mesh is { } mesh)
        {
            writer.WriteLine($"mesh    lods={mesh.LodCount} slots={mesh.MaterialSlotCount} morphs={mesh.MorphTargetCount} uv={mesh.UvChannels} vcol={(mesh.HasVertexColors ? "yes" : "no")}");
            var lod0 = mesh.Lods.FirstOrDefault(l => l.Index == 0);
            if (lod0 is not null)
            {
                writer.WriteLine($"lod0    verts={lod0.Vertices} tris={lod0.Triangles} indices={lod0.Indices} sections={lod0.Sections}");
            }

            if (!string.IsNullOrWhiteSpace(mesh.UeModel))
            {
                writer.WriteLine($"uemodel {mesh.UeModel}");
            }
        }

        if (manifest.Skeleton is { } skeleton)
        {
            writer.WriteLine($"skel    bones={skeleton.BoneCount} sockets={skeleton.SocketCount} root={skeleton.RootBone ?? "-"}");
        }

        writer.WriteLine($"deps    materials={manifest.Materials.Count} textures={manifest.Textures.Count} files={manifest.Files.Count}");
        writer.WriteLine($"out     {result.JobDirectory}");
        if (manifest.Golden is { } golden)
        {
            writer.WriteLine($"golden  {(golden.Matched ? "match" : "mismatch")}");
            foreach (var mismatch in golden.Mismatches)
            {
                writer.WriteLine($"        {mismatch}");
            }
        }

        foreach (var warning in manifest.Warnings)
        {
            writer.WriteLine($"warn    {warning}");
        }
    }

    public static async Task WriteResultCopyAsync(ExportManifest manifest, string resultPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
        await using var stream = File.Create(resultPath);
        await JsonSerializer.SerializeAsync(stream, manifest, ConfigLoader.JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
