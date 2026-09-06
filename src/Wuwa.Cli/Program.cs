using Wuwa.Cli;
using Wuwa.Core;
using Wuwa.Export;
using Wuwa.Extractor;

var workingDirectory = Directory.GetCurrentDirectory();
if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintHelp();
    return 2;
}

if (args[0] is "-v" or "--version" or "version")
{
    Console.WriteLine(ToolVersions.Tool);
    return 0;
}

var command = args[0];
var rest = args.Skip(1).ToArray();
return command.ToLowerInvariant() switch
{
    "doctor" => await RunDoctorAsync(workingDirectory, rest),
    "search" => await RunSearchAsync(workingDirectory, rest),
    "export" => await RunExportAsync(workingDirectory, rest),
    "blender" => await RunBlenderAsync(workingDirectory, rest),
    "run" => await RunPipelineAsync(workingDirectory, rest),
    _ => Unknown(command)
};

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'. Implemented: doctor, search, export, blender, run. Use --help.");
    return 2;
}

static void PrintHelp()
{
    Console.WriteLine($"wuwa2blender — Wuthering Waves local archive to Blender ({ToolVersions.Tool})");
    Console.WriteLine("Usage:");
    Console.WriteLine("  wuwa2blender --version");
    Console.WriteLine("  wuwa2blender doctor [--config <path>] [--result <path>]");
    Console.WriteLine("  wuwa2blender search <query> [--type SkeletalMesh] [--limit 25] [--config <path>] [--result <path>]");
    Console.WriteLine("  wuwa2blender export --asset <object-path> [--out <dir>] [--lods highest|all] [--config <path>]");
    Console.WriteLine("  wuwa2blender blender --manifest <manifest.json> [--save <file.blend>] [--profile <3x.json>] [--config <path>]");
    Console.WriteLine("  wuwa2blender run --asset <object-path-or-query> [--save <file.blend>] [--force] [--from-stage <stage>] [--config <path>]");
}

static async Task<int> RunDoctorAsync(string workingDirectory, string[] rest)
{
    string? configArg = null;
    string? resultArg = null;
    for (var i = 0; i < rest.Length; i++)
    {
        if (rest[i] is "--config" && i + 1 < rest.Length)
        {
            configArg = rest[++i];
        }
        else if (rest[i] is "--result" && i + 1 < rest.Length)
        {
            resultArg = rest[++i];
        }
        else
        {
            Console.Error.WriteLine($"Unknown option '{rest[i]}'.");
            return 2;
        }
    }

    var configPath = Path.GetFullPath(configArg ?? ConfigLoader.DefaultLocalConfigPath(workingDirectory));
    var resultPath = Path.GetFullPath(resultArg ?? Path.Combine(workingDirectory, "work", "doctor", "result.json"));
    var logPath = CreateLogPath(workingDirectory, "doctor");

    DoctorResult result;
    try
    {
        result = await DoctorRunner.RunAsync(configPath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"doctor crashed: {ex}");
        return 1;
    }

    await DoctorRunner.WriteResultAsync(result, resultPath);
    DoctorRunner.WriteHuman(result, Console.Out);
    await File.WriteAllTextAsync(logPath, System.Text.Json.JsonSerializer.Serialize(result, ConfigLoader.JsonOptions) + Environment.NewLine);
    Console.WriteLine($"result.json: {resultPath}");
    Console.WriteLine($"log: {logPath}");
    return result.Checks.Any(c => c.Status == DoctorStatus.Fail) ? 1 : 0;
}

static async Task<int> RunSearchAsync(string workingDirectory, string[] rest)
{
    string? query = null;
    string? configArg = null;
    string? resultArg = null;
    string? typeFilter = null;
    var limit = 25;
    var scan = 200;
    for (var i = 0; i < rest.Length; i++)
    {
        if (rest[i] is "--config" && i + 1 < rest.Length)
        {
            configArg = rest[++i];
        }
        else if (rest[i] is "--result" && i + 1 < rest.Length)
        {
            resultArg = rest[++i];
        }
        else if (rest[i] is "--type" && i + 1 < rest.Length)
        {
            typeFilter = rest[++i];
        }
        else if (rest[i] is "--limit" && i + 1 < rest.Length && int.TryParse(rest[i + 1], out var parsedLimit))
        {
            limit = parsedLimit;
            i++;
        }
        else if (rest[i] is "--scan" && i + 1 < rest.Length && int.TryParse(rest[i + 1], out var parsedScan))
        {
            scan = parsedScan;
            i++;
        }
        else if (rest[i].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Unknown option '{rest[i]}'.");
            return 2;
        }
        else if (query is null)
        {
            query = rest[i];
        }
        else
        {
            query += " " + rest[i];
        }
    }

    if (string.IsNullOrWhiteSpace(query))
    {
        Console.Error.WriteLine("search requires a query, e.g. wuwa2blender search Jinxi --type SkeletalMesh");
        return 2;
    }

    var configPath = Path.GetFullPath(configArg ?? ConfigLoader.DefaultLocalConfigPath(workingDirectory));
    var resultPath = Path.GetFullPath(resultArg ?? Path.Combine(workingDirectory, "work", "search", "result.json"));
    var logPath = CreateLogPath(workingDirectory, "search");
    SearchResult result;
    try
    {
        result = await SearchRunner.RunAsync(
            configPath,
            query,
            new AssetSearchOptions { Limit = Math.Max(1, limit), Scan = Math.Max(limit, scan), TypeFilter = typeFilter });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"search failed ({ex.GetType().Name}): {ex.InnerException?.Message ?? ex.Message}");
        await File.WriteAllTextAsync(logPath, ex.ToString());
        Console.WriteLine($"log: {logPath}");
        return 1;
    }

    await SearchRunner.WriteResultAsync(result, resultPath);
    SearchRunner.WriteHuman(result, Console.Out);
    await File.WriteAllTextAsync(logPath, System.Text.Json.JsonSerializer.Serialize(result, ConfigLoader.JsonOptions) + Environment.NewLine);
    Console.WriteLine();
    Console.WriteLine($"result.json: {resultPath}");
    Console.WriteLine($"log: {logPath}");
    return result.Hits.Count == 0 ? 1 : 0;
}

static async Task<int> RunExportAsync(string workingDirectory, string[] rest)
{
    string? asset = null;
    string? configArg = null;
    string? outArg = null;
    var lods = "highest";
    var includeAnim = false;
    for (var i = 0; i < rest.Length; i++)
    {
        if (rest[i] is "--config" && i + 1 < rest.Length)
        {
            configArg = rest[++i];
        }
        else if (rest[i] is "--asset" && i + 1 < rest.Length)
        {
            asset = rest[++i];
        }
        else if (rest[i] is "--out" && i + 1 < rest.Length)
        {
            outArg = rest[++i];
        }
        else if (rest[i] is "--lods" && i + 1 < rest.Length)
        {
            lods = rest[++i];
        }
        else if (rest[i] is "--anim")
        {
            includeAnim = true;
        }
        else if (rest[i].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Unknown option '{rest[i]}'.");
            return 2;
        }
        else if (asset is null)
        {
            asset = rest[i];
        }
        else
        {
            asset += " " + rest[i];
        }
    }

    if (string.IsNullOrWhiteSpace(asset))
    {
        Console.Error.WriteLine("export requires --asset <object-path>. Example:");
        Console.Error.WriteLine("  wuwa2blender export --asset Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011.R2T1JinxiMd10011");
        return 2;
    }

    var configPath = Path.GetFullPath(configArg ?? ConfigLoader.DefaultLocalConfigPath(workingDirectory));
    var logPath = CreateLogPath(workingDirectory, "export");
    ExportPipelineResult result;
    try
    {
        result = await ExportRunner.RunAsync(
            configPath,
            new ExportRequest
            {
                Asset = asset,
                OutputDirectory = string.IsNullOrWhiteSpace(outArg) ? null : Path.GetFullPath(outArg),
                IncludeAnimations = includeAnim,
                MeshQuality = lods
            });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"export failed ({ex.GetType().Name}): {ex.InnerException?.Message ?? ex.Message}");
        await File.WriteAllTextAsync(logPath, ex.ToString());
        Console.WriteLine($"log: {logPath}");
        return 1;
    }

    ExportRunner.WriteHuman(result, Console.Out);
    await File.WriteAllTextAsync(logPath, System.Text.Json.JsonSerializer.Serialize(result.Manifest, ConfigLoader.JsonOptions) + Environment.NewLine);
    Console.WriteLine();
    Console.WriteLine($"manifest: {result.ManifestPath}");
    Console.WriteLine($"log: {result.LogPath}");
    if (result.Manifest.Mesh?.UeModel is null)
    {
        return 1;
    }

    if (result.Manifest.Golden is { Matched: false })
    {
        return 1;
    }

    return 0;
}

static async Task<int> RunBlenderAsync(string workingDirectory, string[] rest)
{
    string? manifest = null;
    string? configArg = null;
    string? saveArg = null;
    string? profileArg = null;
    string? reportArg = null;
    var pack = true;
    for (var i = 0; i < rest.Length; i++)
    {
        if (rest[i] is "--config" && i + 1 < rest.Length)
        {
            configArg = rest[++i];
        }
        else if (rest[i] is "--manifest" && i + 1 < rest.Length)
        {
            manifest = rest[++i];
        }
        else if (rest[i] is "--save" && i + 1 < rest.Length)
        {
            saveArg = rest[++i];
        }
        else if (rest[i] is "--profile" && i + 1 < rest.Length)
        {
            profileArg = rest[++i];
        }
        else if (rest[i] is "--report" && i + 1 < rest.Length)
        {
            reportArg = rest[++i];
        }
        else if (rest[i] is "--no-pack")
        {
            pack = false;
        }
        else if (rest[i] is "--pack")
        {
            pack = true;
        }
        else if (rest[i].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Unknown option '{rest[i]}'.");
            return 2;
        }
        else if (manifest is null)
        {
            manifest = rest[i];
        }
        else
        {
            Console.Error.WriteLine($"Unexpected argument '{rest[i]}'.");
            return 2;
        }
    }

    if (string.IsNullOrWhiteSpace(manifest))
    {
        Console.Error.WriteLine("blender requires --manifest <manifest.json>. Example:");
        Console.Error.WriteLine("  wuwa2blender blender --manifest work/exports/Jinhsi/manifest.json --save work/blend/Jinhsi.blend");
        return 2;
    }

    var configPath = Path.GetFullPath(configArg ?? ConfigLoader.DefaultLocalConfigPath(workingDirectory));
    var logPath = CreateLogPath(workingDirectory, "blender");
    BlenderJobResult result;
    try
    {
        result = await BlenderRunner.RunAsync(
            workingDirectory,
            configPath,
            new BlenderRequest
            {
                ManifestPath = Path.GetFullPath(manifest, workingDirectory),
                SavePath = string.IsNullOrWhiteSpace(saveArg) ? null : Path.GetFullPath(saveArg, workingDirectory),
                ProfilePath = string.IsNullOrWhiteSpace(profileArg) ? null : Path.GetFullPath(profileArg, workingDirectory),
                ReportPath = string.IsNullOrWhiteSpace(reportArg) ? null : Path.GetFullPath(reportArg, workingDirectory),
                PackImages = pack
            },
            logPath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"blender failed ({ex.GetType().Name}): {ex.InnerException?.Message ?? ex.Message}");
        await File.WriteAllTextAsync(logPath, ex.ToString());
        Console.WriteLine($"log: {logPath}");
        return 1;
    }

    BlenderRunner.WriteHuman(result, Console.Out);
    Console.WriteLine();
    Console.WriteLine($"blend: {result.BlendPath}");
    Console.WriteLine($"report: {result.ReportPath}");
    Console.WriteLine($"log: {result.LogPath}");
    if (result.BlenderExitCode != 0 || result.Report.Errors.Count > 0)
    {
        return 1;
    }

    if (result.Report.Golden is { Matched: false })
    {
        return 1;
    }

    if (!result.Report.Saved || !result.Report.ReopenedClean)
    {
        return 1;
    }

    return 0;
}

static async Task<int> RunPipelineAsync(string workingDirectory, string[] rest)
{
    string? asset = null;
    string? configArg = null;
    string? saveArg = null;
    string? outArg = null;
    string? profileArg = null;
    string? reportArg = null;
    string? jobArg = null;
    string? resultArg = null;
    string? fromStage = null;
    var lods = "highest";
    var includeAnim = false;
    var pack = true;
    var force = false;
    for (var i = 0; i < rest.Length; i++)
    {
        if (rest[i] is "--config" && i + 1 < rest.Length)
        {
            configArg = rest[++i];
        }
        else if (rest[i] is "--asset" && i + 1 < rest.Length)
        {
            asset = rest[++i];
        }
        else if (rest[i] is "--save" && i + 1 < rest.Length)
        {
            saveArg = rest[++i];
        }
        else if (rest[i] is "--out" && i + 1 < rest.Length)
        {
            outArg = rest[++i];
        }
        else if (rest[i] is "--profile" && i + 1 < rest.Length)
        {
            profileArg = rest[++i];
        }
        else if (rest[i] is "--report" && i + 1 < rest.Length)
        {
            reportArg = rest[++i];
        }
        else if (rest[i] is "--job" && i + 1 < rest.Length)
        {
            jobArg = rest[++i];
        }
        else if (rest[i] is "--result" && i + 1 < rest.Length)
        {
            resultArg = rest[++i];
        }
        else if (rest[i] is "--from-stage" && i + 1 < rest.Length)
        {
            fromStage = rest[++i];
        }
        else if (rest[i] is "--lods" && i + 1 < rest.Length)
        {
            lods = rest[++i];
        }
        else if (rest[i] is "--anim")
        {
            includeAnim = true;
        }
        else if (rest[i] is "--force")
        {
            force = true;
        }
        else if (rest[i] is "--no-pack")
        {
            pack = false;
        }
        else if (rest[i] is "--pack")
        {
            pack = true;
        }
        else if (rest[i].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Unknown option '{rest[i]}'.");
            return 2;
        }
        else if (asset is null)
        {
            asset = rest[i];
        }
        else
        {
            asset += " " + rest[i];
        }
    }

    if (string.IsNullOrWhiteSpace(asset))
    {
        Console.Error.WriteLine("run requires --asset <object-path-or-query>. Example:");
        Console.Error.WriteLine("  wuwa2blender run --asset Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011.R2T1JinxiMd10011 --save work/blend/Jinhsi.blend");
        return 2;
    }

    if (!string.IsNullOrWhiteSpace(fromStage) && RunStages.Canonical(fromStage) is null)
    {
        Console.Error.WriteLine($"Unknown --from-stage '{fromStage}'. Expected one of: {string.Join(", ", RunStages.Order)}");
        return 2;
    }

    var configPath = Path.GetFullPath(configArg ?? ConfigLoader.DefaultLocalConfigPath(workingDirectory));
    var jobId = RunJobLayout.DeriveJobId(asset, saveArg, jobArg);
    var logPath = CreateLogPath(workingDirectory, $"run-{jobId}");
    RunJob job;
    try
    {
        job = await RunRunner.RunAsync(
            workingDirectory,
            configPath,
            new RunRequest
            {
                Asset = asset,
                SavePath = string.IsNullOrWhiteSpace(saveArg) ? null : Path.GetFullPath(saveArg, workingDirectory),
                OutputDirectory = string.IsNullOrWhiteSpace(outArg) ? null : Path.GetFullPath(outArg, workingDirectory),
                ProfilePath = string.IsNullOrWhiteSpace(profileArg) ? null : Path.GetFullPath(profileArg, workingDirectory),
                ReportPath = string.IsNullOrWhiteSpace(reportArg) ? null : Path.GetFullPath(reportArg, workingDirectory),
                JobId = jobArg,
                ResultPath = string.IsNullOrWhiteSpace(resultArg) ? null : Path.GetFullPath(resultArg, workingDirectory),
                FromStage = fromStage,
                Force = force,
                PackImages = pack,
                IncludeAnimations = includeAnim,
                MeshQuality = lods
            },
            logPath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"run failed ({ex.GetType().Name}): {ex.InnerException?.Message ?? ex.Message}");
        await File.WriteAllTextAsync(logPath, ex.ToString());
        Console.WriteLine($"log: {logPath}");
        return 1;
    }

    Console.WriteLine();
    RunRunner.WriteHuman(job, Console.Out);
    Console.WriteLine();
    Console.WriteLine($"blend: {job.BlendPath}");
    Console.WriteLine($"manifest: {job.ManifestPath}");
    Console.WriteLine($"job: {job.JobPath}");
    Console.WriteLine($"log: {job.LogPath}");
    if (!job.Ok || job.Errors.Count > 0)
    {
        return 1;
    }

    return 0;
}

static string CreateLogPath(string workingDirectory, string command)
{
    var logDir = Path.Combine(workingDirectory, "work", "logs");
    Directory.CreateDirectory(logDir);
    return Path.Combine(logDir, $"{command}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
}
