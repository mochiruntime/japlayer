namespace Japlayer.Data.Entities;

public partial class LibraryPreset
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

    public virtual ICollection<LibraryPresetTag> Tags { get; set; } = [];

    public virtual ICollection<LibraryPresetGenre> Genres { get; set; } = [];

    public virtual ICollection<LibraryPresetPerson> People { get; set; } = [];

    public virtual ICollection<LibraryPresetStudio> Studios { get; set; } = [];

    public virtual ICollection<LibraryPresetSeries> Series { get; set; } = [];
}

public partial class LibraryPresetTag
{
    public int LibraryPresetId { get; set; }

    public int UserTagId { get; set; }

    public bool IsIncluded { get; set; }

    public virtual LibraryPreset LibraryPreset { get; set; } = null!;

    public virtual UserTag UserTag { get; set; } = null!;
}

public partial class LibraryPresetGenre
{
    public int LibraryPresetId { get; set; }

    public int GenreId { get; set; }

    public bool IsIncluded { get; set; }

    public virtual LibraryPreset LibraryPreset { get; set; } = null!;

    public virtual MediaGenre Genre { get; set; } = null!;
}

public partial class LibraryPresetPerson
{
    public int LibraryPresetId { get; set; }

    public int PersonId { get; set; }

    public bool IsIncluded { get; set; }

    public virtual LibraryPreset LibraryPreset { get; set; } = null!;

    public virtual MediaPerson Person { get; set; } = null!;
}

public partial class LibraryPresetStudio
{
    public int LibraryPresetId { get; set; }

    public int StudioId { get; set; }

    public bool IsIncluded { get; set; }

    public virtual LibraryPreset LibraryPreset { get; set; } = null!;

    public virtual MediaStudio Studio { get; set; } = null!;
}

public partial class LibraryPresetSeries
{
    public int LibraryPresetId { get; set; }

    public int SeriesId { get; set; }

    public bool IsIncluded { get; set; }

    public virtual LibraryPreset LibraryPreset { get; set; } = null!;

    public virtual MediaSeries Series { get; set; } = null!;
}
