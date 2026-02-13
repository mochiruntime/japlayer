using System;
using System.Collections.Generic;

namespace Japlayer.Data.Entities;

public partial class Media
{
    public string MediaId { get; set; } = null!;

    public virtual ICollection<MediaImage> MediaImages { get; set; } = [];

    public virtual ICollection<MediaMetadata> MediaMetadata { get; set; } = [];

    public virtual ICollection<MediaGenre> Genres { get; set; } = [];

    public virtual ICollection<MediaPerson> People { get; set; } = [];

    public virtual ICollection<MediaPerson> PeopleNavigation { get; set; } = [];

    public virtual ICollection<MediaSeries> Series { get; set; } = [];

    public virtual ICollection<MediaStudio> Studios { get; set; } = [];

    public virtual ICollection<UserTag> UserTags { get; set; } = [];
}
