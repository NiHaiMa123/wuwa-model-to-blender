using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Wuwa.Core;

namespace Wuwa.Extractor.Tests;

internal static class BlenderProcess
{
    public static async Task<BlenderJobResult> RunAsync(
        string blenderExe,
        string workingDirectory,
        string manifestPath,
        string savePath,
        string profilePath,
        string reportPath,
        string logPath,
        CancellationToken cancellationToken = default)
    {
        var arguments = BlenderLaunch.BuildArguments(
            BlenderLaunch.DefaultScriptPath(workingDirectory),
            manifestPath,
            savePath,
            profilePath,
            reportPath,
            packImages: true);

        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var output = new StringBuilder();
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
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start Blender: {blenderExe}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
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
                // ignored
            }

            throw new TimeoutException("Blender smoke exceeded 5 minutes.");
        }

        output.AppendLine($"exit {process.ExitCode}");
        await File.WriteAllTextAsync(logPath, output.ToString(), cancellationToken).ConfigureAwait(false);

        BlenderValidationReport report;
        if (!File.Exists(reportPath))
        {
            report = new BlenderValidationReport
            {
                ToolVersion = ToolVersions.Tool,
                ManifestPath = manifestPath,
                BlendPath = savePath,
                Errors = ["Blender did not write a validation report.", output.ToString()]
            };
        }
        else
        {
            report = JsonSerializer.Deserialize<BlenderValidationReport>(
                await File.ReadAllTextAsync(reportPath, cancellationToken).ConfigureAwait(false),
                ConfigLoader.JsonOptions)
                ?? new BlenderValidationReport
                {
                    Errors = ["Failed to parse validation report."]
                };
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
}
