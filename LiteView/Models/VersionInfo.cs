using System;
using System.Text.Json.Serialization;

namespace LiteView.Models
{
    /// <summary>
    /// Legacy version info model. Prefer <see cref="RemoteVersion"/> for new code.
    /// </summary>
    public class VersionInfo
    {
        /// <summary>Primary key in the remote database.</summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>Foreign key identifying the software product.</summary>
        [JsonPropertyName("software_id")]
        public int SoftwareId { get; set; }

        /// <summary>Human-readable version name.</summary>
        [JsonPropertyName("version_name")]
        public string VersionName { get; set; }

        /// <summary>URL to the downloadable asset.</summary>
        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; }

        /// <summary>Release notes text.</summary>
        [JsonPropertyName("release_notes")]
        public string ReleaseNotes { get; set; }

        /// <summary>Whether this is the latest published version.</summary>
        [JsonPropertyName("is_latest")]
        public bool IsLatest { get; set; }

        /// <summary>UTC timestamp of creation.</summary>
        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Numeric version code for comparison.</summary>
        [JsonPropertyName("VersionsCode")]
        public int VersionsCode { get; set; }
    }
}
