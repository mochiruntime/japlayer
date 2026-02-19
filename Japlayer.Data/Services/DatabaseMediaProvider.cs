using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Japlayer.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Services
{
    public class DatabaseMediaProvider(DatabaseContext context) : IMediaProvider
    {
        private readonly DatabaseContext _context = context;

        public async Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync()
        {
            // Use projection to fetch exactly what we need in one query.
            // We start from Media and only include those that have associated locations (files).
            return await _context.Media
                .AsNoTracking()
                .Where(m => m.MediaLocations.Any())
                .Select(m => new LibraryItem
                {
                    MediaId = m.MediaId,
                    // We take the title and cover from the first metadata entry
                    Title = m.MediaMetadata.Select(md => md.Title).FirstOrDefault(),
                    CoverImagePath = m.MediaMetadata.Select(md => md.Cover).FirstOrDefault()
                })
                .OrderBy(m => m.MediaId)
                .ToListAsync();
        }

        public async Task<MediaItem> GetMediaItemAsync(string mediaId)
        {
            // Fetch everything in one single query using navigation properties
            var media = await _context.Media
                .AsNoTracking()
                .Include(m => m.MediaMetadata)
                .Include(m => m.Series)
                .Include(m => m.Studios)
                .Include(m => m.People)
                .Include(m => m.PeopleNavigation)
                .Include(m => m.Genres)
                .FirstOrDefaultAsync(m => m.MediaId == mediaId);

            if (media == null) return null!;

            var metadata = media.MediaMetadata.FirstOrDefault();

            return new MediaItem
            {
                MediaId = mediaId,
                Title = metadata?.Title,
                ContentId = metadata?.ContentId,
                ReleaseDate = metadata?.ReleaseDate,
                Runtime = metadata?.RuntimeMinutes.HasValue == true
                    ? TimeSpan.FromMinutes(metadata.RuntimeMinutes.Value)
                    : null,
                Series = [.. media.Series.Select(s => s.Name)],
                Studios = [.. media.Studios.Select(s => s.Name)],
                Cast = [.. media.People.Select(p => p.Name)],
                Staff = [.. media.PeopleNavigation.Select(p => p.Name)],
                Genres = [.. media.Genres.Select(g => g.Name)]
            };
        }
    }
}
