using CommunityToolkit.WinUI;
using LiteView.Helpers;
using LiteView.Models;
using LiteView.Native;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Foundation;

namespace LiteView.Controls;

/// <summary>
/// Virtualized PDF viewer built on PdfiumViewer + native PDFium. Renders pages on-demand
/// as the user scrolls, with a two-tier strategy:
/// <list type="number">
///   <item>Full-page bitmap at a base DPI for each visible page.</item>
///   <item>Partial (viewport-cropped) high-DPI overlay for the top and bottom pages.</item>
/// </list>
/// Scrolling and zooming use debounced callbacks to avoid redundant renders.
/// </summary>
public sealed partial class PdfViewerControl : UserControl, INotifyPropertyChanged
{
    /// <summary>Zero-based index of the page currently centered in the viewport.</summary>
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            if (_currentPageIndex != value)
            {
                _currentPageIndex = value;
                OnPropertyChanged(nameof(CurrentPageIndex));
            }
        }
    }
    /// <summary>Total number of pages in the loaded PDF.</summary>
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
    
    /// <summary>Maps page index to cumulative Y offset from the top of the document.</summary>
    private Dictionary<int, double> _pageToTopDistances = new();

    private double _averagePageHeight = 0;

    private CancellationTokenSource? _cts;

    /// <summary>
    /// DPI used for the base full-page render. The name "BASIC" is misleading:
    /// this is NOT the true PDF DPI — it's a compromise resolution that renders
    /// full pages fast at roughly 300 DPI. Partial overlays for top/bottom visible
    /// pages use a separate higher DPI (see OVERLAY_DPI). The two-tier DPI scheme
    /// trades off-screen render quality for fast scroll performance.
    /// </summary>
    private const double MAX_BASIC_DPI = 300;

    private const double MAX_DPI = 1000;

    /// <summary>Debounce delay in milliseconds before triggering a scroll-driven load.</summary>
    private const int LOAD_DEBOUNCE_MS = 100;

    /// <summary>View models for every page in the document. Bound to an ItemsRepeater.</summary>
    public List<PdfPageViewModel> PdfPages { get; set; } = new();

    /// <summary>Path to the PDF file. Setting this triggers a full reload.</summary>
    public string PdfPath
    {
        get { return (string)GetValue(PdfPathProperty); }
        set { SetValue(PdfPathProperty, value); }
    }

    public static readonly DependencyProperty PdfPathProperty = DependencyProperty.Register(
        "PdfPath", typeof(string), typeof(PdfViewerControl), new PropertyMetadata(null, OnPdfPathChanged));

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Current zoom factor applied to the ScrollViewer.</summary>
    public float ViewerZoom
    {
        get { return (float)GetValue(ViewerZoomProperty); }
        set { SetValue(ViewerZoomProperty, value); }
    }
    public static readonly DependencyProperty ViewerZoomProperty =
        DependencyProperty.Register("ViewerZoom", typeof(float), typeof(PdfViewerControl), new PropertyMetadata(1.0f));

    /// <summary>Current scroll offset of the viewer.</summary>
    public Windows.Foundation.Point ViewerOffset
    {
        get { return (Windows.Foundation.Point)GetValue(ViewerOffsetProperty); }
        set { SetValue(ViewerOffsetProperty, value); }
    }
    public static readonly DependencyProperty ViewerOffsetProperty =
        DependencyProperty.Register("ViewerOffset", typeof(Windows.Foundation.Point), typeof(PdfViewerControl), new PropertyMetadata(new Windows.Foundation.Point(0, 0)));

    /// <summary>The simplified tolerance of AnnotationCanvasControl.</summary>
    public float SimplifiedTolerance
    {
        get => (float)GetValue(SimplifiedToleranceProperty);
        set
        {
            SetValue(SimplifiedToleranceProperty, value);
            Debug.WriteLine(SimplifiedTolerance);
        }
    }
    public static readonly DependencyProperty SimplifiedToleranceProperty = DependencyProperty.Register(
        "SimplifiedThreshold", typeof(float), typeof(AnnotationCanvasControl), new PropertyMetadata(null));

    public PdfViewerControl()
    {
        InitializeComponent();
        Unloaded += PdfViewerControl_Unloaded;
    }

    /// <summary>
    /// Unloaded handler. Disposes the PDF document and clears all page data.
    ///
    /// NOTE: Unloaded also fires on Frame navigation (page caching) or reparenting.
    /// Since PdfPath is unchanged afterwards, OnPdfPathChanged won't re-fire, so
    /// returning to this page leaves a disposed document and empty PdfPages.
    /// A reload mechanism (e.g., re-setting PdfPath) would be needed to recover.
    /// </summary>
    private void PdfViewerControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _pdfDocument?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        PdfPages.Clear();
    }

    /// <summary>
    /// Load a PDF, build the page layout model, and render the first two pages.
    /// Each page's DocumentTop is the cumulative height of all preceding pages plus a 10pt gap.
    /// </summary>
    private async Task InitializePdf(string pdfPath)
    {
        _pdfDocument?.Dispose();
        PdfPages.Clear();
        _pageToTopDistances.Clear();
        PdfPagesRepeater.ItemsSource = null;

        _pdfDocument = PdfDocument.Load(pdfPath);

        _averagePageHeight = _pdfDocument.PageSizes.Average(size => size.Height);
        PageCount = _pdfDocument.PageCount;

        double cumulativeTop = 0;
        for (int i = 0; i < _pdfDocument.PageCount; i++)
        {
            var pageSize = _pdfDocument.PageSizes[i];

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
    }

    public void NextPage()
    {
        if (_pageToTopDistances.Count == 0) return;
        CurrentPageIndex += 1;
        CurrentPageIndex = Math.Clamp(CurrentPageIndex, 0, PageCount - 1);
        JumpToPage(CurrentPageIndex);
    }

    /// <summary>
    /// Scroll so that the top of the given page aligns with the viewport top,
    /// preserving the current horizontal center.
    /// </summary>
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

    /// <summary>
    /// Compute the zoom factor that fits the current page within the viewport,
    /// then scroll to that page at the computed zoom.
    /// </summary>
    public void FitToWindow()
    {
        var currentPage = CurrentPageIndex;
        var pageWidth = PdfPages[currentPage].PageWidth;
        var pageHeight = PdfPages[currentPage].PageHeight;

        var viewportWidth = PdfScrollViewer.ViewportWidth;
        var viewportHeight = PdfScrollViewer.ViewportHeight;

        double zoom = Math.Min(viewportWidth / pageWidth, viewportHeight / pageHeight);

        var horizontalOffset = (PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth) / 2.0;
        horizontalOffset = Math.Max(0, Math.Min(horizontalOffset, PdfScrollViewer.ExtentWidth - PdfScrollViewer.ViewportWidth));

        PdfScrollViewer.ChangeView(horizontalOffset, _pageToTopDistances[currentPage] * zoom, (float)zoom);
    }

    /// <summary>Enable or disable pointer input on the annotation canvas.</summary>
    public void AllowAnnotate(bool isAllow)
    {
        AnnotationCanvas.IsHitTestVisible = isAllow;
    }

    /// <summary>Toggle between pen and eraser mode on the annotation canvas.</summary>
    public void SetAnnotationEraseMode(bool isEraseMode)
    {
        AnnotationCanvas.IsEraser = isEraseMode;
    }

    /// <summary>Set the pen color for new annotation strokes.</summary>
    public void SetAnnotationColor(Windows.UI.Color color)
    {
        AnnotationCanvas.SetPenColor(color);
    }

    /// <summary>Set the stroke thickness for new annotation strokes.</summary>
    public void SetAnnotationThickness(double thickness)
    {
        AnnotationCanvas.SetStrokeThickness(thickness);
    }

    /// <summary>Remove all annotation strokes from the canvas.</summary>
    public void ClearAnnotations()
    {
        AnnotationCanvas.ClearStrokes();
    }

    /// <summary>Enable or disable horizontal and vertical scrolling.</summary>
    public void SetScrollingEnabled(bool isEnabled)
    {
        PdfScrollViewer.HorizontalScrollMode = isEnabled ? ScrollMode.Enabled : ScrollMode.Disabled;
        PdfScrollViewer.VerticalScrollMode = isEnabled ? ScrollMode.Enabled : ScrollMode.Disabled;
    }

    /// <summary>
    /// DependencyProperty changed callback — triggers a full PDF reload when PdfPath changes.
    ///
    /// WARNING: InitializePdf is async Task but is invoked without await (fire-and-forget).
    /// This means:
    ///   - The try/catch only catches synchronous exceptions before the first await.
    ///   - Any async failure (e.g., bad file path inside PdfDocument.Load) becomes an
    ///     unobserved Task exception, not caught here.
    /// This is a known limitation of DP callbacks which cannot be async.
    /// </summary>
    private static void OnPdfPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as PdfViewerControl;

        try
        {
            control?.InitializePdf((string)e.NewValue);
        }
        catch (Exception ex)
        { 
            Debug.WriteLine($"Failed to load PDF file: {ex.Message}");
        }
    }

    /// <summary>
    /// Scroll-changed handler with debounce. On each scroll:
    /// 1. Update CurrentPageIndex based on viewport center.
    /// 2. Cancel any pending load, then wait LOAD_DEBOUNCE_MS.
    /// 3. Load pages within the visible range +/- 1 page.
    /// The debounce prevents redundant renders during fast fling gestures.
    /// </summary>
    private async void PdfScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender;

        double viewportHeight = scrollViewer.ViewportHeight;
        double verticalOffset = scrollViewer.VerticalOffset;
        double horizontalOffset = scrollViewer.HorizontalOffset;

        if (Math.Abs(verticalOffset - _lastScrollingVerticalOffset) < 10 && 
            Math.Abs(horizontalOffset - _lastScrollingHorizontalOffset) < 10) 
            return;

        _lastScrollingVerticalOffset = verticalOffset; 
        _lastScrollingHorizontalOffset = horizontalOffset;

        double centerPointY = verticalOffset + (viewportHeight / 2.0);
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

    /// <summary>
    /// Guarded wrapper around LoadPagesAsync that prevents concurrent loads.
    /// Determines the visible page range from the ScrollViewer's current state.
    /// </summary>
    private async Task LoadPagesAsync_WithLock(ScrollViewer scrollViewer)
    {
        if (_isLoadingPages) return;
        // `_isLoadingPages` acts as a reentrancy guard, not a queue.
        // Scroll events that arrive while a load is in-flight are silently dropped.
        // Once the in-flight load completes, the next scroll event will pick up the
        // current viewport position. No "pending load" is scheduled — the final
        // scroll position is what matters for visible pages.
        _isLoadingPages = true;

        try
        {
            double zoom = scrollViewer.ZoomFactor;
            double viewportHeight = scrollViewer.ViewportHeight;
            double verticalOffset = scrollViewer.VerticalOffset;

            int startIndex = FindPageByPosition(verticalOffset / zoom);
            int endIndex = FindPageByPosition((verticalOffset + viewportHeight) / zoom);

            startIndex = Math.Max(0, startIndex - 1);
            endIndex = Math.Min(_pdfDocument.PageCount - 1, endIndex + 1);

            await LoadPagesAsync(startIndex, endIndex, zoom);
        }
        finally
        {
            _isLoadingPages = false;
        }
    }

    /// <summary>
    /// Render full-page bitmaps for pages in [startIndex, endIndex] that haven't been
    /// rendered yet, then render viewport-cropped high-DPI overlays for the top and
    /// bottom visible pages.
    /// </summary>
    private async Task LoadPagesAsync(int startIndex, int endIndex, double zoom)
    {
        // Target dpi for RenderPartialForSpecificPage
        double basicDpi = Math.Min(GetDeviceDpi(PdfScrollViewer), MAX_BASIC_DPI);
        double targetDpi = Math.Min(GetDeviceDpi(PdfScrollViewer) * zoom, MAX_DPI);
        Debug.WriteLine(targetDpi);

        for (int i = startIndex; i <= endIndex; i++)
        {
            int pageIndex = i;
            if (PdfPages[pageIndex].PageImage != null) continue;

            PdfPages[pageIndex].IsLoading = true;

            try
            {
                var bitmap = await RenderBitmap(pageIndex, basicDpi);
                PdfPages[pageIndex].PageImage = bitmap;
                PdfPages[pageIndex].IsLoading = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to render page {pageIndex}: {ex.Message}");
                PdfPages[pageIndex].IsLoading = false;
            }
        }

        int topPageIndex = FindPageByPosition(PdfScrollViewer.VerticalOffset / zoom);
        int bottomPageIndex = FindPageByPosition((PdfScrollViewer.VerticalOffset + PdfScrollViewer.ViewportHeight) / zoom);

        await RenderPartialForSpecificPage(topPageIndex, zoom, targetDpi, ParticalImageTop);
        if (bottomPageIndex != topPageIndex) await RenderPartialForSpecificPage(bottomPageIndex, zoom, targetDpi, ParticalImageBottom);
        else ParticalImageBottom.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Rasterize a single page at the given DPI and return the assembled WriteableBitmap.
    /// Uses native PDFium via PdfRenderer.RenderFullPage.
    /// </summary>
    private async Task<WriteableBitmap> RenderBitmap(int pageIndex, double dpi)
    {
        var rawBitmapData = Native.PdfRenderer.RenderFullPage(PdfPath, pageIndex, dpi);
        var bitmap = await ImageHelper.AssembleBitmapAsync(rawBitmapData);

        return bitmap;
    }

    /// <summary>
    /// Render a viewport-cropped region of the given page at high DPI.
    /// The crop rectangle is the intersection of the ScrollViewer's visible area
    /// with the page bounds, ensuring we only rasterize what the user can actually see.
    /// The result is positioned as an overlay image on top of the base full-page bitmap.
    /// </summary>
    private async Task RenderPartialForSpecificPage(int pageIndex, double zoom, double dpi, Microsoft.UI.Xaml.Controls.Image targetImage)
    {
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

        Rect visibleRectInPage = new Rect(
            Math.Max(0, visibleTopLeft.X),
            Math.Max(0, visibleTopLeft.Y),
            PdfScrollViewer.ViewportWidth / zoom,
            PdfScrollViewer.ViewportHeight / zoom
        );

        var pageVm = PdfPages[pageIndex];
        Rect pageBounds = new Rect(0, 0, pageVm.PageWidth, pageVm.PageHeight);
        visibleRectInPage.Intersect(pageBounds);

        if (visibleRectInPage.Width <= 0 || visibleRectInPage.Height <= 0)
        {
            targetImage.Visibility = Visibility.Collapsed;
            return;
        }

        Rect renderRect = new Rect(
            visibleRectInPage.X,
            visibleRectInPage.Y,
            visibleRectInPage.Width,
            visibleRectInPage.Height
        );

        string currentFilePath = PdfPath;
        var rawBitmapData = await Task.Run(() => Native.PdfRenderer.RenderRegion(currentFilePath, pageIndex, renderRect, dpi));

        if (PdfPagesRepeater.TryGetElement(pageIndex) == null || rawBitmapData.Pixels == null || rawBitmapData.Pixels.Length == 0)
        {
            targetImage.Visibility = Visibility.Collapsed;
            return;
        }

        var particalBitmap = await ImageHelper.AssembleBitmapAsync(rawBitmapData);

        targetImage.Source = particalBitmap;
        targetImage.Width = renderRect.Width;
        targetImage.Height = renderRect.Height;

        // Canvas.GetTop returns NaN if no explicit top is set (e.g., before layout).
        // NaN > 0 evaluates to false, so the fallback ActualOffset.Y kicks in.
        // This handles the case where pageContainer hasn't been positioned yet by Canvas yet.
        double pageOffsetY = Canvas.GetTop(pageContainer) > 0 
            ? Canvas.GetTop(pageContainer) 
            : pageContainer.ActualOffset.Y;

        // Same NaN-fallback pattern for X offset.
        double pageOffsetX = Canvas.GetLeft(pageContainer) > 0
            ? Canvas.GetLeft(pageContainer)
            : pageContainer.ActualOffset.X;

        Canvas.SetLeft(targetImage, pageOffsetX + renderRect.X);
        Canvas.SetTop(targetImage, pageOffsetY + renderRect.Y);

        targetImage.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Calculate offsets to keep the viewport center point fixed when changing zoom level.
    /// </summary>
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
    /// Binary search to find the page index at a given vertical position.
    /// </summary>
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

    private float GetDeviceDpi(UIElement element)
    {
        if (element.XamlRoot == null) return 96f;

        return 96f * (float)element.XamlRoot.RasterizationScale;
    }
}
