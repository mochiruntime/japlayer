using Japlayer.Models;
using System.Collections.Generic;

namespace Japlayer.Contracts
{
    public interface IMediaSceneProvider
    {
        IEnumerable<MediaScene> GetScenes(string mediaId);
    }
}
