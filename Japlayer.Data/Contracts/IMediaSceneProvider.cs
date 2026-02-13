using Japlayer.Data.Models;
using System.Collections.Generic;

namespace Japlayer.Data.Contracts
{
    public interface IMediaSceneProvider
    {
        Task<IEnumerable<MediaScene>> GetMediaScenesAsync(string mediaId);
    }
}
