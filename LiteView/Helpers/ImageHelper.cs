using CommunityToolkit.WinUI;
using LiteView.Native;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace LiteView.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// 将 System.Drawing.Image 转换为 IRandomAccessStream
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static async Task<BitmapImage> ConvertToBitmapImage(System.Drawing.Image image)
        {
            //var memoryStream = new InMemoryRandomAccessStream();
            
            //image.Save(memoryStream.AsStream(), System.Drawing.Imaging.ImageFormat.Png);
            
            //memoryStream.Seek(0);
            //return memoryStream;
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

                //var bitmapImage = new BitmapImage();
                //bitmapImage.DecodePixelType = DecodePixelType.Logical;
                //await bitmapImage.SetSourceAsync(randomAccessStream);
                //return bitmapImage;

            }
        }

        /// <summary>
        /// 组装 Bitamp
        /// </summary>
        /// <param name="rawBitmapData"></param>
        /// <returns></returns>
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
