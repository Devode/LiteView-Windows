using LiteView.Models;
using LiteView.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    /// <summary>
    /// Manages the in-memory PDF list and persists it to a local JSON file.
    /// All mutations go through this service so that subscribers are notified via <see cref="PdfListUpdated"/>.
    /// </summary>
    public interface IPdfDataService
    {
        /// <summary>
        /// The shared, observable collection of PDF items. Consumers may bind directly to this.
        /// </summary>
        ObservableCollection<PdfItem> PdfList { get; }

        /// <summary>
        /// True while a <see cref="LoadPdfDataAsync"/> operation is in progress.
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Raised after any mutation (add, remove, bulk load) so that ViewModels can refresh.
        /// </summary>
        event EventHandler<PdfListUpdatedEventArgs> PdfListUpdated;

        /// <summary>
        /// Add a single PDF entry and raise <see cref="PdfListUpdated"/>.
        /// </summary>
        void AddPdf(PdfItem pdfItem);

        /// <summary>
        /// Add multiple PDF entries in bulk and raise <see cref="PdfListUpdated"/> once.
        /// </summary>
        void AddPdfs(List<PdfItem> pdfs);

        /// <summary>
        /// Remove a PDF entry and raise <see cref="PdfListUpdated"/>.
        /// </summary>
        void RemovePdf(PdfItem pdfItem);

        /// <summary>
        /// Deserialize the JSON file at <paramref name="dataFilePath"/> and populate <see cref="PdfList"/>.
        /// Guarded by <see cref="IsLoading"/> to prevent concurrent loads.
        /// </summary>
        Task LoadPdfDataAsync(string dataFilePath);

        /// <summary>
        /// Serialize <see cref="PdfList"/> to the JSON file at <paramref name="dataFilePath"/>.
        /// </summary>
        Task SavePdfDataAsync(string dataFilePath);
    }
}
