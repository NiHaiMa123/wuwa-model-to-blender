using Wuwa.Core;

namespace Wuwa.Extractor;

public sealed class LocalFileAesKeyProvider : IAesKeyProvider
{
    private readonly string _file;

    public LocalFileAesKeyProvider(string file) => _file = file;

    public Task<AesKeySet> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_file))
        {
            throw new FileNotFoundException($"AES key file not found: {_file}", _file);
        }

        var json = File.ReadAllText(_file);
        return Task.FromResult(AesKeyDocument.Parse(json, $"local-file:{Path.GetFileName(_file)}"));
    }
}

public sealed class RemoteJsonAesKeyProvider : IAesKeyProvider
{
    private readonly string _endpoint;
    private readonly string _cacheFile;
    private readonly HttpClient _http;

    public RemoteJsonAesKeyProvider(string endpoint, string cacheFile, HttpClient? http = null)
    {
        _endpoint = endpoint;
        _cacheFile = cacheFile;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<AesKeySet> GetAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            throw new InvalidOperationException("AES endpoint is empty.");
        }

        Exception? downloadError = null;
        try
        {
            using var response = await _http.GetAsync(_endpoint, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(_cacheFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_cacheFile))!);
                await File.WriteAllTextAsync(_cacheFile, json, cancellationToken).ConfigureAwait(false);
            }

            return AesKeyDocument.Parse(json, _endpoint);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            downloadError = ex;
        }

        if (!string.IsNullOrWhiteSpace(_cacheFile) && File.Exists(_cacheFile))
        {
            var cached = await File.ReadAllTextAsync(_cacheFile, cancellationToken).ConfigureAwait(false);
            var parsed = AesKeyDocument.Parse(cached, $"cache:{Path.GetFileName(_cacheFile)}");
            return parsed with { SourceId = $"{parsed.SourceId}; endpoint-failed={downloadError?.GetType().Name}" };
        }

        throw new InvalidOperationException(
            $"Failed to download AES keys from {_endpoint} and no cache exists at {_cacheFile}. {downloadError?.Message}",
            downloadError);
    }
}

public sealed class ManualAesKeyProvider : IAesKeyProvider
{
    private readonly AesKeySet _keys;

    public ManualAesKeyProvider(AesKeySet keys) => _keys = keys;

    public Task<AesKeySet> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_keys);
}

public static class AesKeyProviderFactory
{
    public static IAesKeyProvider Create(ProviderConfig config, HttpClient? http = null)
        => config.Mode switch
        {
            "local-file" or "file" => new LocalFileAesKeyProvider(config.File),
            "endpoint" or "remote" or "remote-json" => new RemoteJsonAesKeyProvider(config.Endpoint, config.CacheFile, http),
            "manual" => throw new InvalidOperationException(
                "AES mode 'manual' requires keys supplied at runtime; use local-file or endpoint for doctor."),
            "none" or "" => throw new InvalidOperationException(
                "AES provider mode is 'none'. Set decryption.aes.mode to endpoint or local-file."),
            _ => throw new InvalidOperationException($"Unknown AES provider mode: {config.Mode}")
        };
}
