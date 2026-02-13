namespace Japlayer.Data.Models
{
    public class MediaItem
    {
        public required string MediaId { get; init; }

        public string? Title { get; init; }

        public string? ContentId { get; init; }

        public DateOnly? ReleaseDate { get; init; }

        public TimeSpan? Runtime { get; init; }

        public List<string> Series { get; init; } = [];

        public List<string> Studios { get; init; } = [];

        public List<string> Cast { get; init; } = [];

        public List<string> Staff { get; init; } = [];

        public List<string> Genres { get; init; } = [];
    }
}