using LiteView.Controls;
using LiteView.Helpers;
using LiteView.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Windows.UI;

namespace LiteView.Pages
{
    /// <summary>
    /// PDF viewer page. Hosts a <see cref="PdfViewerControl"/> and provides toolbar
    /// buttons for navigation, zoom, annotation tools (pen/eraser/select), and
    /// full-screen toggle. Tracks pen color and stroke thickness via bindable properties.
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
        // 14.0 is the toolbar's default zoom display value (percentage-like, not DPI).
        private double _buttonZoom = 14.0;

        // Multiplier to convert ButtonZoom into a pixel-based UI scale.
        // 3.5 is a magic number tuned by trial — 14.0 * 3.5 ≈ 49px toolbar item width.
        // If ButtonZoom semantics change, this multiplier must be retuned.
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

        public float StrokeSimplifiedTolerance
        {
            get => _strokeSimplifiedTolerance;
            set
            {
                if (value != _strokeSimplifiedTolerance)
                {
                    _strokeSimplifiedTolerance = value;
                    OnPropertyChanged();
                }
            }
        }
        private float _strokeSimplifiedTolerance = 0.5f;

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
            Unloaded += (s, e) => PdfViewer.PropertyChanged -= PdfViewer_PropertyChanged;
        }

        private void PdfViewer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
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
                if (pdfItem == null || File.Exists(pdfItem.FilePath) == false)
                {
                    return;
                }

                PdfViewer.PdfPath = pdfItem.FilePath;
            }
        }

        /// <summary>
        /// Handle toggle button clicks for the annotation tool bar.
        /// Ensures mutual exclusion: only one tool (Select, Pen, Eraser) can be active.
        ///
        /// The toggle logic has a subtle double-path: if the user clicks an already-checked
        /// button, IsChecked becomes false (WPF/WinUI ToggleButton default), so we immediately
        /// re-check it — effectively making tools "sticky" (click twice to deselect).
        /// This is intentional: Select mode is the default, so deselecting a tool snaps back
        /// to Select via the UncheckOthers + UpdateToolState path. However, note that clicking
        /// the already-active Select button also re-enters Select mode (a no-op, but redundant).
        /// </summary>
        private void ToolButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton clickedButton)
            {
                if (clickedButton.IsChecked == true)
                {
                    UncheckOthers(clickedButton);
                    UpdateToolState(clickedButton.Name);
                }
                else
                {
                    clickedButton.IsChecked = true;
                    UncheckOthers(clickedButton);
                    UpdateToolState(clickedButton.Name);
                }
            }
        }

        /// <summary>
        /// Deselect all other tool buttons, ensuring only the current one is selected.
        /// </summary>
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

        /// <summary>
        /// Apply the selected tool's state to the PdfViewerControl:
        /// Select mode disables annotations and enables scrolling;
        /// Pen/Eraser modes enable annotations and disable scrolling.
        /// </summary>
        private void UpdateToolState(string toolName)
        {
            switch (toolName)
            {
                case "BtnSelect":
                    PdfViewer.AllowAnnotate(false);
                    PdfViewer.SetScrollingEnabled(true);
                    break;
                case "BtnPen":
                    PdfViewer.AllowAnnotate(true);
                    PdfViewer.SetAnnotationEraseMode(false);
                    PdfViewer.SetScrollingEnabled(false);
                    break;
                case "BtnEraser":
                    PdfViewer.AllowAnnotate(true);
                    PdfViewer.SetAnnotationEraseMode(true);
                    PdfViewer.SetScrollingEnabled(false);
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
            PdfViewer.ClearAnnotations();
        }

        /// <summary>
        /// Show a page-jump dialog with input validation (numeric format + range check).
        /// The dialog stays open with an error message until valid input is provided.
        /// </summary>
        private async void PageBtn_Click(object sender, RoutedEventArgs e)
        {
            TextBox inputBox = new TextBox
            {
                PlaceholderText = "Enter page number",
                Header = "Jump to page"
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
                Title = "Jump to page",
                Content = panel,
                PrimaryButtonText = "OK",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            dialog.PrimaryButtonClick += (sender, args) =>
            {
                int pageIndex;
                if (!int.TryParse(inputBox.Text.Trim(), out pageIndex))
                {
                    args.Cancel = true;

                    errorTip.Text = "Invalid format, please enter a valid number";
                    errorTip.Visibility = Visibility.Visible;

                    inputBox.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
                } 
                else if (pageIndex - 1 < 0 || pageIndex - 1 >= PdfViewer.PageCount)
                {
                    args.Cancel = true;

                    errorTip.Text = "Invalid page number";
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

            if (tag == "EnterFullScreen")
            {
                WindowHelper.SetFullScreen(App.MainWindowInstance, true);
                optionFullScreen.Text = "Exit Full Screen";
                optionFullScreen.Tag = "ExitFullScreen";
            }
            else
            {
                WindowHelper.SetFullScreen(App.MainWindowInstance, false);
                optionFullScreen.Text = "Enter Full Screen";
                optionFullScreen.Tag = "EnterFullScreen";
            }
        }

        private void PenColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            PenColor = sender.Color;
            PdfViewer.SetAnnotationColor(PenColor);
        }

        private void StrokeThicknessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            PdfViewer.SetAnnotationThickness(StrokeThickness);
        }

        private void StrokeSimlifiedThresholdSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            //PdfViewer.
            Debug.WriteLine(StrokeSimplifiedTolerance);
        }
    }
}
