using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Wuwa.Core;

namespace Wuwa.Extractor;

public sealed record Cue4ParseMountResult(
    string UeVersion,
    int ArchivesOnDisk,
    int MountedCount,
    int UnloadedCount,
    int FileCount,
    IReadOnlyList<string> Warnings);

public sealed class Cue4ParseSession : IDisposable
{
    public Cue4ParseSession(DefaultFileProvider provider, Cue4ParseMountResult info)
    {
        Provider = provider;
        Info = info;
    }

    public DefaultFileProvider Provider { get; }
    public Cue4ParseMountResult Info { get; }

    public void Dispose() => Provider.Dispose();
}

public static class Cue4ParseMount
{
    public static Cue4ParseMountResult Mount(
        string paksDir,
        string ueVersion,
        AesKeySet aes,
        MappingsDescriptor mappings)
    {
        using var session = Open(paksDir, ueVersion, aes, mappings);
        return session.Info;
    }

    public static Cue4ParseSession Open(
        string paksDir,
        string ueVersion,
        AesKeySet aes,
        MappingsDescriptor mappings)
    {
        if (!Enum.TryParse<EGame>(ueVersion, ignoreCase: true, out var game))
        {
            var names = string.Join(", ", Enum.GetNames<EGame>().Where(n => n.Contains("UE4_26", StringComparison.OrdinalIgnoreCase) || n.Contains("Wuthering", StringComparison.OrdinalIgnoreCase)));
            throw new InvalidOperationException(
                $"Unknown ueVersion '{ueVersion}'. CUE4Parse EGame nearby values: {names}");
        }

        var warnings = new List<string>();
        var provider = new DefaultFileProvider(
            paksDir,
            SearchOption.TopDirectoryOnly,
            new VersionContainer(game),
            StringComparer.OrdinalIgnoreCase);

        try
        {
            if (mappings.Available && !string.IsNullOrWhiteSpace(mappings.LocalPath) && File.Exists(mappings.LocalPath))
            {
                provider.MappingsContainer = new FileUsmapTypeMappingsProvider(
                    mappings.LocalPath,
                    StringComparer.OrdinalIgnoreCase);
            }

            provider.Initialize();

            var keys = new List<KeyValuePair<FGuid, FAesKey>>();
            if (!string.IsNullOrWhiteSpace(aes.MainKey))
            {
                keys.Add(new KeyValuePair<FGuid, FAesKey>(new FGuid(), new FAesKey(aes.MainKey)));
            }

            foreach (var entry in aes.DynamicKeys)
            {
                keys.Add(new KeyValuePair<FGuid, FAesKey>(ParseGuid(entry.Guid), new FAesKey(entry.Key)));
            }

            if (keys.Count == 0)
            {
                throw new InvalidOperationException("AES provider returned no keys; cannot mount encrypted archives.");
            }

            provider.SubmitKeys(keys);

            var mounted = provider.MountedVfs.Count;
            var unloaded = provider.UnloadedVfs.Count;
            var files = provider.Files.Count;
            if (mounted == 0)
            {
                throw new InvalidOperationException(
                    $"CUE4Parse initialized {ueVersion} but mounted 0 archives ({unloaded} still unloaded, {files} files). AES keys or pak version likely mismatch.");
            }

            if (unloaded > 0)
            {
                warnings.Add($"{unloaded} archives remain unloaded after submitting AES keys.");
            }

            var info = new Cue4ParseMountResult(game.ToString(), mounted + unloaded, mounted, unloaded, files, warnings);
            return new Cue4ParseSession(provider, info);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    public static async Task<Cue4ParseSession> OpenFromConfigAsync(
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        var aes = await AesKeyProviderFactory.Create(config.Decryption.Aes).GetAsync(cancellationToken).ConfigureAwait(false);
        var mappings = await MappingsProviderFactory.Create(config.Decryption.Mappings).GetAsync(cancellationToken).ConfigureAwait(false);
        return Open(config.Game.PaksDir, config.Game.UeVersion, aes, mappings);
    }

    private static FGuid ParseGuid(string guid)
    {
        var hex = guid.Replace("-", "", StringComparison.Ordinal).Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hex = hex[2..];
        }

        if (hex.Length != 32)
        {
            throw new InvalidDataException($"AES dynamic key guid must be 32 hex chars, got '{guid}'.");
        }

        return new FGuid(hex);
    }
}
