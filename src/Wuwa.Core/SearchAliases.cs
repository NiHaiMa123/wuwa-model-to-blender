using System.Text.Json;

namespace Wuwa.Core;

public sealed class SearchAliasFile
{
    public Dictionary<string, List<string>> Aliases { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class SearchAliasLoader
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(path);
        var parsed = JsonSerializer.Deserialize<SearchAliasFile>(json, ConfigLoader.JsonOptions)
            ?? new SearchAliasFile();
        return parsed.Aliases.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }
}
