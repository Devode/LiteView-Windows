using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;

namespace LiteView.Helpers
{
    public static class WindowHelper
    {
        /// <summary>
        /// 设置指定窗口进入或退出全屏
        /// </summary>
        /// <param name="window">当前的 XAML Window 实例</param>
        /// <param name="isFullScreen">是否全屏</param>
        public static void SetFullScreen(Window window, bool isFullScreen)
        {
            if (window == null) return;

            // 获取当前窗口的原生句柄(hWnd)
            //IntPtr hWnd = WindowNative.GetWindowHandle(window);

            //// 根据句柄获取 WindowId
            //WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);

            //// 根据 WindowId 获取 AppWindow 实例
            //AppWindow appWindow = AppWindow.GetFromWindowId(myWndId);
            var appWindow = window.AppWindow;

            // 切换全屏状态
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
