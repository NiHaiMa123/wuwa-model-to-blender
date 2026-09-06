using System.Diagnostics;

namespace Wuwa.Extractor.Tests;

internal static class PythonProcess
{
    public static string? Find()
    {
        foreach (var name in new[] { "python", "python3", "py" })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = name == "py" ? "-3 --version" : "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process is null)
                {
                    continue;
                }

                process.WaitForExit(5000);
                if (process.ExitCode == 0)
                {
                    return name;
                }
            }
            catch (Exception)
            {
                // try next
            }
        }

        return null;
    }
}
