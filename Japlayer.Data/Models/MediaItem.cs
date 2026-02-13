namespace Japlayer.Data.Models
{
    public class MediaItem
    {
        public required string MediaId { get; init; }

        public string? Title { get; init; }

        public string? ContentId { get; init; }

        public DateOnly? ReleaseDate { get; init; }

        public TimeSpan? Runtime { get; init; }

        public List<string> Series { get; init; } = new();

        public List<string> Studios { get; init; } = new();

        public List<string> Cast { get; init; } = new();

        public List<string> Staff { get; init; } = new();

        public List<string> Genres { get; init; } = new();
    }
}