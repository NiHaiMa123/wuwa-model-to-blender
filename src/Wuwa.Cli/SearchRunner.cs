using System.Text.Json;
using Wuwa.Core;
using Wuwa.Extractor;

namespace Wuwa.Cli;

public static class SearchRunner
{
    public static async Task<SearchResult> RunAsync(
        string configPath,
        string query,
        AssetSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        var config = ConfigLoader.Load(configPath);
        var aliasPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath))!, "search-aliases.json");
        var aliases = SearchAliasLoader.Load(aliasPath);
        var terms = SearchQuery.Expand(query, aliases);
        if (terms.Count == 0)
        {
            throw new InvalidOperationException("Search query is empty.");
        }

        using var session = await Cue4ParseMount.OpenFromConfigAsync(config, cancellationToken).ConfigureAwait(false);
        return AssetIndex.Search(session.Provider, query, terms, options);
    }

    public static async Task WriteResultAsync(SearchResult result, string resultPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
        await using var stream = File.Create(resultPath);
        await JsonSerializer.SerializeAsync(stream, result, ConfigLoader.JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public static void WriteHuman(SearchResult result, TextWriter writer)
    {
        writer.WriteLine($"wuwa2blender search  tool={result.ToolVersion}  query={result.Query}");
        writer.WriteLine($"terms: {string.Join(", ", result.Terms)}");
        if (!string.IsNullOrWhiteSpace(result.TypeFilter))
        {
            writer.WriteLine($"type: {result.TypeFilter}");
        }

        writer.WriteLine($"index: {result.MountedFiles} files; path candidates {result.CandidateCount}; resolved {result.ResolvedCount}; hits {result.Hits.Count}");
        var i = 1;
        foreach (var hit in result.Hits)
        {
            writer.WriteLine();
            writer.WriteLine($"[{i}] {hit.ExportType}  {hit.CharacterGrouping ?? "-"}  score={hit.Score}");
            writer.WriteLine($"    object  {hit.ObjectPath}");
            writer.WriteLine($"    unreal  {hit.UnrealObjectPath}");
            writer.WriteLine($"    package {hit.Package}");
            if (hit.ExportTypes.Count > 1)
            {
                writer.WriteLine($"    exports {string.Join(", ", hit.ExportTypes)}");
            }

            i++;
        }

        if (result.Hits.Count == 0)
        {
            writer.WriteLine("No packages matched. Try the Unreal object path, mesh asset name, or an alias from config/search-aliases.json.");
        }
    }
}
