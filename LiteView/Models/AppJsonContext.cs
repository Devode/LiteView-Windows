using System.Text.Json.Serialization;
using LiteView.Models;
using System.Collections.Generic;

namespace LiteView.Models
{
    /// <summary>
    /// Source-generated JSON serializer context for AOT-compatible serialization
    /// of <see cref="PdfDataRoot"/> and <see cref="PdfItem"/>.
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
    [JsonSerializable(typeof(PdfDataRoot))]
    [JsonSerializable(typeof(PdfItem))]
    [JsonSerializable(typeof(RemoteVersion))]
    [JsonSerializable(typeof(RemoteVersion[]))]
    [JsonSerializable(typeof(DownloadUrl))]
    [JsonSerializable(typeof(DownloadUrl[]))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
