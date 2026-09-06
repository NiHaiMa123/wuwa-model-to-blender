using Wuwa.Core;
using Xunit;

namespace Wuwa.Extractor.Tests;

public sealed class ConfigLoaderTests
{
    [Fact]
    public void Parse_ResolvesRelativePathsAgainstBase()
    {
        const string json = """
            {
              "game": {
                "installDir": "game-root",
                "paksDir": "game-root/Client/Content/Paks",
                "version": "3.6.0",
                "ueVersion": "GAME_WutheringWaves",
                "platform": "Windows",
                "region": "overseas"
              },
              "decryption": {
                "aes": {
                  "mode": "endpoint",
                  "endpoint": "https://example.invalid/keys.json",
                  "cacheFile": "work/cache/aes.json"
                },
                "mappings": { "mode": "none" }
              },
              "blender": {
                "executable": "blender/blender.exe",
                "targetVersion": "4.5",
                "ueFormatAddonRequired": true
              },
              "output": { "root": "work/exports", "preserveUnrealPaths": true }
            }
            """;

        var baseDir = Path.Combine(Path.GetTempPath(), "wuwa-config-test");
        Directory.CreateDirectory(baseDir);
        var config = ConfigLoader.Parse(json, baseDir);

        Assert.Equal("GAME_WutheringWaves", config.Game.UeVersion);
        Assert.Equal("endpoint", config.Decryption.Aes.Mode);
        Assert.Equal("none", config.Decryption.Mappings.Mode);
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "game-root")), config.Game.InstallDir);
        Assert.Equal(Path.GetFullPath(Path.Combine(baseDir, "work", "cache", "aes.json")), config.Decryption.Aes.CacheFile);
        Assert.True(config.Output.PreserveUnrealPaths);
    }

    [Fact]
    public void PathNormalization_MapsClientContentToGame()
    {
        var unreal = PathNormalization.ToUnrealObjectPath(
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011");
        Assert.Equal(
            "/Game/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011",
            unreal);
        Assert.Equal(
            "Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011",
            PathNormalization.ToArchiveObjectPath(unreal));
    }
}
