using LiteView.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace LiteView.Contracts
{
    public interface IPdfService
    {
        public string? PdfPath { get; }
        public int PageCount { get; }
        public IReadOnlyList<Size> PageSizes { get; }
        public bool IsUnloaded { get; }

        public event EventHandler? DocumentChanged;

        Task LoadAsync(string filePath, CancellationToken ct = default);
        Task ClosePdf();

        Task<RawBitmapData> RenderFullPageAsync(int pageIndex, double dpi, CancellationToken ct = default);
        Task<RawBitmapData> RenderRegionAsync(int pageIndex, Rect cropRect, double dpi, CancellationToken ct = default);
    }
}
