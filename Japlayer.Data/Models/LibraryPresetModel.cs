namespace Japlayer.Data.Models
{
    public sealed class LibraryPresetFacetSelection
    {
        public IReadOnlyList<string> Included { get; init; } = [];
        public IReadOnlyList<string> Excluded { get; init; } = [];
    }

    public sealed class LibraryPresetModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsCustomName { get; set; }
        public string SearchText { get; set; } = string.Empty;
        public string SortField { get; set; } = "AddedDate";
        public bool IsSortDescending { get; set; } = true;
        public double RatingMin { get; set; }
        public double RatingMax { get; set; } = 5;
        public double RuntimeMin { get; set; }
        public double? RuntimeMax { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public LibraryPresetFacetSelection Tags { get; set; } = new();
        public LibraryPresetFacetSelection Genres { get; set; } = new();
        public LibraryPresetFacetSelection Cast { get; set; } = new();
        public LibraryPresetFacetSelection Studios { get; set; } = new();
        public LibraryPresetFacetSelection Series { get; set; } = new();
    }
}
