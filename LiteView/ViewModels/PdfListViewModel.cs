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
    public partial class PdfListViewModel : ObservableObject
    {
        private readonly IPdfDataService _pdfDataService;
        private readonly INavigationService _navigationService;
        private readonly IMessageDialogService _dialogService;
        private readonly IFilePickerService _filePickerService;

        [ObservableProperty]
        private ObservableCollection<PdfItem> _pdfItems = new();

        [ObservableProperty]
        private Visibility _emptyVisibility = Visibility.Visible;

        public IAsyncRelayCommand AddPdfCommand { get; }
        public IRelayCommand RemovePdfCommand { get; }
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

        private async System.Threading.Tasks.Task OnAddPdfAsync()
        {
            var filePath = await _filePickerService.PickSingleFileAsync(new[] { ".pdf" });
            if (string.IsNullOrEmpty(filePath))
                return;

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

        public void Cleanup()
        {
            _pdfDataService.PdfListUpdated -= OnPdfListUpdated;
        }
    }
}