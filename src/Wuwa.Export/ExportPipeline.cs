using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using Wuwa.Core;
using Wuwa.Extractor;

namespace Wuwa.Export;

public sealed class ExportPipelineResult
{
    public required ExportManifest Manifest { get; init; }
    public required string JobDirectory { get; init; }
    public required string ManifestPath { get; init; }
    public required string LogPath { get; init; }
}

public static class ExportPipeline
{
    public static async Task<ExportPipelineResult> RunAsync(
        AppConfig config,
        Cue4ParseSession session,
        AesKeySet aes,
        MappingsDescriptor mappings,
        ExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectPathResolver.LooksLikeObjectPath(request.Asset))
        {
            throw new InvalidOperationException(
                $"export requires an Unreal object path, got '{request.Asset}'. Run search first, then pass the object path.");
        }

        var loaded = ObjectLoader.LoadRequired(session.Provider, request.Asset);
        if (loaded is not USkeletalMesh mesh)
        {
            throw new InvalidOperationException(
                $"'{request.Asset}' loaded as {loaded.ExportType}, expected SkeletalMesh.");
        }

        var graph = DependencyWalker.Walk(mesh, includePhysics: false);
        var jobDirectory = ResolveJobDirectory(config, request, mesh.Name);
        Directory.CreateDirectory(jobDirectory);
        var logDir = Path.Combine(jobDirectory, "logs");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "export.log");
        var logLines = new List<string>
        {
            $"wuwa2blender export {ToolVersions.Tool}",
            $"job {Path.GetFileName(jobDirectory)}",
            $"source {graph.MeshObjectPath}",
            $"lods {graph.MeshInfo.LodCount}  slots {graph.MeshInfo.MaterialSlotCount}  morphs {graph.MeshInfo.MorphTargetCount}  textures {graph.Textures.Count}  bones {graph.Skeleton.BoneCount}"
        };

        if (request.IncludeAnimations)
        {
            graph.Warnings.Add("Animation export is not enabled in P3; pass the mesh only. P4+ will add optional .ueanim staging.");
        }

        var exportOptions = new UeFormatExportOptions(request.MeshQuality, ExportMaterials: true, ExportMorphTargets: true);
        IReadOnlyList<CUE4Parse_Conversion.ExportResult> results;
        try
        {
            results = await UeFormatAdapter.ExportAsync(graph.ExportRoots, jobDirectory, exportOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            graph.Warnings.Add($"UEFormat exporter threw {ex.GetType().Name}: {ex.Message}");
            logLines.Add(ex.ToString());
            results = [];
        }

        var filesByObject = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var allFiles = new List<string>();
        var failed = 0;
        foreach (var result in results)
        {
            if (!result.Success)
            {
                failed++;
                graph.Warnings.Add($"export failed for {result.ObjectPath}: {result.Error?.Message ?? "unknown error"}");
                continue;
            }

            if (result.DiskFilePaths is null)
            {
                continue;
            }

            var relatives = new List<string>();
            foreach (var disk in result.DiskFilePaths)
            {
                if (string.IsNullOrWhiteSpace(disk) || !File.Exists(disk))
                {
                    continue;
                }

                var relative = UeFormatAdapter.RelativeToJob(jobDirectory, disk);
                relatives.Add(relative);
                allFiles.Add(relative);
            }

            filesByObject[PathNormalization.NormalizeLocal(result.ObjectPath)] = relatives;
        }

        if (failed > 0)
        {
            logLines.Add($"exporter failures: {failed}");
        }

        var ueModel = FirstFile(filesByObject, graph.MeshObjectPath, ".uemodel")
                      ?? allFiles.FirstOrDefault(f => f.EndsWith(".uemodel", StringComparison.OrdinalIgnoreCase));
        if (ueModel is null)
        {
            graph.Warnings.Add("No .uemodel was written.");
        }

        graph.MeshInfo.UeModel = ueModel;
        foreach (var material in graph.Materials)
        {
            if (!string.IsNullOrWhiteSpace(material.ObjectPath))
            {
                material.JsonFile = FirstFile(filesByObject, material.ObjectPath, ".json");
            }
        }

        foreach (var texture in graph.Textures)
        {
            texture.File = FirstFile(filesByObject, texture.ObjectPath, ".png")
                           ?? FirstFile(filesByObject, texture.ObjectPath, ".tga")
                           ?? FirstFile(filesByObject, texture.ObjectPath, ".hdr")
                           ?? FirstFile(filesByObject, texture.ObjectPath, ".dds");
        }

        var meshInfo = graph.MeshInfo;
        var materials = graph.Materials;
        var textures = graph.Textures;

        foreach (var texture in textures.Where(t => string.IsNullOrWhiteSpace(t.File)))
        {
            graph.Warnings.Add($"Texture was walked but not written: {texture.ObjectPath}");
        }

        var uniqueFiles = allFiles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        var fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relative in uniqueFiles)
        {
            var full = Path.Combine(jobDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
            {
                fileHashes[relative] = ContentHashing.Sha256File(full);
            }
        }

        var jobId = Path.GetFileName(jobDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var manifest = new ExportManifest
        {
            SchemaVersion = ToolVersions.ManifestSchema,
            JobId = jobId,
            Timestamp = DateTimeOffset.UtcNow,
            GameVersion = config.Game.Version,
            UeVersion = session.Info.UeVersion,
            SourceObjectPath = graph.MeshObjectPath,
            UnrealObjectPath = PathNormalization.ToUnrealObjectPath(graph.MeshObjectPath),
            ToolVersions = new Dictionary<string, string>
            {
                ["wuwa2blender"] = ToolVersions.Tool,
                ["cue4parse"] = "1.2.2.202609",
                ["cue4parse-conversion"] = "1.2.2.202609",
                ["ueformat"] = "CUE4Parse-Conversion UEFormat"
            },
            Mesh = meshInfo,
            Skeleton = graph.Skeleton,
            Materials = materials,
            Textures = textures,
            Animations = [],
            MaterialParameters = graph.MaterialParameters,
            Files = uniqueFiles,
            SourceHashes = new SourceHashInfo
            {
                AesSource = aes.SourceId,
                AesContentHash = aes.ContentHash,
                MappingsSource = mappings.SourceId,
                MappingsContentHash = mappings.ContentHash,
                Files = fileHashes
            },
            Warnings = graph.Warnings.Distinct(StringComparer.Ordinal).ToList()
        };

        if (GoldenInvariants.Matches(graph.MeshObjectPath) || GoldenInvariants.Matches(request.Asset))
        {
            manifest.Golden = GoldenInvariants.Compare(manifest);
            if (manifest.Golden is { Matched: false })
            {
                foreach (var mismatch in manifest.Golden.Mismatches)
                {
                    manifest.Warnings.Add("golden: " + mismatch);
                }
            }
        }

        var manifestPath = Path.Combine(jobDirectory, "manifest.json");
        await ManifestWriter.WriteAsync(manifestPath, manifest, cancellationToken).ConfigureAwait(false);
        var warningsPath = Path.Combine(jobDirectory, "warnings.json");
        await File.WriteAllTextAsync(
            warningsPath,
            System.Text.Json.JsonSerializer.Serialize(new { manifest.JobId, manifest.Warnings }, ConfigLoader.JsonOptions),
            cancellationToken).ConfigureAwait(false);

        logLines.Add($"wrote {uniqueFiles.Count} files, {manifest.Warnings.Count} warnings");
        logLines.Add($"manifest {manifestPath}");
        if (manifest.Golden is not null)
        {
            logLines.Add($"golden {(manifest.Golden.Matched ? "match" : "mismatch")}");
            logLines.AddRange(manifest.Golden.Mismatches.Select(m => "  " + m));
        }

        await File.WriteAllLinesAsync(logPath, logLines, cancellationToken).ConfigureAwait(false);
        return new ExportPipelineResult
        {
            Manifest = manifest,
            JobDirectory = jobDirectory,
            ManifestPath = manifestPath,
            LogPath = logPath
        };
    }

    public static string ResolveJobDirectory(AppConfig config, ExportRequest request, string meshName)
    {
        if (!string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return Path.GetFullPath(request.OutputDirectory);
        }

        var root = string.IsNullOrWhiteSpace(config.Output.Root)
            ? Path.Combine(Directory.GetCurrentDirectory(), "work", "exports")
            : config.Output.Root;
        var safe = string.Concat(meshName.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "export";
        }

        return Path.GetFullPath(Path.Combine(root, $"{safe}-{DateTime.Now:yyyyMMdd-HHmmss}"));
    }

    private static string? FirstFile(IReadOnlyDictionary<string, List<string>> files, string objectPath, string extension)
    {
        var key = PathNormalization.NormalizeLocal(objectPath);
        if (files.TryGetValue(key, out var list))
        {
            var direct = list.FirstOrDefault(f => f.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
            if (direct is not null)
            {
                return direct;
            }
        }

        var archive = PathNormalization.ToArchiveObjectPath(key);
        var leaf = ObjectPathResolver.PackageFileName(key);
        foreach (var (candidate, value) in files)
        {
            var candidateLeaf = ObjectPathResolver.PackageFileName(candidate);
            if (PathNormalization.ToArchiveObjectPath(candidate).Equals(archive, StringComparison.OrdinalIgnoreCase) ||
                candidateLeaf.Equals(leaf, StringComparison.OrdinalIgnoreCase))
            {
                var hit = value.FirstOrDefault(f => f.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
                if (hit is not null)
                {
                    return hit;
                }
            }
        }

        return null;
    }
}
