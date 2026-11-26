using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Japlayer.Data.Helpers;
using Japlayer.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Services
{
    public class DatabaseMediaProvider(IDbContextFactory<DatabaseContext> contextFactory) : IMediaProvider
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory = contextFactory;

        public async Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Use projection to fetch exactly what we need in one query.
            // We start from Media and only include those that have associated locations (files).
            var items = await context.Media
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

            // Batch-load fallback thumbnail paths for coverless items instead of N+1 per-card queries.
            var coverlessIds = items
                .Where(item => string.IsNullOrEmpty(item.CoverImagePath))
                .Select(item => item.MediaId)
                .ToList();

            if (coverlessIds.Count > 0)
            {
                const int batchSize = 500;
                var coverlessThumbnails = new List<(string MediaId, int Scene, int Timestamp, string Path)>();

                foreach (var batch in coverlessIds.Chunk(batchSize))
                {
                    var batchIds = batch.ToArray();
                    var batchThumbnails = await context.MediaThumbnails
                        .AsNoTracking()
                        .Where(thumbnail => batchIds.Contains(thumbnail.MediaId))
                        .Select(thumbnail => new { thumbnail.MediaId, thumbnail.Scene, thumbnail.Timestamp, thumbnail.Path })
                        .ToListAsync();

                    coverlessThumbnails.AddRange(batchThumbnails.Select(entry => (entry.MediaId, entry.Scene, entry.Timestamp, entry.Path)));
                }

                var thumbnailsByMediaId = coverlessThumbnails
                    .OrderBy(entry => entry.Scene)
                    .ThenBy(entry => entry.Timestamp)
                    .GroupBy(entry => entry.MediaId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Select(entry => entry.Path).ToList(), StringComparer.OrdinalIgnoreCase);

                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.CoverImagePath) &&
                        thumbnailsByMediaId.TryGetValue(item.MediaId, out var paths))
                    {
                        item.FallbackThumbnailPath = ThumbnailFallbackSelector.SelectFallbackItem(paths);
                    }
                }
            }

            return items;
        }

        public async Task<IEnumerable<string>> GetUserTagsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.UserTags
                .AsNoTracking()
                .Select(userTag => userTag.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetGenresAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MediaGenres
                .AsNoTracking()
                .Select(genre => genre.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetCastAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Media
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
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Media
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
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Media
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
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Fetch everything in one single query using navigation properties
            var media = await context.Media
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

            await using var context = await _contextFactory.CreateDbContextAsync();
            var media = await context.Media
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

            var existingTag = await context.UserTags
                .FirstOrDefaultAsync(userTag => userTag.Name.ToLower() == trimmedTagName.ToLower());

            if (existingTag == null)
            {
                existingTag = new Entities.UserTag { Name = trimmedTagName };
                context.UserTags.Add(existingTag);
            }

            media.UserTags.Add(existingTag);
            await context.SaveChangesAsync();
        }

        public async Task RemoveUserTagAsync(string mediaId, string tagName)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var media = await context.Media
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
                await context.SaveChangesAsync();
            }
        }

        public async Task SetRatingAsync(string mediaId, int rating)
        {
            var clampedRating = Math.Clamp(rating, 0, 5);
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingUserData = await context.UserMediaData
                .FirstOrDefaultAsync(userData => userData.MediaId == mediaId);

            if (existingUserData == null)
            {
                if (clampedRating == 0)
                {
                    return;
                }

                context.UserMediaData.Add(new Entities.UserMediaData
                {
                    MediaId = mediaId,
                    Rating = clampedRating
                });
            }
            else
            {
                existingUserData.Rating = clampedRating == 0 ? null : clampedRating;
            }

            await context.SaveChangesAsync();
        }
    }
}
