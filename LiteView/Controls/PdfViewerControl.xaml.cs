using LiteView.Contracts;
using LiteView.EventArguments;
using LiteView.Helpers;
using LiteView.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
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
    private int _currentPageIndex = 0;

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
    private int _pageCount = 0;


    //private PdfDocument _pdfDocument;
    private double _lastScrollingVerticalOffset = 0;
    private double _lastScrollingHorizontalOffset = 0;
    private bool _isLoadInFlight = false;
    private bool _isLoadPending = false;
    //private bool _isUnloaded = false;

    /// <summary>Maps page index to cumulative Y offset from the top of the document.</summary>
    private Dictionary<int, double> _pageToTopDistances = new();


    private readonly IPdfService _pdfService;

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
    private const int LOAD_DEBOUNCE_MS = 50;

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

    public event EventHandler<PdfCurrentPageChangedEventArgs>? PdfCurrentPageChanged;

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
        "SimplifiedTolerance", typeof(float), typeof(PdfViewerControl), new PropertyMetadata(0f));

    public PdfViewerControl()
    {
        InitializeComponent();
        Unloaded += PdfViewerControl_Unloaded;

        _pdfService = App.Host!.Services.GetRequiredService<IPdfService>();
    }

    /// <summary>
    /// Unloaded handler. Disposes the PDF document and clears all page data.
    ///
    /// NOTE: Unloaded also fires on Frame navigation (page caching) or reparenting.
    /// Callers are expected to re-set PdfPath to trigger a reload when the page
    /// is revisited.
    /// </summary>
    private void PdfViewerControl_Unloaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"[INFO] PdfViewerControl Unloaded");
        _pdfService.ClosePdf();

        var cts = _cts;
        _cts = null;
        try { cts?.Cancel(); } catch { }
        try { cts?.Dispose(); } catch { }

        PdfPages.Clear();
        PdfPagesRepeater.ItemsSource = null;
    }

    /// <summary>
    /// DependencyProperty changed callback — triggers a full PDF reload when PdfPath changes.
    /// </summary>
    private static void OnPdfPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PdfViewerControl control) return;

        _ = SafeInitializePdf(control, e.NewValue as string);
    }

    /// <summary>
    /// Fire-and-forget wrapper around <see cref="InitializePdf"/>.
    /// Used because DependencyProperty callbacks cannot be async.
    /// </summary>
    /// <param name="control"></param>
    /// <param name="pdfPath"></param>
    /// <returns></returns>
    private static async Task SafeInitializePdf(PdfViewerControl control, string pdfPath)
    {
        try
        {
            if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath))
            {
                Debug.WriteLine($"[InitializePdf] PDF file does not exist: {pdfPath}");
                return;
            }
            await control.InitializePdf(pdfPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InitializePdf] {ex.Message}");
        }
    }

    /// <summary>
    /// Load a PDF, build the page layout model, and render the first few pages.
    /// Each page's DocumentTop is the cumulative height of all preceding pages plus a 10pt gap.
    /// </summary>
    private async Task InitializePdf(string pdfPath)
    {
        //_isUnloaded = false;
        //_pdfDocument?.Dispose();
        PdfPages.Clear();
        _pageToTopDistances.Clear();
        PdfPagesRepeater.ItemsSource = null;

        //_pdfDocument = PdfDocument.Load(pdfPath);

        //_averagePageHeight = _pdfDocument.PageSizes.Average(size => size.Height);
        PageCount = _pdfService.PageCount;

        double cumulativeTop = 0;
        for (int i = 0; i < PageCount; i++)
        {
            var pageSize = _pdfService.PageSizes[i];

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

        CurrentPageIndex = 0;
        PdfCurrentPageChanged?.Invoke(this, new PdfCurrentPageChangedEventArgs(CurrentPageIndex));
        await LoadPagesAsync(0, Math.Min(2, PageCount - 1), 1);
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
        if (PdfPages == null || PdfPages.Count == 0) return;

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
    /// Scroll-changed handler with debounce. On each scroll:
    /// 1. Update CurrentPageIndex based on viewport center.
    /// 2. Cancel any pending load, then wait LOAD_DEBOUNCE_MS.
    /// 3. Load pages within the visible range +/- 1 page.
    /// The debounce prevents redundant renders during fast fling gestures.
    /// </summary>
    private async void PdfScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        


        //Debug.WriteLine($"[INFO] IsUnloaded: {_isUnloaded}");
        //Debug.WriteLine($"[INFO] Is CTS Null: {_cts == null}");
        if (_pdfService.IsUnloaded) return;

        //Debug.WriteLine($"[INFO] IsLoadInFlight: {_isLoadInFlight}");
        //if (_cts == null) return;

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
        int centerPageIndex = FindPage(centerPointY / scrollViewer.ZoomFactor);

        if (centerPageIndex >= 0 && centerPageIndex < PageCount)
        {
            CurrentPageIndex = centerPageIndex;

            PdfCurrentPageChanged?.Invoke(this, new PdfCurrentPageChangedEventArgs(centerPageIndex));
        }

        var oldCts = _cts;
        var newCts = new CancellationTokenSource();
        _cts = newCts;
        try { oldCts?.Cancel(); oldCts?.Dispose(); } catch { }

        try
        {
            await Task.Delay(LOAD_DEBOUNCE_MS, newCts.Token);
            await RequestLoadAsync(newCts.Token);
            //await LoadPagesAsync_WithLock(scrollViewer);
        }
        catch (OperationCanceledException) { return; }
        catch (ObjectDisposedException) { return; }
        catch (Exception ex)
        {
            //Debug.WriteLine($"[ViewChanged] {ex.Message}");
            return;
        }
    }

    /// <summary>
    /// Requests a page load. Multiple calls are collapsed: while a load is in
    /// flight, subsequent calls set a pending flag; when the current pass
    /// completes, the loop re-reads the ScrollViewer state and renders the
    /// latest visible range (latest-wins).
    /// </summary>
    /// <returns></returns>
    private async Task RequestLoadAsync(CancellationToken ct)
    {
        _isLoadPending = true;

        if (_isLoadInFlight) return;
        _isLoadInFlight = true;

        //Debug.WriteLine($"[INFO] IsLoadInFlight: {_isLoadInFlight}");

        try
        {
            while (_isLoadPending)
            {
                _isLoadPending = false;

                if (_pdfService.IsUnloaded || PageCount == 0) break;

                var scrollViewer = PdfScrollViewer;
                double zoom = scrollViewer.ZoomFactor;
                if (zoom <= 0) break;

                double viewportHeight = scrollViewer.ViewportHeight;
                double verticalOffset = scrollViewer.VerticalOffset;

                int startIndex = FindPage(verticalOffset / zoom);
                int endIndex = FindPage((verticalOffset + viewportHeight) / zoom);

                //Debug.WriteLine($"[LoadPage] startIndex: {startIndex}, endIndex: {endIndex}");

                if (startIndex < 0 || endIndex < 0) break;

                startIndex = Math.Max(0, startIndex - 1);
                endIndex = Math.Min(PageCount - 1, endIndex + 1);
                if (startIndex > endIndex) break;

                //Debug.WriteLine($"[Load Page] Index: {startIndex} - {endIndex}");

                try
                {
                    await LoadPagesAsync(startIndex, endIndex, zoom, ct);

                }
                catch (Exception ex)
                {
                    //Debug.WriteLine($"[RequestLoadAsync] {ex.Message}");
                    break;
                }
            }
        }
        finally
        {
            _isLoadInFlight = false;
        }
    }


    /// <summary>
    /// Render full-page bitmaps for pages in [startIndex, endIndex] that haven't been
    /// rendered yet, then render viewport-cropped high-DPI overlays for the top and
    /// bottom visible pages.
    /// </summary>
    private async Task LoadPagesAsync(int startIndex, int endIndex, double zoom, CancellationToken ct = default)
    {
        // Target dpi for RenderPartialForSpecificPage
        double basicDpi = Math.Min(GetDeviceDpi(PdfScrollViewer), MAX_BASIC_DPI);
        double targetDpi = Math.Min(GetDeviceDpi(PdfScrollViewer) * zoom, MAX_DPI);
        Debug.WriteLine(targetDpi);

        for (int i = startIndex; i <= endIndex; i++)
        {
            if (_pdfService.IsUnloaded) return;
            if (i < 0 || i >= PdfPages.Count) return;

            int pageIndex = i;
            if (PdfPages[pageIndex].PageImage != null) continue;
            PdfPages[pageIndex].IsLoading = true;

            ct.ThrowIfCancellationRequested();

            try
            {
                var bitmap = await RenderBitmap(pageIndex, basicDpi, ct);

                if (_pdfService.IsUnloaded) return;
                if (pageIndex >= PdfPages.Count) return;

                PdfPages[pageIndex].PageImage = bitmap;
                PdfPages[pageIndex].IsLoading = false;
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"Failed to render page {pageIndex}: {ex.Message}");

                if (pageIndex < PdfPages.Count)
                    PdfPages[pageIndex].IsLoading = false;
            }
        }

        int topPageIndex = FindPage(PdfScrollViewer.VerticalOffset / zoom);
        int bottomPageIndex = FindPage((PdfScrollViewer.VerticalOffset + PdfScrollViewer.ViewportHeight) / zoom);

        await RenderPartialForSpecificPage(topPageIndex, zoom, targetDpi, ParticalImageTop, ct);
        if (bottomPageIndex != topPageIndex) await RenderPartialForSpecificPage(bottomPageIndex, zoom, targetDpi, ParticalImageBottom, ct);
        else ParticalImageBottom.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Rasterize a single page at the given DPI and return the assembled WriteableBitmap.
    /// Uses native PDFium via PdfRenderer.RenderFullPage.
    /// </summary>
    private async Task<WriteableBitmap> RenderBitmap(int pageIndex, double dpi, CancellationToken ct)
    {
        var rawBitmapData = await _pdfService.RenderFullPageAsync(pageIndex, dpi, ct);
        var bitmap = await ImageHelper.AssembleBitmapAsync(rawBitmapData);

        return bitmap;
    }

    /// <summary>
    /// Render a viewport-cropped region of the given page at high DPI.
    /// The crop rectangle is the intersection of the ScrollViewer's visible area
    /// with the page bounds, ensuring we only rasterize what the user can actually see.
    /// The result is positioned as an overlay image on top of the base full-page bitmap.
    /// </summary>
    private async Task RenderPartialForSpecificPage(int pageIndex, double zoom, double dpi, Image targetImage, CancellationToken ct)
    {
        if (_pdfService.IsUnloaded) return;

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
        var rawBitmapData = await Task.Run(() => _pdfService.RenderRegionAsync(pageIndex, renderRect, dpi, ct));

        if (_pdfService.IsUnloaded) return;
        if (pageIndex >= PdfPages.Count) return;

        if (PdfPagesRepeater.TryGetElement(pageIndex) == null || rawBitmapData.Pixels == null || rawBitmapData.Pixels.Length == 0)
        {
            targetImage.Visibility = Visibility.Collapsed;
            return;
        }

        var particalBitmap = await ImageHelper.AssembleBitmapAsync(rawBitmapData);

        if (_pdfService.IsUnloaded) return;
        if (pageIndex >= PdfPages.Count) return;

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

    private int FindPage(double positionY)
    {
        return MathHelper.FindPageByPosition(PdfPages, positionY);
    }

    private float GetDeviceDpi(UIElement element)
    {
        if (element.XamlRoot == null) return 96f;

        return 96f * (float)element.XamlRoot.RasterizationScale;
    }
}
