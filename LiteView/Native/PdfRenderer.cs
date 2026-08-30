using System;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace LiteView.Native
{
    public static class PdfRenderer
    {
        public static RawBitmapData RenderRegion(
            string filePath, 
            int pageIndex,
            Rect cropRect,
            double dpi = 96.0)
        {
            PdfiumBootstrap.Initialize();

            float scale = (float)(dpi / 72.0);

            int bitmapW = Math.Max(1, (int)(cropRect.Width * scale));
            int bitmapH = Math.Max(1, (int)(cropRect.Height * scale));

            using var doc = PdfDocumentHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadDocument(filePath, IntPtr.Zero));
            if (doc.IsInvalid) throw new InvalidOperationException($"Cannot open PDF: {filePath}");

            using var page = PdfPageHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadPage(doc.DangerousGetHandle(), pageIndex));
            if (page.IsInvalid) throw new InvalidOperationException($"Cannot load page {pageIndex}");

            double pageWidth = PdfiumNative.FPDF_GetPageWidth(page.DangerousGetHandle());
            double pageHeight = PdfiumNative.FPDF_GetPageHeight(page.DangerousGetHandle());

            int fullPageRenderW = Math.Max(1, (int)(pageWidth * scale));
            int fullPageRenderH = Math.Max(1, (int)(pageHeight * scale));

            // Negative offsets shift the page so cropRect aligns to (0,0)
            int startX = -(int)(cropRect.X * scale);
            int startY = -(int)(cropRect.Y * scale);

            using var bitmap = PdfBitmapHandle.FromIntPtr(
                PdfiumNative.FPDFBitmap_CreateEx(bitmapW, bitmapH, PdfiumNative.FPDF_BITMAP_BGRA, IntPtr.Zero, bitmapW * 4));
            if (bitmap.IsInvalid) throw new OutOfMemoryException("Cannot create render bitmap");

            PdfiumNative.FPDFBitmap_FillRect(bitmap.DangerousGetHandle(), 0, 0, bitmapW, bitmapH, 0xFFFFFFFF);

            PdfiumNative.FPDF_RenderPageBitmap(
                bitmap.DangerousGetHandle(), page.DangerousGetHandle(),
                startX, startY, fullPageRenderW, fullPageRenderH,
                0, PdfiumNative.FPDF_ANNOT | PdfiumNative.FPDF_LCD_TEXT);

            IntPtr buffer = PdfiumNative.FPDFBitmap_GetBuffer(bitmap.DangerousGetHandle());

            byte[] pixels = new byte[bitmapW * bitmapH * 4];
            Marshal.Copy(buffer, pixels, 0, pixels.Length);

            return new RawBitmapData
            {
                Pixels = pixels,
                Width = bitmapW,
                Height = bitmapH
            };
        }

        public static RawBitmapData RenderFullPage(string filePath, int pageIndex, int renderWidth, int renderHeight, double dpi = 300.0)
        {
            PdfiumBootstrap.Initialize();

            float scale = (float)(dpi / 72.0);

            using var doc = PdfDocumentHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadDocument(filePath, IntPtr.Zero));
            if (doc.IsInvalid) throw new InvalidOperationException($"Cannot open PDF: {filePath}");

            using var page = PdfPageHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadPage(doc.DangerousGetHandle(), pageIndex));
            if (page.IsInvalid) throw new InvalidOperationException($"Cannot load page {pageIndex}");

            using var bitmap = PdfBitmapHandle.FromIntPtr(
                PdfiumNative.FPDFBitmap_CreateEx(renderWidth, renderHeight, PdfiumNative.FPDF_BITMAP_BGRA, IntPtr.Zero, renderWidth * 4));
            if (bitmap.IsInvalid) throw new OutOfMemoryException("Cannot create render bitmap");

            PdfiumNative.FPDFBitmap_FillRect(bitmap.DangerousGetHandle(), 0, 0, renderWidth, renderHeight, 0xFFFFFFFF);
            PdfiumNative.FPDF_RenderPageBitmap(
                bitmap.DangerousGetHandle(), page.DangerousGetHandle(),
                0, 0, renderWidth, renderHeight,
                0, PdfiumNative.FPDF_ANNOT | PdfiumNative.FPDF_LCD_TEXT);

            IntPtr buffer = PdfiumNative.FPDFBitmap_GetBuffer(bitmap.DangerousGetHandle());
            byte[] pixels = new byte[renderWidth * renderHeight * 4];
            Marshal.Copy(buffer, pixels, 0, pixels.Length);

            return new RawBitmapData { 
                Pixels = pixels, 
                Width = renderWidth, 
                Height = renderHeight 
            };
        }
    }

    public struct RawBitmapData
    {
        public byte[] Pixels;
        public int Width;
        public int Height;
    }
}
