using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Japlayer.Data.Services
{
    public class DatabaseImageProvider(DatabaseContext context) : IImageProvider
    {
        private readonly DatabaseContext _context = context;

        public async Task<IEnumerable<string>> GetGalleryPathsAsync(string mediaId)
        {
            return await _context.MediaImages
                .AsNoTracking()
                .Where(img => img.MediaId == mediaId)
                .Select(img => img.Filepath)
                .ToListAsync();
        }
    }
}
