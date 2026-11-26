using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Japlayer.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Services
{
    public class DatabaseMediaSceneProvider(IDbContextFactory<DatabaseContext> contextFactory) : IMediaSceneProvider
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory = contextFactory;

        public async Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var scenes = await context.MediaLocations
                .AsNoTracking()
                .Where(ml => ml.MediaId == mediaId)
                .GroupBy(ml => ml.Scene)
                .Select(g => new
                {
                    SceneNumber = g.Key,
                    Paths = g.Select(ml => ml.Path).ToList()
                })
                .ToListAsync();

            return scenes.Select(s => new MediaScene
            {
                MediaId = mediaId,
                SceneNumber = s.SceneNumber,
                FilePaths = s.Paths
            });
        }
    }
}
