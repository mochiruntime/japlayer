namespace Japlayer.Data.Entities;

public partial class MediaLocation
{
    public string MediaId { get; set; } = null!;

    public int Scene { get; set; }

    public string Hostname { get; set; } = null!;

    public string Path { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Media Media { get; set; } = null!;
}
