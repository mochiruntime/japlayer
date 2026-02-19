using Japlayer.Data.Models;

namespace Japlayer.Data.Contracts
{
    public interface IMediaSceneProvider
    {
        Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId);
    }
}
