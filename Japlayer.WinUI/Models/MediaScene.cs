using System.Text.Json.Serialization;

namespace Japlayer.Models
{
    public class MediaScene
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("scene")]
        public int? Scene { get; set; }

        [JsonPropertyName("file")]
        public string File { get; set; }
    }
}
