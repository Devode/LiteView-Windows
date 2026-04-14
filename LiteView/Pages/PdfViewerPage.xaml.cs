using CommunityToolkit.WinUI;
using LiteView.Models;
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using WinRT.Interop;
using static System.Net.Mime.MediaTypeNames;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView.Pages
{
    public class PdfPageItem
    {
        public BitmapImage PageImage { get; set; }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PdfViewerPage : Page
    {
        string filePath;

        private List<Microsoft.UI.Xaml.Controls.Image> _recycledImages = new List<Microsoft.UI.Xaml.Controls.Image>(); // 图片控件池
        private int _visiblePageStart = -1;
        private int _visiblePageEnd = -1;

        private float pageHeight; // in PDF points (1/72 inch)
        private float pageWidth;  // in PDF points
        private double pageHeightDip; // in device-independent pixels (DIP)
        private double pageWidthDip;  // in DIP

        private Dictionary<int, BitmapImage> _lowQualityCache = new Dictionary<int, BitmapImage>();

        private DispatcherTimer _renderTimer; // 渲染防抖计时器


        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(3, 3);
        private CancellationTokenSource _renderCts = new CancellationTokenSource();

        private PdfDocument _document;
        private ObservableCollection<PdfPageItem> _pages = new ObservableCollection<PdfPageItem>();

        const double lodZoom = 0.75; // 低模缩放
        const int maxDpi = 300; // DPI 最大值，防止内存爆炸

        public PdfViewerPage()
        {
            InitializeComponent();

            _renderTimer = new DispatcherTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(100);
            _renderTimer.Tick += (s, e) =>
            {
                Debug.WriteLine("高清渲染");
                _isUpdatingVisiblePages = true;
                try
                {
                    UpdateVisiblePages(true);
                }
                finally
                {
                    _isUpdatingVisiblePages = false;
                }
                _renderTimer.Stop();
            };
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is PdfItem pdfItem)
            {
                filePath = pdfItem.FilePath;

                loadPdf();
            }
        }

        private async void loadPdf()
        {
            if (!File.Exists(filePath)) return;

            _pages.Clear();

            await LoadPdfAsync(filePath);

            // prepare low-quality thumbnails at a reasonable DPI for display
            const double pointToDip = 96.0 / 72.0; // convert PDF points to DIP
            int lowDpi = (int)(96 * lodZoom * pointToDip);

            for (int i = 0; i < _document.PageCount; i++)
            {
                using (var lowResImage = _document.Render(i, lowDpi, lowDpi, false))
                {
                    byte[] imageBytes;
                    using (var ms = new MemoryStream())
                    {
                        lowResImage.Save(ms, ImageFormat.Png);
                        imageBytes = ms.ToArray();
                    }

                    var bitmap = await ConvertToBitmapImage(imageBytes);
                    if (bitmap != null)
                        _lowQualityCache[i] = bitmap;
                }
            }

            UpdateVisiblePages();

            if (_document.PageCount > 0)
            {
                var pageSize = _document.PageSizes[0];
                pageHeight = pageSize.Height; // points
                pageWidth = pageSize.Width;

                // convert to DIP for XAML layout
                pageWidthDip = pageWidth * pointToDip;
                pageHeightDip = pageHeight * pointToDip;

                PdfCanvas.Width = pageWidthDip;
                PdfCanvas.Height = pageHeightDip * _document.PageCount;
            }

            
        }

        private bool _isUpdatingVisiblePages = false;

        private void PdfListView_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // Prevent re-entrancy which can cause infinite loops
            if (_isUpdatingVisiblePages) return;

            // Cancel any in-flight high-quality renders when user is interacting
            if (e.IsIntermediate)
            {
                // user is actively scrolling/zooming -> cancel ongoing high-quality renders and debounce
                CancelAndReplaceCts();
                _renderTimer.Stop();
                _renderTimer.Start();
            }
            else
            {
                // interaction finished -> cancel previous renders and immediately render high-quality
                CancelAndReplaceCts();
                _renderTimer.Stop();
                _isUpdatingVisiblePages = true;
                try
                {
                    UpdateVisiblePages(true);
                }
                finally
                {
                    _isUpdatingVisiblePages = false;
                }
            }
            //UpdateVisiblePages();
            //Debug.WriteLine(PdfListView.VerticalOffset);
            //Debug.WriteLine(PdfListView.ZoomFactor);
        }

        private void CancelAndReplaceCts()
        {
            try
            {
                _renderCts?.Cancel();
            }
            catch { }
            try { _renderCts?.Dispose(); } catch { }
            _renderCts = new CancellationTokenSource();
        }

        private async void UpdateVisiblePages(bool highQuality = false)
        {
            var scrollViewer = PdfListView;
            if (scrollViewer == null || pageHeightDip <= 0) return;

            var verticalOffset = scrollViewer.VerticalOffset;
            var zoomFactor = scrollViewer.ZoomFactor;

            // Avoid division by zero or invalid calculations
            if (zoomFactor <= 0) zoomFactor = 1.0f;

            int startPage = (int)(verticalOffset / zoomFactor / pageHeightDip);
            int endPage = startPage + (int)(scrollViewer.ViewportHeight / zoomFactor / pageHeightDip) + 2; // +2 buffer

            // Clamp to valid page range
            if (startPage < 0) startPage = 0;
            if (endPage > _document.PageCount) endPage = _document.PageCount;

            // Avoid unnecessary updates when not changing and not requesting high quality
            if (startPage == _visiblePageStart && endPage == _visiblePageEnd && !highQuality) return;

            Debug.WriteLine($"更新可见页：{startPage} - {endPage}");

            _visiblePageStart = startPage;
            _visiblePageEnd = endPage;

            // Remove images that are no longer visible
            for (int i = PdfCanvas.Children.Count - 1; i >= 0; i--)
            {
                var canvasImg = PdfCanvas.Children[i] as Microsoft.UI.Xaml.Controls.Image;
                
                if (canvasImg == null) continue;

                int imgPage = (int)canvasImg.Tag;

                if (imgPage < startPage || imgPage >= endPage)
                {
                    PdfCanvas.Children.RemoveAt(i);
                    canvasImg.Source = null; // Release image resources
                    _recycledImages.Add(canvasImg);
                }
            }

            // Add/update visible page images
            for (int page = startPage; page < endPage; page++)
            {
                if (page >= _document.PageCount) break;

                bool alreadyLoaded = false;

                foreach (var child in PdfCanvas.Children)
                {
                    var childImg = child as Microsoft.UI.Xaml.Controls.Image;

                    if (childImg != null && (int)childImg.Tag == page)
                    {
                        alreadyLoaded = true;
                        if (highQuality)
                            _ = LoadImageForPage(childImg, page, true);
                        
                        break;
                    }
                }

                if (alreadyLoaded) continue;
                Debug.WriteLine("渲染");

                var newImg = GetOrCreateImage();

                newImg.Tag = page;
                newImg.Width = pageWidthDip;
                newImg.Height = pageHeightDip;

                Canvas.SetLeft(newImg, 0);
                Canvas.SetTop(newImg, page * pageHeightDip);

                PdfCanvas.Children.Add(newImg);

                _ = LoadImageForPage(newImg, page, highQuality);
            }
        }

        private async Task LoadImageForPage(Microsoft.UI.Xaml.Controls.Image img, int pageIndex, bool highQuality)
        {
            Debug.WriteLine(highQuality);
            var ct = _renderCts?.Token ?? CancellationToken.None;

            BitmapImage bitmap = null;

            if (highQuality)
            {
                try
                {
                    bitmap = await RenderPageToBitmapImage(pageIndex, PdfListView.ZoomFactor, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            else
            {
                _lowQualityCache.TryGetValue(pageIndex, out bitmap);
            }

            if (bitmap != null && !(ct.IsCancellationRequested))
            {
                // Ensure the image still represents the same page (avoid race)
                if (img.Tag is int tag && tag == pageIndex)
                {
                    img.Source = bitmap;
                }
            }
        }


        private async void OpenBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(filePath)) return;

            _pages.Clear();

            await LoadPdfAsync(filePath);

            UpdateVisiblePages();

            if (_document.PageCount > 0)
            {
                var pageSize = _document.PageSizes[0];
                pageHeight = pageSize.Height;
                pageWidth = pageSize.Width;

                const double pointToDip = 96.0 / 72.0;
                pageWidthDip = pageWidth * pointToDip;
                pageHeightDip = pageHeight * pointToDip;

                PdfCanvas.Width = pageWidthDip;
                PdfCanvas.Height = pageHeightDip * _document.PageCount;
            }

            //await Task.Run(async () =>
            //{
            //    _document = PdfDocument.Load(filePath);

            //    int pageCount = Math.Min(_document.PageCount, 10);

            //    for (int i = 0; i < pageCount; i++)
            //    {
            //        var bitmapImage = await RenderPageToBitmapImage(i);

            //        DispatcherQueue.TryEnqueue(() =>
            //        {
            //            _pages.Add(new PdfPageItem { PageImage = bitmapImage });
            //        });
            //    }
            //});
        }

        public async Task LoadPdfAsync(string path)
        {
            // ... 打开文档的代码 ...
            _document = PdfDocument.Load(path);

            //var tasks = new List<Task>();

            //for (int i = 0; i < _document.PageCount; i++)
            //{
            //    // 1. 渲染图片 (在 UI 线程)
            //    var img = await RenderPageToBitmapImage(i);

            //    // 2. 如果渲染成功，添加到列表
            //    if (img != null)
            //    {
            //        _pages.Add(new PdfPageItem { PageImage = img });
            //    }

            //    // 3. 关键：交出控制权，让 UI 线程有机会刷新界面
            //    // 这样列表会一边加载一边显示，而不是卡住直到全部加载完
            //    await Task.Yield();
            //}
            pageHeight = _document.PageSizes[0].Height;
            pageWidth = _document.PageSizes[0].Width;
        }

        private async Task<BitmapImage> RenderPageToBitmapImage(int pageIndex, double currentZoom = 1.0, CancellationToken ct = default)
        {
            try
            {
                int targetDpi = (int)(96 * currentZoom * 1.2);

                int dpiX = Math.Min(targetDpi, maxDpi);
                int dpiY = dpiX;
                Debug.WriteLine($"DPI: {dpiX}，{dpiY}");

                await _semaphore.WaitAsync(ct);
                byte[] imageBytes;
                try
                {
                    // Render and encode to PNG on a background thread to avoid blocking UI
                    imageBytes = await Task.Run(() =>
                    {
                        ct.ThrowIfCancellationRequested();
                        using (var bmp = _document.Render(pageIndex, dpiX, dpiY, true))
                        {
                            using (var ms = new MemoryStream())
                            {
                                bmp.Save(ms, ImageFormat.Png);
                                return ms.ToArray();
                            }
                        }
                    }, ct);
                }
                finally
                {
                    _semaphore.Release();
                }

                ct.ThrowIfCancellationRequested();

                // Convert to WinUI image on the caller's context
                return await ConvertToBitmapImage(imageBytes);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                System.Diagnostics.Debug.WriteLine($"渲染第 {pageIndex} 页失败: 0x{ex.HResult:X} - {ex.Message}");
                return null;
            }
        }

        // 2. 转换方法：接收 byte[]，并正确使用 await
        private async Task<BitmapImage> ConvertToBitmapImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;

            var bitmap = new BitmapImage();

            try
            {
                // 1. 创建流，不要用 using
                var stream = new InMemoryRandomAccessStream();

                // 2. 写入数据
                await stream.WriteAsync(imageData.AsBuffer());
                stream.Seek(0);

                // 3. 设置源
                await bitmap.SetSourceAsync(stream);

                // 流现在被 bitmap 持有，不要在这里关闭 stream
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转换图片失败: 0x{ex.HResult:X} - {ex.Message}");
                return null;
            }

            return bitmap;
        }

        private Microsoft.UI.Xaml.Controls.Image GetOrCreateImage()
        {
            if (_recycledImages.Count > 0)
            {
                var img = _recycledImages[0];
                _recycledImages.RemoveAt(0);
                // Use DIP values for consistent sizing
                img.Width = pageWidthDip;
                img.Height = pageHeightDip;
                return img;
            }

            return new Microsoft.UI.Xaml.Controls.Image
            {
                Width = pageWidthDip,
                Height = pageHeightDip
            };
        }

    }
}
