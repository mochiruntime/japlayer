using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Services
{
    public class DatabaseMediaHighlightProvider(DatabaseContext context) : IMediaHighlightProvider
    {
        private readonly DatabaseContext _context = context;

        public async Task<IEnumerable<MediaHighlight>> GetHighlightsAsync(string mediaId)
        {
            return await _context.MediaHighlights
                .AsNoTracking()
                .Where(highlight => highlight.MediaId == mediaId)
                .OrderBy(highlight => highlight.Scene)
                .ThenBy(highlight => highlight.Timestamp)
                .ToListAsync();
        }

        public async Task AddHighlightAsync(string mediaId, int scene, int timestamp)
        {
            var exists = await _context.MediaHighlights
                .AnyAsync(highlight => highlight.MediaId == mediaId && highlight.Scene == scene && highlight.Timestamp == timestamp);

            if (!exists)
            {
                var newHighlight = new MediaHighlight
                {
                    MediaId = mediaId,
                    Scene = scene,
                    Timestamp = timestamp
                };
                _context.MediaHighlights.Add(newHighlight);
                await _context.SaveChangesAsync();
            }
        }

        public async Task RemoveHighlightAsync(string mediaId, int scene, int timestamp)
        {
            var existingHighlight = await _context.MediaHighlights
                .FirstOrDefaultAsync(highlight => highlight.MediaId == mediaId && highlight.Scene == scene && highlight.Timestamp == timestamp);

            if (existingHighlight != null)
            {
                _context.MediaHighlights.Remove(existingHighlight);
                await _context.SaveChangesAsync();
            }
        }
    }
}
