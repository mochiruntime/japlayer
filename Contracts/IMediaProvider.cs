using Japlayer.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Japlayer.Contracts
{
    public interface IMediaProvider
    {
        Task<IEnumerable<MediaItem>> GetAllItemsAsync();
        Task<MediaItem> GetItemAsync(string id);
    }
}
