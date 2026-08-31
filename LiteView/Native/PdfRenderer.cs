using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace LiteView.Native
{
    public static class PdfRenderer
    {
        public const int MAX_PIXEL_WIDTH = 16384;

        public static RawBitmapData RenderRegion(
            string filePath, 
            int pageIndex,
            Rect cropRect,
            double dpi = 96.0f)
        {
            PdfiumBootstrap.Initialize();

            float scale = (float)(dpi / 72f);
            int renderWidth = Math.Min((int)Math.Round(cropRect.Width * scale), MAX_PIXEL_WIDTH);
            float actualScale = (float)(renderWidth / cropRect.Width);
            Debug.WriteLine($"scale: {scale}, actualScale: {actualScale}");

            int bitmapW = Math.Max(1, (int)(cropRect.Width * actualScale));
            int bitmapH = Math.Max(1, (int)(cropRect.Height * actualScale));

            using var doc = PdfDocumentHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadDocument(filePath, IntPtr.Zero));
            if (doc.IsInvalid) throw new InvalidOperationException($"Cannot open PDF: {filePath}");

            using var page = PdfPageHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadPage(doc.DangerousGetHandle(), pageIndex));
            if (page.IsInvalid) throw new InvalidOperationException($"Cannot load page {pageIndex}");

            double pageWidth = PdfiumNative.FPDF_GetPageWidth(page.DangerousGetHandle());
            double pageHeight = PdfiumNative.FPDF_GetPageHeight(page.DangerousGetHandle());

            int fullPageRenderW = Math.Max(1, (int)(pageWidth * actualScale));
            int fullPageRenderH = Math.Max(1, (int)(pageHeight * actualScale));

            // Negative offsets shift the page so cropRect aligns to (0,0)
            int startX = -(int)(cropRect.X * actualScale);
            int startY = -(int)(cropRect.Y * actualScale);

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

        public static RawBitmapData RenderFullPage(string filePath, int pageIndex, double dpi = 300.0)
        {
            PdfiumBootstrap.Initialize();

            using var doc = PdfDocumentHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadDocument(filePath, IntPtr.Zero));
            if (doc.IsInvalid) throw new InvalidOperationException($"Cannot open PDF: {filePath}");

            using var page = PdfPageHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadPage(doc.DangerousGetHandle(), pageIndex));
            if (page.IsInvalid) throw new InvalidOperationException($"Cannot load page {pageIndex}");


            double pageWidth = PdfiumNative.FPDF_GetPageWidth(page.DangerousGetHandle());
            double pageHeight = PdfiumNative.FPDF_GetPageHeight(page.DangerousGetHandle());
            //Debug.WriteLine($"pageSize: {pageWidth}x{pageHeight}, _pageSize: {_pageWidth}x{_pageHeight}");

            float scale = (float)(dpi / 72.0);
            int renderWidth = Math.Min((int)Math.Round(pageWidth * scale), MAX_PIXEL_WIDTH);
            float actualScale = (float)(renderWidth / pageWidth);
            Debug.WriteLine($"fullpage scale: {scale}, actualScale: {actualScale}");

            int fullPageRenderW = Math.Max(1, (int)(pageWidth * actualScale));
            int fullPageRenderH = Math.Max(1, (int)(pageHeight * actualScale));


            using var bitmap = PdfBitmapHandle.FromIntPtr(
                PdfiumNative.FPDFBitmap_CreateEx(fullPageRenderW, fullPageRenderH, PdfiumNative.FPDF_BITMAP_BGRA, IntPtr.Zero, fullPageRenderW * 4));
            if (bitmap.IsInvalid) throw new OutOfMemoryException("Cannot create render bitmap");

            PdfiumNative.FPDFBitmap_FillRect(bitmap.DangerousGetHandle(), 0, 0, fullPageRenderW, fullPageRenderH, 0xFFFFFFFF);
            PdfiumNative.FPDF_RenderPageBitmap(
                bitmap.DangerousGetHandle(), page.DangerousGetHandle(),
                0, 0, fullPageRenderW, fullPageRenderH,
                0, PdfiumNative.FPDF_ANNOT | PdfiumNative.FPDF_LCD_TEXT);

            IntPtr buffer = PdfiumNative.FPDFBitmap_GetBuffer(bitmap.DangerousGetHandle());
            byte[] pixels = new byte[fullPageRenderW * fullPageRenderH * 4];
            Marshal.Copy(buffer, pixels, 0, pixels.Length);

            return new RawBitmapData { 
                Pixels = pixels, 
                Width = fullPageRenderW, 
                Height = fullPageRenderH
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
