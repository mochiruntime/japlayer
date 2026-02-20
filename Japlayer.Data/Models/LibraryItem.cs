namespace Japlayer.Data.Models
{
    public class LibraryItem
    {
        public required string MediaId { get; init; }
        public string? Title { get; init; }
        public string? CoverImagePath { get; init; }
        public IReadOnlyList<string> UserTags { get; init; } = [];
        public IReadOnlyList<string> Genres { get; init; } = [];
    }
}
