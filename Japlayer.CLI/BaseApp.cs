using System.Text.Json;

namespace Japlayer.CLI;

public abstract class BaseApp : IApp
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Task RunAsync(string[] args);

    protected string LogFilePath { get; set; } = "app.log";
    private readonly object _logLock = new();

    protected string FindSettingsFile()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            // Support local file override first, then fallback to standard
            var localPath = Path.Combine(current, "japlayer.settings.json.local");
            if (File.Exists(localPath))
            {
                return localPath;
            }

            var path = Path.Combine(current, "japlayer.settings.json");
            if (File.Exists(path))
            {
                return path;
            }

            current = Path.GetDirectoryName(current);
        }
        return string.Empty;
    }

    protected AppSettings? LoadSettings(string settingsPath)
    {
        try
        {
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception exception)
        {
            LogError($"Failed to read settings file: {exception.Message}");
            return null;
        }
    }

    protected void LogInfo(string message) => Log($"[INFO] {message}");
    protected void LogWarning(string message) => Log($"[WARN] {message}");
    protected void LogError(string message) => Log($"[ERROR] {message}");

    private void Log(string message)
    {
        var formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        Console.WriteLine(formatted);
        lock (_logLock)
        {
            try
            {
                File.AppendAllText(LogFilePath, formatted + Environment.NewLine);
            }
            catch
            {
                // Silently swallow logging write errors to prevent crashes if file is locked
            }
        }
    }
}

public class AppSettings
{
    public string? ImagePath { get; set; }
    public string? SqliteDatabasePath { get; set; }
}
