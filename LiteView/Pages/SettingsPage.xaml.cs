using LiteView.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage;

namespace LiteView.Pages
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

    /// <summary>
    /// Application settings page. Allows the user to switch between
    /// Light, Dark, and Default (system) themes. The selection is persisted
    /// to ApplicationData.LocalSettings and applied immediately via <see cref="Helpers.ThemeHelper"/>.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private const string themeSettingKey = "AppTheme";

        public SettingsPage()
        {
            InitializeComponent();

            InitializeThemeSetting();

            // Subscribe after initialization to avoid firing during setup
            themeMode.SelectionChanged += themeMode_SelectionChanged;
        }

        private void InitializeThemeSetting()
        {
            themeMode.SelectionChanged -= themeMode_SelectionChanged;

            var localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.ContainsKey(themeSettingKey))
            {
                var savedTheme = localSettings.Values[themeSettingKey].ToString();
                if (Enum.TryParse<Themes>(savedTheme, out var theme))
                {
                    themeMode.SelectedIndex = (int)theme;
                }
                else
                {
                    themeMode.SelectedIndex = (int)Themes.Default;
                }
            }
            else
            {
                themeMode.SelectedIndex = (int)Themes.Default;
            }
        }

        private void themeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (themeMode.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedThemeString = selectedItem.Tag.ToString();

                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[themeSettingKey] = selectedThemeString;

                if (Enum.TryParse<ElementTheme>(selectedThemeString, out var theme)) {
                    ThemeHelper.RootTheme = theme;
                }
            }
        }
    }
}
