using LiteView.Controls;
using LiteView.Helpers;
using LiteView.Models;
using LiteView.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;
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
        public PdfViewerViewModel ViewModel { get; }

        
        private bool _penFlyoutReady;
        private int _currentTipIndex = 0;
        private TeachingTip[] _tips;

        /// <summary>
        /// 程序化选择保护计数器。>0 表示当前正在以代码方式设置 SelectedIndex，
        /// 期间产生的 SelectionChanged 应视为代码触发，忽略之。
        /// 用计数而非布尔，避免 ScrollIntoView 等潜在嵌套触发导致提前复位。
        /// </summary>
        private int _programmaticSelectionDepth = 0;

        /// <summary>
        /// 用户点击缩略图后，查看器尚未到达目标页期间记录的目标页索引。
        /// 在此期间忽略查看器回传的中间页，防止缩略图选择被来回拉扯。
        /// -1 表示当前没有进行中的跳转。
        /// </summary>
        private int _pendingJumpTargetPage = -1;

        private bool _isThumbnailPanelShown = true;
        private Storyboard? _thumbnailPanelStoryboard;


        private const double MinThumbnailHeight = 120;


        public SolidColorBrush ToBrush(Color color) => new SolidColorBrush(color);
        
        // Multiplier to convert ButtonZoom into a pixel-based UI scale.
        // 3.5 is a magic number tuned by trial — 14.0 * 3.5 ≈ 49px toolbar item width.
        // If ButtonZoom semantics change, this multiplier must be retuned.
        public double AplyButtonZoom(double buttonZoom) => buttonZoom * 3.5;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public PdfViewerPage()
        {
            ViewModel = App.Host!.Services.GetRequiredService<PdfViewerViewModel>();
            DataContext = ViewModel;

            InitializeComponent();

            Loaded += PdfViewerPage_Loaded;
            PdfViewer.PropertyChanged += PdfViewer_PropertyChanged;
            PdfViewer.PdfCurrentPageChanged += PdfViewer_PdfCurrentPageChanged;
            Unloaded += (s, e) => PdfViewer.PropertyChanged -= PdfViewer_PropertyChanged;
        }

        private void PdfViewerPage_Loaded(object sender, RoutedEventArgs e)
        {
            //SetupThumbnailImplicitAnimations();

            if (!ViewModel.IsFirstRun) return;

            WelcomeTip.IsOpen = true;
            _tips = new[] { SelectBtnTip, PenBtnTip, EraserBtnTip, ClearBtnTip, FitToWindowBtnTip, ZoomInBtnTip, ZoomOutBtnTip, SettingsBtnTip, PageBtnTip };

            WelcomeTip.Closed += (s, _) => ShowNextTip();
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
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

                await ViewModel.SafeInitializePdf(pdfItem.FilePath);

                ViewModel.CurrentFilePath = pdfItem.FilePath;
                PdfViewer.PdfPath = pdfItem.FilePath;

                ThumbnailList.ItemsSource = ViewModel.PdfItems;
            }
        }

        private void PdfViewer_PdfCurrentPageChanged(object? sender, EventArguments.PdfCurrentPageChangedEventArgs e)
        {
            if (_pendingJumpTargetPage >= 0)
            {
                if (e.CurrentPage == _pendingJumpTargetPage)
                {
                    // 已到达目标页，清除跳转标记
                    _pendingJumpTargetPage = -1;
                }
                // 未到达前的中间状态：直接丢弃
                return;
            }

            SetThumbnailSelectionProgrammatically(e.CurrentPage);
        }

        private void PdfViewer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not PdfViewerControl pdfViewerControl) return;

            if (e.PropertyName == nameof(PdfViewerControl.CurrentPageIndex))
            {
                var pageIndex = pdfViewerControl.CurrentPageIndex + 1;
                ViewModel.CurrentPageIndex = pageIndex;
            }

            if (e.PropertyName == nameof(PdfViewerControl.PageCount))
                ViewModel.PageCount = pdfViewerControl.PageCount;

            //if (sender is PdfViewerControl pdfViewerControl)
            //{
            //    if (pdfViewerControl.CurrentPageIndex == _targetPage)
            //    {
            //        _targetPage = -1;
            //        _isThumbnailListScrolling = false;
            //    }

            //    ViewModel.CurrentPageIndex = pdfViewerControl.CurrentPageIndex + 1;
            //    ViewModel.PageCount = pdfViewerControl.PageCount;
            //}
        }


        #region Tutorials
        private void TutorialBtn_Click(object sender, RoutedEventArgs e)
        {
            WelcomeTip.IsOpen = true;
            _tips = new[] { SelectBtnTip, PenBtnTip, EraserBtnTip, ClearBtnTip, FitToWindowBtnTip, ZoomInBtnTip, ZoomOutBtnTip, SettingsBtnTip, PageBtnTip };
            _currentTipIndex = 0;

            WelcomeTip.Closed += (s, _) => ShowNextTip();
        }

        private void ShowNextTip()
        {
            if (_currentTipIndex >= _tips.Length)
            {
                ViewModel.MarkTutorialSeen();
                _currentTipIndex = 0;
                return;
            }

            var tip = _tips[_currentTipIndex];
            void OnClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
            {
                sender.Closed -= OnClosed;
                _currentTipIndex++;
                ShowNextTip();
            }
            tip.Closed += OnClosed;
            tip.IsOpen = true;
        }
        #endregion


        #region ToolButtons

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
            if (sender is not ToggleButton btn) return;

            btn.IsChecked = true;
            UncheckOthers(btn);
            ApplyTool(btn);

            Debug.WriteLine($"[Toobar Button] is pressed: {btn.IsPressed}");
            Debug.WriteLine($"[Toobar Button] is checked: {btn.IsChecked}");


            if (btn == BtnPen)
            {
                if (!_penFlyoutReady)
                {
                    _penFlyoutReady = true;
                }
                else
                {
                    FlyoutBase.ShowAttachedFlyout(BtnPen);

                }
            }
            else
            {
                _penFlyoutReady = false;
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

        private void ApplyTool(ToggleButton btn)
        {
            if (btn == BtnSelect)       SetTool(ToolKind.Select);
            else if (btn == BtnPen)     SetTool(ToolKind.Pen);
            else if (btn == BtnEraser)  SetTool(ToolKind.Eraser);
        }

        private void SetTool(ToolKind tool)
        {
            ViewModel.CurrentTool = tool;

            switch(tool)
            {
                case ToolKind.Select:
                    PdfViewer.AllowAnnotate(false);
                    PdfViewer.SetScrollingEnabled(true);
                    break;
                case ToolKind.Pen:
                    PdfViewer.AllowAnnotate(true);
                    PdfViewer.SetAnnotationEraseMode(false);
                    PdfViewer.SetScrollingEnabled(false);
                    break;
                case ToolKind.Eraser:
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

        #endregion


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
            PdfViewer.SetAnnotationColor(ViewModel.PenColor);
        }

        private void StrokeThicknessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            PdfViewer.SetAnnotationThickness(ViewModel.StrokeThickness);
        }


        #region Jumping Page and ThubnailList

        /// <summary>
        /// Show a or close ThumbnailList
        /// </summary>
        private async void PageBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleThumbnailPanel();
        }

        /// <summary>
        /// 切换缩略图面板显示状态。
        /// </summary>
        private void ToggleThumbnailPanel()
        {
            PlayThumbnailPanelSlideAnimation(!_isThumbnailPanelShown);
        }

        private void ThumbnailList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            Debug.WriteLine("Changed: " + args.ItemIndex);

            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Phase == 0)
            {
                args.RegisterUpdateCallback(OnThumbnailPhase1);
            }
            args.Handled = true;
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newHeight = ViewModel.ThumbnailHeight - e.VerticalChange;

            // 110.0 是经过微调的值，确保缩略图列表不会超出可用空间，且保持较好的视觉效果。这个值可能需要根据实际 UI 布局进行调整。
            double maxHeight = Math.Max(MinThumbnailHeight, (PdfGrid.ActualHeight - PageController.ActualHeight - 110.0));

            newHeight = Math.Clamp(newHeight, MinThumbnailHeight, maxHeight);

            ViewModel.ThumbnailHeight = newHeight;
        }

        private void ThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_programmaticSelectionDepth > 0) return;

            int newIndex = ThumbnailList.SelectedIndex;
            if (newIndex < 0) return;

            _pendingJumpTargetPage = newIndex;
            PdfViewer.JumpToPage(newIndex);
        }
        private void PageNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Debug.WriteLine($"[PageNumberTextBox_TextChanged] {ViewModel.CurrentPageNumberTextBoxText}");

            int targetPageIndex;
            if (!int.TryParse(ViewModel.CurrentPageNumberTextBoxText.Trim(), out targetPageIndex))
            {
                if (!string.IsNullOrEmpty(ViewModel.CurrentPageNumberTextBoxText))
                {
                    PageNumberTextBox.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
                }
                else
                {
                    ThumbnailList.ScrollIntoView(ViewModel.PdfItems[ViewModel.CurrentPageIndex - 1], ScrollIntoViewAlignment.Leading);

                    PageNumberTextBox.ClearValue(Control.BorderBrushProperty);
                }
                return;
            }
            // 因为是从 1 开始的页码，所以要检查是否小于 1 或大于总页数
            else if (targetPageIndex < 1 || targetPageIndex > ViewModel.PageCount)
            {
                PageNumberTextBox.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            else
            {

                ThumbnailList.ScrollIntoView(ViewModel.PdfItems[targetPageIndex - 1], ScrollIntoViewAlignment.Leading);

                PageNumberTextBox.ClearValue(Control.BorderBrushProperty);
            }
        }


        private void OnThumbnailPhase1(
            ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;

            var item = (PageItem)args.Item;
            // 渲染
            item.Render();
            args.Handled = true;
        }

        /// <summary>
        /// 以代码方式设置缩略图选中项，期间屏蔽 SelectionChanged 回传。
        /// </summary>
        private void SetThumbnailSelectionProgrammatically(int index)
        {
            if (index < 0 || index >= ThumbnailList.Items.Count) return;
            if (ThumbnailList.SelectedIndex == index) return;

            _programmaticSelectionDepth++;
            try
            {
                ThumbnailList.SelectedIndex = index;
                ThumbnailList.ScrollIntoView(ThumbnailList.SelectedItem, ScrollIntoViewAlignment.Leading);

            }
            finally
            {
                _programmaticSelectionDepth--;
            }
        }


        /// <summary>
        /// 播放缩略图面板“从屏幕底部伸出 / 收回底部”的动画。
        /// </summary>
        /// <param name="show">true = 从底部滑入；false = 向下滑出。</param>
        private void PlayThumbnailPanelSlideAnimation(bool show)
        {
            _isThumbnailPanelShown = show;

            var visual = ElementCompositionPreview.GetElementVisual(ThumbnailPanel);
            var compositor = visual.Compositor;

            double distance = GetThumbnailSlideDistance();

            // ---- 缓动曲线：近似 XAML 的 CubicEase ----
            // EaseOut ≈ cubic-bezier(0.1, 0.9, 0.2, 1)
            // EaseIn  ≈ cubic-bezier(0.7, 0,   0.84, 0)
            var ease = show
                ? compositor.CreateCubicBezierEasingFunction(
                    new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f))
                : compositor.CreateCubicBezierEasingFunction(
                    new Vector2(0.7f, 0.0f), new Vector2(0.84f, 0.0f));

            // ---- 位移 ----
            // 目标 Y 由 show 决定；起点不写死，从"当前视觉值"出发，这样连点不跳。
            var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
            offsetAnim.Target = "Offset";
            offsetAnim.Duration = TimeSpan.FromMilliseconds(show ? 380 : 300);

            var targetOffset = new Vector3(
                visual.Offset.X,
                show ? 0f : (float)distance,
                visual.Offset.Z); // Z 保持 Translation="0,0,16" 写进来的 16

            offsetAnim.InsertKeyFrame(1f, targetOffset, ease);

            // ---- 透明度 ----
            var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
            opacityAnim.Target = "Opacity";
            opacityAnim.Duration = TimeSpan.FromMilliseconds(show ? 280 : 200);
            opacityAnim.InsertKeyFrame(1f, show ? 1f : 0f, ease);

            // 伸出时先打开命中测试，否则按钮点不到
            if (show) ThumbnailPanel.IsHitTestVisible = true;

            // ---- 用 ScopedBatch 拿到“动画完成”回调，对应原 Storyboard 的 Completed ----
            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            visual.StartAnimation("Offset", offsetAnim);
            visual.StartAnimation("Opacity", opacityAnim);

            batch.Completed += (_, __) =>
            {
                // 合成器动画结束后 Offset / Opacity 已经是终值，
                // 但为了下次动画能从"当前位置"出发（而不是某个过期基础值），
                // 把终值再写一遍作为基础值。
                visual.Offset = targetOffset;
                visual.Opacity = show ? 1f : 0f;

                // 缩回完成后禁止命中测试，防止滑动出去的按钮仍被点到
                ThumbnailPanel.IsHitTestVisible = show;
            };
            batch.End();
        }

        /// <summary>
        /// 计算让面板完全滑出可视区域所需的纵向位移。
        /// 面板位于屏幕底部附近，仅用 ActualHeight 是不够的，需要换算到 PdfGrid 坐标系。
        /// </summary>
        private double GetThumbnailSlideDistance()
        {
            try
            {
                var topLeft = ThumbnailPanel
                    .TransformToVisual(PdfGrid)
                    .TransformPoint(new Windows.Foundation.Point(0, 0));

                // 面板顶部要越过 PdfGrid 底边，再留 40px 缓冲
                double needed = PdfGrid.ActualHeight - topLeft.Y + 40.0;

                return Math.Max(ThumbnailPanel.ActualHeight, needed);
            }
            catch
            {
                // 布局尚未就绪时的兜底值
                return ThumbnailPanel.ActualHeight + 240.0;
            }
        }

        #endregion

        private void PdfGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double maxThumbnailHeight = Math.Max(MinThumbnailHeight, (PdfGrid.ActualHeight - PageController.ActualHeight - 110.0));

            double newThumbnailHeight = Math.Clamp(ViewModel.ThumbnailHeight, MinThumbnailHeight, maxThumbnailHeight);

            ViewModel.ThumbnailHeight = newThumbnailHeight;
        }
    }
}
