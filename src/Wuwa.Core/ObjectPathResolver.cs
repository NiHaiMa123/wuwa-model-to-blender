namespace Wuwa.Core;

public static class ObjectPathResolver
{
    public static IReadOnlyList<string> Candidates(string input)
    {
        var raw = PathNormalization.NormalizeLocal(input).Trim().Trim('"');
        if (raw.Length == 0)
        {
            return [];
        }

        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = PathNormalization.NormalizeLocal(value);
            if (seen.Add(normalized))
            {
                ordered.Add(normalized);
            }

            var trimmed = normalized.TrimStart('/');
            if (seen.Add(trimmed))
            {
                ordered.Add(trimmed);
            }
        }

        Add(raw);
        Add(PathNormalization.ToArchiveObjectPath(raw));
        Add(PathNormalization.ToUnrealObjectPath(raw));

        var (package, export) = Split(raw);
        Add(package);
        if (!string.IsNullOrWhiteSpace(export))
        {
            Add($"{package}.{export}");
            Add($"{PathNormalization.ToArchiveObjectPath(package)}.{export}");
            Add($"{PathNormalization.ToUnrealObjectPath(package)}.{export}");
        }
        else
        {
            var name = PackageFileName(package);
            if (!string.IsNullOrWhiteSpace(name))
            {
                Add($"{package}.{name}");
                Add($"{PathNormalization.ToArchiveObjectPath(package)}.{name}");
            }
        }

        Add(package + ".uasset");
        Add(PathNormalization.ToArchiveObjectPath(package) + ".uasset");

        return ordered;
    }

    public static (string Package, string? ExportName) Split(string objectPath)
    {
        var value = PathNormalization.NormalizeLocal(objectPath).Trim().TrimStart('/');
        if (value.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^".uasset".Length];
        }

        var slash = value.LastIndexOf('/');
        var leaf = slash >= 0 ? value[(slash + 1)..] : value;
        var dot = leaf.LastIndexOf('.');
        if (dot <= 0)
        {
            return (value, null);
        }

        var export = leaf[(dot + 1)..];
        var packageLeaf = leaf[..dot];
        var package = slash >= 0 ? value[..(slash + 1)] + packageLeaf : packageLeaf;
        return (package, string.IsNullOrWhiteSpace(export) ? null : export);
    }

    public static string PackageFileName(string packagePath)
    {
        var value = PathNormalization.NormalizeLocal(packagePath).Trim().TrimStart('/');
        if (value.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^".uasset".Length];
        }

        var slash = value.LastIndexOf('/');
        return slash >= 0 ? value[(slash + 1)..] : value;
    }

    public static bool LooksLikeObjectPath(string input)
    {
        var value = PathNormalization.NormalizeLocal(input).Trim();
        return value.Contains('/') || value.Contains('\\');
    }
}
