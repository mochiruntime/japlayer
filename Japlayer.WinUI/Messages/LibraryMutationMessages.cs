#nullable enable

namespace Japlayer.Messages
{
    public sealed record MediaRatingChangedMessage(string MediaId, int Rating);

    public sealed record MediaUserTagAddedMessage(string MediaId, string TagName);

    public sealed record MediaUserTagRemovedMessage(string MediaId, string TagName);

    public sealed record MediaHighlightCountChangedMessage(string MediaId, int HighlightCount);
}
