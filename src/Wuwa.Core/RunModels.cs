using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wuwa.Core;

public static class RunStages
{
    public const string ResolveConfig = "ResolveConfig";
    public const string Doctor = "Doctor";
    public const string Index = "Index";
    public const string ResolveDependencies = "ResolveDependencies";
    public const string Export = "Export";
    public const string ValidateExport = "ValidateExport";
    public const string LaunchBlender = "LaunchBlender";
    public const string ValidateBlend = "ValidateBlend";
    public const string SaveResult = "SaveResult";

    public static readonly string[] Order =
    [
        ResolveConfig,
        Doctor,
        Index,
        ResolveDependencies,
        Export,
        ValidateExport,
        LaunchBlender,
        ValidateBlend,
        SaveResult
    ];

    public static int IndexOf(string id)
    {
        for (var i = 0; i < Order.Length; i++)
        {
            if (Order[i].Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public static string? Canonical(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var index = IndexOf(id);
        return index < 0 ? null : Order[index];
    }
}

public static class RunStageStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Pass = "pass";
    public const string Warn = "warn";
    public const string Fail = "fail";
    public const string Skipped = "skipped";

    public static bool Completed(string status)
        => status is Pass or Warn or Skipped;

    public static bool Failed(string status)
        => status == Fail;
}

public sealed class RunRequest
{
    public required string Asset { get; init; }
    public string? SavePath { get; init; }
    public string? OutputDirectory { get; init; }
    public string? ProfilePath { get; init; }
    public string? ReportPath { get; init; }
    public string? JobId { get; init; }
    public string? ResultPath { get; init; }
    public string? FromStage { get; init; }
    public bool Force { get; init; }
    public bool PackImages { get; init; } = true;
    public bool IncludeAnimations { get; init; }
    public string MeshQuality { get; init; } = "highest";
}

public sealed class RunFingerprints
{
    public string Config { get; set; } = "";
    public string Source { get; set; } = "";
    public string Profile { get; set; } = "";
    public string Combined { get; set; } = "";
}

public sealed class RunStageRecord
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = RunStageStatus.Pending;
    public DateTimeOffset? Started { get; set; }
    public DateTimeOffset? Finished { get; set; }
    public string Summary { get; set; } = "";
    public Dictionary<string, string> Details { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RunJob
{
    public string SchemaVersion { get; set; } = "1";
    public string ToolVersion { get; set; } = ToolVersions.Tool;
    public string JobId { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string ConfigPath { get; set; } = "";
    public string AssetInput { get; set; } = "";
    public string ResolvedAsset { get; set; } = "";
    public string SavePath { get; set; } = "";
    public string ExportDirectory { get; set; } = "";
    public string ManifestPath { get; set; } = "";
    public string BlendPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public string JobPath { get; set; } = "";
    public string LogPath { get; set; } = "";
    public string ProfilePath { get; set; } = "";
    public RunFingerprints Fingerprints { get; set; } = new();
    public string OverallStatus { get; set; } = DoctorStatus.Fail;
    public bool ReusedExport { get; set; }
    public bool ReusedBlend { get; set; }
    public List<RunStageRecord> Stages { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];

    [JsonIgnore]
    public bool Ok => OverallStatus is DoctorStatus.Pass or DoctorStatus.Warn;

    public RunStageRecord Stage(string id)
        => Stages.First(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public RunStageRecord? TryStage(string id)
        => Stages.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static RunJob Create(string jobId)
    {
        return new RunJob
        {
            JobId = jobId,
            ToolVersion = ToolVersions.Tool,
            Timestamp = DateTimeOffset.UtcNow,
            Stages = RunStages.Order.Select(id => new RunStageRecord { Id = id }).ToList()
        };
    }

    public void RefreshOverall()
    {
        if (Stages.Any(s => s.Status == RunStageStatus.Fail))
        {
            OverallStatus = DoctorStatus.Fail;
            return;
        }

        if (Errors.Count > 0)
        {
            OverallStatus = DoctorStatus.Fail;
            return;
        }

        if (Stages.Any(s => s.Status is RunStageStatus.Warn or DoctorStatus.Warn))
        {
            OverallStatus = DoctorStatus.Warn;
            return;
        }

        OverallStatus = DoctorStatus.Pass;
    }
}

public static class RunJobLayout
{
    public static string DeriveJobId(string asset, string? savePath, string? explicitId)
    {
        if (!string.IsNullOrWhiteSpace(explicitId))
        {
            return Sanitize(explicitId);
        }

        if (!string.IsNullOrWhiteSpace(savePath))
        {
            return Sanitize(Path.GetFileNameWithoutExtension(savePath));
        }

        if (ObjectPathResolver.LooksLikeObjectPath(asset))
        {
            var (_, export) = ObjectPathResolver.Split(asset);
            var leaf = string.IsNullOrWhiteSpace(export)
                ? ObjectPathResolver.PackageFileName(asset)
                : export;
            var dot = leaf.IndexOf('.');
            if (dot > 0)
            {
                leaf = leaf[..dot];
            }

            return Sanitize(leaf);
        }

        return Sanitize(asset);
    }

    public static string Sanitize(string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "run" : value.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(c, '_');
        }

        trimmed = trimmed.Replace(' ', '_');
        return string.IsNullOrWhiteSpace(trimmed) ? "run" : trimmed;
    }

    public static string JobDirectory(string workingDirectory, string jobId)
        => Path.GetFullPath(Path.Combine(workingDirectory, "work", "runs", jobId));

    public static string DefaultJobPath(string workingDirectory, string jobId)
        => Path.Combine(JobDirectory(workingDirectory, jobId), "job.json");

    public static string DefaultExportDirectory(string workingDirectory, string jobId)
        => Path.GetFullPath(Path.Combine(workingDirectory, "work", "exports", jobId));

    public static string DefaultSavePath(string workingDirectory, string jobId)
        => Path.GetFullPath(Path.Combine(workingDirectory, "work", "blend", $"{jobId}.blend"));

    public static string CanonicalAsset(string asset)
    {
        var normalized = PathNormalization.NormalizeLocal(asset);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "";
        }

        var archive = PathNormalization.ToArchiveObjectPath(normalized);
        var (package, export) = ObjectPathResolver.Split(archive);
        if (string.IsNullOrWhiteSpace(export))
        {
            var leaf = ObjectPathResolver.PackageFileName(package);
            return string.IsNullOrWhiteSpace(leaf) ? package : $"{package}.{leaf}";
        }

        return $"{package}.{export}";
    }
}

public static class RunFingerprint
{
    public static RunFingerprints Compute(RunFingerprintInput input)
    {
        var config = HashLines(
            "ue=" + input.UeVersion,
            "game=" + input.GameVersion,
            "paks=" + input.PaksDir,
            "install=" + input.InstallDir,
            "blender=" + input.BlenderExe,
            "aesMode=" + input.AesMode,
            "aesEndpoint=" + input.AesEndpoint,
            "mappingsMode=" + input.MappingsMode);
        var source = HashLines(
            "tool=" + input.ToolVersion,
            "asset=" + input.CanonicalAsset,
            "lods=" + input.MeshQuality,
            "anim=" + (input.IncludeAnimations ? "1" : "0"),
            "aes=" + input.AesHash,
            "mappings=" + input.MappingsHash,
            "ue=" + input.UeVersion);
        var profile = HashLines(
            "profile=" + input.ProfilePath,
            "profileHash=" + input.ProfileHash,
            "pack=" + (input.PackImages ? "1" : "0"),
            "save=" + input.SavePath,
            "blender=" + input.BlenderExe);
        return new RunFingerprints
        {
            Config = config,
            Source = source,
            Profile = profile,
            Combined = ContentHashing.Sha256Hex(config + source + profile)
        };
    }

    private static string HashLines(params string[] lines)
        => ContentHashing.Sha256Hex(string.Join('\n', lines));
}

public sealed record RunFingerprintInput
{
    public string ToolVersion { get; init; } = ToolVersions.Tool;
    public string CanonicalAsset { get; init; } = "";
    public string SavePath { get; init; } = "";
    public string ProfilePath { get; init; } = "";
    public string ProfileHash { get; init; } = "";
    public bool PackImages { get; init; } = true;
    public bool IncludeAnimations { get; init; }
    public string MeshQuality { get; init; } = "highest";
    public string UeVersion { get; init; } = "";
    public string GameVersion { get; init; } = "";
    public string PaksDir { get; init; } = "";
    public string InstallDir { get; init; } = "";
    public string BlenderExe { get; init; } = "";
    public string AesMode { get; init; } = "";
    public string AesEndpoint { get; init; } = "";
    public string AesHash { get; init; } = "";
    public string MappingsMode { get; init; } = "";
    public string MappingsHash { get; init; } = "";
}

public static class RunCache
{
    public static bool CanSkipDoctor(RunJob? previous, string configFingerprint, string blenderExe, bool force)
    {
        if (force || previous is null)
        {
            return false;
        }

        if (!string.Equals(previous.Fingerprints.Config, configFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(blenderExe) || !File.Exists(blenderExe))
        {
            return false;
        }

        return RunStageStatus.Completed(previous.TryStage(RunStages.Doctor)?.Status ?? "");
    }

    public static bool CanSkipIndex(RunJob? previous, string assetInput, string? resolvedIfKnown, bool force)
    {
        if (force || previous is null)
        {
            return false;
        }

        if (!RunStageStatus.Completed(previous.TryStage(RunStages.Index)?.Status ?? ""))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(previous.ResolvedAsset))
        {
            return false;
        }

        if (string.Equals(previous.AssetInput, assetInput, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!ObjectPathResolver.LooksLikeObjectPath(assetInput))
        {
            return false;
        }

        var current = RunJobLayout.CanonicalAsset(string.IsNullOrWhiteSpace(resolvedIfKnown) ? assetInput : resolvedIfKnown);
        return current.Equals(RunJobLayout.CanonicalAsset(previous.ResolvedAsset), StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanSkipExport(
        RunJob? previous,
        string sourceFingerprint,
        string manifestPath,
        string exportDirectory,
        string canonicalAsset,
        string aesHash,
        bool force)
    {
        if (force)
        {
            return false;
        }

        if (!ExportStaging.IsReusableFor(manifestPath, exportDirectory, canonicalAsset, aesHash, out _))
        {
            return false;
        }

        if (previous is null)
        {
            return true;
        }

        return string.Equals(previous.Fingerprints.Source, sourceFingerprint, StringComparison.Ordinal);
    }

    public static bool CanSkipBlender(
        RunJob? previous,
        string profileFingerprint,
        string sourceFingerprint,
        string blendPath,
        string reportPath,
        bool force)
    {
        if (force || previous is null)
        {
            return false;
        }

        if (!string.Equals(previous.Fingerprints.Profile, profileFingerprint, StringComparison.Ordinal) ||
            !string.Equals(previous.Fingerprints.Source, sourceFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        if (!File.Exists(blendPath) || !File.Exists(reportPath))
        {
            return false;
        }

        return RunStageStatus.Completed(previous.TryStage(RunStages.LaunchBlender)?.Status ?? "") &&
               RunStageStatus.Completed(previous.TryStage(RunStages.ValidateBlend)?.Status ?? "");
    }

    public static bool MustRunFrom(string stageId, string? fromStage)
    {
        var from = RunStages.Canonical(fromStage);
        if (from is null)
        {
            return false;
        }

        return RunStages.IndexOf(stageId) >= RunStages.IndexOf(from);
    }

    public static bool CanSkipBefore(string stageId, string? fromStage, RunJob? previous)
    {
        var from = RunStages.Canonical(fromStage);
        if (from is null)
        {
            return false;
        }

        if (RunStages.IndexOf(stageId) >= RunStages.IndexOf(from))
        {
            return false;
        }

        return previous is not null && RunStageStatus.Completed(previous.TryStage(stageId)?.Status ?? "");
    }
}

public static class ExportStaging
{
    public static bool IsReusable(string manifestPath, string exportDirectory, out string reason)
    {
        if (!File.Exists(manifestPath))
        {
            reason = "manifest.json is missing";
            return false;
        }

        ExportManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ExportManifest>(File.ReadAllText(manifestPath), ConfigLoader.JsonOptions);
        }
        catch (Exception ex)
        {
            reason = "manifest.json is not readable: " + ex.Message;
            return false;
        }

        if (manifest is null)
        {
            reason = "manifest.json is empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.Mesh?.UeModel))
        {
            reason = "manifest has no .uemodel";
            return false;
        }

        if (manifest.Golden is { Matched: false })
        {
            reason = "stored export golden comparison failed";
            return false;
        }

        var missing = MissingFiles(manifest, exportDirectory);
        if (missing.Count > 0)
        {
            reason = "staging file missing: " + missing[0];
            return false;
        }

        reason = "ok";
        return true;
    }

    public static bool IsReusableFor(
        string manifestPath,
        string exportDirectory,
        string canonicalAsset,
        string aesHash,
        out string reason)
    {
        if (!IsReusable(manifestPath, exportDirectory, out reason))
        {
            return false;
        }

        var manifest = JsonSerializer.Deserialize<ExportManifest>(File.ReadAllText(manifestPath), ConfigLoader.JsonOptions);
        if (manifest is null)
        {
            reason = "manifest.json is empty";
            return false;
        }

        var stored = RunJobLayout.CanonicalAsset(manifest.SourceObjectPath);
        if (!stored.Equals(canonicalAsset, StringComparison.OrdinalIgnoreCase))
        {
            reason = "manifest source path differs from the requested asset";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(aesHash) &&
            !string.IsNullOrWhiteSpace(manifest.SourceHashes.AesContentHash) &&
            !aesHash.Equals(manifest.SourceHashes.AesContentHash, StringComparison.OrdinalIgnoreCase))
        {
            reason = "AES content hash changed since the cached export";
            return false;
        }

        reason = "ok";
        return true;
    }

    public static List<string> MissingFiles(ExportManifest manifest, string exportDirectory)
    {
        var missing = new List<string>();
        foreach (var relative in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            var full = Path.Combine(exportDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                missing.Add(relative);
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest.Mesh?.UeModel))
        {
            var ueModel = Path.Combine(exportDirectory, manifest.Mesh.UeModel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(ueModel) && !missing.Contains(manifest.Mesh.UeModel, StringComparer.OrdinalIgnoreCase))
            {
                missing.Add(manifest.Mesh.UeModel);
            }
        }

        return missing;
    }
}

public static class RunJobStore
{
    public static RunJob? TryLoad(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RunJob>(File.ReadAllText(path), ConfigLoader.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async Task SaveAsync(RunJob job, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        job.JobPath = path;
        job.RefreshOverall();
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, job, ConfigLoader.JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}

public static class RunIndex
{
    public static bool NeedsSearch(string asset)
        => !ObjectPathResolver.LooksLikeObjectPath(asset);
}
