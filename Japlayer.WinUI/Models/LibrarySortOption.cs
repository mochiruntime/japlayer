using System.Collections.Generic;

namespace Japlayer.Models
{
    public enum LibrarySortField
    {
        AddedDate,
        ReleaseDate,
        Title,
        Highlights,
        Rating,
        Runtime,
        Random
    }

    public sealed class LibrarySortFieldOption(LibrarySortField field, string displayName)
    {
        public LibrarySortField Field { get; } = field;
        public string DisplayName { get; } = displayName;
        public override string ToString() => DisplayName;
    }

    public sealed class LibrarySortOption
    {
        public static readonly LibrarySortOption AlphabeticalAscending = new("Alphabetical (A-Z)");
        public static readonly LibrarySortOption AlphabeticalDescending = new("Alphabetical (Z-A)");
        public static readonly LibrarySortOption ReleaseDateAscending = new("Release Date (Oldest)");
        public static readonly LibrarySortOption ReleaseDateDescending = new("Release Date (Newest)");
        public static readonly LibrarySortOption AddedDateAscending = new("Added Date (Oldest)");
        public static readonly LibrarySortOption AddedDateDescending = new("Added Date (Newest)");
        public static readonly LibrarySortOption HighlightsAscending = new("Highlights (Fewest)");
        public static readonly LibrarySortOption HighlightsDescending = new("Highlights (Most)");
        public static readonly LibrarySortOption RatingAscending = new("Rating (Lowest)");
        public static readonly LibrarySortOption RatingDescending = new("Rating (Highest)");
        public static readonly LibrarySortOption RuntimeAscending = new("Runtime (Shortest)");
        public static readonly LibrarySortOption RuntimeDescending = new("Runtime (Longest)");
        public static readonly LibrarySortOption Random = new("Random");

        public string DisplayName { get; }

        private LibrarySortOption(string displayName) => DisplayName = displayName;

        public static IReadOnlyList<LibrarySortOption> All { get; } =
        [
            AlphabeticalAscending,
            AlphabeticalDescending,
            ReleaseDateAscending,
            ReleaseDateDescending,
            AddedDateAscending,
            AddedDateDescending,
            HighlightsAscending,
            HighlightsDescending,
            RatingAscending,
            RatingDescending,
            RuntimeAscending,
            RuntimeDescending,
            Random
        ];

        public static IReadOnlyList<LibrarySortFieldOption> FieldOptions { get; } =
        [
            new(LibrarySortField.AddedDate, "Added Date"),
            new(LibrarySortField.ReleaseDate, "Release Date"),
            new(LibrarySortField.Title, "Title (A-Z)"),
            new(LibrarySortField.Highlights, "Highlights"),
            new(LibrarySortField.Rating, "Rating"),
            new(LibrarySortField.Runtime, "Runtime"),
            new(LibrarySortField.Random, "Random")
        ];

        public override string ToString() => DisplayName;
    }
}
