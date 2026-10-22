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
                    appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
                    MainWindow.current.SetTitleBarVisiblility(false);

                    // 获取当前窗口的 OverlappedPresenter
                    var presenter = appWindow.Presenter as OverlappedPresenter;
                    if (presenter != null)
                    {
                        // 同时禁用边框和标题栏
                        presenter.SetBorderAndTitleBar(false, false);
                    }
                }
                else
                {
                    appWindow.SetPresenter(AppWindowPresenterKind.Default);
                    appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
                    MainWindow.current.SetTitleBarVisiblility(true);

                    // 获取当前窗口的 OverlappedPresenter
                    var presenter = appWindow.Presenter as OverlappedPresenter;
                    if (presenter != null)
                    {
                        // 同时禁用边框和标题栏
                        presenter.SetBorderAndTitleBar(true, true);
                    }
                }
            }
        }
    }
}
