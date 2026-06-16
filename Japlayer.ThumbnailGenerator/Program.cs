using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Japlayer.Data.Context;
using Japlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32.SafeHandles;

namespace Japlayer.ThumbnailGenerator
{
    internal class Program
    {
        private static string _logFilePath = "thumbnail_generation.log";
        private static readonly object _logLock = new();

        // Concurrency controls
        private static readonly SemaphoreSlim _globalSemaphore = new(Environment.ProcessorCount);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _driveSemaphores = new(StringComparer.OrdinalIgnoreCase);

        private static async Task Main(string[] args)
        {
            // Find settings file by searching upwards
            var settingsPath = FindSettingsFile();
            if (string.IsNullOrEmpty(settingsPath))
            {
                LogError("Could not find 'japlayer.settings.json' in any parent directory.");
                return;
            }

            _logFilePath = Path.Combine(Path.GetDirectoryName(settingsPath)!, "thumbnail_generation.log");
            LogInfo($"Using settings file: {settingsPath}");
            LogInfo($"Log file: {_logFilePath}");

            // Parse settings
            AppSettings? settings;
            try
            {
                var json = File.ReadAllText(settingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                LogError($"Failed to read settings file: {ex.Message}");
                return;
            }

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
            catch (Exception ex)
            {
                LogError($"Failed to initialize database table: {ex.Message}");
                return;
            }

            // Scan missing items
            List<MediaLocation> targets;
            try
            {
                LogInfo("Scanning database for media files...");
                var locations = await context.MediaLocations.AsNoTracking().ToListAsync();
                var existingThumbs = await context.MediaThumbnails
                    .AsNoTracking()
                    .Select(t => new { t.MediaId, t.Scene })
                    .Distinct()
                    .ToListAsync();

                var processed = existingThumbs.Select(t => (t.MediaId, t.Scene)).ToHashSet();
                targets = [.. locations.Where(l => !processed.Contains((l.MediaId, l.Scene)))];

                LogInfo($"Found {locations.Count} total media locations. {processed.Count} already have previews. {targets.Count} need processing.");
            }
            catch (Exception ex)
            {
                LogError($"Failed to query database: {ex.Message}");
                return;
            }

            if (targets.Count == 0)
            {
                LogInfo("All media locations are already processed. Exiting.");
                return;
            }

            // Interleave targets by drive root to maximize parallelism across physical drives
            var driveGroups = targets
                .GroupBy(t => Path.GetPathRoot(t.Path) ?? "", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.ToList())
                .ToList();

            var interleavedTargets = new List<MediaLocation>();
            if (driveGroups.Count > 0)
            {
                var maxGroupSize = driveGroups.Max(g => g.Count);
                for (var i = 0; i < maxGroupSize; i++)
                {
                    foreach (var group in driveGroups)
                    {
                        if (i < group.Count)
                        {
                            interleavedTargets.Add(group[i]);
                        }
                    }
                }
                targets = interleavedTargets;
            }

            // Max global parallelism is equal to CPU core count.
            // We set MaxDegreeOfParallelism in Parallel.ForEachAsync to a larger value (ProcessorCount * 4)
            // to allow pipeline tasks to wait on drive-level semaphores without blocking the threadpool,
            // while the _globalSemaphore limits active tasks to Environment.ProcessorCount.
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

            await Parallel.ForEachAsync(targets, parallelOptions, async (location, cancellationToken) =>
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
                        for (var t = 0; t < videoInfo.Duration; t += 120)
                        {
                            timestamps.Add(t);
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

                        foreach (var ts in timestamps)
                        {
                            var fileName = $"thumb_{ts}.jpg";
                            var outputPath = Path.Combine(thumbsSubdir, fileName);
                            var relativePath = Path.Combine("thumbs", location.MediaId, $"scene_{location.Scene}", fileName).Replace('\\', '/');

                            var extracted = await ExtractFrameAsync(location.Path, ts, vfFilter, outputPath);
                            if (!extracted)
                            {
                                LogError($"{logPrefix} Failed to extract frame at timestamp {ts}s.");
                                allFramesExtracted = false;
                                break;
                            }

                            filesCreated.Add(outputPath);
                            thumbsToInsert.Add(new MediaThumbnail
                            {
                                MediaId = location.MediaId,
                                Scene = location.Scene,
                                Timestamp = ts,
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

        private static string FindSettingsFile()
        {
            var current = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                var path = Path.Combine(current, "japlayer.settings.json");
                if (File.Exists(path))
                {
                    return path;
                }
                current = Path.GetDirectoryName(current);
            }
            return "";
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
            // Fast seek input parameter: -ss <seconds> before -i <input>
            // Apply scale & crop filters in standard 16:9 854x480 resolution
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

        private static void LogInfo(string message) => Log($"[INFO] {message}");
        private static void LogError(string message) => Log($"[ERROR] {message}");

        private static void Log(string message)
        {
            var formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Console.WriteLine(formatted);
            lock (_logLock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, formatted + Environment.NewLine);
                }
                catch { }
            }
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

                // Format volume path as \\.\C:
                var volumeName = @"\\.\" + root.TrimEnd('\\');

                using var handle = CreateFile(
                    volumeName,
                    0, // No access rights needed for query property
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
                    PropertyId = 7, // StorageDeviceSeekPenaltyProperty
                    QueryType = 0, // PropertyStandardQuery
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
                    return !descriptor.IncursSeekPenalty; // No seek penalty means SSD
                }
            }
            catch
            {
                // Fallback to HDD if detection fails
            }
            return false;
        }
    }

    public class AppSettings
    {
        public string? ImagePath { get; set; }
        public string? SqliteDatabasePath { get; set; }
    }
}
