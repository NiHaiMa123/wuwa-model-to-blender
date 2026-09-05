using System.Text.Json;
using Wuwa.Core;

namespace Wuwa.Export;

public static class ManifestWriter
{
    public static async Task WriteAsync(string path, ExportManifest manifest, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }
}
