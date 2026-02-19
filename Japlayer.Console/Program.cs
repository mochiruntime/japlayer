using Japlayer.Data.Context;
using Japlayer.Data.Services;
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

    Console.WriteLine("\n--------------------------------------------------");
    Console.WriteLine("Testing Services...");
    Console.WriteLine("--------------------------------------------------\n");

    // Test DatabaseMediaProviderService
    var mediaProvider = new DatabaseMediaProvider(context);

    Console.WriteLine("1. Testing GetLibraryItemsAsync...");
    var libraryItems = (await mediaProvider.GetLibraryItemsAsync()).ToList();
    Console.WriteLine($"   Retrieved {libraryItems.Count} library items.");

    var firstItem = libraryItems.FirstOrDefault(item => !string.IsNullOrEmpty(item.Title));

    if (firstItem != null)
    {
        Console.WriteLine($"   First Item with Metadata: [{firstItem.MediaId}] {firstItem.Title} (Cover: {firstItem.CoverImagePath})");

        Console.WriteLine($"\n2. Testing GetMediaItemAsync for ID: {firstItem.MediaId}...");
        var mediaItem = await mediaProvider.GetMediaItemAsync(firstItem.MediaId);
        Console.WriteLine($"   Title: {mediaItem.Title}");
        Console.WriteLine($"   Release Date: {mediaItem.ReleaseDate}");
        Console.WriteLine($"   Runtime: {mediaItem.Runtime}");
        Console.WriteLine($"   Studios: {string.Join(", ", mediaItem.Studios)}");
        Console.WriteLine($"   Cast: {string.Join(", ", mediaItem.Cast.Take(3))}{(mediaItem.Cast.Count > 3 ? "..." : "")}");

        // Test DatabaseMediaSceneProviderService
        var sceneProvider = new DatabaseMediaSceneProvider(context);
        Console.WriteLine($"\n3. Testing GetMediaScenesAsync for ID: {firstItem.MediaId}...");
        var scenes = (await sceneProvider.GetMediaScenesAsync(firstItem.MediaId)).ToList();
        Console.WriteLine($"   Retrieved {scenes.Count} scenes.");
        foreach (var scene in scenes)
        {
            Console.WriteLine($"   - Scene {scene.SceneNumber}: {scene.FilePaths.Count} file(s)");
            if (scene.FilePaths.Any())
            {
                Console.WriteLine($"     -> {scene.FilePaths.First()}");
            }
        }

        // Test DatabaseImageProvider
        var imageProvider = new DatabaseImageProvider(context);
        Console.WriteLine($"\n4. Testing GetGalleryPathsAsync for ID: {firstItem.MediaId}...");
        var galleryPaths = (await imageProvider.GetGalleryPathsAsync(firstItem.MediaId)).ToList();
        Console.WriteLine($"   Retrieved {galleryPaths.Count} gallery images.");

        if (galleryPaths.Any())
        {
            Console.WriteLine($"   First 3 paths:");
            foreach (var path in galleryPaths.Take(3))
            {
                Console.WriteLine($"     -> {path}");
            }
        }
    }
    else
    {
        Console.WriteLine("   No library items found to test GetMediaItemAsync or GetMediaScenesAsync.");
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
