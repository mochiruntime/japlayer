namespace Japlayer.Data.Entities;

public partial class MediaStudio
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Media> Media { get; set; } = [];
}
