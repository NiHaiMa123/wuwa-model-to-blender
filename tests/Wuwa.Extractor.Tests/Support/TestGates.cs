using Xunit;

namespace Wuwa.Extractor.Tests;

[CollectionDefinition("GameInstall", DisableParallelization = true)]
public sealed class GameInstallCollection;

internal static class TestGates
{
    public const string IntegrationEnv = "WUWA_INTEGRATION_TESTS";
    public const string BlenderSmokeEnv = "WUWA_BLENDER_SMOKE";

    public static bool IntegrationEnabled => FlagEnabled(IntegrationEnv);

    public static bool BlenderSmokeEnabled => FlagEnabled(BlenderSmokeEnv);

    public static bool FlagEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is "1" or "true" or "TRUE" or "yes" or "YES";
    }
}

public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!TestGates.IntegrationEnabled)
        {
            Skip = $"Set {TestGates.IntegrationEnv}=1 to run local golden-path tests against a real game install.";
        }
    }
}

public sealed class BlenderSmokeFactAttribute : FactAttribute
{
    public BlenderSmokeFactAttribute()
    {
        if (!TestGates.BlenderSmokeEnabled)
        {
            Skip = $"Set {TestGates.BlenderSmokeEnv}=1 to run the headless Blender smoke against the self-authored UEFormat fixture.";
        }
    }
}
