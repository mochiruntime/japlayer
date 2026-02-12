using System;
using System.Collections.Generic;

namespace Japlayer.Data.Models;

public partial class MediaMetadata
{
    public string MetadataUrl { get; set; } = null!;

    public string MediaId { get; set; } = null!;

    public string? ContentId { get; set; }

    public string Title { get; set; } = null!;

    public string? Cover { get; set; }

    public string? Thumbnail { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public int? RuntimeMinutes { get; set; }

    public virtual MediaImage? CoverNavigation { get; set; }

    public virtual Media Media { get; set; } = null!;

    public virtual MediaImage? ThumbnailNavigation { get; set; }
}
