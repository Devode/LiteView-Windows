using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace LiteView.Helpers
{
    /// <summary>
    /// Centralizes application-wide theme management. Sets the RequestedTheme on the
    /// main window's root element and updates title bar button colors accordingly.
    /// Uses a static reference to MainWindow — safe for single-window apps.
    /// </summary>
    public static partial class ThemeHelper
    {
        /// <summary>
        /// Gets or sets the application-wide theme. Setting this updates both the
        /// root element's RequestedTheme and the title bar button foreground color.
        /// </summary>
        public static ElementTheme RootTheme
        {
            get
            {
                if (MainWindow.current?.Content is FrameworkElement rootElement)
                    return rootElement.RequestedTheme;
                return ElementTheme.Default;
            }
            set
            {
                if (MainWindow.current?.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = value;
                    UpdateTitleBarButtonsTheme(value);
                }
            }
        }

        private static void UpdateTitleBarButtonsTheme(ElementTheme theme)
        {
            if (MainWindow.current == null) return;

            var appWindow = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.current);
            var windowId = Win32Interop.GetWindowIdFromWindow(appWindow);
            var appWindowInstance = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            appWindowInstance.TitleBar.ButtonForegroundColor = theme switch
            {
                ElementTheme.Light => Colors.Black,
                ElementTheme.Dark => Colors.White,
                _ => null
            };
        }
    }
}
