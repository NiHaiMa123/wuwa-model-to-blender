using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using Wuwa.Core;

namespace Wuwa.Extractor;

public static class ObjectLoader
{
    public static UObject LoadRequired(DefaultFileProvider provider, string input)
    {
        if (TryLoad(provider, input, out var obj) && obj is not null)
        {
            return obj;
        }

        var tried = string.Join(", ", ObjectPathResolver.Candidates(input).Take(8));
        throw new FileNotFoundException(
            $"Could not load Unreal object '{input}'. Tried: {tried}. Use search to get a full object path.");
    }

    public static bool TryLoad(DefaultFileProvider provider, string input, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out UObject? obj)
    {
        obj = null;
        foreach (var candidate in ObjectPathResolver.Candidates(input))
        {
            if (TryLoadOne(provider, candidate, out obj) && obj is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryLoadOne(DefaultFileProvider provider, string candidate, out UObject? obj)
    {
        obj = null;
        try
        {
            if (provider.TryLoadPackageObject(candidate, out var loaded) && loaded is not null)
            {
                obj = loaded;
                return true;
            }
        }
        catch (Exception)
        {
            // try package + export next
        }

        var (packagePath, exportName) = ObjectPathResolver.Split(candidate);
        foreach (var pkg in PackageCandidates(packagePath))
        {
            try
            {
                if (!provider.TryLoadPackage(pkg, out var package) || package is null)
                {
                    continue;
                }

                var exports = package.GetExports().ToList();
                if (exports.Count == 0)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(exportName))
                {
                    var named = exports.FirstOrDefault(e => e.Name.Equals(exportName, StringComparison.OrdinalIgnoreCase));
                    if (named is not null)
                    {
                        obj = named;
                        return true;
                    }
                }

                var mesh = exports.FirstOrDefault(e =>
                    ExportTypeNames.ShortName(e.ExportType ?? e.GetType().Name)
                        .Equals("SkeletalMesh", StringComparison.OrdinalIgnoreCase));
                obj = mesh ?? exports[0];
                return obj is not null;
            }
            catch (Exception)
            {
                // next candidate
            }
        }

        return false;
    }

    private static IEnumerable<string> PackageCandidates(string packagePath)
    {
        var value = PathNormalization.NormalizeLocal(packagePath).TrimStart('/');
        yield return value;
        if (!value.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            yield return value + ".uasset";
        }
    }
}
