using System.Diagnostics;
using System.Text.Json;
using Wuwa.Core;
using Wuwa.Extractor;

namespace Wuwa.Cli;

public static class DoctorRunner
{
    public static async Task<DoctorResult> RunAsync(
        string configPath,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<DoctorCheck>();
        AppConfig? config = null;

        try
        {
            config = ConfigLoader.Load(configPath);
            checks.Add(Pass("config", $"Loaded {configPath}", new()
            {
                ["ueVersion"] = config.Game.UeVersion,
                ["gameVersion"] = config.Game.Version
            }));
        }
        catch (Exception ex)
        {
            checks.Add(Fail("config", ex.Message));
            return Finish(checks, configPath);
        }

        checks.Add(CheckDirectory("game-install-dir", config.Game.InstallDir, "Game install directory"));
        checks.Add(CheckDirectory("paks-dir", config.Game.PaksDir, "PAK/IoStore directory"));

        var archives = ArchiveDiscovery.Scan(config.Game.PaksDir);
        if (!archives.HasArchives)
        {
            checks.Add(Fail(
                "archives",
                $"No .pak or .utoc files in {config.Game.PaksDir}",
                Details(archives)));
        }
        else
        {
            var summary = $"{archives.PakCount} pak, {archives.UtocCount} utoc, {archives.UcasCount} ucas, {archives.SigCount} sig";
            var status = archives.UtocCount == 0 && archives.PakCount > 0
                ? DoctorStatus.Pass
                : DoctorStatus.Pass;
            checks.Add(new DoctorCheck
            {
                Id = "archives",
                Status = status,
                Summary = summary,
                Details = Details(archives)
            });
        }

        AesKeySet? aes = null;
        try
        {
            var provider = AesKeyProviderFactory.Create(config.Decryption.Aes);
            aes = await provider.GetAsync(cancellationToken).ConfigureAwait(false);
            if (aes.KeyCount == 0)
            {
                checks.Add(Fail("aes", "AES provider returned zero keys.", new()
                {
                    ["source"] = aes.SourceId,
                    ["hash"] = aes.ContentHash
                }));
            }
            else
            {
                checks.Add(Pass("aes", aes.RedactedSummary(), new()
                {
                    ["source"] = aes.SourceId,
                    ["hash"] = aes.ContentHash,
                    ["keyCount"] = aes.KeyCount.ToString()
                }));
            }
        }
        catch (Exception ex)
        {
            checks.Add(Fail("aes", Locate(ex, "AES")));
        }

        MappingsDescriptor mappings = new("none", "", null, false);
        try
        {
            var provider = MappingsProviderFactory.Create(config.Decryption.Mappings);
            mappings = await provider.GetAsync(cancellationToken).ConfigureAwait(false);
            if (!mappings.Available)
            {
                checks.Add(Warn(
                    "mappings",
                    "No mappings provider configured. P0 FModel path for 3.6.0 mounted without a .usmap; property names may be incomplete.",
                    new() { ["mode"] = config.Decryption.Mappings.Mode }));
            }
            else
            {
                checks.Add(Pass("mappings", $"source={mappings.SourceId}; hash={mappings.ContentHash}", new()
                {
                    ["source"] = mappings.SourceId,
                    ["hash"] = mappings.ContentHash
                }));
            }
        }
        catch (Exception ex)
        {
            checks.Add(Fail("mappings", Locate(ex, "Mappings")));
        }

        if (aes is not null && aes.KeyCount > 0 && archives.HasArchives)
        {
            try
            {
                var mount = Cue4ParseMount.Mount(config.Game.PaksDir, config.Game.UeVersion, aes, mappings);
                var details = new Dictionary<string, string>
                {
                    ["ueVersion"] = mount.UeVersion,
                    ["mounted"] = mount.MountedCount.ToString(),
                    ["unloaded"] = mount.UnloadedCount.ToString(),
                    ["files"] = mount.FileCount.ToString()
                };
                if (mount.UnloadedCount > 0)
                {
                    checks.Add(Warn(
                        "cue4parse-index",
                        $"{mount.UeVersion}: mounted {mount.MountedCount}, unloaded {mount.UnloadedCount}, files {mount.FileCount}",
                        details));
                }
                else
                {
                    checks.Add(Pass(
                        "cue4parse-index",
                        $"{mount.UeVersion}: mounted {mount.MountedCount} archives, {mount.FileCount} files",
                        details));
                }
            }
            catch (Exception ex)
            {
                checks.Add(Fail("cue4parse-index", Locate(ex, "CUE4Parse")));
            }
        }
        else
        {
            checks.Add(Fail("cue4parse-index", "Skipped: archives or AES keys are not available."));
        }

        AddBlenderChecks(config, checks);
        return Finish(checks, configPath);
    }

    public static async Task WriteResultAsync(DoctorResult result, string resultPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
        await using var stream = File.Create(resultPath);
        await JsonSerializer.SerializeAsync(stream, result, ConfigLoader.JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public static void WriteHuman(DoctorResult result, TextWriter writer)
    {
        writer.WriteLine($"wuwa2blender doctor  tool={result.ToolVersion}  overall={result.OverallStatus}");
        writer.WriteLine($"config: {result.ConfigPath}");
        foreach (var check in result.Checks)
        {
            writer.WriteLine($"[{check.Status,-4}] {check.Id}: {check.Summary}");
        }
    }

    private static void AddBlenderChecks(AppConfig config, List<DoctorCheck> checks)
    {
        var exe = config.Blender.Executable;
        if (string.IsNullOrWhiteSpace(exe))
        {
            checks.Add(Fail("blender-exe", "blender.executable is empty in config."));
            checks.Add(Fail("blender-version", "Skipped: no Blender executable."));
            checks.Add(AddonCheck(config, version: null));
            return;
        }

        if (!File.Exists(exe))
        {
            checks.Add(Fail("blender-exe", $"Blender executable not found: {exe}"));
            checks.Add(Fail("blender-version", "Skipped: Blender executable missing."));
            checks.Add(AddonCheck(config, version: null));
            return;
        }

        checks.Add(Pass("blender-exe", exe));

        var version = ReadBlenderVersion(exe, out var versionError);
        if (version is null)
        {
            checks.Add(Fail("blender-version", versionError ?? "Could not read Blender version."));
            checks.Add(AddonCheck(config, version: null));
            return;
        }

        var target = config.Blender.TargetVersion.Trim();
        if (version.StartsWith(target, StringComparison.OrdinalIgnoreCase) ||
            VersionPrefix(version) == target)
        {
            checks.Add(Pass("blender-version", $"found {version}, target {target}"));
        }
        else
        {
            checks.Add(Warn(
                "blender-version",
                $"found {version}, target {target}. P0 golden blend was imported with 5.2.1; 4.5 remains the documented baseline.",
                new() { ["found"] = version, ["target"] = target }));
        }

        checks.Add(AddonCheck(config, version));
    }

    private static DoctorCheck AddonCheck(AppConfig config, string? version)
    {
        var hits = FindUeFormatAddon(config.Blender.Executable);
        if (hits.Count > 0)
        {
            return Pass("ueformat-addon", $"Found UEFormat add-on: {hits[0]}", new()
            {
                ["path"] = hits[0]
            });
        }

        var summary = "UEFormat Blender add-on not found in Blender scripts/addons or user extensions.";
        if (config.Blender.UeFormatAddonRequired)
        {
            return Fail("ueformat-addon", summary + " Install https://github.com/h4lfheart/UEFormat and re-run doctor.");
        }

        return Warn("ueformat-addon", summary);
    }

    private static List<string> FindUeFormatAddon(string blenderExe)
    {
        var hits = new List<string>();
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(blenderExe) && File.Exists(blenderExe))
        {
            var exeDir = Path.GetDirectoryName(blenderExe);
            if (exeDir is not null)
            {
                foreach (var versionDir in Directory.GetDirectories(exeDir))
                {
                    var name = Path.GetFileName(versionDir);
                    if (name.Length == 0 || !char.IsDigit(name[0]))
                    {
                        continue;
                    }

                    roots.Add(Path.Combine(versionDir, "scripts", "addons"));
                    roots.Add(Path.Combine(versionDir, "scripts", "addons_core"));
                    roots.Add(Path.Combine(versionDir, "extensions"));
                }
            }
        }

        var roaming = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Blender Foundation",
            "Blender");
        if (Directory.Exists(roaming))
        {
            foreach (var verDir in Directory.GetDirectories(roaming))
            {
                roots.Add(Path.Combine(verDir, "scripts", "addons"));
                roots.Add(Path.Combine(verDir, "extensions"));
            }
        }

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var candidate in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(candidate);
                if (name.Contains("ueformat", StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add(candidate);
                }
            }
        }

        return hits;
    }

    private static string? ReadBlenderVersion(string exe, out string? error)
    {
        error = null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                error = "Failed to start Blender process.";
                return null;
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(15000);
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Blender ", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed["Blender ".Length..].Trim();
                }
            }

            error = "Blender --version did not print a 'Blender x.y' line.";
            return null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private static string VersionPrefix(string version)
    {
        var parts = version.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var numeric = parts.Length == 0 ? version : parts[0];
        var bits = numeric.Split('.');
        return bits.Length >= 2 ? $"{bits[0]}.{bits[1]}" : numeric;
    }

    private static Dictionary<string, string> Details(ArchiveInventory archives) => new()
    {
        ["directory"] = archives.Directory,
        ["pak"] = archives.PakCount.ToString(),
        ["sig"] = archives.SigCount.ToString(),
        ["utoc"] = archives.UtocCount.ToString(),
        ["ucas"] = archives.UcasCount.ToString()
    };

    private static DoctorResult Finish(List<DoctorCheck> checks, string configPath)
        => new()
        {
            SchemaVersion = "1",
            ToolVersion = ToolVersions.Tool,
            Timestamp = DateTimeOffset.UtcNow,
            OverallStatus = DoctorResultFactory.Overall(checks),
            ConfigPath = configPath,
            Checks = checks
        };

    private static DoctorCheck Pass(string id, string summary, Dictionary<string, string>? details = null)
        => new() { Id = id, Status = DoctorStatus.Pass, Summary = summary, Details = details ?? [] };

    private static DoctorCheck Warn(string id, string summary, Dictionary<string, string>? details = null)
        => new() { Id = id, Status = DoctorStatus.Warn, Summary = summary, Details = details ?? [] };

    private static DoctorCheck Fail(string id, string summary, Dictionary<string, string>? details = null)
        => new() { Id = id, Status = DoctorStatus.Fail, Summary = summary, Details = details ?? [] };

    private static DoctorCheck CheckDirectory(string id, string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail(id, $"{label} is empty.");
        }

        return Directory.Exists(path)
            ? Pass(id, path)
            : Fail(id, $"{label} not found: {path}");
    }

    private static string Locate(Exception ex, string stage)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return $"{stage} failed ({ex.GetType().Name}): {message}";
    }
}
