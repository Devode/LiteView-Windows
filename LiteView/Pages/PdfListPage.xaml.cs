using LiteView.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PdfListPage : Page
    {
        public ObservableCollection<PdfItem> PdfList { get; set; } = new();

        public PdfListPage()
        {
            InitializeComponent();

            // 模拟数据
            PdfList.Add(new PdfItem { FileName = "maths_book.pdf", ModifyTime = "2025-12-01 21:53", FilePath = "C:\\Users\\lenovo\\Documents\\maths_book.pdf" });
            PdfList.Add(new PdfItem { FileName = "演示 PDF.pdf", ModifyTime = "2026-01-23 20:07", FilePath = "C:\\Users\\lenovo\\Documents\\演示 PDF.pdf" });
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            Debug.WriteLine("Item clicked: " + (e.ClickedItem as PdfItem)?.FileName);
            var grid = sender as Grid;
            var selectedItem = e.ClickedItem as PdfItem;

            if (selectedItem != null)
            {
                this.Frame.Navigate(typeof(PdfViewerPage), selectedItem);
            }
        }

        private void PdfCard_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            var pdf = fe?.DataContext as PdfItem;
            if (pdf != null)
            {
                this.Frame.Navigate(typeof(PdfViewerPage), pdf);
            }
        }
    }
}
