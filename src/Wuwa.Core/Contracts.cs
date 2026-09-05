namespace Wuwa.Core;

public sealed record ExportManifest(
    string SchemaVersion,
    string JobId,
    string GameVersion,
    string UnrealObjectPath,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Warnings);

public interface IAesKeyProvider
{
    Task<AesKeySet> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record AesKeySet(string SourceId, string ContentHash, IReadOnlyList<string> Keys);

public interface IMappingsProvider
{
    Task<MappingsDescriptor> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record MappingsDescriptor(string SourceId, string ContentHash, string LocalPath);
