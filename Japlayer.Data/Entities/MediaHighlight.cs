namespace Japlayer.Data.Entities;

public partial class MediaHighlight
{
    public string MediaId { get; set; } = null!;

    public int Scene { get; set; }

    public int Timestamp { get; set; }

    public virtual Media Media { get; set; } = null!;
}
