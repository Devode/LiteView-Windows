using System.Text.Json.Serialization;

namespace LiteView.Models
{
    /// <summary>
    /// A download URL record from the Supabase "download_url" table.
    /// Linked to a <see cref="RemoteVersion"/> via <see cref="VersionId"/>.
    /// </summary>
    public class DownloadUrl
    {
        /// <summary>Primary key in the remote database.</summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>Foreign key referencing <see cref="RemoteVersion.Id"/>.</summary>
        [JsonPropertyName("version_id")]
        public int VersionId { get; set; }
        
        /// <summary>Full URL to the downloadable installer or archive.</summary>
        [JsonPropertyName("download_url")]
        public string Url { get; set; } = string.Empty;
    }
}
