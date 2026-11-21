using Japlayer.Data.Models;

namespace Japlayer.Data.Contracts
{
    public interface IMediaProvider
    {
        public Task<IEnumerable<LibraryItem>> GetLibraryItemsAsync();

        public Task<MediaItem> GetMediaItemAsync(string mediaId);

        public Task<IEnumerable<string>> GetUserTagsAsync();

        public Task<IEnumerable<string>> GetGenresAsync();

        public Task AddUserTagAsync(string mediaId, string tagName);

        public Task RemoveUserTagAsync(string mediaId, string tagName);

        public Task SetRatingAsync(string mediaId, int rating);
    }
}
