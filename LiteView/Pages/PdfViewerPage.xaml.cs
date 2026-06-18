using CommunityToolkit.WinUI;
using LiteView.Controls;
using LiteView.Helpers;
using LiteView.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using WinRT.Interop;
using static System.Net.Mime.MediaTypeNames;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PdfViewerPage : Page, INotifyPropertyChanged
    {
        public double ButtonZoom
        {
            get => _buttonZoom;
            set
            {
                if (_buttonZoom != value)
                {
                    _buttonZoom = value;
                    OnPropertyChanged();
                }
            }
        }
        private double _buttonZoom = 14.0;

        public double AplyButtonZoom(double buttonZoom) => buttonZoom * 3.5;

        public double StrokeThickness
        {
            get => _strokeThickness;
            set
            {
                if (_strokeThickness != value)
                {
                    _strokeThickness = value;
                    OnPropertyChanged();
                }
            }
        }
        private double _strokeThickness = 1.0;

        public Color PenColor
        {
            get => _penColor;
            set
            {
                if (_penColor != value)
                {
                    _penColor = value;
                    OnPropertyChanged();
                }
            }
        }
        private Color _penColor = Microsoft.UI.Colors.Red;

        public SolidColorBrush ToBrush(Color color) => new SolidColorBrush(color);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public PdfViewerPage()
        {
            InitializeComponent();

            PdfViewer.PropertyChanged += PdfViewer_PropertyChanged;
        }

        private void PdfViewer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is PdfViewerControl pdfViewerControl)
            {
                CurrentPageText.Text = (pdfViewerControl.CurrentPageIndex + 1).ToString();
                TotalPageText.Text = pdfViewerControl.PageCount.ToString();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var navView = App.MainWindowInstance.GetNavView();
            
            if (navView != null)
            {
                // 遍历 MenuItems 找到对应类型的 Item
                foreach (var item in navView.MenuItems)
                {
                    if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == this.GetType().Name)
                    {
                        navView.SelectedItem = navItem;
                        break;
                    }
                }
            }

            if (e.Parameter is PdfItem pdfItem)
            {
                if (pdfItem != null && pdfItem.FilePath.Length != 0) PdfViewer.PdfPath = pdfItem.FilePath;

                //AnnotationCanvas.BindToScrollViewer(PdfViewer);
                //if(PdfViewer.PageCount != 0) PageCounts.Text = $"1/{PdfViewer.PageCount}";
            }
        }

        private async Task LoadPdf(string filePath)
        {
            var file = StorageFile.GetFileFromPathAsync(filePath);
            var pdfDoc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync((IStorageFile)file);
        }

        private void ToolButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取当前被点击的按钮
            if (sender is ToggleButton clickedButton)
            {
                // 如果用户点击了已经选中的按钮，通常不需要做任何事（保持选中）
                // 如果希望点击已选中的按钮能取消选中（即允许全不选），则注释掉下面的判断
                if (clickedButton.IsChecked == true)
                {
                    UncheckOthers(clickedButton);
                    UpdateToolState(clickedButton.Name);
                }
                else
                {
                    // 可选：如果点击未选中的，先选中它，再取消其他的
                    clickedButton.IsChecked = true;
                    UncheckOthers(clickedButton);
                    UpdateToolState(clickedButton.Name);
                }
            }
        }

        private void UncheckOthers(ToggleButton currentButton)
        {
            var tools = new[] { BtnSelect, BtnPen, BtnEraser };

            foreach (var btn in tools)
            {
                if (btn != currentButton)
                {
                    btn.IsChecked = false;
                }
            }
        }

        // 业务逻辑处理
        private void UpdateToolState(string toolName)
        {
            switch (toolName)
            {
                case "BtnSelect":
                    System.Diagnostics.Debug.WriteLine("切换到：选择模式");
                    PdfViewer.GetAnnotationCanvas().IsHitTestVisible = false;
                    break;
                case "BtnPen":
                    System.Diagnostics.Debug.WriteLine("切换到：画笔模式");
                    PdfViewer.GetAnnotationCanvas().IsHitTestVisible = true;
                    PdfViewer.GetAnnotationCanvas().IsEraser = false;
                    break;
                case "BtnEraser":
                    PdfViewer.GetAnnotationCanvas().IsHitTestVisible = true;
                    PdfViewer.GetAnnotationCanvas().IsEraser = true;
                    System.Diagnostics.Debug.WriteLine("切换到：橡皮擦模式");
                    break;
            }
        }

        private void NextPageBtn_Click(object sender, RoutedEventArgs e)
        {
            PdfViewer.NextPage();
        }

        private void PreviousPageBtn_Click(object sender, RoutedEventArgs e)
        {
            PdfViewer.PreviousPage();
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            PdfViewer.ZoomIn(0.1f);
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            PdfViewer.ZoomOut(0.1f);
        }

        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            PdfViewer.FitToWindow();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            PdfViewer.GetAnnotationCanvas().ClearStrokes();
        }

        private async void PageBtn_Click(object sender, RoutedEventArgs e)
        {
            TextBox inputBox = new TextBox
            {
                PlaceholderText = "输入页码",
                Header = "跳转到页码"
            };

            TextBlock errorTip = new TextBlock
            {
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                Margin = new Thickness(0, 5, 0, 0),
                Visibility = Visibility.Collapsed
            };

            var panel = new StackPanel();
            panel.Children.Add(inputBox);
            panel.Children.Add(errorTip);

            ContentDialog dialog = new ContentDialog
            {
                Title = "跳转到页码",
                Content = panel,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };

            dialog.PrimaryButtonClick += (sender, args) =>
            {
                int pageIndex;
                if (!int.TryParse(inputBox.Text.Trim(), out pageIndex))
                {
                    args.Cancel = true;

                    errorTip.Text = "格式错误，请输入有效数字";
                    errorTip.Visibility = Visibility.Visible;

                    inputBox.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
                } 
                else if (pageIndex - 1 < 0 || pageIndex - 1 >= PdfViewer.PageCount)
                {
                    args.Cancel = true;

                    errorTip.Text = "页码无效，请输入有效页码";
                    errorTip.Visibility = Visibility.Visible;

                    inputBox.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
                }
                else
                {
                    errorTip.Visibility = Visibility.Collapsed;
                    inputBox.BorderBrush = null;
                    PdfViewer.JumpToPage(pageIndex - 1);
                }
            };

            dialog.XamlRoot = this.Content.XamlRoot;
            await dialog.ShowAsync();
        }

        private void OptionFullScreen_Click(object sender, RoutedEventArgs e)
        {
            var optionFullScreen = (MenuFlyoutItem)sender;

            if (optionFullScreen == null) return;

            var tag = optionFullScreen.Tag.ToString();

            //var currentWindow = Window.Current;

            if (tag == "EnterFullScreen")
            {
                WindowHelper.SetFullScreen(App.MainWindowInstance, true);
                optionFullScreen.Text = "退出全屏";
                optionFullScreen.Tag = "ExitFullScreen";
            }
            else
            {
                WindowHelper.SetFullScreen(App.MainWindowInstance, false);
                optionFullScreen.Text = "进入全屏";
                optionFullScreen.Tag = "EnterFullScreen";
            }
        }

        private void PenColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            PenColor = sender.Color;
            PdfViewer.GetAnnotationCanvas().SetPenColor(PenColor);
        }

        private void StrokeThicknessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            PdfViewer.GetAnnotationCanvas().SetStrokeThickness(StrokeThickness);
        }
    }
}
