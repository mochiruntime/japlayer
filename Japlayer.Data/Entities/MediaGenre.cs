namespace Japlayer.Data.Entities;

public partial class MediaGenre
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Media> Media { get; set; } = [];
}
