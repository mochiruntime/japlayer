using Japlayer.Data.Entities;

namespace Japlayer.Data.Contracts
{
    public interface IMediaHighlightProvider
    {
        public Task<IEnumerable<MediaHighlight>> GetHighlightsAsync(string mediaId);
        public Task AddHighlightAsync(string mediaId, int scene, int timestamp);
        public Task RemoveHighlightAsync(string mediaId, int scene, int timestamp);
    }
}
