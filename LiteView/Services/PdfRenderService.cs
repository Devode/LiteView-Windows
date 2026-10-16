using LiteView.Contracts;
using LiteView.Native;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace LiteView.Services
{
    public class PdfRenderService : IPdfRenderService
    {
        private readonly ConditionalWeakTable<PdfDocument, SemaphoreSlim> _documentLocks = new();

        public Task<RawBitmapData> RenderFullPage(PdfDocument document, string pdfPath, int pageIndex, double dpi, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            return RenderSerializedAsync(
                document,
                () => Native.PdfRenderer.RenderFullPage(pdfPath, pageIndex, dpi),
                ct);
        }

        public Task<RawBitmapData> RenderRegion(PdfDocument document, string pdfPath, int pageIndex, Rect cropRect, double dpi, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            return RenderSerializedAsync(
                document,
                () => Native.PdfRenderer.RenderRegion(pdfPath, pageIndex, cropRect, dpi),
                ct);
        }

        /// <summary>
        /// Serializes the render against the given document and runs the
        /// synchronous PDFium call on the thread pool so it never blocks the
        /// caller's thread (typically the UI thread).
        /// </summary>
        private async Task<RawBitmapData> RenderSerializedAsync(
            PdfDocument document,
            Func<RawBitmapData> render,
            CancellationToken ct)
        {
            var gate = _documentLocks.GetValue(document, _ => new SemaphoreSlim(1, 1));

            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();

                return await Task.Run(render, ct).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
