using System.Collections.Generic;

namespace LiteView.Models
{
    /// <summary>
    /// Root object for the JSON-serialized PDF list file.
    /// Maps directly to the structure written by <see cref="Contracts.IPdfDataService.SavePdfDataAsync"/>.
    /// </summary>
    public class PdfDataRoot
    {
        /// <summary>The list of saved PDF entries.</summary>
        public List<PdfItem> PdfItems { get; set; }
    }
}
