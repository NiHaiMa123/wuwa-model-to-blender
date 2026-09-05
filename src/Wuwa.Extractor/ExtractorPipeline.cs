using Wuwa.Core;

namespace Wuwa.Extractor;

public sealed class ExtractorPipeline
{
    public Task IndexAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("P2: initialize CUE4Parse and build/search the package index.");

    public Task<ExportManifest> ExportAsync(string unrealObjectPath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("P3: resolve dependencies and export UEFormat staging files.");
}
