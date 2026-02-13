using System;
using System.Collections.Generic;

namespace Japlayer.Data.Entities;

public partial class MediaPerson
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Media> Media { get; set; } = [];

    public virtual ICollection<Media> MediaNavigation { get; set; } = [];
}
