namespace Wuwa.Core;

public sealed class AppConfig
{
    public GameConfig Game { get; init; } = new();
    public DecryptionConfig Decryption { get; init; } = new();
    public BlenderConfig Blender { get; init; } = new();
    public OutputConfig Output { get; init; } = new();
}

public sealed class GameConfig
{
    public string InstallDir { get; init; } = "";
    public string PaksDir { get; init; } = "";
    public string Version { get; init; } = "";
    public string UeVersion { get; init; } = "GAME_UE4_26";
    public string Platform { get; init; } = "Windows";
    public string Region { get; init; } = "overseas";
}

public sealed class DecryptionConfig
{
    public ProviderConfig Aes { get; init; } = new();
    public ProviderConfig Mappings { get; init; } = new();
}

public sealed class ProviderConfig
{
    public string Mode { get; init; } = "none";
    public string File { get; init; } = "";
    public string Endpoint { get; init; } = "";
    public string JsonPath { get; init; } = "";
    public string CacheFile { get; init; } = "";
}

public sealed class BlenderConfig
{
    public string Executable { get; init; } = "";
    public string TargetVersion { get; init; } = "4.5";
    public bool UeFormatAddonRequired { get; init; } = true;
}

public sealed class OutputConfig
{
    public string Root { get; init; } = "work/exports";
    public bool PreserveUnrealPaths { get; init; } = true;
}

public sealed class GameProfile
{
    public required string GameVersion { get; init; }
    public required string UeVersion { get; init; }
    public required string Platform { get; init; }
    public required string Region { get; init; }

    public static GameProfile FromConfig(AppConfig config) => new()
    {
        GameVersion = config.Game.Version,
        UeVersion = config.Game.UeVersion,
        Platform = config.Game.Platform,
        Region = config.Game.Region
    };
}
