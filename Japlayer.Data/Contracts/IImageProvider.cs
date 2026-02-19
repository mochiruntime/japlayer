namespace Japlayer.Data.Contracts
{
    public interface IImageProvider
    {
        Task<IEnumerable<string>> GetGalleryPathsAsync(string mediaId);
    }
}
