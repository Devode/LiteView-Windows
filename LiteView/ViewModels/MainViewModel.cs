using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteView.Contracts;
using LiteView.Models;
using LiteView.Pages;
using Microsoft.Windows.BadgeNotifications;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LiteView.ViewModels
{
    /// <summary>
    /// ViewModel for the application shell (title bar, navigation, search).
    /// Owns the navigation commands, the search suggestion list, and the background update check.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly IPdfDataService _pdfDataService;
        private readonly IUpdateService _updateService;
        private readonly INetworkService _networkService;
        private readonly INavigationService _navigationService;
        private readonly IMessageDialogService _dialogService;

        /// <summary>All known PDF file names, kept in sync via <see cref="IPdfDataService.PdfListUpdated"/>.</summary>
        [ObservableProperty]
        private ObservableCollection<string> _pdfItemNames = new();

        /// <summary>Current text in the search box. Two-way bound in XAML.</summary>
        [ObservableProperty]
        private string _searchText;

        /// <summary>Filtered suggestions displayed in the search flyout.</summary>
        [ObservableProperty]
        private ObservableCollection<string> _suggestionItems = new();

        /// <summary>Whether the navigation pane is expanded.</summary>
        [ObservableProperty]
        private bool _isPaneOpen = true;

        /// <summary>Controls visibility of the back button in the title bar.</summary>
        [ObservableProperty]
        private bool _isBackButtonVisible = false;

        /// <summary>Navigate to a page by its tag string ("PdfListPage", "PdfViewerPage", or null for settings).</summary>
        public IRelayCommand<string?> NavigateCommand { get; }

        /// <summary>Go back in the navigation frame.</summary>
        public IRelayCommand GoBackCommand { get; }

        /// <summary>Toggle the navigation pane open/closed.</summary>
        public IRelayCommand TogglePaneCommand { get; }

        public MainViewModel(IPdfDataService pdfDataService, 
                             IUpdateService updateService, 
                             INetworkService networkService, 
                             INavigationService navigationService, 
                             IMessageDialogService dialogService)
        {
            _pdfDataService = pdfDataService;
            _updateService = updateService;
            _networkService = networkService;
            _navigationService = navigationService;
            _dialogService = dialogService;

            NavigateCommand = new RelayCommand<string?>(OnNavigate);
            GoBackCommand = new RelayCommand(OnGoBack);
            TogglePaneCommand = new RelayCommand(() => IsPaneOpen = !IsPaneOpen);

            IsBackButtonVisible = _navigationService.CanGoBack;

            _navigationService.Navigated += OnNavigated;
            _pdfDataService.PdfListUpdated += OnPdfListUpdated;

            // Fire-and-forget: check for updates on startup
            _ = CheckForUpdateAsync();
        }

        private void OnNavigated(object? sender, Services.NavigatedEventArgs e)
        {
            IsBackButtonVisible = e.CanGoBack;
        }

        /// <summary>
        /// Rebuild the flat name list from the data service's current list,
        /// then persist the data so any structural changes (e.g. reordering) are saved.
        /// </summary>
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

        /// <summary>
        /// Query Supabase for the latest version. If newer than the running package,
        /// show a badge glyph and prompt the user with a dialog.
        /// </summary>
        private async Task CheckForUpdateAsync()
        {
            BadgeNotificationManager.Current.ClearBadge();

            try
            {
                var latestVersion = await _updateService.CheckUpdateAsync();

                if (latestVersion != null)
                {
                    BadgeNotificationManager.Current.SetBadgeAsGlyph(BadgeNotificationGlyph.Activity);

                    var content = $"Update notes: {latestVersion.ReleaseNotes ?? "None"}";

                    var result = await _dialogService.ShowAsync(
                        "Update available", content, "View details", "Ignore");

                    // NOTE: "version_id=eq.2" is a hardcoded Supabase filter targeting version_id = 2.
                    // The returned array ordering depends on database row order (no explicit ORDER BY).
                    // downloadUrls[0] is assumed to be the correct URL; if multiple rows exist,
                    // the first one returned wins — this is fragile and should be parameterized.
                    var downloadUrls = await _networkService.GetSupabaseDataAsync("download_url?version_id=eq.2", AppJsonContext.Default.DownloadUrlArray);


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

        /// <summary>
        /// CommunityToolkit partial method: called whenever <see cref="SearchText"/> changes.
        /// Populates <see cref="SuggestionItems"/> with case-insensitive matches.
        /// </summary>
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

        /// <summary>
        /// Unsubscribe from events. Call when the owning Window closes.
        /// </summary>
        public void Cleanup()
        {
            _pdfDataService.PdfListUpdated -= OnPdfListUpdated;
        }
    }
}
