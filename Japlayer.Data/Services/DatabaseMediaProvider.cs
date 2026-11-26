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
                .Select(media => new LibraryItem
                {
                    MediaId = media.MediaId,
                    // We take the title and cover from the first metadata entry
                    Title = media.MediaMetadata.Select(metadata => metadata.Title).FirstOrDefault(),
                    ReleaseDate = media.MediaMetadata.Select(metadata => metadata.ReleaseDate).FirstOrDefault(),
                    CoverImagePath = media.MediaMetadata.Select(metadata => metadata.Cover).FirstOrDefault(),
                    RuntimeMinutes = media.MediaMetadata.Select(metadata => metadata.RuntimeMinutes).FirstOrDefault(),
                    UserTags = media.UserTags.Select(userTag => userTag.Name).ToList(),
                    Genres = media.Genres.Select(genre => genre.Name).ToList(),
                    Cast = media.People.Select(person => person.Name).ToList(),
                    Studios = media.Studios.Select(studio => studio.Name).ToList(),
                    Series = media.Series.Select(series => series.Name).ToList(),
                    CreatedAt = media.MediaLocations.Select(mediaLocation => (DateTime?)mediaLocation.CreatedAt).Min(),
                    HighlightCount = media.MediaHighlights.Count,
                    SceneCount = media.MediaLocations.Select(mediaLocation => mediaLocation.Scene).Distinct().Count(),
                    Rating = media.UserMediaData != null ? media.UserMediaData.Rating ?? 0 : 0
                })
                .OrderBy(media => media.MediaId)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetUserTagsAsync()
        {
            return await _context.UserTags
                .AsNoTracking()
                .Select(userTag => userTag.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetGenresAsync()
        {
            return await _context.MediaGenres
                .AsNoTracking()
                .Select(genre => genre.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetCastAsync()
        {
            return await _context.Media
                .AsNoTracking()
                .Where(media => media.MediaLocations.Any())
                .SelectMany(media => media.People)
                .Select(person => person.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetStudiosAsync()
        {
            return await _context.Media
                .AsNoTracking()
                .Where(media => media.MediaLocations.Any())
                .SelectMany(media => media.Studios)
                .Select(studio => studio.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetSeriesAsync()
        {
            return await _context.Media
                .AsNoTracking()
                .Where(media => media.MediaLocations.Any())
                .SelectMany(media => media.Series)
                .Select(series => series.Name)
                .Distinct()
                .OrderBy(name => name)
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
                .Include(m => m.UserTags)
                .Include(m => m.UserMediaData)
                .FirstOrDefaultAsync(m => m.MediaId == mediaId);

            if (media == null)
            {
                return null!;
            }

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
                Genres = [.. media.Genres.Select(g => g.Name)],
                UserTags = [.. media.UserTags.Select(userTag => userTag.Name).OrderBy(name => name)],
                Rating = media.UserMediaData?.Rating ?? 0
            };
        }

        public async Task AddUserTagAsync(string mediaId, string tagName)
        {
            var trimmedTagName = tagName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedTagName))
            {
                return;
            }

            var media = await _context.Media
                .Include(m => m.UserTags)
                .FirstOrDefaultAsync(m => m.MediaId == mediaId);

            if (media == null)
            {
                return;
            }

            if (media.UserTags.Any(userTag => string.Equals(userTag.Name, trimmedTagName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var existingTag = await _context.UserTags
                .FirstOrDefaultAsync(userTag => userTag.Name.ToLower() == trimmedTagName.ToLower());

            if (existingTag == null)
            {
                existingTag = new Entities.UserTag { Name = trimmedTagName };
                _context.UserTags.Add(existingTag);
            }

            media.UserTags.Add(existingTag);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveUserTagAsync(string mediaId, string tagName)
        {
            var media = await _context.Media
                .Include(m => m.UserTags)
                .FirstOrDefaultAsync(m => m.MediaId == mediaId);

            if (media == null)
            {
                return;
            }

            var existingTag = media.UserTags
                .FirstOrDefault(userTag => string.Equals(userTag.Name, tagName, StringComparison.OrdinalIgnoreCase));

            if (existingTag != null)
            {
                media.UserTags.Remove(existingTag);
                await _context.SaveChangesAsync();
            }
        }

        public async Task SetRatingAsync(string mediaId, int rating)
        {
            var clampedRating = Math.Clamp(rating, 0, 5);
            var existingUserData = await _context.UserMediaData
                .FirstOrDefaultAsync(userData => userData.MediaId == mediaId);

            if (existingUserData == null)
            {
                if (clampedRating == 0)
                {
                    return;
                }

                _context.UserMediaData.Add(new Entities.UserMediaData
                {
                    MediaId = mediaId,
                    Rating = clampedRating
                });
            }
            else
            {
                existingUserData.Rating = clampedRating == 0 ? null : clampedRating;
            }

            await _context.SaveChangesAsync();
        }
    }
}
