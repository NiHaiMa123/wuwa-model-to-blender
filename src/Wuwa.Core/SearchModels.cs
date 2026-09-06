namespace Wuwa.Core;

public sealed class SearchHit
{
    public required string ObjectPath { get; init; }
    public required string UnrealObjectPath { get; init; }
    public required string Package { get; init; }
    public required string ExportType { get; init; }
    public string? CharacterGrouping { get; init; }
    public IReadOnlyList<string> ExportTypes { get; init; } = [];
    public int Score { get; init; }
}

public sealed class SearchResult
{
    public string SchemaVersion { get; init; } = "1";
    public string ToolVersion { get; init; } = ToolVersions.Tool;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string Query { get; init; } = "";
    public IReadOnlyList<string> Terms { get; init; } = [];
    public string? TypeFilter { get; init; }
    public int MountedFiles { get; init; }
    public int CandidateCount { get; init; }
    public int ResolvedCount { get; init; }
    public List<SearchHit> Hits { get; init; } = [];
}

public sealed record IndexedAsset(
    string Path,
    string NameWithoutExtension,
    string Directory,
    bool IsUePackage);

public static class CharacterGrouping
{
    public static string? FromPackagePath(string packagePath)
    {
        var parts = PathNormalization.NormalizeLocal(packagePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var character = Array.FindIndex(parts, p => p.Equals("Character", StringComparison.OrdinalIgnoreCase));
        if (character < 0 || character + 1 >= parts.Length)
        {
            return null;
        }

        var take = Math.Min(4, parts.Length - character);
        return string.Join('/', parts.Skip(character).Take(take));
    }
}

public static class SearchQuery
{
    public static IReadOnlyList<string> Expand(string query, IReadOnlyDictionary<string, IReadOnlyList<string>> aliases)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var terms = new List<string> { trimmed };
        if (aliases.TryGetValue(trimmed, out var mapped) ||
            aliases.TryGetValue(trimmed.ToLowerInvariant(), out mapped))
        {
            foreach (var item in mapped)
            {
                if (!string.IsNullOrWhiteSpace(item) &&
                    !terms.Exists(t => t.Equals(item, StringComparison.OrdinalIgnoreCase)))
                {
                    terms.Add(item.Trim());
                }
            }
        }

        return terms;
    }

    public static bool Matches(string haystack, IReadOnlyList<string> terms)
        => terms.Any(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));

    public static int Score(IndexedAsset asset, IReadOnlyList<string> terms)
    {
        if (!asset.IsUePackage)
        {
            return int.MinValue;
        }

        var path = PathNormalization.NormalizeLocal(asset.Path);
        var name = asset.NameWithoutExtension;
        var dir = PathNormalization.NormalizeLocal(asset.Directory);
        var score = 0;

        if (!Matches(path, terms) && !Matches(name, terms))
        {
            return int.MinValue;
        }

        foreach (var term in terms)
        {
            if (name.Equals(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
            else if (name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
            }

            if (path.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
        }

        if (dir.EndsWith("/Model", StringComparison.OrdinalIgnoreCase) &&
            dir.EndsWith("/" + name + "/Model", StringComparison.OrdinalIgnoreCase))
        {
            score += 80;
        }
        else if (dir.EndsWith("/Model", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (path.Contains("/Character/Role/", StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }
        else if (path.Contains("/Character/", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        if (name.StartsWith("T_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("MI_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("M_", StringComparison.OrdinalIgnoreCase))
        {
            score -= 25;
        }

        if (name.EndsWith("_OL", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("_Skeleton", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("_Physics", StringComparison.OrdinalIgnoreCase))
        {
            score -= 10;
        }

        return score;
    }
}

public static class ExportTypeNames
{
    private static readonly string[] Priority =
    [
        "SkeletalMesh",
        "StaticMesh",
        "Skeleton",
        "PhysicsAsset",
        "AnimSequence",
        "Texture2D",
        "TextureCube",
        "MaterialInstanceConstant",
        "Material",
        "MorphTarget"
    ];

    public static string ShortName(string className)
    {
        var value = className.Replace('\\', '/').Trim();
        var last = value.Contains('.') ? value[(value.LastIndexOf('.') + 1)..] : value;
        if (last.StartsWith('U') && last.Length > 2 && char.IsUpper(last[1]) && last.IndexOf("Script", StringComparison.Ordinal) < 0)
        {
            last = last[1..];
        }

        return last;
    }

    public static string Primary(IReadOnlyList<string> exportTypes)
    {
        foreach (var preferred in Priority)
        {
            var match = exportTypes.FirstOrDefault(t => t.Equals(preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return exportTypes.Count > 0 ? exportTypes[0] : "Unknown";
    }
}
