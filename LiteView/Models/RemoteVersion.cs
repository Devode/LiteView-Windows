using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteView.Models
{
    public class RemoteVersion
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("software_id")]
        public int SoftwareId { get; set; }

        // 显示用的版本名（如 "v1.0.0 alpha - 1"）
        [JsonPropertyName("version_name")]
        public string VersionName { get; set; } = string.Empty;

        [JsonPropertyName("release_notes")]
        public string? ReleaseNotes { get; set; }

        [JsonPropertyName("is_latest")]
        public bool IsLatest { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        // 用于逻辑比较的纯数字版本（如 "1.0.0"）
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }
}
