using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Japlayer.Data.Context;
using Japlayer.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32.SafeHandles;

namespace Japlayer.CLI.Apps;

public class GenerateThumbnailsApp : BaseApp
{
    public override string Name => "generate-thumbnails";
    public override string Description => "Scans database and physical drives to extract media thumbnail previews.";

    private const int ThumbnailIntervalSeconds = 60;
    private const int MaxDatabaseBusyRetries = 5;
    private const int MaxFileMoveRetries = 3;
    private static readonly int FfmpegThreads = Math.Clamp(Environment.ProcessorCount / 4, 1, 4);

    private readonly SemaphoreSlim _globalSemaphore = new(Environment.ProcessorCount);
    private readonly SemaphoreSlim _databaseWriteSemaphore = new(1, 1);
    private readonly SqlitePragmaInterceptor _sqlitePragmaInterceptor = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _driveSemaphores = new(StringComparer.OrdinalIgnoreCase);

    public override async Task RunAsync(string[] args)
    {
        if (!TryParseOptions(args, out var forceRegenerate, out var strategy, out var ignoredFilter, out var onlyFilter, out var parseError))
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

        LogFilePath = Path.Combine(Path.GetDirectoryName(settingsPath)!, "thumbnail_generation.log");
        LogInfo($"Using settings file: {settingsPath}");
        LogInfo($"Log file: {LogFilePath}");

        var settings = LoadSettings(settingsPath);
        if (settings == null || string.IsNullOrEmpty(settings.SqliteDatabasePath) || string.IsNullOrEmpty(settings.ImagePath))
        {
            LogError("Invalid settings configuration. Make sure SqliteDatabasePath and ImagePath are defined.");
            return;
        }

        LogInfo($"Database path: {settings.SqliteDatabasePath}");
        LogInfo($"Images folder: {settings.ImagePath}");

        if (!File.Exists(settings.SqliteDatabasePath))
        {
            LogError($"Database file does not exist: {settings.SqliteDatabasePath}");
            return;
        }

        if (!Directory.Exists(settings.ImagePath))
        {
            LogError($"Images directory does not exist: {settings.ImagePath}");
            return;
        }

        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
        optionsBuilder.UseSqlite($"Data Source={settings.SqliteDatabasePath}")
            .AddInterceptors(_sqlitePragmaInterceptor);

        using var context = new DatabaseContext(optionsBuilder.Options);

        try
        {
            LogInfo("Checking / creating database table schema...");
            await EnsureTableCreatedAsync(context);
        }
        catch (Exception exception)
        {
            LogError($"Failed to initialize database table: {exception.Message}");
            return;
        }

        CleanupLeftoverTempFiles(settings.ImagePath);

        // Skip full-table disk verification when targeting specific scenes (--only) or forcing full regeneration.
        if (!forceRegenerate && !onlyFilter.HasRules)
        {
            await VerifyDatabaseConsistencyAsync(optionsBuilder.Options, settings.ImagePath);
        }
        else if (forceRegenerate)
        {
            LogInfo("Force regeneration enabled (--force): all matching scenes will be reprocessed.");
        }

        var targetLocations = await QueryTargetLocationsAsync(context, forceRegenerate, onlyFilter, ignoredFilter);
        if (targetLocations == null)
        {
            return;
        }

        if (targetLocations.Count == 0)
        {
            LogInfo("All media locations are already processed. Exiting.");
            return;
        }

        targetLocations = InterleaveByDrive(targetLocations);

        var globalLimit = Environment.ProcessorCount;
        var pipelineLimit = Math.Max(100, globalLimit * 4);
        var strategyName = strategy switch
        {
            ExtractionStrategy.Auto => "auto (seek default; single-pass for 8K/HEVC/AVI)",
            ExtractionStrategy.SinglePass => "single-pass (-skip_frame nokey)",
            _ => "seek (-ss)"
        };
        LogInfo($"Starting thumbnail generation using {strategyName} strategy (max global running tasks: {globalLimit}, pipeline tasks limit: {pipelineLimit})...");

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = pipelineLimit
        };

        var successCount = 0;
        var failCount = 0;
        var stopwatch = Stopwatch.StartNew();

        await Parallel.ForEachAsync(targetLocations, parallelOptions, async (location, cancellationToken) =>
        {
            var succeeded = await ProcessLocationAsync(
                location,
                optionsBuilder.Options,
                settings.ImagePath,
                forceRegenerate,
                strategy,
                cancellationToken);

            if (succeeded)
            {
                Interlocked.Increment(ref successCount);
            }
            else
            {
                Interlocked.Increment(ref failCount);
            }
        });

        stopwatch.Stop();
        LogInfo($"Generation completed in {stopwatch.Elapsed.TotalMinutes:F2} minutes. Successfully processed: {successCount}, Failed: {failCount}.");
    }

    private async Task VerifyDatabaseConsistencyAsync(DbContextOptions<DatabaseContext> databaseOptions, string imagePath)
    {
        try
        {
            LogInfo("Verifying thumbnail files on disk consistency...");
            using var context = new DatabaseContext(databaseOptions);
            var databaseThumbnails = await context.MediaThumbnails
                .AsNoTracking()
                .Select(thumbnail => new { thumbnail.MediaId, thumbnail.Scene, thumbnail.Path })
                .ToListAsync();

            var groupedThumbnails = databaseThumbnails
                .GroupBy(thumbnail => new { thumbnail.MediaId, thumbnail.Scene })
                .ToList();

            var missingGroups = new List<(string MediaId, int Scene)>();
            foreach (var group in groupedThumbnails)
            {
                var firstThumbnail = group.First();
                var absolutePath = Path.Combine(imagePath, firstThumbnail.Path);
                if (!File.Exists(absolutePath))
                {
                    missingGroups.Add((group.Key.MediaId, group.Key.Scene));
                }
            }

            if (missingGroups.Count == 0)
            {
                LogInfo("All database preview records have corresponding files on disk.");
                return;
            }

            LogInfo($"Found {missingGroups.Count} processed scenes with missing files on disk. Cleaning up database records for reprocessing...");

            const int BatchSize = 100;
            for (var index = 0; index < missingGroups.Count; index += BatchSize)
            {
                var batch = missingGroups.Skip(index).Take(BatchSize).ToList();

                using var deleteContext = new DatabaseContext(databaseOptions);
                using var transaction = await deleteContext.Database.BeginTransactionAsync();
                try
                {
                    foreach (var (mediaId, scene) in batch)
                    {
                        var recordsToDelete = await deleteContext.MediaThumbnails
                            .Where(thumbnail => thumbnail.MediaId == mediaId && thumbnail.Scene == scene)
                            .ToListAsync();
                        deleteContext.MediaThumbnails.RemoveRange(recordsToDelete);
                    }
                    await deleteContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception exception)
                {
                    await transaction.RollbackAsync();
                    LogError($"Failed to delete orphan database records: {exception.Message}");
                }
            }

            LogInfo("Database consistency cleanup complete.");
        }
        catch (Exception exception)
        {
            LogError($"Failed to perform consistency check: {exception.Message}");
        }
    }

    private async Task<List<MediaLocation>?> QueryTargetLocationsAsync(
        DatabaseContext context,
        bool forceRegenerate,
        SceneFilterSet onlyFilter,
        SceneFilterSet ignoredFilter)
    {
        try
        {
            LogInfo("Scanning database for media files...");
            var mediaLocations = await context.MediaLocations.AsNoTracking().ToListAsync();
            var existingThumbnails = await context.MediaThumbnails
                .AsNoTracking()
                .Select(thumbnail => new { thumbnail.MediaId, thumbnail.Scene })
                .Distinct()
                .ToListAsync();

            var processedScenes = existingThumbnails.Select(thumbnail => (thumbnail.MediaId, thumbnail.Scene)).ToHashSet();
            var rawTargetLocations = forceRegenerate
                ? mediaLocations
                : [.. mediaLocations.Where(location => !processedScenes.Contains((location.MediaId, location.Scene)))];

            var targetLocations = rawTargetLocations
                .GroupBy(location => new { location.MediaId, location.Scene })
                .Select(SelectRepresentativeLocation)
                .ToList();

            LogInfo($"Found {mediaLocations.Count} total media locations. {processedScenes.Count} already have previews. {targetLocations.Count} scenes need processing.");

            if (onlyFilter.HasRules)
            {
                targetLocations = [.. targetLocations.Where(location => onlyFilter.Matches(location.MediaId, location.Scene))];
                LogInfo($"Filtered to {targetLocations.Count} scenes via --only / --scenes.");
            }

            if (ignoredFilter.HasRules)
            {
                var beforeIgnoreCount = targetLocations.Count;
                targetLocations = [.. targetLocations.Where(location => !ignoredFilter.Matches(location.MediaId, location.Scene))];
                var ignoredCount = beforeIgnoreCount - targetLocations.Count;
                LogInfo($"Ignored {ignoredCount} scenes via --ignore / --ignore-file. {targetLocations.Count} scenes remaining.");
            }

            return targetLocations;
        }
        catch (Exception exception)
        {
            LogError($"Failed to query database: {exception.Message}");
            return null;
        }
    }

    private static MediaLocation SelectRepresentativeLocation(IGrouping<object, MediaLocation> group)
    {
        var candidates = group.ToList();
        var existingCandidates = candidates.Where(candidate => File.Exists(candidate.Path)).ToList();

        if (existingCandidates.Count > 0)
        {
            // Prefer 8K or 4K files for higher-resolution sources; otherwise sort alphabetically.
            return existingCandidates
                .OrderByDescending(candidate => Path.GetFileNameWithoutExtension(candidate.Path).Contains("8k", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => Path.GetFileNameWithoutExtension(candidate.Path).Contains("4k", StringComparison.OrdinalIgnoreCase))
                .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        return candidates.First();
    }

    private static List<MediaLocation> InterleaveByDrive(List<MediaLocation> targetLocations)
    {
        var driveGroups = targetLocations
            .GroupBy(location => Path.GetPathRoot(location.Path) ?? "", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.ToList())
            .ToList();

        if (driveGroups.Count == 0)
        {
            return targetLocations;
        }

        var interleaved = new List<MediaLocation>(targetLocations.Count);
        var maxGroupSize = driveGroups.Max(group => group.Count);
        for (var index = 0; index < maxGroupSize; index++)
        {
            foreach (var group in driveGroups)
            {
                if (index < group.Count)
                {
                    interleaved.Add(group[index]);
                }
            }
        }

        return interleaved;
    }

    private async Task<bool> ProcessLocationAsync(
        MediaLocation location,
        DbContextOptions<DatabaseContext> databaseOptions,
        string imagePath,
        bool forceRegenerate,
        ExtractionStrategy strategy,
        CancellationToken cancellationToken)
    {
        var logPrefix = $"[{location.MediaId} - Scene {location.Scene}]";

        if (!File.Exists(location.Path))
        {
            LogError($"{logPrefix} File does not exist at local path: {location.Path}");
            return false;
        }

        var driveRoot = Path.GetPathRoot(location.Path) ?? "";
        var driveSemaphore = _driveSemaphores.GetOrAdd(driveRoot, root =>
        {
            var isSsd = DiskTypeDetector.IsSsd(root);
            var limit = isSsd ? Environment.ProcessorCount : 1;
            LogInfo($"Drive Throttler: Detected drive '{root}' as {(isSsd ? "SSD" : "HDD (concurrency limited to 1)")}.");
            return new SemaphoreSlim(limit, limit);
        });

        // Acquire drive semaphore FIRST, then global semaphore to prevent thread/task starvation
        await driveSemaphore.WaitAsync(cancellationToken);
        try
        {
            await _globalSemaphore.WaitAsync(cancellationToken);
            try
            {
                LogInfo($"{logPrefix} Starting processing on drive '{driveRoot}'...");

                var videoInfo = await GetVideoInfoAsync(location.Path);
                if (videoInfo == null || videoInfo.Duration <= 0)
                {
                    LogError($"{logPrefix} Could not retrieve video metadata. skipping.");
                    return false;
                }

                LogInfo($"{logPrefix} Video duration: {videoInfo.Duration:F2}s, resolution: {videoInfo.Width}x{videoInfo.Height}, codec: {videoInfo.CodecName ?? "unknown"}.");

                var resolvedStrategy = ResolveExtractionStrategy(strategy, location.Path, videoInfo, logPrefix);
                var videoFilter = BuildVideoFilter(location, videoInfo, logPrefix);
                var thumbsSubdirectory = Path.Combine(imagePath, "thumbs", location.MediaId, $"scene_{location.Scene}");
                Directory.CreateDirectory(thumbsSubdirectory);

                var tempPrefix = $"thumb_tmp_{Guid.NewGuid():N}_";
                var extractedFrames = resolvedStrategy switch
                {
                    ExtractionStrategy.SinglePass => await ExtractThumbnailsSinglePassAsync(location.Path, videoFilter, thumbsSubdirectory, tempPrefix, logPrefix),
                    _ => await ExtractThumbnailsSeekAsync(location.Path, videoInfo.Duration, videoFilter, thumbsSubdirectory, tempPrefix, logPrefix)
                };

                // Under --strategy auto, fall back to seek if single-pass fails (e.g. -hwaccel or -skip_frame incompatibility).
                if (extractedFrames.Count == 0 &&
                    strategy == ExtractionStrategy.Auto &&
                    resolvedStrategy == ExtractionStrategy.SinglePass)
                {
                    DeleteTempFiles(thumbsSubdirectory, tempPrefix);
                    LogInfo($"{logPrefix} Single-pass failed under auto strategy; falling back to seek (-ss)...");
                    extractedFrames = await ExtractThumbnailsSeekAsync(location.Path, videoInfo.Duration, videoFilter, thumbsSubdirectory, tempPrefix, logPrefix);
                }

                if (extractedFrames.Count == 0)
                {
                    DeleteTempFiles(thumbsSubdirectory, tempPrefix);
                    return false;
                }

                var plannedMoves = new List<(string TempFilePath, string OutputPath)>(extractedFrames.Count);
                var thumbnailsToInsert = new List<MediaThumbnail>(extractedFrames.Count);

                foreach (var (tempFilePath, timestampValue) in extractedFrames)
                {
                    var fileName = $"thumb_{timestampValue}.jpg";
                    var outputPath = Path.Combine(thumbsSubdirectory, fileName);
                    var relativePath = Path.Combine("thumbs", location.MediaId, $"scene_{location.Scene}", fileName).Replace('\\', '/');

                    plannedMoves.Add((tempFilePath, outputPath));
                    thumbnailsToInsert.Add(new MediaThumbnail
                    {
                        MediaId = location.MediaId,
                        Scene = location.Scene,
                        Timestamp = timestampValue,
                        Path = relativePath,
                        OriginalHostname = location.Hostname,
                        OriginalPath = location.Path
                    });
                }

                return await SaveSceneThumbnailsAsync(
                    databaseOptions,
                    location.MediaId,
                    location.Scene,
                    thumbsSubdirectory,
                    plannedMoves,
                    thumbnailsToInsert,
                    forceRegenerate,
                    logPrefix,
                    cancellationToken);
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
    }

    private string BuildVideoFilter(MediaLocation location, VideoInfo videoInfo, string logPrefix)
    {
        var isVr = location.Path.Contains("VR", StringComparison.OrdinalIgnoreCase) ||
                   location.MediaId.Contains("VR", StringComparison.OrdinalIgnoreCase);

        if (!isVr)
        {
            return "scale=1280:720:force_original_aspect_ratio=increase:flags=fast_bilinear,crop=1280:720";
        }

        if (videoInfo.Width > videoInfo.Height)
        {
            LogInfo($"{logPrefix} VR detected: Side-by-Side. Applying left-eye crop + 720p scale.");
            return "crop=iw/2:ih:0:0,scale=-2:720:flags=fast_bilinear";
        }

        LogInfo($"{logPrefix} VR detected: Over-Under. Applying top-eye crop + 720p scale.");
        return "crop=iw:ih/2:0:0,scale=-2:720:flags=fast_bilinear";
    }

    private async Task<List<(string TempFilePath, int Timestamp)>> ExtractThumbnailsSeekAsync(
        string videoPath,
        double durationSeconds,
        string videoFilter,
        string thumbsSubdirectory,
        string tempPrefix,
        string logPrefix)
    {
        var timestamps = new List<int>();
        // Avoid seeking within the final second where container audio duration can outlast the last video frame.
        for (var timestampValue = 0; timestampValue < durationSeconds - 1; timestampValue += ThumbnailIntervalSeconds)
        {
            timestamps.Add(timestampValue);
        }

        if (timestamps.Count == 0)
        {
            timestamps.Add(0);
        }

        LogInfo($"{logPrefix} Extracting {timestamps.Count} thumbnails (every {ThumbnailIntervalSeconds}s)...");

        var extractedFrames = new List<(string TempFilePath, int Timestamp)>(timestamps.Count);
        foreach (var timestampValue in timestamps)
        {
            var tempFilePath = Path.Combine(thumbsSubdirectory, $"{tempPrefix}{timestampValue}.jpg");
            if (!await ExtractFrameAsync(videoPath, timestampValue, videoFilter, tempFilePath))
            {
                LogError($"{logPrefix} Failed to extract frame at timestamp {timestampValue}s.");
                return [];
            }

            if (!File.Exists(tempFilePath))
            {
                // ffmpeg can exit 0 without writing a frame if the video stream ends slightly before container duration.
                if (timestampValue > 0 && timestampValue >= durationSeconds - 10)
                {
                    LogInfo($"{logPrefix} No frame written near end of stream at {timestampValue}s (duration {durationSeconds:F2}s); stopping extraction.");
                    break;
                }

                LogError($"{logPrefix} Frame file was not created at timestamp {timestampValue}s.");
                return [];
            }

            extractedFrames.Add((tempFilePath, timestampValue));
        }

        return extractedFrames;
    }

    /// <summary>
    /// Single-pass keyframe extraction (--single-pass). Reads the full file sequentially, which can be slower than
    /// <see cref="ExtractThumbnailsSeekAsync"/> on large HDD files because it decodes every keyframe.
    /// </summary>
    private async Task<List<(string TempFilePath, int Timestamp)>> ExtractThumbnailsSinglePassAsync(
        string videoPath,
        string videoFilter,
        string thumbsSubdirectory,
        string tempPrefix,
        string logPrefix)
    {
        LogInfo($"{logPrefix} Extracting thumbnails in single pass (every {ThumbnailIntervalSeconds}s)...");

        var tempOutputPattern = Path.Combine(thumbsSubdirectory, $"{tempPrefix}%d.jpg");
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            // Enable -hwaccel auto only on single-pass (1 process per scene) where GPU init cost amortizes across the whole video.
            Arguments = $"-skip_frame nokey -hwaccel auto -an -sn -dn -threads {FfmpegThreads} -i \"{videoPath}\" -vf \"fps=1/{ThumbnailIntervalSeconds},{videoFilter}\" -q:v 4 -start_number 0 \"{tempOutputPattern}\" -y",
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            LogError($"{logPrefix} Failed to extract thumbnails in single pass.");
            return [];
        }

        var extractedFrames = Directory.GetFiles(thumbsSubdirectory, $"{tempPrefix}*.jpg")
            .Select(filePath => new
            {
                FilePath = filePath,
                Index = int.TryParse(Path.GetFileNameWithoutExtension(filePath).AsSpan(tempPrefix.Length), out var parsedIndex)
                    ? parsedIndex
                    : -1
            })
            .Where(item => item.Index >= 0)
            .OrderBy(item => item.Index)
            .Select(item => (item.FilePath, item.Index * ThumbnailIntervalSeconds))
            .ToList();

        if (extractedFrames.Count == 0)
        {
            LogError($"{logPrefix} Single-pass extraction produced 0 frames.");
        }

        return extractedFrames;
    }

    private static void DeleteTempFiles(string thumbsSubdirectory, string tempPrefix)
    {
        foreach (var tempFile in Directory.GetFiles(thumbsSubdirectory, $"{tempPrefix}*.jpg"))
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static async Task EnsureTableCreatedAsync(DatabaseContext context)
    {
        await context.Database.OpenConnectionAsync();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS MediaThumbnails (
                mediaId TEXT NOT NULL,
                scene INTEGER NOT NULL,
                timestamp INTEGER NOT NULL,
                path TEXT NOT NULL,
                originalHostname TEXT NOT NULL,
                originalPath TEXT NOT NULL,
                PRIMARY KEY (mediaId, scene, timestamp),
                FOREIGN KEY (mediaId) REFERENCES Media(mediaId) ON DELETE CASCADE
            );";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<VideoInfo?> GetVideoInfoAsync(string videoPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v error -select_streams v:0 -show_entries format=duration:stream=width,height,codec_name -of json \"{videoPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            return null;
        }

        try
        {
            var output = await stdoutTask;
            using var doc = JsonDocument.Parse(output);

            var streams = doc.RootElement.GetProperty("streams");
            var width = 0;
            var height = 0;
            string? codecName = null;
            if (streams.GetArrayLength() > 0)
            {
                var firstStream = streams[0];
                width = firstStream.GetProperty("width").GetInt32();
                height = firstStream.GetProperty("height").GetInt32();
                if (firstStream.TryGetProperty("codec_name", out var codecProperty))
                {
                    codecName = codecProperty.GetString();
                }
            }

            var format = doc.RootElement.GetProperty("format");
            double duration = 0;
            if (format.TryGetProperty("duration", out var durationProp))
            {
                duration = double.Parse(durationProp.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            }

            return new VideoInfo { Width = width, Height = height, Duration = duration, CodecName = codecName };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> ExtractFrameAsync(string videoPath, int seconds, string filter, string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            // Skip audio/subtitles/data and cap threads per process (no -hwaccel here: per-process GPU init hurts seek speed).
            Arguments = $"-ss {seconds} -an -sn -dn -threads {FfmpegThreads} -i \"{videoPath}\" -vframes 1 -q:v 4 -vf \"{filter}\" \"{outputPath}\" -y",
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync();

        return process.ExitCode == 0;
    }

    private async Task<bool> SaveSceneThumbnailsAsync(
        DbContextOptions<DatabaseContext> databaseOptions,
        string mediaId,
        int scene,
        string thumbsSubdirectory,
        List<(string TempFilePath, string OutputPath)> plannedMoves,
        List<MediaThumbnail> thumbnailsToInsert,
        bool forceRegenerate,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        // Serialize DB commits across CLI workers so SQLite's single write lock never causes internal worker contention.
        await _databaseWriteSemaphore.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 1; attempt <= MaxDatabaseBusyRetries; attempt++)
            {
                var filesMoved = new List<string>(plannedMoves.Count);
                using var workerContext = new DatabaseContext(databaseOptions);
                using var transaction = await workerContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    if (forceRegenerate)
                    {
                        var existingRecords = await workerContext.MediaThumbnails
                            .Where(thumbnail => thumbnail.MediaId == mediaId && thumbnail.Scene == scene)
                            .ToListAsync(cancellationToken);
                        if (existingRecords.Count > 0)
                        {
                            workerContext.MediaThumbnails.RemoveRange(existingRecords);
                            await workerContext.SaveChangesAsync(cancellationToken);
                            // Clear tracker before re-adding rows with the same (MediaId, Scene, Timestamp) primary key.
                            workerContext.ChangeTracker.Clear();
                        }
                    }

                    foreach (var thumbnail in thumbnailsToInsert)
                    {
                        workerContext.MediaThumbnails.Add(thumbnail);
                    }

                    await workerContext.SaveChangesAsync(cancellationToken);

                    // Move staged files into place only after DB changes succeed, then commit to keep DB and disk in sync.
                    foreach (var (tempFilePath, outputPath) in plannedMoves)
                    {
                        await MoveFileWithRetryAsync(tempFilePath, outputPath, cancellationToken);
                        filesMoved.Add(outputPath);
                    }

                    await transaction.CommitAsync(cancellationToken);

                    if (forceRegenerate)
                    {
                        var createdSet = filesMoved.ToHashSet(StringComparer.OrdinalIgnoreCase);
                        foreach (var existingFile in Directory.GetFiles(thumbsSubdirectory, "thumb_*.jpg"))
                        {
                            if (!createdSet.Contains(existingFile) && File.Exists(existingFile))
                            {
                                try
                                {
                                    File.Delete(existingFile);
                                }
                                catch (IOException ioException)
                                {
                                    LogInfo($"{logPrefix} Could not delete stale file '{existingFile}' (in use): {ioException.Message}");
                                }
                            }
                        }
                    }

                    LogInfo($"{logPrefix} Successfully saved {thumbnailsToInsert.Count} thumbnails to database and committed transaction.");
                    return true;
                }
                catch (SqliteException sqliteException) when ((sqliteException.SqliteErrorCode == 5 || sqliteException.SqliteErrorCode == 6) && attempt < MaxDatabaseBusyRetries)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    LogInfo($"{logPrefix} SQLite busy (attempt {attempt}/{MaxDatabaseBusyRetries}), retrying...");
                    await Task.Delay(100 * attempt, cancellationToken);
                }
                catch (Exception exception)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    foreach (var (tempFilePath, _) in plannedMoves)
                    {
                        if (File.Exists(tempFilePath))
                        {
                            File.Delete(tempFilePath);
                        }
                    }

                    // Under --force, keep any pre-existing files on rollback instead of leaving DB rows pointing to deleted files.
                    if (!forceRegenerate)
                    {
                        foreach (var movedPath in filesMoved)
                        {
                            if (File.Exists(movedPath))
                            {
                                File.Delete(movedPath);
                            }
                        }
                    }

                    LogError($"{logPrefix} Failed to save metadata to database (rolled back): {exception.Message}");
                    return false;
                }
            }

            LogError($"{logPrefix} Failed to save metadata to database after {MaxDatabaseBusyRetries} busy retries.");
            return false;
        }
        finally
        {
            _databaseWriteSemaphore.Release();
        }
    }

    private static async Task MoveFileWithRetryAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxFileMoveRetries; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MaxFileMoveRetries)
            {
                await Task.Delay(100 * attempt, cancellationToken);
            }
        }
    }

    private void CleanupLeftoverTempFiles(string imagePath)
    {
        var thumbsRoot = Path.Combine(imagePath, "thumbs");
        if (!Directory.Exists(thumbsRoot))
        {
            return;
        }

        try
        {
            var leftoverFiles = Directory.GetFiles(thumbsRoot, "thumb_tmp_*.jpg", SearchOption.AllDirectories);
            foreach (var leftoverFile in leftoverFiles)
            {
                File.Delete(leftoverFile);
            }

            if (leftoverFiles.Length > 0)
            {
                LogInfo($"Cleaned up {leftoverFiles.Length} leftover temp thumbnail files from a previous run.");
            }
        }
        catch (Exception exception)
        {
            LogError($"Failed to clean up leftover temp files: {exception.Message}");
        }
    }

    private static bool TryParseOptions(
        string[] args,
        out bool forceRegenerate,
        out ExtractionStrategy strategy,
        out SceneFilterSet ignoredFilter,
        out SceneFilterSet onlyFilter,
        out string errorMessage)
    {
        forceRegenerate = false;
        strategy = ExtractionStrategy.Auto;
        ignoredFilter = new SceneFilterSet();
        onlyFilter = new SceneFilterSet();
        errorMessage = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "--regenerate", StringComparison.OrdinalIgnoreCase))
            {
                forceRegenerate = true;
            }
            else if (TryReadFlagValue(args, ref index, argument, "--strategy", out var strategyValue, out errorMessage))
            {
                if (!TryParseStrategy(strategyValue, out strategy, out errorMessage))
                {
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
            else if (TryReadFlagValue(args, ref index, argument, "--ignore", out var ignoreValue, out errorMessage))
            {
                if (ignoreValue != null)
                {
                    ignoredFilter.AddTokens(ignoreValue);
                }
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
            else if (TryReadFlagValue(args, ref index, argument, "--ignore-file", out var ignoreFilePath, out errorMessage))
            {
                if (ignoreFilePath != null && !ignoredFilter.TryLoadFile(ignoreFilePath, out errorMessage))
                {
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
            else if (TryReadFlagValue(args, ref index, argument, "--only", out var onlyValue, out errorMessage) ||
                     TryReadFlagValue(args, ref index, argument, "--scenes", out onlyValue, out errorMessage))
            {
                if (onlyValue != null)
                {
                    onlyFilter.AddTokens(onlyValue);
                }
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadFlagValue(
        string[] args,
        ref int index,
        string argument,
        string flagName,
        out string? value,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.Equals(argument, flagName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                errorMessage = $"Missing value after '{flagName}'.";
                value = null;
                return false;
            }

            value = args[++index];
            return true;
        }

        var prefix = $"{flagName}=";
        if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[prefix.Length..];
            return true;
        }

        value = null;
        return false;
    }

    private sealed class SceneFilterSet
    {
        private readonly HashSet<string> _mediaIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<(string MediaId, int Scene)> _scenes = new(new MediaSceneEqualityComparer());

        public bool HasRules => _mediaIds.Count > 0 || _scenes.Count > 0;

        public bool Matches(string mediaId, int scene) =>
            _mediaIds.Contains(mediaId) || _scenes.Contains((mediaId, scene));

        public bool TryLoadFile(string filePath, out string errorMessage)
        {
            if (!File.Exists(filePath))
            {
                errorMessage = $"Filter file not found: {filePath}";
                return false;
            }

            foreach (var rawLine in File.ReadLines(filePath))
            {
                var commentIndex = rawLine.IndexOf('#');
                var cleanedLine = (commentIndex >= 0 ? rawLine[..commentIndex] : rawLine).Trim();
                if (!string.IsNullOrEmpty(cleanedLine))
                {
                    AddTokens(cleanedLine);
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        public void AddTokens(string rawValue)
        {
            foreach (var rawToken in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colonIndex = rawToken.LastIndexOf(':');
                if (colonIndex > 0 && int.TryParse(rawToken[(colonIndex + 1)..], out var sceneNumber))
                {
                    var mediaId = rawToken[..colonIndex].Trim();
                    if (!string.IsNullOrEmpty(mediaId))
                    {
                        _scenes.Add((mediaId, sceneNumber));
                    }
                }
                else
                {
                    _mediaIds.Add(rawToken);
                }
            }
        }
    }

    private ExtractionStrategy ResolveExtractionStrategy(
        ExtractionStrategy requestedStrategy,
        string videoPath,
        VideoInfo videoInfo,
        string logPrefix)
    {
        if (requestedStrategy != ExtractionStrategy.Auto)
        {
            return requestedStrategy;
        }

        // VP9/AV1 do not skip inter-frames effectively under -skip_frame nokey and can be orders of magnitude slower in single-pass.
        if (string.Equals(videoInfo.CodecName, "vp9", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(videoInfo.CodecName, "av1", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractionStrategy.Seek;
        }

        var isEightK = videoInfo.Width >= 7680;
        var isHevc = string.Equals(videoInfo.CodecName, "hevc", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(videoInfo.CodecName, "h265", StringComparison.OrdinalIgnoreCase);
        var isAvi = string.Equals(Path.GetExtension(videoPath), ".avi", StringComparison.OrdinalIgnoreCase);

        if (isEightK || isHevc || isAvi)
        {
            var reason = isEightK ? "8K+" : isHevc ? "HEVC" : "AVI";
            LogInfo($"{logPrefix} Auto-selected single-pass strategy ({reason}).");
            return ExtractionStrategy.SinglePass;
        }

        return ExtractionStrategy.Seek;
    }

    private static bool TryParseStrategy(string? rawValue, out ExtractionStrategy strategy, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.Equals(rawValue, "auto", StringComparison.OrdinalIgnoreCase))
        {
            strategy = ExtractionStrategy.Auto;
            return true;
        }

        if (string.Equals(rawValue, "seek", StringComparison.OrdinalIgnoreCase))
        {
            strategy = ExtractionStrategy.Seek;
            return true;
        }

        if (string.Equals(rawValue, "single-pass", StringComparison.OrdinalIgnoreCase))
        {
            strategy = ExtractionStrategy.SinglePass;
            return true;
        }

        strategy = ExtractionStrategy.Auto;
        errorMessage = $"Unknown strategy '{rawValue}'. Valid values: auto, seek, single-pass.";
        return false;
    }

    private enum ExtractionStrategy
    {
        Auto,
        Seek,
        SinglePass
    }

    private sealed class MediaSceneEqualityComparer : IEqualityComparer<(string MediaId, int Scene)>
    {
        public bool Equals((string MediaId, int Scene) first, (string MediaId, int Scene) second) =>
            string.Equals(first.MediaId, second.MediaId, StringComparison.OrdinalIgnoreCase) &&
            first.Scene == second.Scene;

        public int GetHashCode((string MediaId, int Scene) item) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(item.MediaId), item.Scene);
    }
}

public class VideoInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public double Duration { get; set; }
    public string? CodecName { get; set; }
}

public static class DiskTypeDetector
{
    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public uint PropertyId;
        public uint QueryType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.I1)]
        public bool IncursSeekPenalty;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref STORAGE_PROPERTY_QUERY lpInBuffer,
        uint nInBufferSize,
        ref DEVICE_SEEK_PENALTY_DESCRIPTOR lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    public static bool IsSsd(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root))
            {
                return false;
            }

            var volumeName = @"\\.\" + root.TrimEnd('\\');

            using var handle = CreateFile(
                volumeName,
                0,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                return false;
            }

            var query = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = 7,
                QueryType = 0,
                AdditionalParameters = new byte[1]
            };

            var descriptor = new DEVICE_SEEK_PENALTY_DESCRIPTOR();

            var result = DeviceIoControl(
                handle,
                IOCTL_STORAGE_QUERY_PROPERTY,
                ref query,
                (uint)Marshal.SizeOf(query),
                ref descriptor,
                (uint)Marshal.SizeOf(descriptor),
                out _,
                IntPtr.Zero);

            if (result)
            {
                return !descriptor.IncursSeekPenalty;
            }
        }
        catch
        {
            // Fallback
        }
        return false;
    }
}
