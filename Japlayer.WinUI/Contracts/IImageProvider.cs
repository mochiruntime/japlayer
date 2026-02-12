using System.Collections.Generic;

namespace Japlayer.Contracts
{
    public interface IImageProvider
    {
        string GetCoverPath(string id);
        string GetThumbPath(string id);
        IEnumerable<string> GetGalleryPaths(string id);
    }
}
