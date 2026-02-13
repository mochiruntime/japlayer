namespace Japlayer.Data.Models
{
    public class MediaScene
    {
        public required string MediaId { get; init; }
        public int? SceneNumber { get; init; }
        public List<string> FilePaths { get; init; } = [];
    }
}