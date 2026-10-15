using CommunityToolkit.Mvvm.ComponentModel;
using LiteView.Contracts;
using LiteView.Helpers;
using LiteView.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Models
{
    public partial class PageItem : ObservableObject
    {
        [ObservableProperty]
        private WriteableBitmap? _pageImage;

        public int PageIndex { get; set; }
        public int PageNumber => PageIndex + 1;

        /// <summary>Page width in PDF points (1/72 inch).</summary>
        public double PageWidth { get; set; }

        /// <summary>Page height in PDF points (1/72 inch).</summary>
        public double PageHeight { get; set; }

        public async Task Render()
        {
            if (_pageImage != null) return;

            var pdfService = App.Host!.Services.GetRequiredService<IPdfService>();

            var rawBitmapData = await pdfService.RenderFullPageAsync(PageIndex, 32);
                //Native.PdfRenderer.RenderFullPage(PdfPath, PageIndex, 32);
            var bitmap = await ImageHelper.AssembleBitmapAsync(rawBitmapData);

            PageImage = bitmap;
        }
    }
}
