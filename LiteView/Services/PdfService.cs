using LiteView.Contracts;
using LiteView.Native;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace LiteView.Services
{
    public class PdfService : IPdfService
    {
        private readonly IPdfRenderService _renderService;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private PdfDocument? _pdfDocument;
        private IReadOnlyList<Size> _pageSizes = Array.Empty<Size>();

        public PdfService(IPdfRenderService renderService)
        {
            _renderService = renderService;
        }

        public string? PdfPath { get; private set; }
        public int PageCount => _pdfDocument?.PageCount ?? 0;
        public bool IsUnloaded => _pdfDocument is null;
        public IReadOnlyList<Size> PageSizes => _pageSizes;

        public event EventHandler? DocumentChanged;

        public async Task LoadAsync(string filePath, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ct.ThrowIfCancellationRequested();

            // 锁外加载
            var (newDoc, sizes) = await Task.Run(() =>
            {
                var d = PdfDocument.Load(filePath);
                var s = d.PageSizes.Select(x => new Size(x.Width, x.Height)).ToArray();
                return (d, s);
            }, ct).ConfigureAwait(false);

            PdfDocument? old;
            await _gate.WaitAsync(ct);
            try
            {
                old = _pdfDocument;
                _pdfDocument = newDoc;
                _pdfDocument = PdfDocument.Load(filePath);
                _pageSizes = sizes;
                PdfPath = filePath;
            }
            finally
            {
                _gate.Release();
            }
            old?.Dispose();

            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task ClosePdf()
        {
            PdfDocument? old;
            await _gate.WaitAsync();
            try
            {
                old = _pdfDocument;
                _pdfDocument = null;
                _pageSizes = Array.Empty<Size>();
                PdfPath = null;
            }
            finally
            {
                _gate.Release();
            }
            old?.Dispose();

            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<RawBitmapData> RenderFullPageAsync(int pageIndex, double dpi, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var doc = _pdfDocument
                    ?? throw new InvalidOperationException("No PDF document is loaded.");
                var path = PdfPath
                    ?? throw new InvalidOperationException("PDF path is not set.");

                return await _renderService
                    .RenderFullPage(doc, path, pageIndex, dpi, ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<RawBitmapData> RenderRegionAsync(int pageIndex, Rect cropRect, double dpi, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var doc = _pdfDocument
                    ?? throw new InvalidOperationException("No PDF document is loaded.");
                var path = PdfPath
                    ?? throw new InvalidOperationException("PDF path is not set.");

                return await _renderService
                    .RenderRegion(doc, path, pageIndex, cropRect, dpi, ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

    }
}
