using CommunityToolkit.WinUI;
using LiteView.Native;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace LiteView.Helpers
{
    /// <summary>
    /// Utility methods for converting between image formats and assembling
    /// raw PDFium pixel data into WinUI <see cref="WriteableBitmap"/> objects.
    /// </summary>
    public static class ImageHelper
    {
        /// <summary>
        /// Convert a System.Drawing.Image (GDI+) to a WinUI BitmapImage,
        /// marshalling through an in-memory stream. Handles thread affinity
        /// by dispatching to the UI thread if needed.
        /// </summary>
        public static async Task<BitmapImage> ConvertToBitmapImage(System.Drawing.Image image)
        {
            using (var memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                memoryStream.Seek(0, SeekOrigin.Begin);

                var randomAccessStream = new InMemoryRandomAccessStream();
                using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
                {
                    await RandomAccessStream.CopyAsync(
                        memoryStream.AsRandomAccessStream(),
                        outputStream
                    );
                    await outputStream.FlushAsync();
                }

                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();

                if (dispatcherQueue.HasThreadAccess)
                {
                    return await CreateBitmapAsync(randomAccessStream);
                }
                else
                {
                    return await dispatcherQueue.EnqueueAsync(async () =>
                    {
                        return await CreateBitmapAsync(randomAccessStream);
                    });
                }
            }
        }

        /// <summary>
        /// Assemble a WriteableBitmap from raw BGRA pixel data.
        /// </summary>
        public static async Task<WriteableBitmap> AssembleBitmapAsync(RawBitmapData rawBitmapData)
        {
            var bitmap = new WriteableBitmap(rawBitmapData.Width, rawBitmapData.Height);
            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                await stream.WriteAsync(rawBitmapData.Pixels, 0, rawBitmapData.Pixels.Length);
            }
            bitmap.Invalidate();

            return bitmap;
        }

        private static async Task<BitmapImage> CreateBitmapAsync(IRandomAccessStream stream) {
            var bitmapImage = new BitmapImage();
            bitmapImage.DecodePixelType = DecodePixelType.Logical;
            await bitmapImage.SetSourceAsync(stream);
            return bitmapImage;
        }
    }
}
