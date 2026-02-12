using System;
using System.Collections.Generic;

namespace Japlayer.Data.Models;

public partial class MediaPerson
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Media> Media { get; set; } = new List<Media>();

    public virtual ICollection<Media> MediaNavigation { get; set; } = new List<Media>();
}
