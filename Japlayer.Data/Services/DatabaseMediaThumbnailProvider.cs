using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Services
{
    public class DatabaseMediaThumbnailProvider(IDbContextFactory<DatabaseContext> contextFactory) : IMediaThumbnailProvider
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory = contextFactory;

        public async Task<IEnumerable<MediaThumbnail>> GetThumbnailsAsync(string mediaId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MediaThumbnails
                .AsNoTracking()
                .Where(t => t.MediaId == mediaId)
                .OrderBy(t => t.Scene)
                .ThenBy(t => t.Timestamp)
                .ToListAsync();
        }
    }
}
