using Japlayer.Data.Context;
using Japlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;

// Validate command line arguments
if (args.Length == 0)
{
    Console.WriteLine("Error: Database path is required.");
    Console.WriteLine("\nUsage: dotnet run --project Japlayer.Console -- <database-path>");
    Console.WriteLine("\nExample:");
    Console.WriteLine(@"  dotnet run --project Japlayer.Console -- ""path/to/medias.db""");
    return 1;
}

var dbPath = args[0];

// Check if file exists
if (!File.Exists(dbPath))
{
    Console.WriteLine($"Error: Database file not found at: {dbPath}");
    return 1;
}

Console.WriteLine($"Using database: {dbPath}\n");

// Create DbContext with connection string
var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
optionsBuilder.UseSqlite($"Data Source={dbPath}");

using var context = new DatabaseContext(optionsBuilder.Options);

try
{
    Console.WriteLine("Testing database connection...\n");

    // Test 1: Can we connect?
    var canConnect = await context.Database.CanConnectAsync();
    Console.WriteLine($"✓ Connection test: {(canConnect ? "SUCCESS" : "FAILED")}\n");

    // Test 2: Count records in each table
    Console.WriteLine("Record counts:");
    Console.WriteLine($"  Media: {await context.Media.CountAsync()}");
    Console.WriteLine($"  MediaMetadata: {await context.MediaMetadata.CountAsync()}");
    Console.WriteLine($"  MediaLocations: {await context.MediaLocations.CountAsync()}");
    Console.WriteLine($"  MediaImages: {await context.MediaImages.CountAsync()}");
    Console.WriteLine($"  MediaGenres: {await context.MediaGenres.CountAsync()}");
    Console.WriteLine($"  MediaPeople: {await context.MediaPeople.CountAsync()}");
    Console.WriteLine($"  MediaSeries: {await context.MediaSeries.CountAsync()}");
    Console.WriteLine($"  MediaStudios: {await context.MediaStudios.CountAsync()}");
    Console.WriteLine($"  UserTags: {await context.UserTags.CountAsync()}");
    Console.WriteLine();

    // Test 3: Fetch first media item with related data
    var firstMedia = await context.Media
        .Include(m => m.MediaMetadata)
        .Include(m => m.MediaImages)
        .Include(m => m.Genres)
        .Include(m => m.People)
        .Include(m => m.Studios)
        .Include(m => m.Series)
        .FirstOrDefaultAsync();

    if (firstMedia != null)
    {
        Console.WriteLine("Sample Media Item:");
        Console.WriteLine($"  MediaId: {firstMedia.MediaId}");
        Console.WriteLine($"  Metadata entries: {firstMedia.MediaMetadata.Count}");

        if (firstMedia.MediaMetadata.Any())
        {
            var metadata = firstMedia.MediaMetadata.First();
            Console.WriteLine($"    Title: {metadata.Title}");
            Console.WriteLine($"    Release Date: {metadata.ReleaseDate}");
            Console.WriteLine($"    Runtime: {metadata.RuntimeMinutes} minutes");
        }

        Console.WriteLine($"  Images: {firstMedia.MediaImages.Count}");
        Console.WriteLine($"  Genres: {string.Join(", ", firstMedia.Genres.Select(g => g.Name))}");
        Console.WriteLine($"  People: {string.Join(", ", firstMedia.People.Select(p => p.Name))}");
        Console.WriteLine($"  Studios: {string.Join(", ", firstMedia.Studios.Select(s => s.Name))}");
        Console.WriteLine($"  Series: {string.Join(", ", firstMedia.Series.Select(s => s.Name))}");
    }
    Console.WriteLine();

    // Test 4: Query by metadata
    var mediaWithMetadata = await context.MediaMetadata
        .Include(m => m.Media)
        .Include(m => m.CoverNavigation)
        .Include(m => m.ThumbnailNavigation)
        .Take(5)
        .ToListAsync();

    Console.WriteLine($"Retrieved {mediaWithMetadata.Count} metadata entries:");
    foreach (var metadata in mediaWithMetadata)
    {
        Console.WriteLine($"  - {metadata.Title} ({metadata.ReleaseDate?.Year})");
    }
    Console.WriteLine();

    // Test 5: Get locations
    var locations = await context.MediaLocations
        .Take(5)
        .ToListAsync();

    Console.WriteLine($"Retrieved {locations.Count} media locations:");
    foreach (var location in locations)
    {
        Console.WriteLine($"  - {location.MediaId} @ {location.Hostname}:{location.Path} (Scene {location.Scene})");
    }

    Console.WriteLine("\n✓ All tests completed successfully!");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}
