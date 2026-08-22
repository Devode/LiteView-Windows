namespace LiteView.Models
{
    /// <summary>
    /// Represents a single PDF entry in the user's reading list.
    /// Persisted as part of <see cref="PdfDataRoot"/>.
    /// </summary>
    public class PdfItem
    {
        /// <summary>Display name shown in the list (typically the file name).</summary>
        public string FileName { get; set; }

        /// <summary>Last-modified timestamp, stored as a string for JSON serialization.</summary>
        public string ModifyTime { get; set; }

        /// <summary>Absolute path to the PDF file on disk.</summary>
        public string FilePath { get; set; }
    }
}
