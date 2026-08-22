using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteView.Contracts;
using LiteView.Models;
using LiteView.Pages;
using LiteView.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace LiteView.ViewModels
{
    /// <summary>
    /// ViewModel for the PDF reading list page. Exposes commands for adding,
    /// removing, and opening PDFs, and manages the empty-state visibility.
    /// </summary>
    public partial class PdfListViewModel : ObservableObject
    {
        private readonly IPdfDataService _pdfDataService;
        private readonly INavigationService _navigationService;
        private readonly IMessageDialogService _dialogService;
        private readonly IFilePickerService _filePickerService;

        /// <summary>
        /// Shares the same <see cref="ObservableCollection{T}"/> instance as
        /// <see cref="IPdfDataService.PdfList"/> so that mutations are reflected immediately.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<PdfItem> _pdfItems = new();

        /// <summary>
        /// Controls the visibility of the "no items" placeholder in the list.
        /// </summary>
        [ObservableProperty]
        private Visibility _emptyVisibility = Visibility.Visible;

        /// <summary>Open a file picker, validate the selection, and add the PDF to the list.</summary>
        public IAsyncRelayCommand AddPdfCommand { get; }

        /// <summary>Remove a PDF from the list.</summary>
        public IRelayCommand RemovePdfCommand { get; }

        /// <summary>Navigate to the PDF viewer page for the selected item.</summary>
        public IRelayCommand<PdfItem> OpenPdfCommand { get; }

        public PdfListViewModel(
            IPdfDataService pdfDataService,
            INavigationService navigationService,
            IMessageDialogService dialogService,
            IFilePickerService filePickerService)
        {
            _pdfDataService = pdfDataService;
            _navigationService = navigationService;
            _dialogService = dialogService;
            _filePickerService = filePickerService;

            AddPdfCommand = new AsyncRelayCommand(OnAddPdfAsync);
            RemovePdfCommand = new RelayCommand<PdfItem>(OnRemovePdf);
            OpenPdfCommand = new RelayCommand<PdfItem>(OnOpenPdf);

            // Share the same collection reference — mutations in the service are reflected here.
            _pdfItems = _pdfDataService.PdfList;
            _pdfDataService.PdfListUpdated += OnPdfListUpdated;

            UpdateEmptyVisibility();
        }

        private void OnPdfListUpdated(object? sender, PdfListUpdatedEventArgs e)
        {
            UpdateEmptyVisibility();
        }

        private void UpdateEmptyVisibility()
        {
            EmptyVisibility = PdfItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Prompt the user to pick a .pdf file, reject duplicates, and add it to the data service.
        /// </summary>
        private async System.Threading.Tasks.Task OnAddPdfAsync()
        {
            var filePath = await _filePickerService.PickSingleFileAsync(new[] { ".pdf" });
            if (string.IsNullOrEmpty(filePath))
                return;

            // Reject duplicates by full path
            foreach (var item in PdfItems)
            {
                if (item.FilePath == filePath)
                {
                    await _dialogService.ShowAsync("添加失败", "已在列表中添加过相同的文件", "知道了", null);
                    return;
                }
            }

            var fileInfo = new System.IO.FileInfo(filePath);
            var pdf = new PdfItem
            {
                FileName = fileInfo.Name,
                FilePath = filePath,
                ModifyTime = fileInfo.LastWriteTime.ToString()
            };

            _pdfDataService.AddPdf(pdf);
        }

        private void OnRemovePdf(PdfItem pdf)
        {
            if (pdf == null) return;
            _pdfDataService.RemovePdf(pdf);
        }

        /// <summary>
        /// Validate that the file still exists on disk, then navigate to the viewer page.
        /// </summary>
        private void OnOpenPdf(PdfItem pdf)
        {
            if (pdf == null) return;

            if (!System.IO.File.Exists(pdf.FilePath))
            {
                _ = _dialogService.ShowAsync("文件不存在", $"无法找到文件：{pdf.FilePath}\n请检查文件路径是否正确。", null, "关闭");
                return;
            }

            _navigationService.NavigateTo<PdfViewerPage>(pdf);
        }

        /// <summary>
        /// Unsubscribe from data service events. Called on page Unloaded and OnNavigatedFrom.
        /// </summary>
        public void Cleanup()
        {
            _pdfDataService.PdfListUpdated -= OnPdfListUpdated;
        }
    }
}
