using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Services
{
    public class DatabaseMediaThumbnailProvider(DatabaseContext context) : IMediaThumbnailProvider
    {
        private readonly DatabaseContext _context = context;

        public async Task<IEnumerable<MediaThumbnail>> GetThumbnailsAsync(string mediaId)
        {
            return await _context.MediaThumbnails
                .AsNoTracking()
                .Where(t => t.MediaId == mediaId)
                .OrderBy(t => t.Scene)
                .ThenBy(t => t.Timestamp)
                .ToListAsync();
        }
    }
}
