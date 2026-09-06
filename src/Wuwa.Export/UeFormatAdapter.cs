using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Options;
using CUE4Parse_Conversion.Textures.BC;

namespace Wuwa.Export;

public sealed record UeFormatExportOptions(string MeshQuality, bool ExportMaterials, bool ExportMorphTargets);

public static class UeFormatAdapter
{
    public static ExportOptions ToCue4ParseOptions(UeFormatExportOptions options)
        => new(
            meshFormat: EMeshFormat.UEFormat,
            naniteMeshFormat: ENaniteMeshFormat.NoNanite,
            meshQuality: ParseQuality(options.MeshQuality),
            texturePlatform: ETexturePlatform.DesktopMobile,
            textureFormat: ETextureFormat.Png,
            textureQuality: 100,
            exportHdrTexturesAsHdr: true,
            exportAllTextureMips: false,
            materialDepth: EMaterialDepth.TopLayerOnly,
            exportMaterials: options.ExportMaterials,
            exportMorphTargets: options.ExportMorphTargets,
            socketFormat: ESocketFormat.Bone,
            compressionFormat: CUE4Parse_Conversion.Writers.UEFormat.Enums.EFileCompressionFormat.None);

    public static async Task<IReadOnlyList<ExportResult>> ExportAsync(
        IEnumerable<UObject> roots,
        string jobDirectory,
        UeFormatExportOptions options,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(jobDirectory);
        EnsureNativeDecoders();
        var session = new ExportSession();
        var added = 0;
        foreach (var root in roots)
        {
            session.Add(root);
            added++;
        }

        if (added == 0)
        {
            return [];
        }

        return await session.RunAsync(jobDirectory, ToCue4ParseOptions(options), progress: null, cancellationToken)
            .ConfigureAwait(false);
    }

    public static string RelativeToJob(string jobDirectory, string fullPath)
    {
        var relative = Path.GetRelativePath(jobDirectory, fullPath).Replace('\\', '/');
        return relative.StartsWith("./", StringComparison.Ordinal) ? relative[2..] : relative;
    }

    public static void EnsureNativeDecoders()
    {
        var dir = Path.GetDirectoryName(typeof(UeFormatAdapter).Assembly.Location)
                  ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(dir);
        var detexPath = Path.Combine(dir, DetexHelper.DLL_NAME);
        if (!DetexHelper.LoadDll(detexPath))
        {
            throw new InvalidOperationException(
                $"Failed to extract {DetexHelper.DLL_NAME} next to the CLI. BC/BCn texture decode needs Detex.");
        }

        DetexHelper.Initialize(detexPath);
    }

    private static EMeshQuality ParseQuality(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "all" => EMeshQuality.All,
            "lowest" => EMeshQuality.Lowest,
            _ => EMeshQuality.Highest
        };
}
