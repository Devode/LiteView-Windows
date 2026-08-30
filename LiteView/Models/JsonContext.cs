using System.Text.Json.Serialization;

namespace LiteView.Models
{
    /// <summary>
    /// Source-generated JSON serializer context for AOT-compatible serialization
    /// of <see cref="PdfDataRoot"/> and <see cref="PdfItem"/>.
    /// </summary>
    [JsonSerializable(typeof(PdfDataRoot))]
    [JsonSerializable(typeof(PdfItem))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
