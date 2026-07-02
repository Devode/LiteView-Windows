using CommunityToolkit.WinUI;
using LiteView.Helpers;
using LiteView.Models;
using LiteView.Native;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView.Controls;

public sealed partial class PdfViewerControl : UserControl, INotifyPropertyChanged
{
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            if (_currentPageIndex != value)
            {
                _currentPageIndex = value;
                // 显式触发通知
                OnPropertyChanged(nameof(CurrentPageIndex));
            }
        }
    }
    public int PageCount
    {
        get => _pageCount;
        set
        {
            if (_pageCount != value)
            {
                _pageCount = value;
                OnPropertyChanged(nameof(PageCount));
            }
        }
    }

    private int _currentPageIndex = 0;
    private int _pageCount = 0;

    private PdfDocument _pdfDocument;
    private double _lastScrollingVerticalOffset = 0;
    private double _lastScrollingHorizontalOffset = 0;
    private bool _isScrolling = false;
    private bool _isLoadingPages = false;
    
    /// <summary>
    /// 各页面顶部到文档顶部的距离
    /// </summary>
    private Dictionary<int, double> _pageToTopDistances = new();

    private double _averagePageHeight = 0;

    private CancellationTokenSource? _cts; // 用于防抖取消

    private const double BASIC_DPI = 300;
    // 防抖时间间隔 (毫秒)
    private const int LOAD_DEBOUNCE_MS = 100;

    public List<PdfPageViewModel> PdfPages { get; set; } = new();

    // 暴露 PdfPath 属性
    public string PdfPath
    {
        get { return (string)GetValue(PdfPathProperty); }
        set { SetValue(PdfPathProperty, value); }
    }

    // 暴露 PdfPath 属性到 Xaml
    public static readonly DependencyProperty PdfPathProperty = DependencyProperty.Register(
        "PdfPath", typeof(string), typeof(PdfViewerControl), new PropertyMetadata(null, OnPdfPathChanged));

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public float ViewerZoom
    {
        get { return (float)GetValue(ViewerZoomProperty); }
        set { SetValue(ViewerZoomProperty, value); }
    }
    public static readonly DependencyProperty ViewerZoomProperty =
        DependencyProperty.Register("ViewerZoom", typeof(float), typeof(PdfViewerControl), new PropertyMetadata(1.0f));

    public Windows.Foundation.Point ViewerOffset
    {
        get { return (Windows.Foundation.Point)GetValue(ViewerOffsetProperty); }
        set { SetValue(ViewerOffsetProperty, value); }
    }
    public static readonly DependencyProperty ViewerOffsetProperty =
        DependencyProperty.Register("ViewerOffset", typeof(Windows.Foundation.Point), typeof(PdfViewerControl), new PropertyMetadata(new Windows.Foundation.Point(0, 0)));



    public PdfViewerControl()
    {
        InitializeComponent();

        Unloaded += PdfViewerControl_Unloaded;
    }

    private void PdfViewerControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _pdfDocument?.Dispose();

        _cts?.Cancel();
        _cts?.Dispose();

        PdfPages.Clear();
    }

    private async void InitializePdf(string pdfPath)
    {
        _pdfDocument = PdfDocument.Load(pdfPath);

        _averagePageHeight = _pdfDocument.PageSizes.Average(size => size.Height);
        PageCount = _pdfDocument.PageCount;

        // 初始化页面
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

            _pageToTopDistances[i] = cumulativeTop;

            cumulativeTop += pageSize.Height + 10;
        }
        PdfPagesRepeater.ItemsSource = PdfPages;

        await LoadPagesAsync(0, Math.Min(2, _pdfDocument.PageCount - 1), 1);
    }

    public void PreviousPage()
    {
        if (_pageToTopDistances.Count == 0) return;
        CurrentPageIndex -= 1;
        CurrentPageIndex = Math.Clamp(CurrentPageIndex, 0, PageCount - 1);

        JumpToPage(CurrentPageIndex);
        //var horizontalOffset = (PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth) / 2.0;
        //horizontalOffset = Math.Max(0, Math.Min(horizontalOffset, PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth));

        //PdfScrollViewer.ChangeView(horizontalOffset, _pageToTopDistances[CurrentPageIndex] * PdfScrollViewer.ZoomFactor, null);
    }
    public void NextPage()
    {
        if (_pageToTopDistances.Count == 0) return;
        CurrentPageIndex += 1;
        CurrentPageIndex = Math.Clamp(CurrentPageIndex, 0, PageCount - 1);

        JumpToPage(CurrentPageIndex);
        //var horizontalOffset = (PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth) / 2.0;
        //horizontalOffset = Math.Max(0, Math.Min(horizontalOffset, PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth));

        //PdfScrollViewer.ChangeView(horizontalOffset, _pageToTopDistances[CurrentPageIndex] * PdfScrollViewer.ZoomFactor, null);
    }

    public void JumpToPage(int page)
    {
        var pageIndex = Math.Clamp(page, 0, PageCount - 1);

        var horizontalOffset = (PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth) / 2.0;
        horizontalOffset = Math.Max(0, Math.Min(horizontalOffset, PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth));

        PdfScrollViewer.ChangeView(horizontalOffset, _pageToTopDistances[pageIndex] * PdfScrollViewer.ZoomFactor, null);
    }

    public void ZoomIn(float zoomFactor)
    {
        var newZoom = PdfScrollViewer.ZoomFactor + zoomFactor;
        var newOffsets = ZoomAtViewportCenter(PdfScrollViewer.ZoomFactor, newZoom);

        PdfScrollViewer.ChangeView(newOffsets.NewHorizontalOffset, newOffsets.NewVerticalOffset, newZoom);
    }

    public void ZoomOut(float zoomFactor)
    {
        var newZoom = PdfScrollViewer.ZoomFactor - zoomFactor;
        var newOffsets = ZoomAtViewportCenter(PdfScrollViewer.ZoomFactor, newZoom);

        PdfScrollViewer.ChangeView(newOffsets.NewHorizontalOffset, newOffsets.NewVerticalOffset, newZoom);
    }

    public void FitToWindow()
    {
        var currentPage = CurrentPageIndex;
        var pageWidth = PdfPages[currentPage].PageWidth;
        var pageHeight = PdfPages[currentPage].PageHeight;

        var viewportWidth = PdfScrollViewer.ViewportWidth;
        var viewportHeight = PdfScrollViewer.ViewportHeight;

        double zoom;
        if (pageWidth > pageHeight)
        {
            zoom = viewportWidth / pageWidth;
        }
        else
        {
            zoom = viewportHeight / pageHeight;
        }

        var horizontalOffset = (PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth) / 2.0;
        horizontalOffset = Math.Max(0, Math.Min(horizontalOffset, PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth));

        PdfScrollViewer.ChangeView(horizontalOffset, _pageToTopDistances[currentPage] * zoom, (float)zoom);
    }
    public AnnotationCanvasControl GetAnnotationCanvas() => AnnotationCanvas;

    public void AllowAnnotate(bool isAllow)
    {
        AnnotationCanvas.IsHitTestVisible = isAllow;
    }

    

    // 当 PdfPath 修改时，重新加载
    private static void OnPdfPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as PdfViewerControl;
        control?.InitializePdf((string)e.NewValue);
    }

    private async void PdfScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender;

        Debug.WriteLine("ViewChanged");

        //if (!e.IsIntermediate)
        //{
        //    var annotationCanvas = this.AnnotationCanvas;
        //    if (annotationCanvas != null)
        //        annotationCanvas.Update(scrollViewer.ZoomFactor, scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
        //}


        //// 如果正在惯性滚动，暂不加载，等待用户停下
        //if (e.IsIntermediate) return;

        // 获取 高度 和 偏移
        double viewportHeight = scrollViewer.ViewportHeight;
        double verticalOffset = scrollViewer.VerticalOffset;
        double horizontalOffset = scrollViewer.HorizontalOffset;

        // 如果偏移量小于10，则返回
        if (Math.Abs(verticalOffset - _lastScrollingVerticalOffset) < 10 && 
            Math.Abs(horizontalOffset - _lastScrollingHorizontalOffset) < 10) 
            return;
        // 设置上一个偏移
        _lastScrollingVerticalOffset = verticalOffset; 
        _lastScrollingHorizontalOffset = horizontalOffset;

        // 获取中间位置的 Y 坐标
        double centerPointY = verticalOffset + (viewportHeight / 2.0);
        // 根据 Y 坐标获取页面索引
        int centerPageIndex = FindPageByPosition(centerPointY / scrollViewer.ZoomFactor);

        if (centerPageIndex >= 0 && centerPageIndex < PageCount)
        {
            CurrentPageIndex = centerPageIndex;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            await Task.Delay(LOAD_DEBOUNCE_MS, _cts.Token);

            await LoadPagesAsync_WithLock(scrollViewer);
        }
        catch (OperationCanceledException)
        {

        }
    }

    private async Task LoadPagesAsync_WithLock(ScrollViewer scrollViewer)
    {
        if (_isLoadingPages) return;
        _isLoadingPages = true;

        try
        {
            double zoom = scrollViewer.ZoomFactor;
            double viewportHeight = scrollViewer.ViewportHeight;
            double verticalOffset = scrollViewer.VerticalOffset;

            // 计算起始和末尾的页面索引
            int startIndex = FindPageByPosition(verticalOffset / zoom);
            int endIndex = FindPageByPosition((verticalOffset + viewportHeight) / zoom);

            // 钳制
            startIndex = Math.Max(0, startIndex - 1);
            endIndex = Math.Min(_pdfDocument.PageCount - 1, endIndex + 1);

            await LoadPagesAsync(startIndex, endIndex, zoom);
        }
        finally
        {
            _isLoadingPages = false;
        }
    }

    private async Task LoadPagesAsync(int startIndex, int endIndex, double zoom)
    {
        //var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        //var dpi = BASIC_DPI * zoom;

        for (int i = startIndex; i <= endIndex; i++)
        {
            int pageIndex = i;
            if (PdfPages[pageIndex].PageImage != null) continue;
            //if (Math.Abs(PdfPages[pageIndex].dpi - dpi) < 0.001) continue; 

            PdfPages[pageIndex].IsLoading = true;

            try
            {
                // 默认页面底板低模渲染
                var bitmap = await RenderBitmap(pageIndex, BASIC_DPI, 1);
                PdfPages[pageIndex].PageImage = bitmap;
                PdfPages[pageIndex].IsLoading = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"渲染页面 {pageIndex} 时失败：{ex.Message}");
                    
                PdfPages[pageIndex].IsLoading = false;
            }
                    
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
    private async Task<WriteableBitmap> RenderBitmap(int pageIndex, double dpi, double zoom)
    {
        var size = _pdfDocument.PageSizes[pageIndex];
        int renderWidth = (int)(size.Width * zoom);
        int renderHeight = (int)(size.Height * zoom);

        //var image = _pdfDocument.Render(
        //    pageIndex,                      // 页面索引
        //    renderWidth, renderHeight,      // 渲染宽度与高度
        //    (float)dpi, (float)dpi,         // 渲染 DPI
        //    false
        //);
        var rawBitmapData = Native.PdfRenderer.RenderFullPage(PdfPath, pageIndex, renderWidth, renderHeight, dpi);

        //var bitmap = await ImageHelper.ConvertToBitmapImage(image);
        var bitmap = await ImageHelper.AssembleBitmapAsync(rawBitmapData);

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
        // 如果宽或高小于等于0，则没有交集，直接返回
        if (visibleRectInPage.Width <= 0 || visibleRectInPage.Height <= 0)
        {
            targetImage.Visibility = Visibility.Collapsed;
            return;
        }

        //const double PADDING = 100;
        // 渲染区域矩形
        Rect renderRect = new Rect(
            visibleRectInPage.X,
            visibleRectInPage.Y,
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
        var particalBitmap = await ImageHelper.AssembleBitmapAsync(rawBitmapData);
        //var particalBitmap = new WriteableBitmap(rawBitmapData.Width, rawBitmapData.Height);
        //using (var stream = particalBitmap.PixelBuffer.AsStream())
        //{
        //    await stream.WriteAsync(rawBitmapData.Pixels, 0, rawBitmapData.Pixels.Length);
        //}
        //particalBitmap.Invalidate();

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
    /// 计算在以新缩放比例进行缩放时，为保持当前视口中心点位置不变所需的水平和垂直偏移量
    /// </summary>
    /// <param name="oldZoom">当前(旧)的缩放比例</param>
    /// <param name="newZoom">目标(新)缩放比例</param>
    /// <returns>一个包含新的水平偏移量和垂直偏移量的元组 (horizontalOffset, verticalOffset)</returns>
    private (double NewHorizontalOffset, double NewVerticalOffset) ZoomAtViewportCenter(double oldZoom, double newZoom)
    {
        var horizontalOffset = PdfScrollViewer.HorizontalOffset;
        var verticalOffset = PdfScrollViewer.VerticalOffset;

        var viewportWidth = PdfScrollViewer.ViewportWidth;
        var viewportHeight = PdfScrollViewer.ViewportHeight;

        double centerY = (verticalOffset + (viewportHeight / 2.0)) / oldZoom;
        double centerX = (horizontalOffset + (viewportWidth / 2.0)) / oldZoom;

        var newVerticalOffset = centerY * newZoom - viewportHeight / 2;
        var newHorizontalOffset = centerX * newZoom - viewportWidth / 2;

        return (newHorizontalOffset, newVerticalOffset);
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
