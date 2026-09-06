namespace Wuwa.Extractor;

public sealed record ArchiveInventory(
    string Directory,
    int PakCount,
    int SigCount,
    int UtocCount,
    int UcasCount,
    IReadOnlyList<string> PakNames)
{
    public int ArchiveCount => PakCount + UtocCount;
    public bool HasArchives => ArchiveCount > 0;
}

public static class ArchiveDiscovery
{
    public static ArchiveInventory Scan(string paksDir)
    {
        if (string.IsNullOrWhiteSpace(paksDir) || !Directory.Exists(paksDir))
        {
            return new ArchiveInventory(paksDir, 0, 0, 0, 0, []);
        }

        var files = Directory.GetFiles(paksDir, "*.*", SearchOption.TopDirectoryOnly);
        var paks = files.Where(f => f.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var sigs = files.Count(f => f.EndsWith(".sig", StringComparison.OrdinalIgnoreCase));
        var utoc = files.Count(f => f.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase));
        var ucas = files.Count(f => f.EndsWith(".ucas", StringComparison.OrdinalIgnoreCase));
        return new ArchiveInventory(
            Path.GetFullPath(paksDir),
            paks.Length,
            sigs,
            utoc,
            ucas,
            paks.Select(Path.GetFileName).Where(n => n is not null).Cast<string>().ToArray());
    }
}
