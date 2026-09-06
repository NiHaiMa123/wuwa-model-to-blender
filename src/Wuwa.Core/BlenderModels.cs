namespace Wuwa.Core;

public sealed class BlendSceneStats
{
    public string? MeshName { get; init; }
    public string? ArmatureName { get; init; }
    public int Vertices { get; init; }
    public int Faces { get; init; }
    public int Loops { get; init; }
    public int MaterialSlots { get; init; }
    public int MorphTargets { get; init; }
    public int Bones { get; init; }
    public int VertexGroups { get; init; }
    public int UvChannels { get; init; }
    public bool HasVertexColors { get; init; }
    public bool HasArmatureModifier { get; init; }
    public int BoundImages { get; init; }
    public int MissingImages { get; init; }
    public List<string> MaterialNames { get; init; } = [];
}

public sealed class BlenderValidationReport
{
    public string SchemaVersion { get; init; } = "1";
    public string ToolVersion { get; init; } = "";
    public string ManifestPath { get; init; } = "";
    public string BlendPath { get; init; } = "";
    public string ProfileId { get; init; } = "";
    public bool Saved { get; init; }
    public bool ReopenedClean { get; init; }
    public BlendSceneStats? Scene { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public GoldenComparison? Golden { get; set; }
}

public sealed class BlenderRequest
{
    public required string ManifestPath { get; init; }
    public string? SavePath { get; init; }
    public string? ProfilePath { get; init; }
    public string? ReportPath { get; init; }
    public bool PackImages { get; init; } = true;
}

public sealed class BlenderJobResult
{
    public required BlenderValidationReport Report { get; init; }
    public required string BlendPath { get; init; }
    public required string ReportPath { get; init; }
    public required string LogPath { get; init; }
    public int BlenderExitCode { get; init; }
}

public static class BlenderLaunch
{
    public static string DefaultProfilePath(string repoRoot)
        => FirstExisting(
            Path.Combine(repoRoot, "config", "material-profiles", "3x.json"),
            Path.Combine(AppContext.BaseDirectory, "config", "material-profiles", "3x.json"));

    public static string DefaultScriptPath(string repoRoot)
        => FirstExisting(
            Path.Combine(repoRoot, "blender", "scripts", "batch_import.py"),
            Path.Combine(AppContext.BaseDirectory, "blender", "scripts", "batch_import.py"));

    public static string DefaultSavePath(string repoRoot, string jobId)
        => Path.Combine(repoRoot, "work", "blend", $"{Sanitize(jobId)}.blend");

    public static IReadOnlyList<string> BuildArguments(
        string scriptPath,
        string manifestPath,
        string savePath,
        string profilePath,
        string reportPath,
        bool packImages)
    {
        var args = new List<string>
        {
            "--background",
            "--python",
            scriptPath,
            "--",
            "--manifest",
            manifestPath,
            "--save",
            savePath,
            "--profile",
            profilePath,
            "--report",
            reportPath
        };
        args.Add(packImages ? "--pack" : "--no-pack");
        return args;
    }

    private static string FirstExisting(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return Path.GetFullPath(candidates[0]);
    }

    private static string Sanitize(string jobId)
    {
        var trimmed = string.IsNullOrWhiteSpace(jobId) ? "character" : jobId.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(c, '_');
        }

        return trimmed;
    }
}
