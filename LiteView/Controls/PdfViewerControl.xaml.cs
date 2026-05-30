using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PdfiumViewer;
using System.Threading.Tasks;
using System.Drawing;
using System.Diagnostics;
using Microsoft.UI.Xaml.Media.Imaging;
using LiteView.Helpers;
using CommunityToolkit.WinUI;
using LiteView.Models;
using LiteView.Native;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView.Controls;

public sealed partial class PdfViewerControl : UserControl
{
    private PdfDocument _pdfDocument;
    private double _lastScrollingVerticalOffset = 0;
    private bool _isScrolling = false;
    private bool _isLoadingPages = false;

    private double _averagePageHeight = 0;

    private const double BASIC_DPI = 300;

    private PdfPageViewModel _pageRegion1;
    private PdfPageViewModel _pageRegion2;

    public List<PdfPageViewModel> PdfPages { get; set; } = new();

    public PdfViewerControl()
    {
        InitializeComponent();
    }

    // 暴露 PdfPath 属性
    public string PdfPath
    {
        get { return (string)GetValue(PdfPathProperty); }
        set { SetValue(PdfPathProperty, value); }
    }

    // 暴露 PdfPath 属性到 Xaml
    public static readonly DependencyProperty PdfPathProperty = DependencyProperty.Register(
        "PdfPath", typeof(string), typeof(PdfViewerControl), new PropertyMetadata(null, OnPdfPathChanged));

    // 当 PdfPath 修改时，重新加载
    private static void OnPdfPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as PdfViewerControl;
        control?.InitializePdf((string)e.NewValue);
    }

    private async void InitializePdf(string pdfPath)
    {
        _pdfDocument = PdfDocument.Load(pdfPath);

        _averagePageHeight = _pdfDocument.PageSizes.Average(size => size.Height);
        
        double cumulativeTop = 0;
        for (int i = 0; i < _pdfDocument.PageCount; i++)
        {
            var pageSize = _pdfDocument.PageSizes[i];

            //if (i == 0)
            //{
            //    _averagePageHeight = pageSize.Height;
            //}
            //else
            //{
            //    _averagePageHeight = (_averagePageHeight + pageSize.Height) / 2;
            //}
            PdfPages.Add(new PdfPageViewModel
            {
                IsLoading = true,
                PageWidth = pageSize.Width,
                PageHeight = pageSize.Height,
                dpi = 0,
                DocumentTop = cumulativeTop,
            });

            cumulativeTop += pageSize.Height + 10;
        }
        PdfPagesRepeater.ItemsSource = PdfPages;

        _pageRegion1 = new PdfPageViewModel()
        {
            dpi = 0,
            IsLoading = true
        };
        _pageRegion2 = new PdfPageViewModel()
        {
            dpi = 0,
            IsLoading = true
        };

        await LoadPagesAsync(0, Math.Min(2, _pdfDocument.PageCount - 1), 1);
    }

    private void PdfScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender;

        // 如果正在惯性滚动，暂不加载，等待用户停下
        if (e.IsIntermediate) return;

        // 获取 高度 和 偏移
        double viewportHeight = scrollViewer.ViewportHeight;
        double verticalOffset = scrollViewer.VerticalOffset;

        // 如果偏移量小于10，则返回
        if (Math.Abs(verticalOffset - _lastScrollingVerticalOffset) < 10) return;
        _lastScrollingVerticalOffset = verticalOffset; // 设置上一个偏移

        // 获取缩放
        double zoom = scrollViewer.ZoomFactor;
        Debug.WriteLine($"缩放结束，当前倍率：{zoom}");
        Debug.WriteLine($"offset: {verticalOffset / zoom}, height: {viewportHeight}");

        // 获取中间位置的 Y 坐标
        double centerPointY = verticalOffset + (viewportHeight / 2.0);
        // 根据 Y 坐标获取页面索引
        int centerPageIndex = FindPageByPosition(centerPointY / zoom);

        if (centerPageIndex < 0) return;

        // 计算起始和末尾的页面索引
        int startIndex = FindPageByPosition(verticalOffset / zoom);
        int endIndex = FindPageByPosition((verticalOffset + viewportHeight) / zoom);

        // 钳制
        startIndex = Math.Max(0, startIndex - 1);
        endIndex = Math.Min(_pdfDocument.PageCount - 1, endIndex + 1);

        Debug.WriteLine($"index: {centerPageIndex}");

        _ = LoadPagesAsync(startIndex, endIndex, zoom);
    }

    private async Task LoadPagesAsync(int startIndex, int endIndex, double zoom)
    {
        //if (_isLoadingPages) return;
        //_isLoadingPages = true;

        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        //var dpi = BASIC_DPI * zoom;

        for (int i = startIndex; i <= endIndex; i++)
        {
            int pageIndex = i;
            if (PdfPages[pageIndex].PageImage != null) continue;
            //if (Math.Abs(PdfPages[pageIndex].dpi - dpi) < 0.001) continue; 

            PdfPages[pageIndex].IsLoading = true;

            await Task.Run(() =>
            {
                try
                {
                    //var size = _pdfDocument.PageSizes[pageIndex];
                    //int renderWidth = (int)(size.Width);
                    //int renderHeight = (int)(size.Height);

                    //var image = _pdfDocument.Render(pageIndex, renderWidth, renderHeight, (float)BASIC_DPI, (float)BASIC_DPI, false);

                    dispatcherQueue.TryEnqueue(async () =>
                    {
                        // 默认页面底板低模渲染
                        PdfPages[pageIndex].PageImage = await RenderBitmap(pageIndex, BASIC_DPI, 1);
                        PdfPages[pageIndex].IsLoading = false;
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"渲染页面 {pageIndex} 时失败：{ex.Message}");
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        PdfPages[pageIndex].IsLoading = false;
                    });
                }
            });
        }

        //PartialRenderCanvas.Children.Clear();

        // 当前可视区域的顶部和底部页面索引
        int topPageIndex = FindPageByPosition(PdfScrollViewer.VerticalOffset / zoom);
        int bottomPageIndex = FindPageByPosition((PdfScrollViewer.VerticalOffset + PdfScrollViewer.ViewportHeight) / zoom);

        //PartialRenderCanvas.Children.Clear();
        await RenderPartialForSpecificPage(topPageIndex, zoom, ParticalImageTop);
        // 如果 底部页面索引不等于顶部页面索引，则渲染底部页面
        if (bottomPageIndex != topPageIndex) await RenderPartialForSpecificPage(bottomPageIndex, zoom, ParticalImageBottom);
        else ParticalImageBottom.Visibility = Visibility.Collapsed;
        //if (topPageIndex < 0) return;

        //// 获取当前目标页面的 UI 容器
        //var pageContainer = PdfPagesRepeater.TryGetElement(topPageIndex) as FrameworkElement;
        //if (pageContainer == null) return; // 页面尚未完成虚拟化加载

        //// 获取 ScrollViewer 视口相对于 当前页面 的偏移
        //var viewportToPage = PdfScrollViewer.TransformToVisual(pageContainer);
        //var visibleTopLeft = viewportToPage.TransformPoint(new Windows.Foundation.Point(0, 0));

        //// 构建视口在当前页面上的逻辑矩形
        //Rect visibleRectInPage = new Rect(
        //    Math.Max(0, visibleTopLeft.X),
        //    Math.Max(0, visibleTopLeft.Y),
        //    PdfScrollViewer.ViewportWidth,
        //    PdfScrollViewer.ViewportHeight
        //);

        //// 与页面边界求交集，防止越界
        //Rect pageBounds = new Rect(0, 0, PdfPages[topPageIndex].PageWidth, PdfPages[topPageIndex].PageHeight);
        //visibleRectInPage.Intersect(pageBounds);

        //if (visibleRectInPage.IsEmpty) return;

        //await RenderPartical(topPageIndex, visibleRectInPage, zoom);

        //var img1 = _pdfDocument.RectangleFromPdf(startIndex, new RectangleF(0, (float)(PdfScrollViewer.VerticalOffset / zoom), ))
    }

    /// <summary>
    /// 渲染指定页面的整张页面图片
    /// <br />
    /// <paramref name="dpi"/> DPI
    /// <paramref name="pageIndex"/> 页面索引
    /// <paramref name="zoom"/> 视图缩放
    /// </summary>
    /// <param name="pageIndex"></param>
    /// <param name="dpi"></param>
    /// <param name="zoom"></param>
    /// <returns>BitmapImage 图片</returns>
    private async Task<BitmapImage> RenderBitmap(int pageIndex, double dpi, double zoom)
    {
        var size = _pdfDocument.PageSizes[pageIndex];
        int renderWidth = (int)(size.Width * zoom);
        int renderHeight = (int)(size.Height * zoom);

        var image = _pdfDocument.Render(
            pageIndex,                      // 页面索引
            renderWidth, renderHeight,      // 渲染宽度与高度
            (float)dpi, (float)dpi,         // 渲染 DPI
            false
        );

        var bitmap = await ImageHelper.ConvertToBitmapImage(image);

        return bitmap;
    }

    /// <summary>
    /// 局部渲染
    /// </summary>
    /// <param name="pageIndex"></param>
    /// <param name="zoom"></param>
    /// <param name="targetImage"></param>
    /// <returns></returns>
    private async Task RenderPartialForSpecificPage(int pageIndex, double zoom, Microsoft.UI.Xaml.Controls.Image targetImage)
    {
        // 如果pageIndex超出范围，则隐藏targetImage(局部渲染图显示控件)并返回
        if (pageIndex < 0 || pageIndex >= PdfPages.Count)
        {
            targetImage.Visibility = Visibility.Collapsed;
            return;
        }

        var pageContainer = PdfPagesRepeater.TryGetElement(pageIndex) as FrameworkElement;
        if (pageContainer == null)
        {
            targetImage.Visibility = Visibility.Collapsed;
            return;
        }

        Windows.Foundation.Point visibleTopLeft;
        try
        {
            var viewportToPage = PdfScrollViewer.TransformToVisual(pageContainer);
            visibleTopLeft = viewportToPage.TransformPoint(new Windows.Foundation.Point(0, 0));
        }
        catch
        {
            targetImage.Visibility = Visibility.Collapsed;
            return;
        }
        // 页面中可视区域矩形
        Rect visibleRectInPage = new Rect(
            Math.Max(0, visibleTopLeft.X),
            Math.Max(0, visibleTopLeft.Y),
            PdfScrollViewer.ViewportWidth / zoom,
            PdfScrollViewer.ViewportHeight / zoom
        );

        // 获取页面矩形
        var pageVm = PdfPages[pageIndex];
        Rect pageBounds = new Rect(0, 0, pageVm.PageWidth, pageVm.PageHeight);
        visibleRectInPage.Intersect(pageBounds);    // 计算可视区域矩形于页面矩形的交集，防止越界

        //const double PADDING = 100;
        // 渲染区域矩形
        Rect renderRect = new Rect(
            Math.Max(0, visibleRectInPage.X),
            Math.Max(0, visibleRectInPage.Y),
            visibleRectInPage.Width,
            visibleRectInPage.Height
        );

        Debug.WriteLine(renderRect);

        // 渲染并获取rawBitmapData
        double targetDpi = 96.0 * zoom;
        string currentFilePath = PdfPath;
        var rawBitmapData = await Task.Run(() => Native.PdfRenderer.RenderRegion(currentFilePath, pageIndex, renderRect, targetDpi));

        // 如果在等待的时间内用户划走了，或rawBitmapData的数据无效，隐藏控件并返回
        if (PdfPagesRepeater.TryGetElement(pageIndex) == null || rawBitmapData.Pixels == null || rawBitmapData.Pixels.Length == 0)
        {
            targetImage.Visibility = Visibility.Collapsed;
            return;
        }

        // 组装 Bitmap
        var particalBitmap = new WriteableBitmap(rawBitmapData.Width, rawBitmapData.Height);
        using (var stream = particalBitmap.PixelBuffer.AsStream())
        {
            await stream.WriteAsync(rawBitmapData.Pixels, 0, rawBitmapData.Pixels.Length);
        }
        particalBitmap.Invalidate();

        //var image = new Microsoft.UI.Xaml.Controls.Image
        //{
        //    Source = particalBitmap,
        //    Width = renderRect.Width,
        //    Height = renderRect.Height,
        //    Stretch = Stretch.Fill,
        //};

        // 设置控件
        targetImage.Source = particalBitmap;
        targetImage.Width = renderRect.Width;
        targetImage.Height = renderRect.Height;

        double pageOffsetY = Canvas.GetTop(pageContainer) > 0 
            ? Canvas.GetTop(pageContainer) 
            : pageContainer.ActualOffset.Y;

        double pageOffsetX = Canvas.GetLeft(pageContainer) > 0
            ? Canvas.GetLeft(pageContainer)
            : pageContainer.ActualOffset.X;

        Canvas.SetLeft(targetImage, pageOffsetX + renderRect.X);
        Canvas.SetTop(targetImage, pageOffsetY + renderRect.Y);

        targetImage.Visibility = Visibility.Visible;

        //PartialRenderCanvas.Children.Add(image);
    }

    /// <summary>
    /// 二分查找，根据 Y 坐标找到对应页面的索引
    /// </summary>
    /// <param name="positionY"></param>
    /// <returns></returns>
    private int FindPageByPosition(double positionY)
    {
        if (PdfPages == null || !PdfPages.Any()) return -1;

        int left = 0;
        int right = PdfPages.Count - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            var page = PdfPages[mid];

            double pageBottom = (page.DocumentTop + page.PageHeight);

            if (positionY >= page.DocumentTop && positionY < pageBottom)
            {
                return mid;
            }
            else if (positionY < page.DocumentTop)
            {
                right = mid - 1;
            }
            else
            {
                left = mid + 1;
            }
        }

        if (right < 0) return 0;
        if (left >= PdfPages.Count) return PdfPages.Count - 1;

        return -1;
    }

}
