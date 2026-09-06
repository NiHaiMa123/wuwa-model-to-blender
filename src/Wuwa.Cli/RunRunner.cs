using System.Text;
using System.Text.Json;
using Wuwa.Core;
using Wuwa.Export;
using Wuwa.Extractor;

namespace Wuwa.Cli;

public static class RunRunner
{
    public static async Task<RunJob> RunAsync(
        string workingDirectory,
        string configPath,
        RunRequest request,
        string logPath,
        CancellationToken cancellationToken = default)
    {
        var jobId = RunJobLayout.DeriveJobId(request.Asset, request.SavePath, request.JobId);
        var jobDirectory = RunJobLayout.JobDirectory(workingDirectory, jobId);
        var jobPath = string.IsNullOrWhiteSpace(request.ResultPath)
            ? RunJobLayout.DefaultJobPath(workingDirectory, jobId)
            : Path.GetFullPath(request.ResultPath, workingDirectory);
        Directory.CreateDirectory(jobDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var previous = RunJobStore.TryLoad(jobPath);
        var job = RunJob.Create(jobId);
        job.ConfigPath = configPath;
        job.AssetInput = request.Asset.Trim();
        job.JobPath = jobPath;
        job.LogPath = logPath;

        var output = new StringBuilder();
        output.AppendLine($"wuwa2blender run {ToolVersions.Tool}  job={jobId}");
        output.AppendLine($"config  {configPath}");
        output.AppendLine($"asset   {job.AssetInput}");
        Console.WriteLine($"wuwa2blender run  tool={ToolVersions.Tool}  job={jobId}");

        var force = request.Force;
        var fromStage = force ? null : RunStages.Canonical(request.FromStage);

        async Task PersistAsync()
        {
            await RunJobStore.SaveAsync(job, jobPath, cancellationToken).ConfigureAwait(false);
        }

        void Write(string line)
        {
            output.AppendLine(line);
            Console.WriteLine(line);
        }

        async Task BeginAsync(string id)
        {
            var stage = job.Stage(id);
            stage.Status = RunStageStatus.Running;
            stage.Started = DateTimeOffset.UtcNow;
            stage.Summary = "running";
            await PersistAsync().ConfigureAwait(false);
        }

        async Task FinishAsync(string id, string status, string summary, Dictionary<string, string>? details = null)
        {
            var stage = job.Stage(id);
            stage.Status = status;
            stage.Summary = summary;
            stage.Finished = DateTimeOffset.UtcNow;
            if (details is not null)
            {
                foreach (var pair in details)
                {
                    stage.Details[pair.Key] = pair.Value;
                }
            }

            if (status == RunStageStatus.Fail)
            {
                job.Errors.Add($"{id}: {summary}");
            }
            else if (status == RunStageStatus.Warn)
            {
                job.Warnings.Add($"{id}: {summary}");
            }

            Write($"[{status,-7}] {id}: {summary}");
            job.RefreshOverall();
            await PersistAsync().ConfigureAwait(false);
        }

        async Task SkipAsync(string id, string summary, Dictionary<string, string>? details = null)
        {
            var stage = job.Stage(id);
            stage.Status = RunStageStatus.Skipped;
            stage.Summary = summary;
            stage.Started ??= DateTimeOffset.UtcNow;
            stage.Finished = DateTimeOffset.UtcNow;
            if (details is not null)
            {
                foreach (var pair in details)
                {
                    stage.Details[pair.Key] = pair.Value;
                }
            }

            Write($"[{"skipped",-7}] {id}: {summary}");
            await PersistAsync().ConfigureAwait(false);
        }

        bool AllowSkip(string id)
        {
            if (force)
            {
                return false;
            }

            if (fromStage is not null && RunCache.MustRunFrom(id, fromStage))
            {
                return false;
            }

            return true;
        }

        try
        {
            await BeginAsync(RunStages.ResolveConfig).ConfigureAwait(false);
            AppConfig config;
            try
            {
                config = ConfigLoader.Load(configPath);
            }
            catch (Exception ex)
            {
                await FinishAsync(RunStages.ResolveConfig, RunStageStatus.Fail, ex.Message).ConfigureAwait(false);
                return job;
            }

            var profilePath = string.IsNullOrWhiteSpace(request.ProfilePath)
                ? BlenderLaunch.DefaultProfilePath(workingDirectory)
                : Path.GetFullPath(request.ProfilePath, workingDirectory);
            if (!File.Exists(profilePath))
            {
                await FinishAsync(RunStages.ResolveConfig, RunStageStatus.Fail, $"Material profile not found: {profilePath}").ConfigureAwait(false);
                return job;
            }

            var savePath = string.IsNullOrWhiteSpace(request.SavePath)
                ? RunJobLayout.DefaultSavePath(workingDirectory, jobId)
                : Path.GetFullPath(request.SavePath, workingDirectory);
            var exportDirectory = string.IsNullOrWhiteSpace(request.OutputDirectory)
                ? RunJobLayout.DefaultExportDirectory(workingDirectory, jobId)
                : Path.GetFullPath(request.OutputDirectory, workingDirectory);
            var manifestPath = Path.Combine(exportDirectory, "manifest.json");
            var reportPath = string.IsNullOrWhiteSpace(request.ReportPath)
                ? Path.ChangeExtension(savePath, ".validation.json")
                : Path.GetFullPath(request.ReportPath, workingDirectory);

            job.ProfilePath = profilePath;
            job.SavePath = savePath;
            job.BlendPath = savePath;
            job.ExportDirectory = exportDirectory;
            job.ManifestPath = manifestPath;
            job.ReportPath = reportPath;

            var profileHash = ContentHashing.Sha256File(profilePath);
            await FinishAsync(
                RunStages.ResolveConfig,
                RunStageStatus.Pass,
                $"loaded {configPath}",
                new Dictionary<string, string>
                {
                    ["export"] = exportDirectory,
                    ["save"] = savePath,
                    ["profile"] = profilePath
                }).ConfigureAwait(false);

            var configFingerprint = RunFingerprint.Compute(new RunFingerprintInput
            {
                UeVersion = config.Game.UeVersion,
                GameVersion = config.Game.Version,
                PaksDir = config.Game.PaksDir,
                InstallDir = config.Game.InstallDir,
                BlenderExe = config.Blender.Executable,
                AesMode = config.Decryption.Aes.Mode,
                AesEndpoint = config.Decryption.Aes.Endpoint,
                MappingsMode = config.Decryption.Mappings.Mode
            }).Config;

            AesKeySet aes;
            MappingsDescriptor mappings;
            var skipDoctor = AllowSkip(RunStages.Doctor) &&
                              (RunCache.CanSkipBefore(RunStages.Doctor, fromStage, previous) ||
                               RunCache.CanSkipDoctor(previous, configFingerprint, config.Blender.Executable, force));
            if (skipDoctor)
            {
                try
                {
                    aes = await AesKeyProviderFactory.Create(config.Decryption.Aes).GetAsync(cancellationToken).ConfigureAwait(false);
                    mappings = await MappingsProviderFactory.Create(config.Decryption.Mappings).GetAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await BeginAsync(RunStages.Doctor).ConfigureAwait(false);
                    await FinishAsync(RunStages.Doctor, RunStageStatus.Fail, ex.InnerException?.Message ?? ex.Message).ConfigureAwait(false);
                    return job;
                }

                var previousDoctor = previous?.TryStage(RunStages.Doctor);
                await SkipAsync(
                    RunStages.Doctor,
                    previousDoctor?.Summary ?? "reused previous doctor result",
                    previousDoctor?.Details).ConfigureAwait(false);
            }
            else
            {
                await BeginAsync(RunStages.Doctor).ConfigureAwait(false);
                DoctorResult doctor;
                try
                {
                    doctor = await DoctorRunner.RunAsync(configPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await FinishAsync(RunStages.Doctor, RunStageStatus.Fail, ex.InnerException?.Message ?? ex.Message).ConfigureAwait(false);
                    return job;
                }

                var doctorCopy = Path.Combine(jobDirectory, "doctor.json");
                await DoctorRunner.WriteResultAsync(doctor, doctorCopy, cancellationToken).ConfigureAwait(false);
                DoctorRunner.WriteHuman(doctor, Console.Out);
                output.AppendLine($"doctor overall={doctor.OverallStatus}");

                try
                {
                    aes = await AesKeyProviderFactory.Create(config.Decryption.Aes).GetAsync(cancellationToken).ConfigureAwait(false);
                    mappings = await MappingsProviderFactory.Create(config.Decryption.Mappings).GetAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await FinishAsync(RunStages.Doctor, RunStageStatus.Fail, ex.InnerException?.Message ?? ex.Message).ConfigureAwait(false);
                    return job;
                }

                var doctorStatus = doctor.Checks.Any(c => c.Status == DoctorStatus.Fail)
                    ? RunStageStatus.Fail
                    : doctor.OverallStatus == DoctorStatus.Warn ? RunStageStatus.Warn : RunStageStatus.Pass;
                await FinishAsync(
                    RunStages.Doctor,
                    doctorStatus,
                    $"overall={doctor.OverallStatus}",
                    new Dictionary<string, string>
                    {
                        ["result"] = doctorCopy,
                        ["aesHash"] = aes.ContentHash,
                        ["aesSource"] = aes.SourceId
                    }).ConfigureAwait(false);
                if (doctorStatus == RunStageStatus.Fail)
                {
                    return job;
                }
            }

            var skipIndex = AllowSkip(RunStages.Index) &&
                            (RunCache.CanSkipBefore(RunStages.Index, fromStage, previous) ||
                             RunCache.CanSkipIndex(previous, job.AssetInput, null, force));
            Cue4ParseSession? session = null;
            try
            {
                if (skipIndex)
                {
                    job.ResolvedAsset = previous!.ResolvedAsset;
                    await SkipAsync(RunStages.Index, job.ResolvedAsset).ConfigureAwait(false);
                }
                else
                {
                    await BeginAsync(RunStages.Index).ConfigureAwait(false);
                    if (RunIndex.NeedsSearch(job.AssetInput))
                    {
                        session = Cue4ParseMount.Open(config.Game.PaksDir, config.Game.UeVersion, aes, mappings);
                        var aliasPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath))!, "search-aliases.json");
                        var aliases = SearchAliasLoader.Load(aliasPath);
                        var terms = SearchQuery.Expand(job.AssetInput, aliases);
                        var search = AssetIndex.Search(
                            session.Provider,
                            job.AssetInput,
                            terms,
                            new AssetSearchOptions { Limit = 10, Scan = 80, TypeFilter = "SkeletalMesh" });
                        var searchPath = Path.Combine(jobDirectory, "search.json");
                        await SearchRunner.WriteResultAsync(search, searchPath, cancellationToken).ConfigureAwait(false);
                        if (search.Hits.Count == 0)
                        {
                            await FinishAsync(
                                RunStages.Index,
                                RunStageStatus.Fail,
                                $"No SkeletalMesh hit for '{job.AssetInput}'. Pass a full object path.").ConfigureAwait(false);
                            return job;
                        }

                        var hit = search.Hits[0];
                        if (!hit.ExportType.Equals("SkeletalMesh", StringComparison.OrdinalIgnoreCase))
                        {
                            await FinishAsync(
                                RunStages.Index,
                                RunStageStatus.Fail,
                                $"Top hit is {hit.ExportType}, expected SkeletalMesh: {hit.ObjectPath}").ConfigureAwait(false);
                            return job;
                        }

                        job.ResolvedAsset = hit.ObjectPath;
                        if (search.Hits.Count > 1)
                        {
                            job.Warnings.Add($"Index picked top SkeletalMesh of {search.Hits.Count} hits: {hit.ObjectPath}");
                        }

                        await FinishAsync(
                            RunStages.Index,
                            search.Hits.Count > 1 ? RunStageStatus.Warn : RunStageStatus.Pass,
                            hit.ObjectPath,
                            new Dictionary<string, string>
                            {
                                ["unreal"] = hit.UnrealObjectPath,
                                ["score"] = hit.Score.ToString(),
                                ["hits"] = search.Hits.Count.ToString(),
                                ["files"] = search.MountedFiles.ToString()
                            }).ConfigureAwait(false);
                    }
                    else
                    {
                        job.ResolvedAsset = job.AssetInput;
                        await FinishAsync(
                            RunStages.Index,
                            RunStageStatus.Pass,
                            RunJobLayout.CanonicalAsset(job.ResolvedAsset)).ConfigureAwait(false);
                    }
                }

                var canonical = RunJobLayout.CanonicalAsset(job.ResolvedAsset);
                job.Fingerprints = RunFingerprint.Compute(new RunFingerprintInput
                {
                    ToolVersion = ToolVersions.Tool,
                    CanonicalAsset = canonical,
                    SavePath = savePath,
                    ProfilePath = profilePath,
                    ProfileHash = profileHash,
                    PackImages = request.PackImages,
                    IncludeAnimations = request.IncludeAnimations,
                    MeshQuality = request.MeshQuality,
                    UeVersion = config.Game.UeVersion,
                    GameVersion = config.Game.Version,
                    PaksDir = config.Game.PaksDir,
                    InstallDir = config.Game.InstallDir,
                    BlenderExe = config.Blender.Executable,
                    AesMode = config.Decryption.Aes.Mode,
                    AesEndpoint = config.Decryption.Aes.Endpoint,
                    AesHash = aes.ContentHash,
                    MappingsMode = config.Decryption.Mappings.Mode,
                    MappingsHash = mappings.ContentHash
                });
                await PersistAsync().ConfigureAwait(false);

                var skipExport = AllowSkip(RunStages.Export) &&
                                 AllowSkip(RunStages.ResolveDependencies) &&
                                 (RunCache.CanSkipBefore(RunStages.Export, fromStage, previous) ||
                                  RunCache.CanSkipExport(
                                      previous,
                                      job.Fingerprints.Source,
                                      manifestPath,
                                      exportDirectory,
                                      canonical,
                                      aes.ContentHash,
                                      force));

                ExportManifest manifest;
                if (skipExport)
                {
                    job.ReusedExport = true;
                    manifest = JsonSerializer.Deserialize<ExportManifest>(
                        await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false),
                        ConfigLoader.JsonOptions)
                        ?? throw new InvalidDataException($"Failed to parse cached manifest: {manifestPath}");
                    await SkipAsync(
                        RunStages.ResolveDependencies,
                        $"materials={manifest.Materials.Count} textures={manifest.Textures.Count} bones={manifest.Skeleton?.BoneCount ?? 0}").ConfigureAwait(false);
                    await SkipAsync(RunStages.Export, $"reused {exportDirectory}").ConfigureAwait(false);
                }
                else
                {
                    await BeginAsync(RunStages.ResolveDependencies).ConfigureAwait(false);
                    await BeginAsync(RunStages.Export).ConfigureAwait(false);
                    session ??= Cue4ParseMount.Open(config.Game.PaksDir, config.Game.UeVersion, aes, mappings);
                    ExportPipelineResult exported;
                    try
                    {
                        exported = await ExportPipeline.RunAsync(
                            config,
                            session,
                            aes,
                            mappings,
                            new ExportRequest
                            {
                                Asset = job.ResolvedAsset,
                                OutputDirectory = exportDirectory,
                                IncludeAnimations = request.IncludeAnimations,
                                MeshQuality = request.MeshQuality
                            },
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await FinishAsync(RunStages.ResolveDependencies, RunStageStatus.Fail, ex.InnerException?.Message ?? ex.Message).ConfigureAwait(false);
                        await FinishAsync(RunStages.Export, RunStageStatus.Fail, ex.InnerException?.Message ?? ex.Message).ConfigureAwait(false);
                        return job;
                    }

                    manifest = exported.Manifest;
                    job.ManifestPath = exported.ManifestPath;
                    ExportRunner.WriteHuman(exported, Console.Out);
                    var depStatus = manifest.Warnings.Count > 0 ? RunStageStatus.Warn : RunStageStatus.Pass;
                    await FinishAsync(
                        RunStages.ResolveDependencies,
                        depStatus,
                        $"materials={manifest.Materials.Count} textures={manifest.Textures.Count} bones={manifest.Skeleton?.BoneCount ?? 0}").ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(manifest.Mesh?.UeModel))
                    {
                        await FinishAsync(RunStages.Export, RunStageStatus.Fail, "No .uemodel was written.").ConfigureAwait(false);
                        return job;
                    }

                    var exportStatus = manifest.Golden is { Matched: false } ? RunStageStatus.Fail
                        : manifest.Warnings.Count > 0 ? RunStageStatus.Warn
                        : RunStageStatus.Pass;
                    await FinishAsync(
                        RunStages.Export,
                        exportStatus,
                        $"{manifest.Files.Count} files, uemodel={manifest.Mesh.UeModel}",
                        new Dictionary<string, string>
                        {
                            ["manifest"] = exported.ManifestPath,
                            ["files"] = manifest.Files.Count.ToString()
                        }).ConfigureAwait(false);
                    if (exportStatus == RunStageStatus.Fail)
                    {
                        return job;
                    }
                }
            }
            finally
            {
                session?.Dispose();
            }

            await BeginAsync(RunStages.ValidateExport).ConfigureAwait(false);
            ExportManifest validated;
            try
            {
                validated = JsonSerializer.Deserialize<ExportManifest>(
                    await File.ReadAllTextAsync(job.ManifestPath, cancellationToken).ConfigureAwait(false),
                    ConfigLoader.JsonOptions)
                    ?? throw new InvalidDataException("Failed to parse manifest.json");
            }
            catch (Exception ex)
            {
                await FinishAsync(RunStages.ValidateExport, RunStageStatus.Fail, ex.Message).ConfigureAwait(false);
                return job;
            }

            var missing = ExportStaging.MissingFiles(validated, job.ExportDirectory);
            if (missing.Count > 0)
            {
                await FinishAsync(
                    RunStages.ValidateExport,
                    RunStageStatus.Fail,
                    $"staging is incomplete, missing {missing.Count} file(s): {missing[0]}").ConfigureAwait(false);
                return job;
            }

            if (string.IsNullOrWhiteSpace(validated.Mesh?.UeModel))
            {
                await FinishAsync(RunStages.ValidateExport, RunStageStatus.Fail, "manifest has no .uemodel").ConfigureAwait(false);
                return job;
            }

            if (validated.Golden is { Matched: false } exportGolden)
            {
                await FinishAsync(
                    RunStages.ValidateExport,
                    RunStageStatus.Fail,
                    "golden mismatch: " + string.Join("; ", exportGolden.Mismatches)).ConfigureAwait(false);
                return job;
            }

            await FinishAsync(
                RunStages.ValidateExport,
                RunStageStatus.Pass,
                validated.Golden is { Matched: true } ? "golden match" : "staging files present").ConfigureAwait(false);

            var skipBlender = AllowSkip(RunStages.LaunchBlender) &&
                              (RunCache.CanSkipBefore(RunStages.LaunchBlender, fromStage, previous) ||
                               RunCache.CanSkipBlender(
                                   previous,
                                   job.Fingerprints.Profile,
                                   job.Fingerprints.Source,
                                   savePath,
                                   reportPath,
                                   force));

            BlenderValidationReport report;
            if (skipBlender)
            {
                job.ReusedBlend = true;
                await SkipAsync(RunStages.LaunchBlender, savePath).ConfigureAwait(false);
                report = JsonSerializer.Deserialize<BlenderValidationReport>(
                    await File.ReadAllTextAsync(reportPath, cancellationToken).ConfigureAwait(false),
                    ConfigLoader.JsonOptions)
                    ?? new BlenderValidationReport
                    {
                        ToolVersion = ToolVersions.Tool,
                        ManifestPath = job.ManifestPath,
                        BlendPath = savePath,
                        Errors = ["Failed to parse cached validation report."]
                    };
            }
            else
            {
                await BeginAsync(RunStages.LaunchBlender).ConfigureAwait(false);
                BlenderJobResult blender;
                try
                {
                    blender = await BlenderRunner.RunAsync(
                        workingDirectory,
                        configPath,
                        new BlenderRequest
                        {
                            ManifestPath = job.ManifestPath,
                            SavePath = savePath,
                            ProfilePath = profilePath,
                            ReportPath = reportPath,
                            PackImages = request.PackImages
                        },
                        Path.Combine(jobDirectory, "blender.log"),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await FinishAsync(RunStages.LaunchBlender, RunStageStatus.Fail, ex.InnerException?.Message ?? ex.Message).ConfigureAwait(false);
                    return job;
                }

                BlenderRunner.WriteHuman(blender, Console.Out);
                report = blender.Report;
                job.BlendPath = blender.BlendPath;
                job.ReportPath = blender.ReportPath;
                if (blender.BlenderExitCode != 0 || report.Errors.Count > 0)
                {
                    var reason = report.Errors.Count > 0
                        ? report.Errors[0]
                        : $"Blender exited {blender.BlenderExitCode}";
                    await FinishAsync(
                        RunStages.LaunchBlender,
                        RunStageStatus.Fail,
                        reason,
                        new Dictionary<string, string> { ["exit"] = blender.BlenderExitCode.ToString() }).ConfigureAwait(false);
                    return job;
                }

                await FinishAsync(
                    RunStages.LaunchBlender,
                    report.Warnings.Count > 0 ? RunStageStatus.Warn : RunStageStatus.Pass,
                    $"exit=0 saved={(report.Saved ? "yes" : "no")}",
                    new Dictionary<string, string>
                    {
                        ["blend"] = blender.BlendPath,
                        ["report"] = blender.ReportPath
                    }).ConfigureAwait(false);
            }

            await BeginAsync(RunStages.ValidateBlend).ConfigureAwait(false);
            if (report.Scene is not null && GoldenInvariants.Matches(validated.SourceObjectPath))
            {
                report.Golden = GoldenInvariants.CompareBlend(report.Scene);
            }

            if (report.Errors.Count > 0)
            {
                await FinishAsync(RunStages.ValidateBlend, RunStageStatus.Fail, report.Errors[0]).ConfigureAwait(false);
                return job;
            }

            if (!report.Saved)
            {
                await FinishAsync(RunStages.ValidateBlend, RunStageStatus.Fail, ".blend was not saved").ConfigureAwait(false);
                return job;
            }

            if (!report.ReopenedClean)
            {
                await FinishAsync(RunStages.ValidateBlend, RunStageStatus.Fail, "reopen reported missing files").ConfigureAwait(false);
                return job;
            }

            if (report.Golden is { Matched: false } blendGolden)
            {
                await FinishAsync(
                    RunStages.ValidateBlend,
                    RunStageStatus.Fail,
                    "golden mismatch: " + string.Join("; ", blendGolden.Mismatches)).ConfigureAwait(false);
                return job;
            }

            await FinishAsync(
                RunStages.ValidateBlend,
                RunStageStatus.Pass,
                report.Golden is { Matched: true } ? "golden match, packed, reopen clean" : "saved and reopened clean").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            output.AppendLine(ex.ToString());
            Console.Error.WriteLine($"run crashed: {ex}");
            var running = job.Stages.FirstOrDefault(s => s.Status == RunStageStatus.Running);
            if (running is not null)
            {
                running.Status = RunStageStatus.Fail;
                running.Summary = ex.InnerException?.Message ?? ex.Message;
                running.Finished = DateTimeOffset.UtcNow;
                job.Errors.Add($"{running.Id}: {running.Summary}");
            }
            else
            {
                job.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            }
        }
        finally
        {
            var save = job.TryStage(RunStages.SaveResult);
            if (save is not null && save.Status is RunStageStatus.Pending or RunStageStatus.Running)
            {
                save.Status = RunStageStatus.Pass;
                save.Started ??= DateTimeOffset.UtcNow;
                save.Finished = DateTimeOffset.UtcNow;
                save.Summary = jobPath;
                save.Details["job"] = jobPath;
                Write($"[{RunStageStatus.Pass,-7}] {RunStages.SaveResult}: {jobPath}");
            }

            job.RefreshOverall();
            try
            {
                await RunJobStore.SaveAsync(job, jobPath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                output.AppendLine("failed to write job.json: " + ex.Message);
                Console.Error.WriteLine($"failed to write job.json: {ex.Message}");
                if (save is not null)
                {
                    save.Status = RunStageStatus.Fail;
                    save.Summary = ex.Message;
                }

                job.Errors.Add("SaveResult: " + ex.Message);
                job.RefreshOverall();
            }

            output.AppendLine($"overall {job.OverallStatus}");
            output.AppendLine($"blend   {job.BlendPath}");
            output.AppendLine($"job     {job.JobPath}");
            try
            {
                await File.WriteAllTextAsync(logPath, output.ToString(), cancellationToken).ConfigureAwait(false);
                var jobLog = Path.Combine(jobDirectory, "run.log");
                File.Copy(logPath, jobLog, overwrite: true);
            }
            catch (Exception)
            {
                // log copy is best-effort; staging must stay
            }
        }

        return job;
    }

    public static void WriteHuman(RunJob job, TextWriter writer)
    {
        writer.WriteLine($"wuwa2blender run  tool={job.ToolVersion}  job={job.JobId}  overall={job.OverallStatus}");
        writer.WriteLine($"asset   {job.AssetInput}");
        if (!string.IsNullOrWhiteSpace(job.ResolvedAsset) &&
            !job.ResolvedAsset.Equals(job.AssetInput, StringComparison.OrdinalIgnoreCase))
        {
            writer.WriteLine($"resolved {job.ResolvedAsset}");
        }

        writer.WriteLine($"export  {job.ExportDirectory}  reused={(job.ReusedExport ? "yes" : "no")}");
        writer.WriteLine($"blend   {job.BlendPath}  reused={(job.ReusedBlend ? "yes" : "no")}");
        writer.WriteLine($"job     {job.JobPath}");
        foreach (var stage in job.Stages)
        {
            writer.WriteLine($"[{stage.Status,-7}] {stage.Id}: {stage.Summary}");
        }

        foreach (var warning in job.Warnings)
        {
            writer.WriteLine($"warn    {warning}");
        }

        foreach (var error in job.Errors)
        {
            writer.WriteLine($"error   {error}");
        }
    }
}
