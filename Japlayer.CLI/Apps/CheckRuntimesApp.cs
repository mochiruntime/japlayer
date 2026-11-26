using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Japlayer.CLI.Helpers;
using Japlayer.Data.Context;
using Japlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.CLI.Apps;

/// <summary>
/// Checks media runtime consistency against files on disk via <c>ffprobe</c> (dry-run by default)
/// and optionally persists exact per-location durations and synced metadata runtimes (<c>--apply</c>).
/// </summary>
public class CheckRuntimesApp : BaseApp
{
    public override string Name => "check-runtimes";
    public override string Description => "Checks media runtime consistency against files on disk (dry-run) and optionally updates the database.";

    private const int CorruptedRuntimeThresholdMinutes = 600;

    private readonly SemaphoreSlim _globalSemaphore = new(Environment.ProcessorCount);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _driveSemaphores = new(StringComparer.OrdinalIgnoreCase);
    private readonly SqlitePragmaInterceptor _sqlitePragmaInterceptor = new();

    public override async Task RunAsync(string[] args)
    {
        if (!TryParseOptions(args, out var options, out var parseError))
        {
            LogError(parseError);
            return;
        }

        var settingsPath = FindSettingsFile();
        if (string.IsNullOrEmpty(settingsPath))
        {
            LogError("Could not find 'japlayer.settings.json' in any parent directory.");
            return;
        }

        var settingsDirectory = Path.GetDirectoryName(settingsPath)!;
        LogFilePath = Path.Combine(settingsDirectory, "check_runtimes.log");
        LogInfo($"Using settings file: {settingsPath}");
        LogInfo($"Log file: {LogFilePath}");

        var settings = LoadSettings(settingsPath);
        if (settings == null || string.IsNullOrEmpty(settings.SqliteDatabasePath))
        {
            LogError("Invalid settings configuration. Make sure SqliteDatabasePath is defined.");
            return;
        }

        LogInfo($"Database path: {settings.SqliteDatabasePath}");
        if (!File.Exists(settings.SqliteDatabasePath))
        {
            LogError($"Database file does not exist: {settings.SqliteDatabasePath}");
            return;
        }

        var modeLabel = options.Apply ? "WRITE (--apply)" : "DRY-RUN (read-only)";
        LogInfo($"Mode: {modeLabel} | Tolerance: {options.ToleranceMinutes}m | Scene copy diff threshold: {options.SceneDiffSeconds}s");

        if (options.Apply && !options.NoBackup)
        {
            var backupPath = CreateDatabaseBackup(settings.SqliteDatabasePath, settingsDirectory);
            LogInfo($"Created database backup: {backupPath}");
        }

        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
        optionsBuilder.UseSqlite($"Data Source={settings.SqliteDatabasePath}")
            .AddInterceptors(_sqlitePragmaInterceptor);

        using var context = new DatabaseContext(optionsBuilder.Options);

        if (options.Apply)
        {
            LogInfo("Applying pending EF Core migrations...");
            await context.Database.MigrateAsync();
        }

        var locations = await context.MediaLocations
            .AsNoTracking()
            .ToListAsync();

        var metadataRows = await context.MediaMetadata
            .AsNoTracking()
            .ToListAsync();

        var filteredLocations = FilterLocations(locations, options.OnlyFilter, options.IgnoredFilter);
        if (filteredLocations.Count == 0)
        {
            LogInfo("No matching media locations found. Exiting.");
            return;
        }

        var existingPaths = filteredLocations
            .Select(location => location.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var interleavedPaths = MediaProbeHelper.InterleaveByDrive(existingPaths, path => path);
        LogInfo($"Probing {interleavedPaths.Count} distinct location paths across {filteredLocations.Select(l => l.MediaId).Distinct(StringComparer.OrdinalIgnoreCase).Count()} media items...");

        var stopwatch = Stopwatch.StartNew();
        var probedDurations = await ProbePathsAsync(interleavedPaths);
        stopwatch.Stop();
        LogInfo($"Probing completed in {stopwatch.Elapsed.TotalSeconds:F1}s.");

        var report = BuildAuditReport(
            filteredLocations,
            metadataRows,
            probedDurations,
            options);

        LogSummary(report);

        if (!string.IsNullOrWhiteSpace(options.ReportPath))
        {
            WriteCsvReport(report, options.ReportPath);
            LogInfo($"Wrote CSV audit report to: {Path.GetFullPath(options.ReportPath)}");
        }

        if (options.Apply)
        {
            await ApplyUpdatesAsync(context, report, probedDurations, options);
        }
    }

    private async Task<ConcurrentDictionary<string, double?>> ProbePathsAsync(List<string> paths)
    {
        var results = new ConcurrentDictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        var pipelineLimit = Math.Max(64, Environment.ProcessorCount * 4);
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = pipelineLimit };

        await Parallel.ForEachAsync(paths, parallelOptions, async (filePath, cancellationToken) =>
        {
            if (!File.Exists(filePath))
            {
                results[filePath] = null;
                return;
            }

            var driveRoot = Path.GetPathRoot(filePath) ?? string.Empty;
            var driveSemaphore = _driveSemaphores.GetOrAdd(driveRoot, root =>
            {
                var isSsd = DiskTypeDetector.IsSsd(root);
                var limit = isSsd ? Environment.ProcessorCount : 1;
                LogInfo($"Drive Throttler: Detected drive '{root}' as {(isSsd ? "SSD" : "HDD (concurrency limited to 1)")}.");
                return new SemaphoreSlim(limit, limit);
            });

            await driveSemaphore.WaitAsync(cancellationToken);
            try
            {
                await _globalSemaphore.WaitAsync(cancellationToken);
                try
                {
                    var videoInfo = await MediaProbeHelper.GetVideoInfoAsync(filePath, cancellationToken);
                    results[filePath] = videoInfo != null && videoInfo.Duration > 0 ? videoInfo.Duration : null;
                }
                finally
                {
                    _globalSemaphore.Release();
                }
            }
            finally
            {
                driveSemaphore.Release();
            }
        });

        return results;
    }

    private async Task ApplyUpdatesAsync(
        DatabaseContext context,
        RuntimeAuditReport report,
        IReadOnlyDictionary<string, double?> probedDurations,
        CheckRuntimesOptions options)
    {
        var updatedLocationsCount = 0;
        var updatedMetadataCount = 0;

        var auditByMediaId = report.Items.ToDictionary(item => item.MediaId, StringComparer.OrdinalIgnoreCase);
        var targetMediaIds = auditByMediaId.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var trackedLocations = await context.MediaLocations
                .Where(location => targetMediaIds.Contains(location.MediaId))
                .ToListAsync();

            foreach (var location in trackedLocations)
            {
                if (probedDurations.TryGetValue(location.Path, out var probedSeconds) &&
                    probedSeconds.HasValue &&
                    probedSeconds.Value > 0)
                {
                    var roundedSeconds = (int)Math.Round(probedSeconds.Value);
                    if (location.DurationSeconds != roundedSeconds)
                    {
                        location.DurationSeconds = roundedSeconds;
                        updatedLocationsCount++;
                    }
                }
            }

            var trackedMetadata = await context.MediaMetadata
                .Where(metadata => targetMediaIds.Contains(metadata.MediaId))
                .ToListAsync();

            foreach (var metadata in trackedMetadata)
            {
                if (!auditByMediaId.TryGetValue(metadata.MediaId, out var auditItem) ||
                    !auditItem.ActualRuntimeMinutes.HasValue)
                {
                    continue;
                }

                if (options.OnlyInvalid &&
                    auditItem.Status is not (RuntimeAuditStatus.Corrupted or RuntimeAuditStatus.Mismatched or RuntimeAuditStatus.MissingRuntime))
                {
                    continue;
                }

                if (metadata.RuntimeMinutes != auditItem.ActualRuntimeMinutes.Value)
                {
                    metadata.RuntimeMinutes = auditItem.ActualRuntimeMinutes.Value;
                    updatedMetadataCount++;
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            LogInfo($"Applied updates: {updatedLocationsCount} MediaLocations.DurationSeconds row(s), {updatedMetadataCount} MediaMetadata.RuntimeMinutes row(s).");
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync();
            LogError($"Failed to apply runtime updates (rolled back): {exception.Message}");
            throw;
        }
    }

    public static RuntimeAuditReport BuildAuditReport(
        IReadOnlyList<MediaLocation> locations,
        IReadOnlyList<MediaMetadata> metadataRows,
        IReadOnlyDictionary<string, double?> probedDurations,
        CheckRuntimesOptions options)
    {
        var metadataByMediaId = metadataRows
            .GroupBy(metadata => metadata.MediaId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var items = new List<MediaRuntimeAuditItem>();

        foreach (var mediaGroup in locations.GroupBy(location => location.MediaId, StringComparer.OrdinalIgnoreCase))
        {
            var mediaId = mediaGroup.Key;
            var sceneResults = new List<SceneProbeResult>();

            foreach (var sceneGroup in mediaGroup.GroupBy(location => location.Scene).OrderBy(group => group.Key))
            {
                var locationResults = sceneGroup
                    .Select(location =>
                    {
                        probedDurations.TryGetValue(location.Path, out var durationSeconds);
                        var exists = durationSeconds.HasValue || File.Exists(location.Path);
                        return new LocationProbeResult
                        {
                            Hostname = location.Hostname,
                            Path = location.Path,
                            FileExists = exists,
                            DurationSeconds = durationSeconds.HasValue ? Math.Round(durationSeconds.Value, 2) : null
                        };
                    })
                    .ToList();

                var validDurations = locationResults
                    .Where(result => result.DurationSeconds.HasValue && result.DurationSeconds.Value > 0)
                    .Select(result => result.DurationSeconds!.Value)
                    .ToList();

                var maxCopyDelta = validDurations.Count > 1
                    ? Math.Round(validDurations.Max() - validDurations.Min(), 2)
                    : 0;
                var hasCopyDiscrepancy = validDurations.Count > 1 && maxCopyDelta > options.SceneDiffSeconds;

                var representativeDuration = SelectRepresentativeDuration(locationResults);

                sceneResults.Add(new SceneProbeResult
                {
                    Scene = sceneGroup.Key,
                    RepresentativeDurationSeconds = representativeDuration.HasValue ? Math.Round(representativeDuration.Value, 2) : null,
                    HasCopyDiscrepancy = hasCopyDiscrepancy,
                    MaxCopyDeltaSeconds = maxCopyDelta,
                    Locations = locationResults
                });
            }

            var probedScenes = sceneResults.Where(scene => scene.RepresentativeDurationSeconds.HasValue).ToList();
            double? actualTotalSeconds = probedScenes.Count == sceneResults.Count && sceneResults.Count > 0
                ? Math.Round(probedScenes.Sum(scene => scene.RepresentativeDurationSeconds!.Value), 2)
                : null;
            int? actualMinutes = actualTotalSeconds.HasValue
                ? (int)Math.Round(actualTotalSeconds.Value / 60.0)
                : null;

            metadataByMediaId.TryGetValue(mediaId, out var mediaMetadataList);
            var primaryMetadata = mediaMetadataList?.FirstOrDefault();
            var dbMinutes = primaryMetadata?.RuntimeMinutes;

            var status = ClassifyStatus(
                hasMetadataRow: primaryMetadata != null,
                dbMinutes: dbMinutes,
                actualMinutes: actualMinutes,
                toleranceMinutes: options.ToleranceMinutes);

            items.Add(new MediaRuntimeAuditItem
            {
                MediaId = mediaId,
                SceneCount = sceneResults.Count,
                ProbedSceneCount = probedScenes.Count,
                DbRuntimeMinutes = dbMinutes,
                ActualDurationSeconds = actualTotalSeconds,
                ActualRuntimeMinutes = actualMinutes,
                DeltaMinutes = dbMinutes.HasValue && actualMinutes.HasValue ? actualMinutes.Value - dbMinutes.Value : null,
                Status = status,
                HasSceneCopyDiscrepancy = sceneResults.Any(scene => scene.HasCopyDiscrepancy),
                Scenes = sceneResults
            });
        }

        items = [.. items
            .OrderByDescending(item => item.Status != RuntimeAuditStatus.Ok)
            .ThenBy(item => item.Status)
            .ThenBy(item => item.MediaId, StringComparer.OrdinalIgnoreCase)];

        var summary = new RuntimeAuditSummary
        {
            TotalMedia = items.Count,
            Ok = items.Count(item => item.Status == RuntimeAuditStatus.Ok),
            Corrupted = items.Count(item => item.Status == RuntimeAuditStatus.Corrupted),
            Mismatched = items.Count(item => item.Status == RuntimeAuditStatus.Mismatched),
            MissingRuntime = items.Count(item => item.Status == RuntimeAuditStatus.MissingRuntime),
            MissingMetadataRow = items.Count(item => item.Status == RuntimeAuditStatus.MissingMetadataRow),
            FileMissing = items.Count(item => item.Status == RuntimeAuditStatus.FileMissing),
            SceneCopyDiscrepancies = items.Count(item => item.HasSceneCopyDiscrepancy)
        };

        return new RuntimeAuditReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            DryRun = !options.Apply,
            ToleranceMinutes = options.ToleranceMinutes,
            SceneDiffSeconds = options.SceneDiffSeconds,
            Summary = summary,
            Items = items
        };
    }

    public static RuntimeAuditStatus ClassifyStatus(
        bool hasMetadataRow,
        int? dbMinutes,
        int? actualMinutes,
        int toleranceMinutes)
    {
        if (!actualMinutes.HasValue)
        {
            return RuntimeAuditStatus.FileMissing;
        }

        if (!hasMetadataRow)
        {
            return RuntimeAuditStatus.MissingMetadataRow;
        }

        if (!dbMinutes.HasValue)
        {
            return RuntimeAuditStatus.MissingRuntime;
        }

        if (dbMinutes.Value <= 0 || dbMinutes.Value > CorruptedRuntimeThresholdMinutes)
        {
            return RuntimeAuditStatus.Corrupted;
        }

        if (Math.Abs(actualMinutes.Value - dbMinutes.Value) > toleranceMinutes)
        {
            return RuntimeAuditStatus.Mismatched;
        }

        return RuntimeAuditStatus.Ok;
    }

    private static double? SelectRepresentativeDuration(List<LocationProbeResult> locationResults)
    {
        var validCopies = locationResults
            .Where(result => result.DurationSeconds.HasValue && result.DurationSeconds.Value > 0)
            .OrderByDescending(result => Path.GetFileNameWithoutExtension(result.Path).Contains("8k", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(result => Path.GetFileNameWithoutExtension(result.Path).Contains("4k", StringComparison.OrdinalIgnoreCase))
            .ThenBy(result => result.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return validCopies.FirstOrDefault()?.DurationSeconds;
    }

    private static List<MediaLocation> FilterLocations(
        List<MediaLocation> locations,
        SceneFilterSet onlyFilter,
        SceneFilterSet ignoredFilter)
    {
        IEnumerable<MediaLocation> query = locations;

        if (onlyFilter.HasRules)
        {
            query = query.Where(location => onlyFilter.Matches(location.MediaId, location.Scene));
        }

        if (ignoredFilter.HasRules)
        {
            query = query.Where(location => !ignoredFilter.Matches(location.MediaId, location.Scene));
        }

        return [.. query];
    }

    private static string CreateDatabaseBackup(string databasePath, string settingsDirectory)
    {
        var backupsDirectory = Path.Combine(settingsDirectory, "backups");
        Directory.CreateDirectory(backupsDirectory);
        var backupFileName = $"medias.backup-{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var backupPath = Path.Combine(backupsDirectory, backupFileName);
        File.Copy(databasePath, backupPath, overwrite: false);
        return backupPath;
    }

    private static void WriteCsvReport(RuntimeAuditReport report, string reportPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("MediaId,SceneCount,ProbedSceneCount,DbRuntimeMinutes,ActualDurationSeconds,ActualRuntimeMinutes,DeltaMinutes,Status,HasSceneCopyDiscrepancy,MaxCopyDeltaSeconds");

        foreach (var item in report.Items)
        {
            var maxCopyDelta = item.Scenes.Count > 0
                ? item.Scenes.Max(scene => scene.MaxCopyDeltaSeconds)
                : 0;

            builder.Append(EscapeCsv(item.MediaId)).Append(',')
                .Append(item.SceneCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(item.ProbedSceneCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(item.DbRuntimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(item.ActualDurationSeconds?.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(item.ActualRuntimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(item.DeltaMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(item.Status).Append(',')
                .Append(item.HasSceneCopyDiscrepancy ? "true" : "false").Append(',')
                .AppendLine(maxCopyDelta.ToString("F2", CultureInfo.InvariantCulture));
        }

        File.WriteAllText(reportPath, builder.ToString());
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private void LogSummary(RuntimeAuditReport report)
    {
        var summary = report.Summary;
        LogInfo("Runtime Audit Summary:");
        LogInfo($"  Total media checked      : {summary.TotalMedia}");
        LogInfo($"  OK (within +/-{report.ToleranceMinutes}m)       : {summary.Ok}");
        LogInfo($"  Corrupted (<=0 or >600m) : {summary.Corrupted}");
        LogInfo($"  Mismatched (>{report.ToleranceMinutes}m delta)  : {summary.Mismatched}");
        LogInfo($"  Missing runtime (NULL)   : {summary.MissingRuntime}");
        LogInfo($"  Missing metadata row     : {summary.MissingMetadataRow}");
        LogInfo($"  Files missing / unprobed : {summary.FileMissing}");
        LogInfo($"  Scene copy discrepancies : {summary.SceneCopyDiscrepancies}");
    }

    public static bool TryParseOptions(string[] args, out CheckRuntimesOptions options, out string errorMessage)
    {
        options = new CheckRuntimesOptions();
        errorMessage = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--apply", StringComparison.OrdinalIgnoreCase))
            {
                options.Apply = true;
            }
            else if (string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                options.Apply = false;
            }
            else if (string.Equals(argument, "--only-invalid", StringComparison.OrdinalIgnoreCase))
            {
                options.OnlyInvalid = true;
            }
            else if (string.Equals(argument, "--no-backup", StringComparison.OrdinalIgnoreCase))
            {
                options.NoBackup = true;
            }
            else if (CliArgumentHelper.TryReadFlagValue(args, ref index, argument, "--report", out var reportPath, out errorMessage))
            {
                options.ReportPath = reportPath;
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
            else if (CliArgumentHelper.TryReadFlagValue(args, ref index, argument, "--tolerance-minutes", out var toleranceText, out errorMessage))
            {
                if (!int.TryParse(toleranceText, out var tolerance) || tolerance < 0)
                {
                    errorMessage = $"Invalid --tolerance-minutes value '{toleranceText}'. Expected non-negative integer.";
                    return false;
                }

                options.ToleranceMinutes = tolerance;
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
            else if (CliArgumentHelper.TryReadFlagValue(args, ref index, argument, "--scene-diff-seconds", out var sceneDiffText, out errorMessage))
            {
                if (!double.TryParse(sceneDiffText, out var sceneDiff) || sceneDiff < 0)
                {
                    errorMessage = $"Invalid --scene-diff-seconds value '{sceneDiffText}'. Expected non-negative number.";
                    return false;
                }

                options.SceneDiffSeconds = sceneDiff;
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
            else if (CliArgumentHelper.TryReadFlagValue(args, ref index, argument, "--ignore", out var ignoreValue, out errorMessage))
            {
                if (ignoreValue != null)
                {
                    options.IgnoredFilter.AddTokens(ignoreValue);
                }
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
            else if (CliArgumentHelper.TryReadFlagValue(args, ref index, argument, "--ignore-file", out var ignoreFilePath, out errorMessage))
            {
                if (ignoreFilePath != null && !options.IgnoredFilter.TryLoadFile(ignoreFilePath, out errorMessage))
                {
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
            else if (CliArgumentHelper.TryReadFlagValue(args, ref index, argument, "--only", out var onlyValue, out errorMessage) ||
                     CliArgumentHelper.TryReadFlagValue(args, ref index, argument, "--scenes", out onlyValue, out errorMessage))
            {
                if (onlyValue != null)
                {
                    options.OnlyFilter.AddTokens(onlyValue);
                }
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
            else
            {
                errorMessage = $"Unknown option '{argument}'.";
                return false;
            }
        }

        return true;
    }
}

public sealed class CheckRuntimesOptions
{
    public bool Apply { get; set; }
    public bool OnlyInvalid { get; set; }
    public bool NoBackup { get; set; }
    public string? ReportPath { get; set; }
    public int ToleranceMinutes { get; set; } = 2;
    public double SceneDiffSeconds { get; set; } = 5;
    public SceneFilterSet OnlyFilter { get; } = new();
    public SceneFilterSet IgnoredFilter { get; } = new();
}

public enum RuntimeAuditStatus
{
    Ok,
    Corrupted,
    Mismatched,
    MissingRuntime,
    MissingMetadataRow,
    FileMissing
}

public sealed class RuntimeAuditReport
{
    public DateTime GeneratedAtUtc { get; set; }
    public bool DryRun { get; set; }
    public int ToleranceMinutes { get; set; }
    public double SceneDiffSeconds { get; set; }
    public RuntimeAuditSummary Summary { get; set; } = new();
    public List<MediaRuntimeAuditItem> Items { get; set; } = [];
}

public sealed class RuntimeAuditSummary
{
    public int TotalMedia { get; set; }
    public int Ok { get; set; }
    public int Corrupted { get; set; }
    public int Mismatched { get; set; }
    public int MissingRuntime { get; set; }
    public int MissingMetadataRow { get; set; }
    public int FileMissing { get; set; }
    public int SceneCopyDiscrepancies { get; set; }
}

public sealed class MediaRuntimeAuditItem
{
    public string MediaId { get; set; } = string.Empty;
    public int SceneCount { get; set; }
    public int ProbedSceneCount { get; set; }
    public int? DbRuntimeMinutes { get; set; }
    public double? ActualDurationSeconds { get; set; }
    public int? ActualRuntimeMinutes { get; set; }
    public int? DeltaMinutes { get; set; }
    public RuntimeAuditStatus Status { get; set; }
    public bool HasSceneCopyDiscrepancy { get; set; }
    public List<SceneProbeResult> Scenes { get; set; } = [];
}

public sealed class SceneProbeResult
{
    public int Scene { get; set; }
    public double? RepresentativeDurationSeconds { get; set; }
    public bool HasCopyDiscrepancy { get; set; }
    public double MaxCopyDeltaSeconds { get; set; }
    public List<LocationProbeResult> Locations { get; set; } = [];
}

public sealed class LocationProbeResult
{
    public string Hostname { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool FileExists { get; set; }
    public double? DurationSeconds { get; set; }
}
