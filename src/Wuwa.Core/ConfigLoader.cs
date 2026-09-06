using System.Text.Json;

namespace Wuwa.Core;

public static class ConfigLoader
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static string DefaultLocalConfigPath(string workingDirectory)
        => Path.Combine(workingDirectory, "config", "wuwa.local.json");

    public static AppConfig Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"Config file not found: {configPath}. Copy config/wuwa.example.json to config/wuwa.local.json.",
                configPath);
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
            ?? throw new InvalidDataException($"Failed to parse config: {configPath}");

        var baseDir = Path.GetDirectoryName(Path.GetFullPath(configPath))
            ?? Directory.GetCurrentDirectory();
        var repoRoot = Directory.GetParent(baseDir)?.FullName ?? baseDir;
        return ResolvePaths(config, repoRoot);
    }

    public static AppConfig Parse(string json, string pathBase)
    {
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
            ?? throw new InvalidDataException("Failed to parse config JSON.");
        return ResolvePaths(config, pathBase);
    }

    public static AppConfig ResolvePaths(AppConfig config, string pathBase)
    {
        return new AppConfig
        {
            Game = new GameConfig
            {
                InstallDir = PathNormalization.ResolveAgainst(config.Game.InstallDir, pathBase),
                PaksDir = PathNormalization.ResolveAgainst(config.Game.PaksDir, pathBase),
                Version = config.Game.Version,
                UeVersion = config.Game.UeVersion,
                Platform = config.Game.Platform,
                Region = config.Game.Region
            },
            Decryption = new DecryptionConfig
            {
                Aes = ResolveProvider(config.Decryption.Aes, pathBase),
                Mappings = ResolveProvider(config.Decryption.Mappings, pathBase)
            },
            Blender = new BlenderConfig
            {
                Executable = PathNormalization.ResolveAgainst(config.Blender.Executable, pathBase),
                TargetVersion = config.Blender.TargetVersion,
                UeFormatAddonRequired = config.Blender.UeFormatAddonRequired
            },
            Output = new OutputConfig
            {
                Root = PathNormalization.ResolveAgainst(config.Output.Root, pathBase),
                PreserveUnrealPaths = config.Output.PreserveUnrealPaths
            }
        };
    }

    private static ProviderConfig ResolveProvider(ProviderConfig provider, string pathBase)
        => new()
        {
            Mode = string.IsNullOrWhiteSpace(provider.Mode) ? "none" : provider.Mode.Trim().ToLowerInvariant(),
            File = PathNormalization.ResolveAgainst(provider.File, pathBase),
            Endpoint = provider.Endpoint.Trim(),
            JsonPath = provider.JsonPath,
            CacheFile = PathNormalization.ResolveAgainst(provider.CacheFile, pathBase)
        };
}
