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
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView.Pages
{
    //public delegate void PdfListChangedHandler(object sender, ObservableCollection<PdfItem> pdfList);

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PdfListPage : Page
    {
        public ObservableCollection<PdfItem> PdfList => App.CurrentApp.PdfService.PdfList;

        //public event PdfListChangedHandler PdfListChanged;

        //private string _localFolderPath;
        //private string _dataFilePath;

        public PdfListPage()
        {
            InitializeComponent();

            // 模拟数据
            //PdfList.Add(new PdfItem { FileName = "maths_book.pdf", ModifyTime = "2025-12-01 21:53", FilePath = "C:\\Users\\lenovo\\Documents\\maths_book.pdf" });
            //PdfList.Add(new PdfItem { FileName = "演示 PDF.pdf", ModifyTime = "2026-01-23 20:07", FilePath = "C:\\Users\\lenovo\\Documents\\演示 PDF.pdf" });


            //_localFolderPath = ApplicationData.Current.LocalFolder.Path;
            //string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            //_localFolderPath = Path.Combine(localAppDataPath, "LiteView");
            //_dataFilePath = Path.Combine(_localFolderPath, "pdf_list_data.json");

            //if (!Directory.Exists(_localFolderPath))
            //{
            //    Directory.CreateDirectory(_localFolderPath);
            //}

            Loaded += PdfListPage_Loaded;
            PdfList.CollectionChanged += OnPdfListChanged;

            EmptyText.Visibility = PdfList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            Debug.WriteLine(App.CurrentApp.PdfDataFilePath);
        }

        private void OnPdfListChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            EmptyText.Visibility = PdfList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PdfListPage_Loaded(object sender, RoutedEventArgs e)
        {
            //LoadData();
            //App.CurrentApp.PdfService.LoadPdfDataAsync(App.CurrentApp.PdfDataFilePath);
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            Debug.WriteLine("Item clicked: " + (e.ClickedItem as PdfItem)?.FileName);
            var grid = sender as Grid;
            var selectedItem = e.ClickedItem as PdfItem;

            // 检查文件路径是否存在
            if (selectedItem == null || File.Exists(selectedItem.FilePath) == false)
            {
                PdfPathNotExistsDialog(selectedItem?.FilePath ?? "Unknown");
                return;
            }

            this.Frame.Navigate(typeof(PdfViewerPage), selectedItem);
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

        private async void AddPdfButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.MainWindowInstance == null) return;

            var picker = new FileOpenPicker(App.MainWindowInstance.AppWindow.Id);

            //var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            //WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            picker.FileTypeFilter.Add(".pdf");
              
            var result = await picker.PickSingleFileAsync();

            if (result != null)
            {
                string filePath = result.Path;

                // 检查是否重复
                foreach (var pdfItem in App.CurrentApp.PdfService.PdfList)
                {
                    if (pdfItem == null) continue;

                    // 若添加过相同的文件，则弹窗提醒，并直接返回
                    if (filePath == pdfItem.FilePath) {
                        ContentDialog dialog = new ContentDialog
                        {
                            Title = "添加失败",
                            Content = "已在列表中添加过相同的文件",
                            PrimaryButtonText = "知道了",
                            DefaultButton = ContentDialogButton.Primary,
                            XamlRoot = Content.XamlRoot
                        };

                        await dialog.ShowAsync();

                        return; 
                    }
                }

                var fileInfo = new FileInfo(filePath);

                string fileName = fileInfo.Name;
                DateTime lastModified = fileInfo.LastWriteTime;

                //PdfList.Add(new PdfItem { FileName = fileName, FilePath = filePath, ModifyTime = lastModified.ToString() });
                App.CurrentApp.PdfService.AddPdf(new PdfItem { 
                    FileName = fileName, 
                    FilePath = filePath, 
                    ModifyTime = lastModified.ToString() 
                });

                //App.CurrentApp.PdfService.SavePdfDataAsync(_dataFilePath);

                //SaveData();
            }

            //PdfListChanged?.Invoke(this, PdfList);
        }

        //private async void LoadData()
        //{
        //    if (!File.Exists(_dataFilePath)) return;

        //    string data = await File.ReadAllTextAsync(_dataFilePath);

        //    var root = JsonSerializer.Deserialize(data, AppJsonContext.Default.PdfDataRoot);

        //    if (root?.PdfItems is null) return;
        //    foreach (var item in root.PdfItems)
        //    {
        //        PdfList.Add(item);
        //    }
        //}

        //private async void SaveData()
        //{
        //    var root = new PdfDataRoot { PdfItems = PdfList.ToList() };
        //    string json = JsonSerializer.Serialize(root, AppJsonContext.Default.PdfDataRoot);

        //    //if (!File.Exists(_dataFilePath))
        //    //{
        //    //    File.Create(_dataFilePath);
        //    //}
        //    await File.WriteAllTextAsync(_dataFilePath, json);
        //}

        /// <summary>
        /// 弹出对话框提示 PDF 文件路径不存在
        /// </summary>
        /// <param name="filePath"></param>
        private async void PdfPathNotExistsDialog(string filePath)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "文件不存在",
                Content = $"无法找到文件：{filePath}\n请检查文件路径是否正确。",
                CloseButtonText = "关闭"
            };
            dialog.XamlRoot = this.XamlRoot;

            await dialog.ShowAsync();
        }
    }
}
