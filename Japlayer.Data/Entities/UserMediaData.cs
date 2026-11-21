namespace Japlayer.Data.Entities;

public partial class UserMediaData
{
    public string MediaId { get; set; } = null!;

    public int? Rating { get; set; }

    public long TotalPlayTimeSeconds { get; set; }

    public DateTime? LastPlayedAt { get; set; }

    public virtual Media Media { get; set; } = null!;
}
