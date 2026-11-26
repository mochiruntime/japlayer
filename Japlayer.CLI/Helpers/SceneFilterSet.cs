namespace Japlayer.CLI.Helpers;

/// <summary>
/// Shared CLI flag parsing and <c>mediaId</c> / <c>mediaId:scene</c> filter matching.
/// </summary>
public sealed class SceneFilterSet
{
    private readonly HashSet<string> _mediaIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string MediaId, int Scene)> _scenes = new(new MediaSceneEqualityComparer());

    public bool HasRules => _mediaIds.Count > 0 || _scenes.Count > 0;

    public bool Matches(string mediaId, int scene) =>
        _mediaIds.Contains(mediaId) || _scenes.Contains((mediaId, scene));

    public bool MatchesMedia(string mediaId) =>
        _mediaIds.Contains(mediaId);

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

    private sealed class MediaSceneEqualityComparer : IEqualityComparer<(string MediaId, int Scene)>
    {
        public bool Equals((string MediaId, int Scene) first, (string MediaId, int Scene) second) =>
            string.Equals(first.MediaId, second.MediaId, StringComparison.OrdinalIgnoreCase) &&
            first.Scene == second.Scene;

        public int GetHashCode((string MediaId, int Scene) item) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(item.MediaId), item.Scene);
    }
}

/// <summary>
/// Helpers for reading <c>--flag value</c> and <c>--flag=value</c> CLI options.
/// </summary>
public static class CliArgumentHelper
{
    public static bool TryReadFlagValue(
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
}
