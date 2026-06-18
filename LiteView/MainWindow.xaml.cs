using LiteView.Models;
using LiteView.Pages;
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
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static MainWindow current;

        private static readonly HttpClient httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
            current = this;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;

            navView.SelectedItem = navView.MenuItems[0];
            navFrame.Navigate(typeof(PdfListPage));

            Init();
        }

        public NavigationView GetNavView()
        {
            return navView;
        }

        private async Task Init()
        {
            VersionInfo versionInfo = ParseVersionInfo(await FetchDataAsync());
            Debug.WriteLine(versionInfo.VersionsCode);

            if (versionInfo.VersionsCode > App.VERSION_CODE)
            {
                var title = new TextBlock
                {
                    Text = "更新内容"
                };
                var content = new TextBlock
                {
                    Text = versionInfo.ReleaseNotes
                };

                var panel = new StackPanel();
                panel.Children.Add(title);
                panel.Children.Add(content);

                ContentDialog dialog = new ContentDialog
                {
                    Title = "检测到新版本",
                    Content = panel,
                    PrimaryButtonText = "查看详情",
                    CloseButtonText = "忽略",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    if (versionInfo.DownloadUrl != null)
                    {
                        var uri = new Uri(versionInfo.DownloadUrl);
                        await Windows.System.Launcher.LaunchUriAsync(uri);
                    }
                }
            }
        }

        private void titleBar_BackRequested(TitleBar sender, object args)
        {
            if (navFrame.CanGoBack)
            {
                navFrame.GoBack();
            }
        }

        private void titleBar_PaneToggleRequested(TitleBar sender, object args)
        {
            navView.IsPaneOpen = !navView.IsPaneOpen;
        }

        private void navView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                navFrame.Navigate(typeof(SettingsPage));

            }
            else
            {
                var selectedItem = args.SelectedItem as NavigationViewItem;
                switch (selectedItem?.Tag)
                {
                    case "PdfListPage":
                        navFrame.Navigate(typeof(PdfListPage));
                        break;
                    case "PdfViewerPage":
                        navFrame.Navigate(typeof(PdfViewerPage));
                        break;
                }
            }
        }

        private async Task<string> FetchDataAsync()
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync("https://ratzizwtoyyhdlypecsn.supabase.co/rest/v1/versions?apikey=sb_publishable_7jN-mL9WzEJtIZlkgWarpA_B5kb4Rbm");

                // 确保请求成功
                response.EnsureSuccessStatusCode();

                // 读取响应内容（字符串）
                string responseBody = await response.Content.ReadAsStringAsync();

                Debug.WriteLine(responseBody);

                return responseBody;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"请求出错: {e.Message}");
                return null;
            }
        }

        private VersionInfo ParseVersionInfo(string versionData)
        {
            try
            {
                // 忽略大小写匹配，防止因大小写不一致导致解析失败
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // 将 JSON 数组反序列化为 List
                List<VersionInfo> versions = JsonSerializer.Deserialize<List<VersionInfo>>(versionData, options);

                

                // 遍历测试输出
                foreach (var v in versions)
                {
                    if (v.SoftwareId == 2) return v;
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON 解析失败: {ex.Message}");
            }

            return null;
        }
    }
}
