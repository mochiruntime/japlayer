using Japlayer.Data.Models;

namespace Japlayer.Data.Contracts
{
    public interface IMediaProvider
    {
        public Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync();

        public Task<MediaItem> GetMediaItemAsync(string mediaId);

        public Task<IEnumerable<string>> GetUserTagsAsync();

        public Task<IEnumerable<string>> GetGenresAsync();
    }
}
