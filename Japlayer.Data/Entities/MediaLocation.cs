using System;
using System.Collections.Generic;

namespace Japlayer.Data.Entities;

public partial class MediaLocation
{
    public string MediaId { get; set; } = null!;

    public int Scene { get; set; }

    public string Hostname { get; set; } = null!;

    public string Path { get; set; } = null!;

    public byte[] Uuid { get; set; } = null!;
}
