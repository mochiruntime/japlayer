using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Japlayer.CLI.Helpers;

/// <summary>
/// Metadata extracted from a video file header via <c>ffprobe</c>.
/// </summary>
public sealed class VideoInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public double Duration { get; set; }
    public string? CodecName { get; set; }
}

/// <summary>
/// Shared helpers for probing video container/stream metadata and interleaving work across physical drives.
/// </summary>
public static class MediaProbeHelper
{
    public static async Task<VideoInfo?> GetVideoInfoAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v error -select_streams v:0 -show_entries format=duration:stream=width,height,codec_name,duration -of json \"{videoPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            return null;
        }

        try
        {
            var output = await stdoutTask;
            using var document = JsonDocument.Parse(output);

            var width = 0;
            var height = 0;
            string? codecName = null;
            double streamDuration = 0;

            if (document.RootElement.TryGetProperty("streams", out var streams) && streams.GetArrayLength() > 0)
            {
                var firstStream = streams[0];
                if (firstStream.TryGetProperty("width", out var widthProperty) && widthProperty.TryGetInt32(out var parsedWidth))
                {
                    width = parsedWidth;
                }

                if (firstStream.TryGetProperty("height", out var heightProperty) && heightProperty.TryGetInt32(out var parsedHeight))
                {
                    height = parsedHeight;
                }

                if (firstStream.TryGetProperty("codec_name", out var codecProperty))
                {
                    codecName = codecProperty.GetString();
                }

                if (firstStream.TryGetProperty("duration", out var streamDurationProperty))
                {
                    streamDuration = ParseDuration(streamDurationProperty);
                }
            }

            double formatDuration = 0;
            if (document.RootElement.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var formatDurationProperty))
            {
                formatDuration = ParseDuration(formatDurationProperty);
            }

            var duration = formatDuration > 0 ? formatDuration : streamDuration;
            return new VideoInfo
            {
                Width = width,
                Height = height,
                Duration = duration,
                CodecName = codecName
            };
        }
        catch
        {
            return null;
        }
    }

    public static List<T> InterleaveByDrive<T>(IReadOnlyList<T> items, Func<T, string> getPath)
    {
        var driveGroups = items
            .GroupBy(item => Path.GetPathRoot(getPath(item)) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.ToList())
            .ToList();

        if (driveGroups.Count <= 1)
        {
            return [.. items];
        }

        var interleaved = new List<T>(items.Count);
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

    private static double ParseDuration(JsonElement property)
    {
        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numericValue))
        {
            return double.IsFinite(numericValue) && numericValue > 0 ? numericValue : 0;
        }

        if (property.ValueKind == JsonValueKind.String &&
            double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue) &&
            double.IsFinite(parsedValue) &&
            parsedValue > 0)
        {
            return parsedValue;
        }

        return 0;
    }
}
