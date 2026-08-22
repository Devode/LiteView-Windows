using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LiteView.Models
{
    /// <summary>
    /// View model for a single rendered PDF page inside <see cref="Controls.PdfViewerControl"/>.
    /// Drives the virtualized ItemsRepeater — only pages within or near the viewport get a <see cref="PageImage"/>.
    /// </summary>
    public class PdfPageViewModel : INotifyPropertyChanged
    {
        private WriteableBitmap _pageImage;
        private bool _isLoading;

        /// <summary>
        /// The rendered bitmap for this page. Null until the page enters the viewport and is rasterized.
        /// </summary>
        public WriteableBitmap PageImage
        {
            get => _pageImage;
            set { _pageImage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// True while the page is being rasterized. Bind to a progress indicator in XAML.
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        /// <summary>DPI used when this page was last rendered.</summary>
        public double dpi { get; set; }

        /// <summary>Page width in PDF points (1/72 inch).</summary>
        public double PageWidth { get; set; }

        /// <summary>Page height in PDF points (1/72 inch).</summary>
        public double PageHeight { get; set; }

        /// <summary>Width divided by height.</summary>
        public double AspectRatio => PageWidth / PageHeight;

        /// <summary>
        /// Cumulative distance from the top of the document to the top of this page,
        /// including the 10-point gap between pages. Used for scroll offset calculations.
        /// </summary>
        public double DocumentTop { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
