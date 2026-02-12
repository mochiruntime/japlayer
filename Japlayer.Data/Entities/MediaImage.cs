using System;
using System.Collections.Generic;

namespace Japlayer.Data.Entities;

public partial class MediaImage
{
    public string MediaId { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string Filepath { get; set; } = null!;

    public virtual Media Media { get; set; } = null!;

    public virtual ICollection<MediaMetadata> MediaMetadataCoverNavigations { get; set; } = new List<MediaMetadata>();

    public virtual ICollection<MediaMetadata> MediaMetadataThumbnailNavigations { get; set; } = new List<MediaMetadata>();
}
