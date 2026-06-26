using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Japlayer.Data.Context;
using Japlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32.SafeHandles;

namespace Japlayer.CLI.Apps;

public class GenerateThumbnailsApp : BaseApp
{
    public override string Name => "generate-thumbnails";
    public override string Description => "Scans database and physical drives to extract media thumbnail previews.";

    private readonly SemaphoreSlim _globalSemaphore = new(Environment.ProcessorCount);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _driveSemaphores = new(StringComparer.OrdinalIgnoreCase);

    public override async Task RunAsync(string[] args)
    {
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
        optionsBuilder.UseSqlite($"Data Source={settings.SqliteDatabasePath}");

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

        // Self-healing: Check if any committed thumbnails are missing their files on disk
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
            var rawTargetLocations = mediaLocations.Where(location => !processedScenes.Contains((location.MediaId, location.Scene))).ToList();

            // Group by MediaId and Scene to pick a single representative file location per scene
            targetLocations = [.. rawTargetLocations
                .GroupBy(location => new { location.MediaId, location.Scene })
                .Select(group =>
                {
                    var candidates = group.ToList();
                    var existingCandidates = candidates.Where(candidate => File.Exists(candidate.Path)).ToList();

                    if (existingCandidates.Count > 0)
                    {
                        // Prefer files that have "8k", "4k", or "UC" in their filename to capture higher quality or uncensored versions.
                        // Otherwise, fallback to alphabetical sorting.
                        return existingCandidates
                            .OrderByDescending(candidate => Path.GetFileNameWithoutExtension(candidate.Path).Contains("8k", StringComparison.OrdinalIgnoreCase))
                            .ThenByDescending(candidate => Path.GetFileNameWithoutExtension(candidate.Path).Contains("4k", StringComparison.OrdinalIgnoreCase))
                            .ThenByDescending(candidate => Path.GetFileNameWithoutExtension(candidate.Path).Contains("UC", StringComparison.OrdinalIgnoreCase))
                            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                            .First();
                    }

                    // If none of the files physically exist on disk, return the first one so that it will be logged as missing during processing.
                    return candidates.First();
                })];

            LogInfo($"Found {mediaLocations.Count} total media locations. {processedScenes.Count} already have previews. {targetLocations.Count} scenes need processing.");
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
                    string vfFilter;
                    if (isVr)
                    {
                        if (videoInfo.Width > videoInfo.Height)
                        {
                            // Side-by-Side (left-right): crop left eye first, then scale to 720p height
                            vfFilter = "crop=iw/2:ih:0:0,scale=-2:720";
                            LogInfo($"{logPrefix} VR detected: Side-by-Side. Applying left-eye crop + 720p scale.");
                        }
                        else
                        {
                            // Over-Under (top-bottom): crop top eye first, then scale to 720p height
                            vfFilter = "crop=iw:ih/2:0:0,scale=-2:720";
                            LogInfo($"{logPrefix} VR detected: Over-Under. Applying top-eye crop + 720p scale.");
                        }
                    }
                    else
                    {
                        // Standard Video: zoom-to-fill 1280x720 (720p) landscape
                        vfFilter = "scale=1280:720:force_original_aspect_ratio=increase,crop=1280:720";
                    }

                    // Determine intervals (every 2 minutes / 120 seconds)
                    var timestamps = new List<int>();
                    for (var timestampValue = 0; timestampValue < videoInfo.Duration; timestampValue += 120)
                    {
                        timestamps.Add(timestampValue);
                    }

                    if (timestamps.Count == 0)
                    {
                        timestamps.Add(0); // at least extract one frame
                    }

                    LogInfo($"{logPrefix} Extracting {timestamps.Count} thumbnails...");

                    // Create thumbs directory
                    var thumbsSubdir = Path.Combine(settings.ImagePath, "thumbs", location.MediaId, $"scene_{location.Scene}");
                    Directory.CreateDirectory(thumbsSubdir);

                    var thumbsToInsert = new List<MediaThumbnail>();
                    var filesCreated = new List<string>();

                    var allFramesExtracted = true;

                    foreach (var timestampValue in timestamps)
                    {
                        var fileName = $"thumb_{timestampValue}.jpg";
                        var outputPath = Path.Combine(thumbsSubdir, fileName);
                        var relativePath = Path.Combine("thumbs", location.MediaId, $"scene_{location.Scene}", fileName).Replace('\\', '/');

                        var extracted = await ExtractFrameAsync(location.Path, timestampValue, vfFilter, outputPath);
                        if (!extracted)
                        {
                            LogError($"{logPrefix} Failed to extract frame at timestamp {timestampValue}s.");
                            allFramesExtracted = false;
                            break;
                        }

                        filesCreated.Add(outputPath);
                        thumbsToInsert.Add(new MediaThumbnail
                        {
                            MediaId = location.MediaId,
                            Scene = location.Scene,
                            Timestamp = timestampValue,
                            Path = relativePath,
                            OriginalHostname = location.Hostname,
                            OriginalPath = location.Path
                        });
                    }

                    if (!allFramesExtracted)
                    {
                        // Cleanup generated files
                        foreach (var path in filesCreated)
                        {
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }
                        }
                        Interlocked.Increment(ref failCount);
                        return;
                    }

                    // Save to Database inside atomic transaction
                    using (var workerContext = new DatabaseContext(optionsBuilder.Options))
                    {
                        using var transaction = await workerContext.Database.BeginTransactionAsync(cancellationToken);
                        try
                        {
                            foreach (var thumb in thumbsToInsert)
                            {
                                workerContext.MediaThumbnails.Add(thumb);
                            }
                            await workerContext.SaveChangesAsync(cancellationToken);
                            await transaction.CommitAsync(cancellationToken);
                            LogInfo($"{logPrefix} Successfully saved all metadata to database and committed transaction.");
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            // Cleanup generated files
                            foreach (var path in filesCreated)
                            {
                                if (File.Exists(path))
                                {
                                    File.Delete(path);
                                }
                            }
                            LogError($"{logPrefix} Failed to save metadata to database (rolled back): {ex.Message}");
                            Interlocked.Increment(ref failCount);
                            return;
                        }
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

    private static async Task<bool> ExtractFrameAsync(string videoPath, int seconds, string filter, string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-ss {seconds} -i \"{videoPath}\" -vframes 1 -q:v 4 -vf \"{filter}\" \"{outputPath}\" -y",
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
