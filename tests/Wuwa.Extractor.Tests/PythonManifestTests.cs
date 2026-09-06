using System.Diagnostics;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class PythonManifestTests
{
    [Fact]
    public void ManifestIo_PythonUnitTestsPassWhenInterpreterExists()
    {
        var python = PythonProcess.Find();
        if (python is null)
        {
            return;
        }

        var pythonDir = Path.Combine(RepoPaths.Root(), "tests", "python");
        var scripts = Directory.GetFiles(pythonDir, "test_*.py");
        Assert.Contains(scripts, path => path.EndsWith("test_manifest_io.py", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(scripts, path => path.EndsWith("test_schemas.py", StringComparison.OrdinalIgnoreCase));
        foreach (var script in scripts)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = python,
                ArgumentList = { script },
                WorkingDirectory = RepoPaths.Root(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Assert.NotNull(process);
            Assert.True(process.WaitForExit(30_000), $"{Path.GetFileName(script)} timed out.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            Assert.True(process.ExitCode == 0, script + Environment.NewLine + stdout + Environment.NewLine + stderr);
        }
    }
}
