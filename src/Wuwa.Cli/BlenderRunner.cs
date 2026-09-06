using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Wuwa.Core;

namespace Wuwa.Cli;

public static class BlenderRunner
{
    public static async Task<BlenderJobResult> RunAsync(
        string workingDirectory,
        string configPath,
        BlenderRequest request,
        string logPath,
        CancellationToken cancellationToken = default)
    {
        var config = ConfigLoader.Load(configPath);
        var manifestPath = Path.GetFullPath(request.ManifestPath, workingDirectory);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"manifest.json not found: {manifestPath}", manifestPath);
        }

        var manifest = JsonSerializer.Deserialize<ExportManifest>(
            await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false),
            ConfigLoader.JsonOptions)
            ?? throw new InvalidDataException($"Failed to parse manifest: {manifestPath}");

        var blenderExe = config.Blender.Executable;
        if (string.IsNullOrWhiteSpace(blenderExe) || !File.Exists(blenderExe))
        {
            throw new FileNotFoundException(
                $"Blender executable not found: {blenderExe}. Set blender.executable in config.",
                blenderExe);
        }

        var scriptPath = BlenderLaunch.DefaultScriptPath(workingDirectory);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"batch_import.py not found: {scriptPath}", scriptPath);
        }

        var profilePath = string.IsNullOrWhiteSpace(request.ProfilePath)
            ? BlenderLaunch.DefaultProfilePath(workingDirectory)
            : Path.GetFullPath(request.ProfilePath, workingDirectory);
        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Material profile not found: {profilePath}", profilePath);
        }

        MaterialProfile.Load(profilePath);

        var savePath = string.IsNullOrWhiteSpace(request.SavePath)
            ? BlenderLaunch.DefaultSavePath(workingDirectory, manifest.JobId)
            : Path.GetFullPath(request.SavePath, workingDirectory);
        var reportPath = string.IsNullOrWhiteSpace(request.ReportPath)
            ? Path.ChangeExtension(savePath, ".validation.json")
            : Path.GetFullPath(request.ReportPath, workingDirectory);

        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var arguments = BlenderLaunch.BuildArguments(
            scriptPath,
            manifestPath,
            savePath,
            profilePath,
            reportPath,
            request.PackImages);

        var output = new StringBuilder();
        output.AppendLine($"wuwa2blender blender {ToolVersions.Tool}");
        output.AppendLine($"exe      {blenderExe}");
        output.AppendLine($"script   {scriptPath}");
        output.AppendLine($"manifest {manifestPath}");
        output.AppendLine($"save     {savePath}");
        output.AppendLine($"profile  {profilePath}");
        output.AppendLine();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = blenderExe,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }

                Console.WriteLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }

                Console.Error.WriteLine(e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start Blender: {blenderExe}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // ignored — process may have exited between cancel and kill
            }

            throw new TimeoutException("Blender import exceeded 15 minutes.");
        }

        output.AppendLine();
        output.AppendLine($"exit {process.ExitCode}");
        await File.WriteAllTextAsync(logPath, output.ToString(), cancellationToken).ConfigureAwait(false);

        var report = ReadReport(reportPath, manifestPath, savePath);
        if (report.Scene is not null && GoldenInvariants.Matches(manifest.SourceObjectPath))
        {
            report.Golden = GoldenInvariants.CompareBlend(report.Scene);
            await File.WriteAllTextAsync(
                reportPath,
                JsonSerializer.Serialize(report, ConfigLoader.JsonOptions),
                cancellationToken).ConfigureAwait(false);
        }

        return new BlenderJobResult
        {
            Report = report,
            BlendPath = savePath,
            ReportPath = reportPath,
            LogPath = logPath,
            BlenderExitCode = process.ExitCode
        };
    }

    public static void WriteHuman(BlenderJobResult result, TextWriter writer)
    {
        var report = result.Report;
        writer.WriteLine($"wuwa2blender blender  tool={ToolVersions.Tool}  exit={result.BlenderExitCode}");
        writer.WriteLine($"manifest {report.ManifestPath}");
        writer.WriteLine($"blend    {result.BlendPath}  saved={(report.Saved ? "yes" : "no")}  reopen={(report.ReopenedClean ? "clean" : "missing-files")}");
        writer.WriteLine($"profile  {report.ProfileId}");
        if (report.Scene is { } scene)
        {
            writer.WriteLine(
                $"scene    mesh={scene.MeshName ?? "-"} verts={scene.Vertices} faces={scene.Faces} loops={scene.Loops} slots={scene.MaterialSlots} morphs={scene.MorphTargets} bones={scene.Bones} uv={scene.UvChannels} vcol={(scene.HasVertexColors ? "yes" : "no")} armature={(scene.HasArmatureModifier ? "yes" : "no")}");
            writer.WriteLine($"images   bound={scene.BoundImages} missing={scene.MissingImages}");
        }

        if (report.Golden is { } golden)
        {
            writer.WriteLine($"golden  {(golden.Matched ? "match" : "mismatch")}");
            foreach (var mismatch in golden.Mismatches)
            {
                writer.WriteLine($"        {mismatch}");
            }
        }

        foreach (var warning in report.Warnings)
        {
            writer.WriteLine($"warn    {warning}");
        }

        foreach (var error in report.Errors)
        {
            writer.WriteLine($"error   {error}");
        }
    }

    private static BlenderValidationReport ReadReport(string reportPath, string manifestPath, string savePath)
    {
        if (!File.Exists(reportPath))
        {
            return new BlenderValidationReport
            {
                ToolVersion = ToolVersions.Tool,
                ManifestPath = manifestPath,
                BlendPath = savePath,
                Errors = ["Blender did not write a validation report."]
            };
        }

        var loaded = JsonSerializer.Deserialize<BlenderValidationReport>(
            File.ReadAllText(reportPath),
            ConfigLoader.JsonOptions);
        return loaded ?? new BlenderValidationReport
        {
            ToolVersion = ToolVersions.Tool,
            ManifestPath = manifestPath,
            BlendPath = savePath,
            Errors = ["Failed to parse validation report."]
        };
    }
}
