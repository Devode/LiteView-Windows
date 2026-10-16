using LiteView.Native;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace LiteView.Contracts
{
    public interface IPdfRenderService
    {
        // ===== Functions =====
        Task<RawBitmapData> RenderFullPage(
            PdfDocument document, 
            string pdfPath, 
            int pageIndex, 
            double dpi, 
            CancellationToken ct = default);

        Task<RawBitmapData> RenderRegion(
            PdfDocument document, 
            string pdfPath, 
            int pageIndex, 
            Rect cropRect, double dpi, 
            CancellationToken ct = default);
    }
}
