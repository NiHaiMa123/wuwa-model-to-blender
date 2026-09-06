using Wuwa.Core;

namespace Wuwa.Extractor;

public sealed class LocalUsmapProvider : IMappingsProvider
{
    private readonly string _file;

    public LocalUsmapProvider(string file) => _file = file;

    public Task<MappingsDescriptor> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_file))
        {
            throw new FileNotFoundException($"Mappings file not found: {_file}", _file);
        }

        return Task.FromResult(new MappingsDescriptor(
            $"local-file:{Path.GetFileName(_file)}",
            ContentHashing.Sha256File(_file),
            Path.GetFullPath(_file),
            Available: true));
    }
}

public sealed class RemoteMappingsProvider : IMappingsProvider
{
    private readonly string _endpoint;
    private readonly string _cacheFile;
    private readonly HttpClient _http;

    public RemoteMappingsProvider(string endpoint, string cacheFile, HttpClient? http = null)
    {
        _endpoint = endpoint;
        _cacheFile = cacheFile;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<MappingsDescriptor> GetAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            throw new InvalidOperationException("Mappings endpoint is empty.");
        }

        using var response = await _http.GetAsync(_endpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(_cacheFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_cacheFile))!);
            await File.WriteAllBytesAsync(_cacheFile, bytes, cancellationToken).ConfigureAwait(false);
        }

        var local = string.IsNullOrWhiteSpace(_cacheFile) ? "" : Path.GetFullPath(_cacheFile);
        return new MappingsDescriptor(_endpoint, ContentHashing.Sha256Hex(bytes), local, Available: true);
    }
}

public sealed class NullMappingsProvider : IMappingsProvider
{
    public static readonly NullMappingsProvider Instance = new();

    public Task<MappingsDescriptor> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new MappingsDescriptor("none", "", null, Available: false));
}

public static class MappingsProviderFactory
{
    public static IMappingsProvider Create(ProviderConfig config, HttpClient? http = null)
        => config.Mode switch
        {
            "none" or "" => NullMappingsProvider.Instance,
            "local-file" or "file" => new LocalUsmapProvider(config.File),
            "endpoint" or "remote" => new RemoteMappingsProvider(config.Endpoint, config.CacheFile, http),
            _ => throw new InvalidOperationException($"Unknown mappings provider mode: {config.Mode}")
        };
}
