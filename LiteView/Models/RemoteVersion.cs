using System;
using System.Text.Json.Serialization;

namespace LiteView.Models
{
    /// <summary>
    /// A version record returned by the Supabase "versions" table.
    /// Used by <see cref="Contracts.IUpdateService.CheckUpdateAsync"/> to determine
    /// whether the running package is outdated.
    /// </summary>
    public class RemoteVersion
    {
        /// <summary>Primary key in the remote database.</summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>Foreign key identifying the software product.</summary>
        [JsonPropertyName("software_id")]
        public int SoftwareId { get; set; }

        /// <summary>
        /// Display name for the version (e.g. "v1.0.0 alpha - 1").
        /// </summary>
        [JsonPropertyName("version_name")]
        public string VersionName { get; set; } = string.Empty;

        /// <summary>Markdown or plain-text release notes shown to the user.</summary>
        [JsonPropertyName("release_notes")]
        public string? ReleaseNotes { get; set; }

        /// <summary>Whether this is the latest published version.</summary>
        [JsonPropertyName("is_latest")]
        public bool IsLatest { get; set; }

        /// <summary>UTC timestamp when the version was created.</summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Pure numeric version string for logical comparison (e.g. "1.0.0").
        /// Parsed by <see cref="System.Version.TryParse"/>.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }
}
