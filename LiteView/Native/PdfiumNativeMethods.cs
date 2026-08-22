using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace LiteView.Native;

#region Safe handles

/// <summary>Safe handle for a PDFium document pointer.</summary>
public sealed class PdfDocumentHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal PdfDocumentHandle() : base(true) { }
    internal static PdfDocumentHandle FromIntPtr(IntPtr ptr)
    {
        var handle = new PdfDocumentHandle();
        if (ptr != IntPtr.Zero) 
            handle.SetHandle(ptr);
        return handle;
    }
    protected override bool ReleaseHandle()
    {
        PdfiumNative.FPDF_CloseDocument(handle);
        return true;
    }
}

/// <summary>Safe handle for a PDFium page pointer.</summary>
public sealed class PdfPageHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal PdfPageHandle() : base(true) { }
    
    internal static PdfPageHandle FromIntPtr(IntPtr ptr)
    {
        var handle = new PdfPageHandle();
        if (ptr != IntPtr.Zero)
            handle.SetHandle(ptr);
        return handle;
    }

    protected override bool ReleaseHandle()
    {
        PdfiumNative.FPDF_ClosePage(handle);
        return true;
    }
}

/// <summary>Safe handle for a PDFium bitmap pointer.</summary>
public sealed class PdfBitmapHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal PdfBitmapHandle() : base(true) { }

    internal static PdfBitmapHandle FromIntPtr(IntPtr ptr)
    {
        var handle = new PdfBitmapHandle();
        if (ptr != IntPtr.Zero)
            handle.SetHandle(ptr);
        return handle;
    }

    protected override bool ReleaseHandle()
    {
        PdfiumNative.FPDFBitmap_Destroy(handle);
        return true;
    }
}

#endregion

#region P/Invoke declarations

internal static partial class PdfiumNative
{
    private const string DllName = "pdfium.dll";
    private const CallingConvention Cdecl = CallingConvention.Cdecl;

    // Engine lifecycle
    [LibraryImport(DllName, EntryPoint = "FPDF_InitLibrary")]
    public static partial void FPDF_InitLibrary();

    [LibraryImport(DllName, EntryPoint = "FPDF_DestroyLibrary")]
    public static partial void FPDF_DestroyLibrary();

    // Document operations
    [LibraryImport(DllName, EntryPoint = "FPDF_LoadDocument", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr FPDF_LoadDocument(string file_path, IntPtr password);

    [LibraryImport(DllName, EntryPoint = "FPDF_CloseDocument")]
    public static partial void FPDF_CloseDocument(IntPtr document);

    // Page operations
    [LibraryImport(DllName, EntryPoint = "FPDF_LoadPage")]
    public static partial IntPtr FPDF_LoadPage(IntPtr document, int page_index);

    [LibraryImport(DllName, EntryPoint = "FPDF_ClosePage")]
    public static partial void FPDF_ClosePage(IntPtr page);

    [LibraryImport(DllName, EntryPoint = "FPDF_GetPageWidth")]
    public static partial double FPDF_GetPageWidth(IntPtr page);

    [LibraryImport(DllName, EntryPoint = "FPDF_GetPageHeight")]
    public static partial double FPDF_GetPageHeight(IntPtr page);

    // Bitmap and rendering
    [LibraryImport(DllName, EntryPoint = "FPDFBitmap_CreateEx")]
    public static partial IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr first_scan, int stride);

    [LibraryImport(DllName, EntryPoint = "FPDFBitmap_FillRect")]
    public static partial void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [LibraryImport(DllName, EntryPoint = "FPDF_RenderPageBitmap")]
    public static partial void FPDF_RenderPageBitmap(
        IntPtr bitmap, IntPtr page,
        int start_x, int start_y, int size_x, int size_y,
        int rotate, int flags);

    [LibraryImport(DllName, EntryPoint = "FPDFBitmap_GetBuffer")]
    public static partial IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);

    [LibraryImport(DllName, EntryPoint = "FPDFBitmap_Destroy")]
    public static partial void FPDFBitmap_Destroy(IntPtr bitmap);

    public const int FPDF_BITMAP_BGRA = 4;
    public const int FPDF_ANNOT = 0x01;
    public const int FPDF_LCD_TEXT = 0x02;
    public const int FPDF_NO_NATIVETEXT = 0x04;
}

#endregion

#region Bootstrap

/// <summary>
/// Ensures PDFium is initialized exactly once; cleans up on process exit.
/// </summary>
public static class PdfiumBootstrap
{
    private static readonly object _lock = new();
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            PdfiumNative.FPDF_InitLibrary();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => PdfiumNative.FPDF_DestroyLibrary();
            _initialized = true;
        }
    }
}

#endregion
