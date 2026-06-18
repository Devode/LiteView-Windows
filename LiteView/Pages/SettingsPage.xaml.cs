using LiteView.Helpers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView.Pages
{
    public enum Themes
    {
        Default,
        Light,
        Dark
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        // 应用主题键常量，防止写错
        private const string themeSettingKey = "AppTheme";

        public SettingsPage()
        {
            InitializeComponent();

            InitializeThemeSetting();

            // 为了防止在初始化时触发事件，先取消订阅，等设置完成后再订阅
            themeMode.SelectionChanged += themeMode_SelectionChanged;
        }

        private void InitializeThemeSetting()
        {
            themeMode.SelectionChanged -= themeMode_SelectionChanged;

            var localSettings = ApplicationData.Current.LocalSettings;

            string path = Windows.Storage.ApplicationData.Current.LocalFolder.Path;

            System.Diagnostics.Debug.WriteLine($"数据存储路径: {path}");

            if (localSettings.Values.ContainsKey(themeSettingKey))
            {
                var savedTheme = localSettings.Values[themeSettingKey].ToString();
                Debug.WriteLine(savedTheme);
                if (Enum.TryParse<Themes>(savedTheme, out var theme))
                {
                    themeMode.SelectedIndex = (int)theme;
                }
                else
                {
                    // 如果保存的值无法解析，默认选择系统主题
                    themeMode.SelectedIndex = (int)Themes.Default;
                }
            }
            else
            {
                // 如果没有保存的设置，默认选择系统主题
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
