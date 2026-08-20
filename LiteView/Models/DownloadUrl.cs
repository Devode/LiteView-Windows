using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteView.Models
{
    public class DownloadUrl
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("version_id")]
        public int VersionId { get; set; }
        
        [JsonPropertyName("download_url")]
        public string Url { get; set; } = string.Empty;
    }
}
