using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Services
{
    public class DatabaseMediaHighlightProvider(IDbContextFactory<DatabaseContext> contextFactory) : IMediaHighlightProvider
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory = contextFactory;

        public async Task<IEnumerable<MediaHighlight>> GetHighlightsAsync(string mediaId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MediaHighlights
                .AsNoTracking()
                .Where(highlight => highlight.MediaId == mediaId)
                .OrderBy(highlight => highlight.Scene)
                .ThenBy(highlight => highlight.Timestamp)
                .ToListAsync();
        }

        public async Task AddHighlightAsync(string mediaId, int scene, int timestamp)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var exists = await context.MediaHighlights
                .AnyAsync(highlight => highlight.MediaId == mediaId && highlight.Scene == scene && highlight.Timestamp == timestamp);

            if (!exists)
            {
                var newHighlight = new MediaHighlight
                {
                    MediaId = mediaId,
                    Scene = scene,
                    Timestamp = timestamp
                };
                context.MediaHighlights.Add(newHighlight);
                await context.SaveChangesAsync();
            }
        }

        public async Task RemoveHighlightAsync(string mediaId, int scene, int timestamp)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingHighlight = await context.MediaHighlights
                .FirstOrDefaultAsync(highlight => highlight.MediaId == mediaId && highlight.Scene == scene && highlight.Timestamp == timestamp);

            if (existingHighlight != null)
            {
                context.MediaHighlights.Remove(existingHighlight);
                await context.SaveChangesAsync();
            }
        }
    }
}
