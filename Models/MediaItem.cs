using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Japlayer.Models
{
    public class MediaItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("series")]
        public List<string> Series { get; set; } = new();

        [JsonPropertyName("mdbId")]
        public string MdbId { get; set; }

        [JsonPropertyName("releaseDate")]
        public string ReleaseDate { get; set; }

        [JsonPropertyName("runtime")]
        public string Runtime { get; set; }

        [JsonPropertyName("studios")]
        public List<string> Studios { get; set; } = new();

        [JsonPropertyName("staff")]
        public List<string> Staff { get; set; } = new();

        [JsonPropertyName("genres")]
        public List<string> Genres { get; set; } = new();

        [JsonPropertyName("cast")]
        public List<string> Cast { get; set; } = new();
    }
}
