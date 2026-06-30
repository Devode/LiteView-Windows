using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteView.Models
{
    [JsonSerializable(typeof(PdfDataRoot))]
    [JsonSerializable(typeof(PdfItem))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
