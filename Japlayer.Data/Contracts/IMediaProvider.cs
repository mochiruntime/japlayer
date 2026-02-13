using Japlayer.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Japlayer.Data.Contracts
{
    public interface IMediaProvider
    {
        Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync();

        Task<MediaItem> GetMediaItemAsync(string mediaId);
    }
}