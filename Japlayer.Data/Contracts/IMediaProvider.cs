using Japlayer.Data.Models;

namespace Japlayer.Data.Contracts
{
    public interface IMediaProvider
    {
        Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync();

        Task<MediaItem> GetMediaItemAsync(string mediaId);
    }
}
