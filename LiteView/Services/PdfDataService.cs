using LiteView.Contracts;
using LiteView.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LiteView.Services
{
    /// <summary>
    /// Event args raised whenever the PDF list is mutated.
    /// Carries the current snapshot so subscribers don't need to re-query the service.
    /// </summary>
    public class PdfListUpdatedEventArgs : EventArgs
    {
        public ObservableCollection<PdfItem> PdfList { get; }
        public PdfListUpdatedEventArgs(ObservableCollection<PdfItem> list) => PdfList = list;
    }

    /// <summary>
    /// Central data service for the PDF reading list. Keeps an in-memory
    /// <see cref="ObservableCollection{T}"/> and persists it to a local JSON file.
    /// </summary>
    public class PdfDataService : IPdfDataService
    {
        /// <inheritdoc/>
        public ObservableCollection<PdfItem> PdfList { get; } = new();

        /// <inheritdoc/>
        public event EventHandler<PdfListUpdatedEventArgs> PdfListUpdated;

        /// <inheritdoc/>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// Broadcast the current list state to all subscribers.
        /// </summary>
        public void NotifyListChanged()
        {
            PdfListUpdated?.Invoke(this, new PdfListUpdatedEventArgs(PdfList));
        }

        /// <inheritdoc/>
        public void AddPdf(PdfItem pdfItem)
        {
            PdfList.Add(pdfItem);
            NotifyListChanged();
        }

        /// <inheritdoc/>
        public void AddPdfs(List<PdfItem> pdfs)
        {
            foreach (var pdf in pdfs)
            {
                PdfList.Add(pdf);
            }

            NotifyListChanged();
        }

        /// <inheritdoc/>
        public void RemovePdf(PdfItem pdfItem)
        {
            PdfList.Remove(pdfItem);
            NotifyListChanged();
        }

        /// <inheritdoc/>
        public async System.Threading.Tasks.Task LoadPdfDataAsync(string dataFilePath)
        {
            // Guard: prevent double-load when called concurrently (e.g., App.Init fire-and-forget
            // and a UI-triggered reload). Only the first caller proceeds; subsequent calls silently return.
            if (IsLoading) return;

            IsLoading = true;

            // Task.Run wraps synchronous file I/O to avoid blocking the UI thread.
            // LoadDataFileAsync itself is a synchronous JsonFileHelper.Load that performs
            // File.ReadAllText — wrapped here to keep the async method responsive.
            var items = await System.Threading.Tasks.Task.Run(() => LoadDataFileAsync(dataFilePath));

            if (items == null)
            {
                IsLoading = false;
                return;
            }
            
            PdfList.Clear();
            foreach (var item in items) PdfList.Add(item);

            IsLoading = false;
            NotifyListChanged();
        }

        /// <inheritdoc/>
        public async Task SavePdfDataAsync(string dataFilePath)
        {
            if (PdfList == null) return;

            var root = new PdfDataRoot { PdfItems = PdfList.ToList() };
            string json = JsonSerializer.Serialize(root, AppJsonContext.Default.PdfDataRoot);

            await File.WriteAllTextAsync(dataFilePath, json);
        }

        /// <summary>
        /// Deserialize the JSON file and return the list of PdfItems, or null on failure.
        /// Runs on a thread-pool thread via <see cref="Task.Run"/>.
        /// </summary>
        private async Task<List<PdfItem>?> LoadDataFileAsync(string dataFilePath)
        {
            try
            {
                if (!File.Exists(dataFilePath)) return null;

                string data = await File.ReadAllTextAsync(dataFilePath);
                var root = JsonSerializer.Deserialize(data, AppJsonContext.Default.PdfDataRoot);

                return root?.PdfItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read PDF data file: {ex.Message}");
                return null;
            }
        }
    }
}
