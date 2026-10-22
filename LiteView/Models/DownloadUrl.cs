using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace LiteView.Models
{
    /// <summary>
    /// A download URL record from the Supabase "download_url" table.
    /// Linked to a <see cref="RemoteVersion"/> via <see cref="VersionId"/>.
    /// </summary>
    [Table("download_url")]
    public class DownloadUrl : BaseModel
    {
        /// <summary>Primary key in the remote database.</summary>
        [PrimaryKey("id")]
        public int Id { get; set; }

        /// <summary>Foreign key referencing <see cref="RemoteVersion.Id"/>.</summary>
        [Column("version_id")]
        public int VersionId { get; set; }
        
        /// <summary>Full URL to the downloadable installer or archive.</summary>
        [Column("download_url")]
        public string Url { get; set; } = string.Empty;
    }
}
