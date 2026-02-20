using Japlayer.Data.Models;

namespace Japlayer.Data.Contracts
{
    public interface IMediaSceneProvider
    {
        public Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId);
    }
}
