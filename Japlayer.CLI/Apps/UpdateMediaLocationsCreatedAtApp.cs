using Japlayer.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.CLI.Apps;

public class UpdateMediaLocationsCreatedAtApp : BaseApp
{
    public override string Name => "update-media-locations";
    public override string Description => "Updates the CreatedAt timestamp for all media locations based on file creation times on disk.";

    public override async Task RunAsync(string[] args)
    {
        var settingsPath = FindSettingsFile();
        if (string.IsNullOrEmpty(settingsPath))
        {
            LogError("Could not find 'japlayer.settings.json' in any parent directory.");
            return;
        }

        LogFilePath = Path.Combine(Path.GetDirectoryName(settingsPath)!, "update_media_locations.log");
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

        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
        optionsBuilder.UseSqlite($"Data Source={settings.SqliteDatabasePath}");

        using var context = new DatabaseContext(optionsBuilder.Options);

        // Ensure table column exists
        try
        {
            LogInfo("Verifying database schema for MediaLocations.createdAt column...");
            var columnExists = false;
            var connection = context.Database.GetDbConnection();
            var connectionWasOpen = connection.State == System.Data.ConnectionState.Open;

            if (!connectionWasOpen)
            {
                await context.Database.OpenConnectionAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(MediaLocations);";
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var nameCol = reader["name"]?.ToString();
                    if (string.Equals(nameCol, "createdAt", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }
            finally
            {
                if (!connectionWasOpen)
                {
                    await context.Database.CloseConnectionAsync();
                }
            }

            if (!columnExists)
            {
                LogWarning("Column 'createdAt' not found in table 'MediaLocations'. Migrating table schema...");
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE MediaLocations ADD COLUMN createdAt TEXT;");
                LogInfo("Successfully added 'createdAt' column to MediaLocations.");
            }
            else
            {
                LogInfo("Column 'createdAt' already exists in MediaLocations table.");
            }

            // Prevent EF Core null-reading exceptions on non-nullable CreatedAt properties
            var rowsInitialized = await context.Database.ExecuteSqlRawAsync("UPDATE MediaLocations SET createdAt = datetime('now') WHERE createdAt IS NULL;");
            if (rowsInitialized > 0)
            {
                LogInfo($"Initialized {rowsInitialized} row(s) with NULL 'createdAt' to default timestamp.");
            }
        }
        catch (Exception exception)
        {
            LogError($"Failed to verify/migrate database schema: {exception.Message}");
            return;
        }

        // Process media locations
        try
        {
            LogInfo("Retrieving media locations from database...");
            var locations = await context.MediaLocations.ToListAsync();
            LogInfo($"Found {locations.Count} locations in database. Starting disk checks and updates...");

            var updatedCount = 0;
            var fileExistsCount = 0;
            var fileNotFoundCount = 0;

            for (var index = 0; index < locations.Count; index++)
            {
                var location = locations[index];
                DateTime creationTime;
                var exists = File.Exists(location.Path);

                if (exists)
                {
                    creationTime = File.GetCreationTimeUtc(location.Path);
                    fileExistsCount++;
                    LogFileOnly($"File exists: {location.Path} -> Created: {creationTime:yyyy-MM-dd HH:mm:ss} UTC");
                }
                else
                {
                    creationTime = DateTime.UtcNow;
                    fileNotFoundCount++;
                    LogFileOnly($"File NOT found: {location.Path} -> Defaulting to: {creationTime:yyyy-MM-dd HH:mm:ss} UTC");
                }

                location.CreatedAt = creationTime;
                updatedCount++;

                // Log progress periodically to console to keep user updated without spamming
                if ((index + 1) % 100 == 0 || (index + 1) == locations.Count)
                {
                    LogInfo($"Progress: Checked {index + 1}/{locations.Count} locations (Exists: {fileExistsCount}, Missing: {fileNotFoundCount}).");
                }
            }

            if (updatedCount > 0)
            {
                LogInfo("Saving changes to database...");
                await context.SaveChangesAsync();
                LogInfo($"Successfully updated {updatedCount} location(s) in the database.");
            }
            else
            {
                LogInfo("No locations found to update.");
            }
        }
        catch (Exception exception)
        {
            LogError($"Failed during media locations update: {exception.Message}");
        }
    }

    private void LogFileOnly(string message)
    {
        var formatted = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [DEBUG] {message}";
        try
        {
            File.AppendAllText(LogFilePath, formatted + Environment.NewLine);
        }
        catch
        {
            // Ignore logging errors to prevent application crashes
        }
    }
}
