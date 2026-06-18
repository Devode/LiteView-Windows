using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteView.Models
{
    public class VersionInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("software_id")]
        public int SoftwareId { get; set; }

        [JsonPropertyName("version_name")]
        public string VersionName { get; set; }

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; }

        [JsonPropertyName("release_notes")]
        public string ReleaseNotes { get; set; }

        [JsonPropertyName("is_latest")]
        public bool IsLatest { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("VersionsCode")]
        public int VersionsCode { get; set; }
    }
}
