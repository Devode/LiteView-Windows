using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteView.Contracts;
using LiteView.Models;
using LiteView.Pages;
using LiteView.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

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

            // 订阅服务列表更新
            _pdfDataService.PdfListUpdated += OnPdfListUpdated;

            // 初始化加载数据
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            // 假设服务中已加载数据，直接获取当前列表
            await _pdfDataService.LoadPdfDataAsync(App.CurrentApp.PdfDataFilePath); // 路径可配置
            // 然后更新自己的集合
            UpdatePdfList(_pdfDataService.PdfList);
        }

        private void OnPdfListUpdated(object? sender, PdfListUpdatedEventArgs e)
        {
            UpdatePdfList(e.PdfList);
        }

        private void UpdatePdfList(IEnumerable<PdfItem> items)
        {
            PdfItems.Clear();
            foreach (var item in items)
                PdfItems.Add(item);
            _emptyVisibility = PdfItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task OnAddPdfAsync()
        {
            // 选择文件
            var filePath = await _filePickerService.PickSingleFileAsync(new[] { ".pdf" });
            if (string.IsNullOrEmpty(filePath))
                return;

            // 检查重复
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

            // 通过服务添加
            _pdfDataService.AddPdf(pdf);
            // 服务触发事件会自动更新列表
        }

        private void OnRemovePdf(PdfItem pdf)
        {
            Debug.WriteLine("RemovePdf");
            if (pdf == null) return;
            _pdfDataService.RemovePdf(pdf);
        }

        private void OnOpenPdf(PdfItem pdf)
        {
            if (pdf == null) return;

            // 检查文件是否存在
            if (!System.IO.File.Exists(pdf.FilePath))
            {
                _ = _dialogService.ShowAsync("文件不存在", $"无法找到文件：{pdf.FilePath}\n请检查文件路径是否正确。", null, "关闭");
                return;
            }

            // 导航到阅读器页面，传递 PdfItem
            _navigationService.NavigateTo<PdfViewerPage>(pdf);
        }

        public void Cleanup()
        {
            _pdfDataService.PdfListUpdated -= OnPdfListUpdated;
        }
    }
}