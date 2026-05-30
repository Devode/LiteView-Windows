using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Models
{
    public class PdfPageViewModel : INotifyPropertyChanged
    {
        private BitmapImage _pageImage;
        private bool _isLoading;

        public BitmapImage PageImage
        {
            get => _pageImage;
            set { _pageImage = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public double dpi { get; set; }

        // 宽度
        public double PageWidth { get; set; }
        // 宽度
        public double PageHeight { get; set; }
        // 宽高比
        public double AspectRatio => PageWidth / PageHeight;

        // 到文档顶部的累计距离
        public double DocumentTop { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
