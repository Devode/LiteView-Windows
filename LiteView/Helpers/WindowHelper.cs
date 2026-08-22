using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace LiteView.Helpers
{
    public static class WindowHelper
    {
        /// <summary>
        /// Toggle the specified window between full-screen and default presentation.
        /// </summary>
        public static void SetFullScreen(Window window, bool isFullScreen)
        {
            if (window == null) return;

            var appWindow = window.AppWindow;

            if (appWindow != null)
            {
                if (isFullScreen)
                {
                    appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
                }
                else
                {
                    appWindow.SetPresenter(AppWindowPresenterKind.Default);
                }
            }
        }
    }
}
