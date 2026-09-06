using Wuwa.Core;
using Wuwa.Export;
using Wuwa.Extractor;
using Xunit;

namespace Wuwa.Extractor.Tests;

[Collection("GameInstall")]
public sealed class GoldenIntegrationTests
{
    [IntegrationFact]
    public async Task Search_FindsGoldenSkeletalMesh()
    {
        var config = LoadLocalConfig();
        var aliases = SearchAliasLoader.Load(RepoPaths.SearchAliases());
        var terms = SearchQuery.Expand("Jinhsi", aliases);
        using var session = await Cue4ParseMount.OpenFromConfigAsync(config);
        var result = AssetIndex.Search(
            session.Provider,
            "Jinhsi",
            terms,
            new AssetSearchOptions { TypeFilter = "SkeletalMesh", Limit = 25, Scan = 200 });

        Assert.NotEmpty(result.Hits);
        Assert.Contains(
            result.Hits,
            hit => GoldenInvariants.Matches(hit.ObjectPath) &&
                   hit.ExportType.Equals("SkeletalMesh", StringComparison.OrdinalIgnoreCase));
    }

    [IntegrationFact]
    public async Task Export_MatchesGoldenInvariants()
    {
        var config = LoadLocalConfig();
        var aes = await AesKeyProviderFactory.Create(config.Decryption.Aes).GetAsync();
        var mappings = await MappingsProviderFactory.Create(config.Decryption.Mappings).GetAsync();
        Assert.False(string.IsNullOrWhiteSpace(aes.ContentHash));
        Assert.DoesNotContain("0x", aes.RedactedSummary(), StringComparison.OrdinalIgnoreCase);

        using var session = Cue4ParseMount.Open(config.Game.PaksDir, config.Game.UeVersion, aes, mappings);
        var outDir = Path.Combine(RepoPaths.Root(), "work", "exports", "p6-golden");
        var result = await ExportPipeline.RunAsync(
            config,
            session,
            aes,
            mappings,
            new ExportRequest
            {
                Asset = GoldenInvariants.ObjectPath,
                OutputDirectory = outDir,
                MeshQuality = "highest"
            });

        Assert.True(File.Exists(result.ManifestPath));
        Assert.True(File.Exists(result.LogPath));
        Assert.NotNull(result.Manifest.Golden);
        Assert.True(
            result.Manifest.Golden.Matched,
            string.Join("; ", result.Manifest.Golden.Mismatches));
        Assert.True(GoldenInvariants.Compare(result.Manifest).Matched);
        Assert.False(string.IsNullOrWhiteSpace(result.Manifest.Mesh?.UeModel));
        Assert.True(File.Exists(Path.Combine(result.JobDirectory, result.Manifest.Mesh!.UeModel!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(GoldenInvariants.UniqueTextures, result.Manifest.Textures.Count);
        Assert.DoesNotContain("0x", await File.ReadAllTextAsync(result.ManifestPath), StringComparison.OrdinalIgnoreCase);
    }

    private static AppConfig LoadLocalConfig()
    {
        var path = RepoPaths.LocalConfig();
        Assert.True(File.Exists(path), $"Integration tests need {path}.");
        return ConfigLoader.Load(path);
    }
}
