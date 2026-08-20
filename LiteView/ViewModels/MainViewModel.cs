using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteView.Contracts;
using LiteView.Models;
using LiteView.Pages;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        public IRelayCommand<NavigationViewItem> NavigateCommand { get; }
        public IRelayCommand GoBackCommand { get; }
        public IRelayCommand TogglePaneCommand { get; }

        public MainViewModel(IPdfDataService pdfDataService, IUpdateService updateService, INetworkService networkService, INavigationService navigationService, IMessageDialogService dialogService)
        {
            _pdfDataService = pdfDataService;
            _updateService = updateService;
            _networkService = networkService;
            _navigationService = navigationService;
            _dialogService = dialogService;

            NavigateCommand = new RelayCommand<NavigationViewItem>(OnNavigate);
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

            _pdfDataService.SavePdfDataAsync(App.CurrentApp.PdfDataFilePath);
        }

        private void OnNavigate(NavigationViewItem item)
        {
            switch (item?.Tag?.ToString())
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
                    var title = new TextBlock { Text = "更新内容" };
                    var content = new TextBlock { Text = latestVersion.ReleaseNotes };
                    var panel = new StackPanel();
                    panel.Children.Add(title);
                    panel.Children.Add(content);

                    var result = await _dialogService.ShowAsync(
                        $"检测到新版本 - {latestVersion.VersionName}", panel, "查看详情", "忽略");

                    DownloadUrl[] downloadUrl = await _networkService.GetSupabaseDataAsync<DownloadUrl[]>("download_url?version_id=eq.2");


                    if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(downloadUrl[0].Url))
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl[0].Url));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CheckForUpdate] {ex.Message}");
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
