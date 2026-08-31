using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Helpers;
using LiteView.Contracts;
using LiteView.Helpers;
using LiteView.Models;
using LiteView.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Windows.BadgeNotifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.Storage;

namespace LiteView.ViewModels
{
    /// <summary>
    /// Mirrors Microsoft.UI.Xaml.ElementTheme with integer indices.
    /// Used for ComboBox.SelectedIndex mapping: (int)Themes.Default == 0, etc.
    /// This enum duplicates ElementTheme intentionally to decouple the UI selection
    /// from the WinUI enum and allow TryParse on persisted string values.
    /// The index-based cast (int)Themes.X is fragile — reordering enum values
    /// will silently break persisted settings.
    /// </summary>
    public enum Themes
    {
        Default,
        Light,
        Dark
    }

    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IUpdateService _updateService;
        private readonly INetworkService _networkService;
        private readonly IMessageDialogService _dialogService;

        [ObservableProperty]
        private int _themeMode = (int)Themes.Default;

        [ObservableProperty]
        private string _currentVersion = "";

        [ObservableProperty]
        private bool _isNoUpdateInfoOpen = false;

        [ObservableProperty]
        private bool _isNetworkErrorInfoOpen = false;

        [ObservableProperty]
        private string _networkErrorInfo;

        public IRelayCommand<string> SwitchThemeCommand { get; }
        public ICommand CheckUpdateCommand { get; }

        private const string themeSettingKey = "AppTheme";

        public SettingsViewModel(IUpdateService updateService, 
            INetworkService networkService, 
            IMessageDialogService dialogService)
        {
            _updateService = updateService;
            _networkService = networkService;
            _dialogService = dialogService;

            SwitchThemeCommand = new RelayCommand<string>(OnSwitchTheme);
            CheckUpdateCommand = new AsyncRelayCommand(CheckForUpdateAsync);

            InitializeThemeSetting();

            CurrentVersion = Package.Current.Id.Version.ToFormattedString();
        }

        private void InitializeThemeSetting()
        {

            var localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.ContainsKey(themeSettingKey))
            {
                var savedTheme = localSettings.Values[themeSettingKey].ToString();
                if (Enum.TryParse<ElementTheme>(savedTheme, out var theme))
                {
                    ThemeMode = (int)theme;
                }
                else
                {
                    ThemeMode = (int)ElementTheme.Default;
                }
            }
            else
            {
                ThemeMode = (int)ElementTheme.Default;
            }
        }

        private void OnSwitchTheme(string tag)
        {
            string selectedThemeString = tag;

            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[themeSettingKey] = selectedThemeString;

            if (Enum.TryParse<ElementTheme>(selectedThemeString, out var theme))
            {
                ThemeHelper.RootTheme = theme;
            }
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
                else
                {
                    IsNoUpdateInfoOpen = true;
                }
            }
            catch (Exception ex)
            {
                IsNetworkErrorInfoOpen = true;
                NetworkErrorInfo = ResourceHelper.GetLocalizedString("NetworkErrorInfo", ex.Message);
                Debug.WriteLine($"[CheckForUpdate] {ex.Message}");
            }
        }
    }
}
