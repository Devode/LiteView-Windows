using LiteView.Helpers;
using LiteView.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage;

namespace LiteView.Pages
{

    /// <summary>
    /// Application settings page. Allows the user to switch between
    /// Light, Dark, and Default (system) themes. The selection is persisted
    /// to ApplicationData.LocalSettings and applied immediately via <see cref="Helpers.ThemeHelper"/>.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        private const string themeSettingKey = "AppTheme";

        public SettingsPage()
        {
            InitializeComponent();

            ViewModel = App.Host!.Services.GetRequiredService<SettingsViewModel>();

            DataContext = ViewModel;
            //InitializeThemeSetting();

            // Subscribe after initialization to avoid firing during setup
            //themeMode.SelectionChanged += themeMode_SelectionChanged;
        }

        //private void InitializeThemeSetting()
        //{
        //    themeMode.SelectionChanged -= themeMode_SelectionChanged;

        //    var localSettings = ApplicationData.Current.LocalSettings;

        //    if (localSettings.Values.ContainsKey(themeSettingKey))
        //    {
        //        var savedTheme = localSettings.Values[themeSettingKey].ToString();
        //        if (Enum.TryParse<Themes>(savedTheme, out var theme))
        //        {
        //            themeMode.SelectedIndex = (int)theme;
        //        }
        //        else
        //        {
        //            themeMode.SelectedIndex = (int)Themes.Default;
        //        }
        //    }
        //    else
        //    {
        //        themeMode.SelectedIndex = (int)Themes.Default;
        //    }
        //}

        private void themeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           
            if (themeMode.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedThemeString = selectedItem.Tag.ToString();

                ViewModel.SwitchThemeCommand.Execute(selectedThemeString);

                //var localSettings = ApplicationData.Current.LocalSettings;
                //localSettings.Values[themeSettingKey] = selectedThemeString;

                //if (Enum.TryParse<ElementTheme>(selectedThemeString, out var theme)) {
                //    ThemeHelper.RootTheme = theme;
                //}
            }
        }
    }
}
