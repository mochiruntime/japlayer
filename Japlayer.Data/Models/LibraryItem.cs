namespace Japlayer.Data.Models
{
    public class LibraryItem
    {
        public required string MediaId { get; init; }
        public string? Title { get; init; }
        public DateOnly? ReleaseDate { get; init; }
        public string? CoverImagePath { get; init; }
        public IReadOnlyList<string> UserTags { get; init; } = [];
        public IReadOnlyList<string> Genres { get; init; } = [];
        public IReadOnlyList<string> Cast { get; init; } = [];
        public IReadOnlyList<string> Studios { get; init; } = [];
        public IReadOnlyList<string> Series { get; init; } = [];
        public DateTime? CreatedAt { get; init; }
        public int HighlightCount { get; init; }
        public int SceneCount { get; init; }
        public int Rating { get; init; }
        public int? RuntimeMinutes { get; init; }
    }
}
