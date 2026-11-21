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

    private readonly SemaphoreSlim _globalSemaphore = new(Environment.ProcessorCount);
    private readonly SemaphoreSlim _databaseWriteSemaphore = new(1, 1);
    private readonly SqlitePragmaInterceptor _sqlitePragmaInterceptor = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _driveSemaphores = new(StringComparer.OrdinalIgnoreCase);

    public override async Task RunAsync(string[] args)
    {
        var forceRegenerate = args.Any(argument =>
            string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, "--regenerate", StringComparison.OrdinalIgnoreCase));

        if (!TryParseIgnoreRules(args, out var ignoredMediaIds, out var ignoredScenes, out var ignoreParseError))
        {
            LogError(ignoreParseError);
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

        // Create context
        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
        optionsBuilder.UseSqlite($"Data Source={settings.SqliteDatabasePath}")
            .AddInterceptors(_sqlitePragmaInterceptor);

        using var context = new DatabaseContext(optionsBuilder.Options);

        // Ensure table exists
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

        // Cleanup leftover temp files from any previously interrupted run
        CleanupLeftoverTempFiles(settings.ImagePath);

        // Self-healing: Check if any committed thumbnails are missing their files on disk
        if (!forceRegenerate)
        {
            try
            {
                LogInfo("Verifying thumbnail files on disk consistency...");
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
                    // Check if the first file in the group exists on disk
                    var firstThumbnail = group.First();
                    var absolutePath = Path.Combine(settings.ImagePath, firstThumbnail.Path);
                    if (!File.Exists(absolutePath))
                    {
                        missingGroups.Add((group.Key.MediaId, group.Key.Scene));
                    }
                }

                if (missingGroups.Count > 0)
                {
                    LogInfo($"Found {missingGroups.Count} processed scenes with missing files on disk. Cleaning up database records for reprocessing...");

                    // Delete the database records in batches to avoid SQLite parameter limits
                    const int BatchSize = 100;
                    for (var index = 0; index < missingGroups.Count; index += BatchSize)
                    {
                        var batch = missingGroups.Skip(index).Take(BatchSize).ToList();

                        using var deleteContext = new DatabaseContext(optionsBuilder.Options);
                        using var transaction = await deleteContext.Database.BeginTransactionAsync();
                        try
                        {
                            foreach (var (MediaId, Scene) in batch)
                            {
                                var recordsToDelete = await deleteContext.MediaThumbnails
                                    .Where(thumbnail => thumbnail.MediaId == MediaId && thumbnail.Scene == Scene)
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
                else
                {
                    LogInfo("All database preview records have corresponding files on disk.");
                }
            }
            catch (Exception exception)
            {
                LogError($"Failed to perform consistency check: {exception.Message}");
            }
        }
        else
        {
            LogInfo("Force regeneration enabled (--force): all scenes will be reprocessed.");
        }

        // Scan missing items
        List<MediaLocation> targetLocations;
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

            // Group by MediaId and Scene to pick a single representative file location per scene
            targetLocations = [.. rawTargetLocations
                .GroupBy(location => new { location.MediaId, location.Scene })
                .Select(group =>
                {
                    var candidates = group.ToList();
                    var existingCandidates = candidates.Where(candidate => File.Exists(candidate.Path)).ToList();

                    if (existingCandidates.Count > 0)
                    {
                        // Prefer files that have "8k" or "4k" in their filename for higher-resolution sources.
                        // Otherwise, fallback to alphabetical sorting.
                        return existingCandidates
                            .OrderByDescending(candidate => Path.GetFileNameWithoutExtension(candidate.Path).Contains("8k", StringComparison.OrdinalIgnoreCase))
                            .ThenByDescending(candidate => Path.GetFileNameWithoutExtension(candidate.Path).Contains("4k", StringComparison.OrdinalIgnoreCase))
                            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                            .First();
                    }

                    // If none of the files physically exist on disk, return the first one so that it will be logged as missing during processing.
                    return candidates.First();
                })];

            LogInfo($"Found {mediaLocations.Count} total media locations. {processedScenes.Count} already have previews. {targetLocations.Count} scenes need processing.");

            if (ignoredMediaIds.Count > 0 || ignoredScenes.Count > 0)
            {
                var beforeIgnoreCount = targetLocations.Count;
                targetLocations =
                [
                    .. targetLocations.Where(location =>
                        !ignoredMediaIds.Contains(location.MediaId) &&
                        !ignoredScenes.Contains((location.MediaId, location.Scene)))
                ];
                var ignoredCount = beforeIgnoreCount - targetLocations.Count;
                LogInfo($"Ignored {ignoredCount} scenes via --ignore / --ignore-file. {targetLocations.Count} scenes remaining.");
            }
        }
        catch (Exception exception)
        {
            LogError($"Failed to query database: {exception.Message}");
            return;
        }

        if (targetLocations.Count == 0)
        {
            LogInfo("All media locations are already processed. Exiting.");
            return;
        }

        // Interleave targets by drive root to maximize parallelism across physical drives
        var driveGroups = targetLocations
            .GroupBy(location => Path.GetPathRoot(location.Path) ?? "", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.ToList())
            .ToList();

        var interleavedTargetLocations = new List<MediaLocation>();
        if (driveGroups.Count > 0)
        {
            var maxGroupSize = driveGroups.Max(group => group.Count);
            for (var index = 0; index < maxGroupSize; index++)
            {
                foreach (var group in driveGroups)
                {
                    if (index < group.Count)
                    {
                        interleavedTargetLocations.Add(group[index]);
                    }
                }
            }
            targetLocations = interleavedTargetLocations;
        }

        // Max global parallelism is equal to CPU core count.
        var globalLimit = Environment.ProcessorCount;
        var pipelineLimit = Math.Max(100, globalLimit * 4);
        LogInfo($"Starting thumbnail generation (max global running tasks: {globalLimit}, pipeline tasks limit: {pipelineLimit})...");

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = pipelineLimit
        };

        var successCount = 0;
        var failCount = 0;

        var stopwatch = Stopwatch.StartNew();

        await Parallel.ForEachAsync(targetLocations, parallelOptions, async (location, cancellationToken) =>
        {
            var logPrefix = $"[{location.MediaId} - Scene {location.Scene}]";

            // Verify file exists
            if (!File.Exists(location.Path))
            {
                LogError($"{logPrefix} File does not exist at local path: {location.Path}");
                Interlocked.Increment(ref failCount);
                return;
            }

            // Resolve disk-based throttling semaphore
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

                    // Query VideoInfo (dimensions & duration) using ffprobe
                    var videoInfo = await GetVideoInfoAsync(location.Path);
                    if (videoInfo == null || videoInfo.Duration <= 0)
                    {
                        LogError($"{logPrefix} Could not retrieve video metadata. skipping.");
                        Interlocked.Increment(ref failCount);
                        return;
                    }

                    LogInfo($"{logPrefix} Video duration: {videoInfo.Duration:F2}s, resolution: {videoInfo.Width}x{videoInfo.Height}.");

                    // Determine if VR format based on file naming
                    var isVr = location.Path.Contains("VR", StringComparison.OrdinalIgnoreCase) ||
                                location.MediaId.Contains("VR", StringComparison.OrdinalIgnoreCase);

                    // Select crop filter depending on VR properties
                    string videoFilter;
                    if (isVr)
                    {
                        if (videoInfo.Width > videoInfo.Height)
                        {
                            // Side-by-Side (left-right): crop left eye first, then scale to 720p height
                            videoFilter = "crop=iw/2:ih:0:0,scale=-2:720:flags=fast_bilinear";
                            LogInfo($"{logPrefix} VR detected: Side-by-Side. Applying left-eye crop + 720p scale.");
                        }
                        else
                        {
                            // Over-Under (top-bottom): crop top eye first, then scale to 720p height
                            videoFilter = "crop=iw:ih/2:0:0,scale=-2:720:flags=fast_bilinear";
                            LogInfo($"{logPrefix} VR detected: Over-Under. Applying top-eye crop + 720p scale.");
                        }
                    }
                    else
                    {
                        // Standard Video: zoom-to-fill 1280x720 (720p) landscape
                        videoFilter = "scale=1280:720:force_original_aspect_ratio=increase:flags=fast_bilinear,crop=1280:720";
                    }

                    // Create thumbs directory
                    var thumbsSubdirectory = Path.Combine(settings.ImagePath, "thumbs", location.MediaId, $"scene_{location.Scene}");
                    Directory.CreateDirectory(thumbsSubdirectory);

                    var tempPrefix = $"thumb_tmp_{Guid.NewGuid():N}_";
                    var tempOutputPattern = Path.Combine(thumbsSubdirectory, $"{tempPrefix}%d.jpg");

                    LogInfo($"{logPrefix} Extracting thumbnails in single pass (every {ThumbnailIntervalSeconds}s)...");
                    var extractedSuccess = await ExtractThumbnailsSinglePassAsync(location.Path, videoFilter, tempOutputPattern);

                    var tempFiles = Directory.GetFiles(thumbsSubdirectory, $"{tempPrefix}*.jpg")
                        .Select(filePath => new
                        {
                            FilePath = filePath,
                            Index = int.TryParse(
                                Path.GetFileNameWithoutExtension(filePath).AsSpan(tempPrefix.Length),
                                out var parsedIndex)
                                ? parsedIndex
                                : -1
                        })
                        .Where(item => item.Index >= 0)
                        .OrderBy(item => item.Index)
                        .ToList();

                    if (!extractedSuccess || tempFiles.Count == 0)
                    {
                        foreach (var tempFile in tempFiles)
                        {
                            if (File.Exists(tempFile.FilePath))
                            {
                                File.Delete(tempFile.FilePath);
                            }
                        }
                        LogError($"{logPrefix} Failed to extract thumbnails in single pass.");
                        Interlocked.Increment(ref failCount);
                        return;
                    }

                    var plannedMoves = new List<(string TempFilePath, string OutputPath)>(tempFiles.Count);
                    var thumbnailsToInsert = new List<MediaThumbnail>(tempFiles.Count);

                    foreach (var tempFile in tempFiles)
                    {
                        var timestampValue = tempFile.Index * ThumbnailIntervalSeconds;
                        var fileName = $"thumb_{timestampValue}.jpg";
                        var outputPath = Path.Combine(thumbsSubdirectory, fileName);
                        var relativePath = Path.Combine("thumbs", location.MediaId, $"scene_{location.Scene}", fileName).Replace('\\', '/');

                        plannedMoves.Add((tempFile.FilePath, outputPath));
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

                    var saved = await SaveSceneThumbnailsAsync(
                        optionsBuilder.Options,
                        location.MediaId,
                        location.Scene,
                        thumbsSubdirectory,
                        plannedMoves,
                        thumbnailsToInsert,
                        forceRegenerate,
                        logPrefix,
                        cancellationToken);

                    if (!saved)
                    {
                        Interlocked.Increment(ref failCount);
                        return;
                    }

                    Interlocked.Increment(ref successCount);
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

        stopwatch.Stop();
        LogInfo($"Generation completed in {stopwatch.Elapsed.TotalMinutes:F2} minutes. Successfully processed: {successCount}, Failed: {failCount}.");
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
            Arguments = $"-v error -select_streams v:0 -show_entries format=duration:stream=width,height -of json \"{videoPath}\"",
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
            if (streams.GetArrayLength() > 0)
            {
                var firstStream = streams[0];
                width = firstStream.GetProperty("width").GetInt32();
                height = firstStream.GetProperty("height").GetInt32();
            }

            var format = doc.RootElement.GetProperty("format");
            double duration = 0;
            if (format.TryGetProperty("duration", out var durationProp))
            {
                duration = double.Parse(durationProp.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            }

            return new VideoInfo { Width = width, Height = height, Duration = duration };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> ExtractThumbnailsSinglePassAsync(string videoPath, string filter, string outputPattern)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-skip_frame nokey -an -sn -dn -threads 1 -i \"{videoPath}\" -vf \"fps=1/{ThumbnailIntervalSeconds},{filter}\" -q:v 4 -start_number 0 \"{outputPattern}\" -y",
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
                            workerContext.ChangeTracker.Clear();
                        }
                    }

                    foreach (var thumbnail in thumbnailsToInsert)
                    {
                        workerContext.MediaThumbnails.Add(thumbnail);
                    }

                    await workerContext.SaveChangesAsync(cancellationToken);

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

    private static bool TryParseIgnoreRules(
        string[] args,
        out HashSet<string> ignoredMediaIds,
        out HashSet<(string MediaId, int Scene)> ignoredScenes,
        out string errorMessage)
    {
        ignoredMediaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ignoredScenes = new HashSet<(string MediaId, int Scene)>(new MediaSceneEqualityComparer());
        errorMessage = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--ignore", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    errorMessage = "Missing value after '--ignore'.";
                    return false;
                }

                AddIgnoreTokens(args[++index], ignoredMediaIds, ignoredScenes);
            }
            else if (argument.StartsWith("--ignore=", StringComparison.OrdinalIgnoreCase))
            {
                AddIgnoreTokens(argument["--ignore=".Length..], ignoredMediaIds, ignoredScenes);
            }
            else if (string.Equals(argument, "--ignore-file", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    errorMessage = "Missing file path after '--ignore-file'.";
                    return false;
                }

                if (!TryLoadIgnoreFile(args[++index], ignoredMediaIds, ignoredScenes, out errorMessage))
                {
                    return false;
                }
            }
            else if (argument.StartsWith("--ignore-file=", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryLoadIgnoreFile(argument["--ignore-file=".Length..], ignoredMediaIds, ignoredScenes, out errorMessage))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryLoadIgnoreFile(
        string filePath,
        HashSet<string> ignoredMediaIds,
        HashSet<(string MediaId, int Scene)> ignoredScenes,
        out string errorMessage)
    {
        if (!File.Exists(filePath))
        {
            errorMessage = $"Ignore file not found: {filePath}";
            return false;
        }

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var commentIndex = rawLine.IndexOf('#');
            var cleanedLine = (commentIndex >= 0 ? rawLine[..commentIndex] : rawLine).Trim();
            if (!string.IsNullOrEmpty(cleanedLine))
            {
                AddIgnoreTokens(cleanedLine, ignoredMediaIds, ignoredScenes);
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static void AddIgnoreTokens(
        string rawValue,
        HashSet<string> ignoredMediaIds,
        HashSet<(string MediaId, int Scene)> ignoredScenes)
    {
        foreach (var rawToken in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIndex = rawToken.LastIndexOf(':');
            if (colonIndex > 0 && int.TryParse(rawToken[(colonIndex + 1)..], out var sceneNumber))
            {
                var mediaId = rawToken[..colonIndex].Trim();
                if (!string.IsNullOrEmpty(mediaId))
                {
                    ignoredScenes.Add((mediaId, sceneNumber));
                }
            }
            else
            {
                ignoredMediaIds.Add(rawToken);
            }
        }
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
