using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace LiteView.Helpers
{
    public static partial class ThemeHelper
    {
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
