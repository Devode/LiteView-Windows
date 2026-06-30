using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
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

            // 目标 Bitmap 尺寸
            int bitmaplW = Math.Max(1, (int)(cropRect.Width * scale));
            int bitmapH = Math.Max(1, (int)(cropRect.Height * scale));

            using var doc = PdfDocumentHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadDocument(filePath, IntPtr.Zero));
            if (doc.IsInvalid) throw new InvalidOperationException($"无法打开 PDF: {filePath}");

            using var page = PdfPageHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadPage(doc.DangerousGetHandle(), pageIndex));
            if (page.IsInvalid) throw new InvalidOperationException($"无法加载第 {pageIndex} 页");

            // 获取整个页面的原始尺寸 (PDF 点)
            double pageWidth = PdfiumNative.FPDF_GetPageWidth(page.DangerousGetHandle());
            double pageHeight = PdfiumNative.FPDF_GetPageHeight(page.DangerousGetHandle());

            // 计算“虚拟”的整页渲染尺寸
            int fullPageRenderW = Math.Max(1, (int)(pageWidth * scale));
            int fullPageRenderH = Math.Max(1, (int)(pageHeight * scale));

            // 计算偏移量：必须是负数，把页面往左上角推，让 cropRect 刚好落在 (0,0)
            int startX = -(int)(cropRect.X *  scale);
            int startY = -(int)(cropRect.Y * scale);

            using var bitmap = PdfBitmapHandle.FromIntPtr(
                PdfiumNative.FPDFBitmap_CreateEx(bitmaplW, bitmapH, PdfiumNative.FPDF_BITMAP_BGRA, IntPtr.Zero, bitmaplW * 4));
            if (bitmap.IsInvalid) throw new OutOfMemoryException("无法创建渲染位图");

            PdfiumNative.FPDFBitmap_FillRect(bitmap.DangerousGetHandle(), 0, 0, bitmaplW, bitmapH, 0xFFFFFFFF);

            PdfiumNative.FPDF_RenderPageBitmap(
                bitmap.DangerousGetHandle(), page.DangerousGetHandle(),
                startX, startY, fullPageRenderW, fullPageRenderH,
                0, PdfiumNative.FPDF_ANNOT | PdfiumNative.FPDF_LCD_TEXT);

            //var wb = new WriteableBitmap(bitmaplW, bitmapH);
            IntPtr buffer = PdfiumNative.FPDFBitmap_GetBuffer(bitmap.DangerousGetHandle());

            byte[] pixels = new byte[bitmaplW * bitmapH * 4];
            Marshal.Copy(buffer, pixels, 0, pixels.Length);
            //using var stream = wb.PixelBuffer.AsStream();
            //stream.Write(pixels, 0, pixels.Length);

            return new RawBitmapData
            {
                Pixels = pixels,
                Width = bitmaplW,
                Height = bitmapH
            };
        }

        public static RawBitmapData RenderFullPage(string filePath, int pageIndex, int renderWidth, int renderHeight, double dpi = 300.0)
        {
            PdfiumBootstrap.Initialize();

            float scale = (float)(dpi / 72.0);

            using var doc = PdfDocumentHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadDocument(filePath, IntPtr.Zero));
            if (doc.IsInvalid) throw new InvalidOperationException($"无法打开 PDF: {filePath}");

            using var page = PdfPageHandle.FromIntPtr(
                PdfiumNative.FPDF_LoadPage(doc.DangerousGetHandle(), pageIndex));
            if (page.IsInvalid) throw new InvalidOperationException($"无法加载第 {pageIndex} 页");

            using var bitmap = PdfBitmapHandle.FromIntPtr(
                PdfiumNative.FPDFBitmap_CreateEx(renderWidth, renderHeight, PdfiumNative.FPDF_BITMAP_BGRA, IntPtr.Zero, renderWidth * 4));
            if (bitmap.IsInvalid) throw new OutOfMemoryException("无法创建渲染位图");

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
