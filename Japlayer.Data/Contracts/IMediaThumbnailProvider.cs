using Japlayer.Data.Entities;

namespace Japlayer.Data.Contracts
{
    public interface IMediaThumbnailProvider
    {
        public Task<IEnumerable<MediaThumbnail>> GetThumbnailsAsync(string mediaId);
    }
}
