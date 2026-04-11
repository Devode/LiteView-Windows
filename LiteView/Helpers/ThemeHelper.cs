using Microsoft.UI;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Helpers
{
    public static partial class ThemeHelper
    {
        /// <summary>
        /// 根主题，应用全局主题（目前暂未处理多窗口场景）
        /// </summary>
        public static ElementTheme RootTheme
        {
            get
            {
                // 这里直接获取 MainWindow 的 Content 的 RequestedTheme 来作为全局主题，简化了全局主题的管理
                if (MainWindow.current.Content is FrameworkElement rootElement)
                {
                    return rootElement.RequestedTheme;
                }
                // 如果无法获取到 MainWindow 或 Content，则默认返回 ElementTheme.Default
                return ElementTheme.Default;
            }
            set
            {
                if (MainWindow.current.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = value;

                    UpdateTitleBarButtonsTheme(value);
                }
            }
        }

        private static void UpdateTitleBarButtonsTheme(ElementTheme theme)
        {
            var appWindow = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.current);
            var windowId = Win32Interop.GetWindowIdFromWindow(appWindow);
            var appWindowInstance = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (theme == ElementTheme.Light)
            {
                // 浅色主题下，按钮应为黑色
                appWindowInstance.TitleBar.ButtonForegroundColor = Colors.Black;
            }
            else if (theme == ElementTheme.Dark)
            {
                // 深色主题下，按钮应为白色
                appWindowInstance.TitleBar.ButtonForegroundColor = Colors.White;
            }
            else
            {
                appWindowInstance.TitleBar.ButtonForegroundColor = null; // 使用系统默认颜色
            }
        }
    }
}
