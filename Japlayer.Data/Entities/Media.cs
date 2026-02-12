using System;
using System.Collections.Generic;

namespace Japlayer.Data.Entities;

public partial class Media
{
    public string MediaId { get; set; } = null!;

    public virtual ICollection<MediaImage> MediaImages { get; set; } = new List<MediaImage>();

    public virtual ICollection<MediaMetadata> MediaMetadata { get; set; } = new List<MediaMetadata>();

    public virtual ICollection<MediaGenre> Genres { get; set; } = new List<MediaGenre>();

    public virtual ICollection<MediaPerson> People { get; set; } = new List<MediaPerson>();

    public virtual ICollection<MediaPerson> PeopleNavigation { get; set; } = new List<MediaPerson>();

    public virtual ICollection<MediaSeries> Series { get; set; } = new List<MediaSeries>();

    public virtual ICollection<MediaStudio> Studios { get; set; } = new List<MediaStudio>();

    public virtual ICollection<UserTag> UserTags { get; set; } = new List<UserTag>();
}
