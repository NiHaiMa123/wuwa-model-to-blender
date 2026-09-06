namespace Wuwa.Core;

public interface IAesKeyProvider
{
    Task<AesKeySet> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record AesKeyEntry(string Guid, string Key);

public sealed record AesKeySet(
    string SourceId,
    string ContentHash,
    string? MainKey,
    IReadOnlyList<AesKeyEntry> DynamicKeys)
{
    public int KeyCount => (string.IsNullOrWhiteSpace(MainKey) ? 0 : 1) + DynamicKeys.Count;

    public string RedactedSummary()
        => $"source={SourceId}; hash={ContentHash}; main={(string.IsNullOrWhiteSpace(MainKey) ? "no" : "yes")}; dynamic={DynamicKeys.Count}";
}

public interface IMappingsProvider
{
    Task<MappingsDescriptor> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record MappingsDescriptor(string SourceId, string ContentHash, string? LocalPath, bool Available);
