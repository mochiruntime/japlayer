namespace Japlayer.Data.Contracts
{
    public interface IImageProvider
    {
        public Task<IEnumerable<string>> GetGalleryPathsAsync(string mediaId);
    }
}
