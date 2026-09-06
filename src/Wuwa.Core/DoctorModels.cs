using System.Text.Json.Serialization;

namespace Wuwa.Core;

public static class DoctorStatus
{
    public const string Pass = "pass";
    public const string Warn = "warn";
    public const string Fail = "fail";
    public const string Skip = "skip";
}

public sealed class DoctorCheck
{
    public required string Id { get; init; }
    public required string Status { get; init; }
    public required string Summary { get; init; }
    public Dictionary<string, string> Details { get; init; } = new();
}

public sealed class DoctorResult
{
    public string SchemaVersion { get; init; } = "1";
    public string ToolVersion { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string OverallStatus { get; init; } = DoctorStatus.Fail;
    public string ConfigPath { get; init; } = "";
    public List<DoctorCheck> Checks { get; init; } = [];

    [JsonIgnore]
    public bool Ok => OverallStatus is DoctorStatus.Pass or DoctorStatus.Warn;
}

public static class DoctorResultFactory
{
    public static string Overall(IReadOnlyList<DoctorCheck> checks)
    {
        if (checks.Any(c => c.Status == DoctorStatus.Fail))
        {
            return DoctorStatus.Fail;
        }

        if (checks.Any(c => c.Status == DoctorStatus.Warn))
        {
            return DoctorStatus.Warn;
        }

        return DoctorStatus.Pass;
    }
}

public static class ToolVersions
{
    public const string Tool = "0.1.0-p7";
    public const string ManifestSchema = "1";
}
