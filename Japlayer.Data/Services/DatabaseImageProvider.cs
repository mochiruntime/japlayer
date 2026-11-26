using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Services
{
    public class DatabaseImageProvider(IDbContextFactory<DatabaseContext> contextFactory) : IImageProvider
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory = contextFactory;

        public async Task<IEnumerable<string>> GetGalleryPathsAsync(string mediaId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.MediaImages
                .AsNoTracking()
                .Where(img => img.MediaId == mediaId)
                .Select(img => img.Filepath)
                .ToListAsync();
        }
    }
}
