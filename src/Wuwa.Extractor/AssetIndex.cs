using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Objects.UObject;
using Wuwa.Core;

namespace Wuwa.Extractor;

public sealed class AssetSearchOptions
{
    public int Limit { get; init; } = 25;
    public int Scan { get; init; } = 200;
    public string? TypeFilter { get; init; }
}

public static class AssetIndex
{
    public static IEnumerable<IndexedAsset> EnumeratePackages(DefaultFileProvider provider)
    {
        foreach (var file in provider.Files.Values)
        {
            if (!file.IsUePackage)
            {
                continue;
            }

            yield return new IndexedAsset(file.Path, file.NameWithoutExtension, file.Directory, true);
        }
    }

    public static SearchResult Search(
        DefaultFileProvider provider,
        string query,
        IReadOnlyList<string> terms,
        AssetSearchOptions options)
    {
        var ranked = EnumeratePackages(provider)
            .Select(asset => (Asset: asset, Score: SearchQuery.Score(asset, terms)))
            .Where(x => x.Score > int.MinValue)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Asset.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hits = new List<SearchHit>();
        var resolved = 0;
        var scanBudget = Math.Max(options.Limit, options.Scan);
        foreach (var candidate in ranked.Take(scanBudget))
        {
            if (!provider.TryLoadPackage(candidate.Asset.Path, out var package) || package is null)
            {
                continue;
            }

            resolved++;
            var exports = ReadExports(package);
            if (exports.Count == 0)
            {
                continue;
            }

            var primaryType = ExportTypeNames.Primary(exports.Select(e => e.Type).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            if (!string.IsNullOrWhiteSpace(options.TypeFilter) &&
                !primaryType.Equals(options.TypeFilter, StringComparison.OrdinalIgnoreCase) &&
                !exports.Exists(e => e.Type.Equals(options.TypeFilter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var primary = PickPrimary(exports, options.TypeFilter ?? primaryType);
            var packagePath = PathNormalization.NormalizeLocal(candidate.Asset.PathWithoutExtensionSafe());
            var objectPath = $"{packagePath}.{primary.Name}";
            hits.Add(new SearchHit
            {
                ObjectPath = objectPath,
                UnrealObjectPath = PathNormalization.ToUnrealObjectPath(objectPath),
                Package = packagePath,
                ExportType = primary.Type,
                CharacterGrouping = CharacterGrouping.FromPackagePath(packagePath),
                ExportTypes = exports.Select(e => e.Type).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray(),
                Score = candidate.Score
            });

            if (hits.Count >= options.Limit)
            {
                break;
            }
        }

        return new SearchResult
        {
            ToolVersion = ToolVersions.Tool,
            Timestamp = DateTimeOffset.UtcNow,
            Query = query,
            Terms = terms,
            TypeFilter = options.TypeFilter,
            MountedFiles = provider.Files.Count,
            CandidateCount = ranked.Count,
            ResolvedCount = resolved,
            Hits = hits
        };
    }

    private static List<(string Name, string Type)> ReadExports(IPackage package)
    {
        var list = new List<(string Name, string Type)>();
        if (package is Package concrete && concrete.ExportMap is { Length: > 0 })
        {
            foreach (var export in concrete.ExportMap)
            {
                var name = ExportName(export.ObjectName);
                var type = ExportTypeNames.ShortName(export.ClassName ?? FallbackExportType(package) ?? "Unknown");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    list.Add((name, type));
                }
            }

            return list;
        }

        try
        {
            foreach (var export in package.GetExports())
            {
                var type = ExportTypeNames.ShortName(export.ExportType ?? export.GetType().Name);
                list.Add((export.Name, type));
            }
        }
        catch (Exception)
        {
            var fallback = FallbackExportType(package);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                list.Add((package.Name, ExportTypeNames.ShortName(fallback)));
            }
        }

        return list;
    }

    private static (string Name, string Type) PickPrimary(List<(string Name, string Type)> exports, string preferredType)
    {
        var match = exports.FirstOrDefault(e => e.Type.Equals(preferredType, StringComparison.OrdinalIgnoreCase));
        if (match.Name is not null)
        {
            return match;
        }

        var primary = ExportTypeNames.Primary(exports.Select(e => e.Type).ToArray());
        return exports.FirstOrDefault(e => e.Type.Equals(primary, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExportName(FName name)
    {
        var text = name.ToString();
        var cut = text.IndexOf('(');
        return cut > 0 ? text[..cut].Trim() : text.Trim();
    }

    private static string? FallbackExportType(IPackage package)
        => package is AbstractUePackage abs ? abs.ExportType : null;
}

file static class IndexedAssetExtensions
{
    public static string PathWithoutExtensionSafe(this IndexedAsset asset)
    {
        var path = PathNormalization.NormalizeLocal(asset.Path);
        return path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
            ? path[..^".uasset".Length]
            : path;
    }
}
