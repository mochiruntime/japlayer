using System.Collections.Generic;

namespace Japlayer.Models
{
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
            Random
        ];

        public override string ToString() => DisplayName;
    }
}
