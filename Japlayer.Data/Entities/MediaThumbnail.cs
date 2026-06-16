namespace Japlayer.Data.Entities;

public partial class MediaThumbnail
{
    public string MediaId { get; set; } = null!;

    public int Scene { get; set; }

    public int Timestamp { get; set; }

    public string Path { get; set; } = null!;

    public string OriginalHostname { get; set; } = null!;

    public string OriginalPath { get; set; } = null!;

    public virtual Media Media { get; set; } = null!;
}
