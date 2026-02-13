using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Japlayer.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Japlayer.Data.Services
{
    public class DatabaseMediaProvider : IMediaProvider
    {
        private readonly DatabaseContext _context;

        public DatabaseMediaProvider(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync()
        {
            // Get all unique media IDs from MediaLocations
            var mediaIds = await _context.MediaLocations
                .AsNoTracking()
                .Select(ml => ml.MediaId)
                .Distinct()
                .ToListAsync();

            // Fetch metadata for these IDs
            var metadataList = await _context.MediaMetadata
                .AsNoTracking()
                .Where(m => mediaIds.Contains(m.MediaId))
                .ToListAsync();

            var metadataMap = metadataList
                .GroupBy(m => m.MediaId)
                .ToDictionary(g => g.Key, g => g.First());

            // Construct LibraryItems
            var libraryItems = mediaIds.Select(mediaId =>
            {
                var hasMetadata = metadataMap.TryGetValue(mediaId, out var metadata);
                return new LibraryItem
                {
                    MediaId = mediaId,
                    Title = hasMetadata ? metadata!.Title : null,
                    CoverImagePath = hasMetadata ? metadata!.Cover : null
                };
            });

            return libraryItems;
        }

        public async Task<MediaItem> GetMediaItemAsync(string mediaId)
        {
            // Fetch Metadata
            var metadata = await _context.MediaMetadata
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MediaId == mediaId);

            // Fetch Relationships via Media entity because relationships are stored there
            var media = await _context.Media
                .AsNoTracking()
                .Include(m => m.Series)
                .Include(m => m.Studios)
                .Include(m => m.People) // Cast
                .Include(m => m.PeopleNavigation) // Staff
                .Include(m => m.Genres)
                .FirstOrDefaultAsync(m => m.MediaId == mediaId);

            return new MediaItem
            {
                MediaId = mediaId,
                Title = metadata?.Title,
                ContentId = metadata?.ContentId,
                ReleaseDate = metadata?.ReleaseDate,
                Runtime = metadata?.RuntimeMinutes.HasValue == true ? TimeSpan.FromMinutes(metadata.RuntimeMinutes.Value) : null,
                Series = media?.Series.Select(s => s.Name).ToList() ?? new List<string>(),
                Studios = media?.Studios.Select(s => s.Name).ToList() ?? new List<string>(),
                Cast = media?.People.Select(p => p.Name).ToList() ?? new List<string>(),
                Staff = media?.PeopleNavigation.Select(p => p.Name).ToList() ?? new List<string>(),
                Genres = media?.Genres.Select(g => g.Name).ToList() ?? new List<string>()
            };
        }
    }
}
