using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteView.Contracts;
using LiteView.Models;
using LiteView.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LiteView.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IPdfDataService _pdfDataService;
        private readonly IUpdateService _updateService;
        private readonly INetworkService _networkService;
        private readonly INavigationService _navigationService;
        private readonly IMessageDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<string> _pdfItemNames = new();

        [ObservableProperty]
        private string _searchText;

        [ObservableProperty]
        private ObservableCollection<string> _suggestionItems = new();

        [ObservableProperty]
        private bool _isPaneOpen = true;

        public IRelayCommand<string?> NavigateCommand { get; }
        public IRelayCommand GoBackCommand { get; }
        public IRelayCommand TogglePaneCommand { get; }

        public MainViewModel(IPdfDataService pdfDataService, IUpdateService updateService, INetworkService networkService, INavigationService navigationService, IMessageDialogService dialogService)
        {
            _pdfDataService = pdfDataService;
            _updateService = updateService;
            _networkService = networkService;
            _navigationService = navigationService;
            _dialogService = dialogService;

            NavigateCommand = new RelayCommand<string?>(OnNavigate);
            GoBackCommand = new RelayCommand(OnGoBack);
            TogglePaneCommand = new RelayCommand(() => IsPaneOpen = !IsPaneOpen);

            _pdfDataService.PdfListUpdated += OnPdfListUpdated;
            _ = CheckForUpdateAsync();
        }

        private void OnPdfListUpdated(object? sender, Services.PdfListUpdatedEventArgs e)
        {
            PdfItemNames.Clear();

            foreach (var pdfItem in e.PdfList)
            {
                PdfItemNames.Add(pdfItem.FileName);
            }

            _ = _pdfDataService.SavePdfDataAsync(App.CurrentApp.PdfDataFilePath);
        }

        private void OnNavigate(string? tag)
        {
            switch (tag)
            {
                case "PdfListPage": _navigationService.NavigateTo<PdfListPage>(); break;
                case "PdfViewerPage": _navigationService.NavigateTo<PdfViewerPage>(); break;
                default: _navigationService.NavigateToSettings(); break;
            }
        }

        private void OnGoBack()
        {
            if (_navigationService.CanGoBack)
                _navigationService.GoBack();
        }

        private async Task CheckForUpdateAsync()
        {
            try
            {
                var latestVersion = await _updateService.CheckUpdateAsync();

                if (latestVersion != null)
                {
                    var content = $"更新内容：{latestVersion.ReleaseNotes ?? "无"}";

                    var result = await _dialogService.ShowAsync(
                        "检测到新版本", content, "查看详情", "忽略");

                    var downloadUrls = await _networkService.GetSupabaseDataAsync<DownloadUrl[]>("download_url?version_id=eq.2");

                    if (result == Contracts.DialogResult.Primary
                        && downloadUrls != null
                        && downloadUrls.Length > 0
                        && !string.IsNullOrEmpty(downloadUrls[0].Url))
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrls[0].Url));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CheckForUpdate] {ex.Message}");
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            SuggestionItems.Clear();
            if (string.IsNullOrWhiteSpace(value))
                return;

            foreach (var pdfName in PdfItemNames)
            {
                if (pdfName.Contains(value, StringComparison.OrdinalIgnoreCase))
                    SuggestionItems.Add(pdfName);
            }
        }

        public void Cleanup()
        {
            _pdfDataService.PdfListUpdated -= OnPdfListUpdated;
        }
    }
}
