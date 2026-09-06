using Wuwa.Core;

namespace Wuwa.Extractor;

public sealed class ExtractorPipeline
{
    public Task IndexAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("P2 search uses Cue4ParseMount.OpenFromConfigAsync + AssetIndex.Search.");

    public Task<ExportManifest> ExportAsync(string unrealObjectPath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("P3 export is Wuwa.Export.ExportPipeline.RunAsync.");
}
