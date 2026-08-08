using LiteView.Helpers;
using LiteView.Models;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView.Controls
{
    public sealed partial class AnnotationCanvasControl : UserControl
    {
        private Color _currentPenColor = Microsoft.UI.Colors.Red;
        private double _currentStrokeThickness = 1.0;

        private readonly List<Stroke> _strokes = new();
        private Stroke _currentDrawingStroke;
        private Microsoft.UI.Xaml.Shapes.Path _currentDrawingPath;

        public bool IsEraser = false;

        //private ScrollViewer _hostScrollViewer;
        //private ExpressionAnimation _offsetAnimation;
        //private Compositor _compositor;
        //private Visual _canvasVisual;

        //public static readonly DependencyProperty ZoomFactorProperty =
        //    DependencyProperty.Register(
        //        "ZoomFactor",
        //        typeof(float),
        //        typeof(AnnotationCanvasControl),
        //        new PropertyMetadata(1.0f, (d, e) => ((AnnotationCanvasControl)d).Invalidate())
        //    );
        //public float ZoomFactor { get; set; } = 1.0f;
        //private float _lastZoom = 1.0f;

        //public static readonly DependencyProperty ViewportOffsetProperty =
        //    DependencyProperty.Register(
        //        "ViewportOffset",
        //        typeof(Point),
        //        typeof(AnnotationCanvasControl),
        //        new PropertyMetadata(new Point(0, 0), (d, e) => ((AnnotationCanvasControl)d).Invalidate())
        //    );
        //public double ViewportOffsetX, ViewportOffsetY;

        //private double _lastOffsetX, _lastOffsetY;

        //private bool _isInIntermediateState = false;

        public AnnotationCanvasControl()
        {
            InitializeComponent();
        }

        public void SetPenColor(Color penColor) => _currentPenColor = penColor;
        public void SetStrokeThickness(double strokeThickness) => _currentStrokeThickness = strokeThickness;

        public void ClearStrokes()
        {
            _strokes.Clear();
            DrawingCanvas.Children.Clear();
        }
        //public void LoadStrokes(IEnumerable<Stroke> strokes)
        //{
        //    _strokes.Clear();
        //    _strokes.AddRange(strokes);
        //    DrawingCanvas.Invalidate();
        //}
        //public void Update(float zoomFactor, double viewportOffsetX, double viewportOffsetY)
        //{
        //    ZoomFactor = zoomFactor;
        //    ViewportOffsetX = viewportOffsetX;
        //    ViewportOffsetY = viewportOffsetY;
        //    //DrawingCanvas.Invalidate();
        //}

        //public void BindToScrollViewer(PdfViewerControl pdfViewer)
        //{
        //    _hostScrollViewer = pdfViewer.HostPdfScrollViewer;


        //    _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        //    _canvasVisual = ElementCompositionPreview.GetElementVisual(DrawingCanvas);

        //    //_canvasVisual.CenterPoint = Vector3.Zero;
        //    _hostScrollViewer.ViewChanged += _hostScrollViewer_ViewChanged;

        //    // 获取容器的视觉对象
        //    // 如果没有传入特定容器，默认使用自己的父级
        //    //var container = canvasContainer ?? (FrameworkElement)this.Parent;
        //    //_containerVisual = ElementCompositionPreview.GetElementVisual(container);

        //    //// 2. 创建表达式动画：容器的 Offset = -ScrollViewer 的 Offset
        //    //// 这里的逻辑是：当 ScrollViewer 向右滚（Offset 变大），画布要向左移（Visual.Offset 变小）
        //    //var compositor = _containerVisual.Compositor;

        //    //var scrollPropSet = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(_hostScrollViewer);
        //    //var expression = compositor.CreateExpressionAnimation(
        //    //    "(0.0 - scrollPropSet.Translation.X), (0.0 - scrollPropSet.Translation.Y), 0.0"
        //    //);

        //    //// 3. 将动画中的变量指向真实的 ScrollViewer
        //    //expression.SetReferenceParameter("scrollPropSet", scrollPropSet);

        //    //// 4. 应用动画到容器的 Offset 属性
        //    //// 此时，画布的位置将完全由渲染线程驱动，UI 线程卡顿也不会影响它！
        //    //_containerVisual.StartAnimation("Offset", expression);
        //}

        //private void _hostScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        //{
        //    var viewer = (ScrollViewer)sender;

        //    Debug.WriteLine($"Vertcal: {viewer.VerticalOffset}, horizontal: {viewer.HorizontalOffset}, Zoom: {viewer.ZoomFactor}");

        //    if (e.IsIntermediate)
        //    {
        //        _isInIntermediateState = true;
        //        // 2. 【核心】提取 ScrollViewer 的底层操作属性集
        //        //var scrollPropertySet = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(viewer);
        //        // 3. 创建表达式动画：让 Visual 的 Offset 自动跟随 ScrollViewer 的 Translation
        //        // 注意：ScrollViewer 向左滚时 Translation.X 为负，所以这里加个负号抵消
        //        //var offsetExpression = _compositor.CreateExpressionAnimation(
        //        //"Vector3(ManipulationPropertySet.Translation.X, ManipulationPropertySet.Translation.Y, 0)");
        //        //offsetExpression.SetReferenceParameter("ManipulationPropertySet", ElementCompositionPreview.GetScrollViewerManipulationPropertySet(viewer));
        //        //_canvasVisual.StartAnimation("Offset", offsetExpression);

        //        // 4. 创建表达式动画：让 Visual 的 Scale 自动跟随 ScrollViewer 的 Scale
        //        //var scaleExpression = _compositor.CreateExpressionAnimation(
        //        //"Vector3(ManipulationPropertySet.Scale.X, ManipulationPropertySet.Scale.Y, 1)");
        //        //scaleExpression.SetReferenceParameter("ManipulationPropertySet", ElementCompositionPreview.GetScrollViewerManipulationPropertySet(viewer));
        //        //_canvasVisual.StartAnimation("Scale", scaleExpression);
        //        //_canvasVisual.Scale = new Vector3(viewer.ZoomFactor / _lastZoom, viewer.ZoomFactor / _lastZoom, 1);
        //        //_canvasVisual.Offset = new Vector3((float)(_lastOffsetX - viewer.HorizontalOffset), (float)(_lastOffsetY - viewer.VerticalOffset), 0);
        //        //CanvasInteractiveTransform.CenterX = viewer.ViewportWidth / 2.0;
        //        //CanvasInteractiveTransform.CenterY = viewer.ViewportHeight / 2.0;
                

        //        double relativeScale = (double)(viewer.ZoomFactor / _lastZoom);

        //        double relativeTranslateX = viewer.HorizontalOffset - _lastOffsetX;
        //        double relativeTranslateY = viewer.VerticalOffset - _lastOffsetY;

        //        CanvasInteractiveTransform.ScaleX = relativeScale;
        //        CanvasInteractiveTransform.ScaleY = relativeScale;
        //        CanvasInteractiveTransform.TranslateX = -relativeTranslateX;
        //        CanvasInteractiveTransform.TranslateY = -relativeTranslateY;
        //    }
        //    else
        //    {
        //        _isInIntermediateState = false;
        //        //_canvasVisual.StopAnimation("Offset");
        //        //_canvasVisual.StopAnimation("Scale");

        //        CanvasInteractiveTransform.ScaleX = 1.0;
        //        CanvasInteractiveTransform.ScaleY = 1.0;
        //        CanvasInteractiveTransform.TranslateX = 0;
        //        CanvasInteractiveTransform.TranslateY = 0;
        //        //CanvasInteractiveTransform.CenterX = 0;
        //        //CanvasInteractiveTransform.CenterY = 0;

        //        ViewportOffsetX = viewer.HorizontalOffset;
        //        ViewportOffsetY = viewer.VerticalOffset;
        //        ZoomFactor = viewer.ZoomFactor;

        //        _lastOffsetX = ViewportOffsetX;
        //        _lastOffsetY = ViewportOffsetY;
        //        _lastZoom = ZoomFactor;

        //        //_canvasVisual.Scale = Vector3.One;

        //        //_canvasVisual.Offset = new Vector3(-(float)ViewportOffsetX, -(float)ViewportOffsetY, 0);
        //        //_canvasVisual.Offset = Vector3.Zero;=

        //        DrawingCanvas.Invalidate();
        //    }


            
        //}

        //public void Detach()
        //{
        //    _canvasVisual?.StopAnimation("Offset");
        //    _offsetAnimation = null;
        //}

        //private void CompositionTarget_Rendering(object? sender, object e)
        //{
        //    if (_hostScrollViewer == null) return;

        //    ZoomFactor = _hostScrollViewer.ZoomFactor;
        //    ViewportOffsetX = _hostScrollViewer.HorizontalOffset;
        //    ViewportOffsetY = _hostScrollViewer.VerticalOffset;

        //}

        //private void DrawingCanvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        //{
        //    var ds = args.DrawingSession;

        //    if (_isInIntermediateState) return;

        //    //// 根据当前控件接收到的外部状态，构建变换矩阵
        //    //Matrix3x2 transform = Matrix3x2.CreateScale(ZoomFactor) *
        //    //    Matrix3x2.CreateTranslation(-(float)ViewportOffsetX, -(float)ViewportOffsetY);

        //    //ds.Transform = transform;

        //    foreach (var stroke in _strokes)
        //    {
        //        using var geometry = CreateSmoothGeometry(sender, stroke.Points, stroke.Thickness);
        //        if (geometry != null)
        //        {
        //            ds.DrawGeometry(geometry, stroke.PenColor, stroke.Thickness);
                    
        //        }
        //    }

        //    if (_currentDrawingStroke != null && _currentDrawingStroke.Points.Count > 1)
        //    {
        //        using var currentGeometry = CreateSmoothGeometry(sender, _currentDrawingStroke.Points, _currentDrawingStroke.Thickness);
        //        if (currentGeometry != null)
        //        {
        //            ds.DrawGeometry(currentGeometry, _currentDrawingStroke.PenColor, _currentDrawingStroke.Thickness);
        //        }
        //    }
        //}

        private void DrawingCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(DrawingCanvas).Position;

            if (IsEraser)
            {
                Erase(point);
                return;
            }

            // 初始化当前笔画
            _currentDrawingStroke = new Stroke(new List<Vector2>(), _currentPenColor, (float)_currentStrokeThickness);
            _currentDrawingStroke.Points.Add(new Vector2((float)point.X, (float)point.Y));

            _currentDrawingPath = new Microsoft.UI.Xaml.Shapes.Path
            {
                Name = "Current",
                Stroke = new SolidColorBrush(_currentDrawingStroke.PenColor),
                StrokeThickness = _currentDrawingStroke.Thickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };

            // 捕获指针
            DrawingCanvas.CapturePointer(e.Pointer);

            DrawingCanvas.Children.Add(_currentDrawingPath);

            // 触发重绘
            //DrawingCanvas.Invalidate();
        }

        private void DrawingCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(DrawingCanvas).Position;

            if (IsEraser)
            {
                Erase(point);
                return;
            }

            if (_currentDrawingStroke == null || _currentDrawingPath == null) return;

            _currentDrawingStroke.Points.Add(new Vector2((float)point.X, (float)point.Y));
            var currentPath = FindName("Current") as Microsoft.UI.Xaml.Shapes.Path;

            if (currentPath != null)
                currentPath.Data = CreateCurrentPathDataFromStroke(_currentDrawingStroke);

            //DrawingCanvas.Invalidate();
        }

        private void CanvasContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ContainerClipRect.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
        }

        private void DrawingCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_currentDrawingStroke?.Points.Count >= 1)
            {
                var path = CreatePathFromStroke(_currentDrawingStroke);
                var currentPath = FindName("Current") as Microsoft.UI.Xaml.Shapes.Path;
                if (path != null) DrawingCanvas.Children.Add(path);
                if (currentPath != null) DrawingCanvas.Children.Remove(currentPath);
                _strokes.Add(_currentDrawingStroke);
            }

            _currentDrawingStroke = null;
            DrawingCanvas.ReleasePointerCapture(e.Pointer);
            //DrawingCanvas.Invalidate();
        }

        private void Erase(Point pointer)
        {
            Dictionary<int, Stroke> pointToRemoved = new();

            for (int i = 0; i < _strokes.Count; i++)
            {
                var stroke = _strokes[i];
                foreach (var point in stroke.Points)
                {
                    var _pointer = pointer.ToVector2();

                    if ((_pointer - point).Length() < 1.0)
                    {
                        //var path = FindName(i.ToString()) as Microsoft.UI.Xaml.Shapes.Path;
                        //DrawingCanvas.Children.Remove(path);
                        pointToRemoved[i] = stroke;
                    }
                }
            }

            foreach (var point in pointToRemoved)
            {
                var path = FindName(point.Key.ToString()) as Microsoft.UI.Xaml.Shapes.Path;
                DrawingCanvas.Children.Remove(path);
                _strokes.Remove(point.Value);
            }        
        }

        private Microsoft.UI.Xaml.Shapes.Path? CreatePathFromStroke(Stroke stroke)
        {
            if (stroke.Points.Count < 2) return null;

            List<Vector2> simplifiedPoints = StrokeHelper.DouglasPeucker(stroke.Points, 1.0f);

            var figure = GenerateBezierPathFigure(simplifiedPoints);

            int count = _strokes.Count;
            //while (FindName(count.ToString()) != null)
            //{
            //    count++;
            //}

            return new Microsoft.UI.Xaml.Shapes.Path
            {
                Name = count.ToString(),
                Data = figure,
                Stroke = new SolidColorBrush(stroke.PenColor),
                StrokeThickness = stroke.Thickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
        }

        /// <summary>
        /// 为当前绘制的笔画创建路径数据
        /// </summary>
        /// <param name="stroke"></param>
        /// <returns></returns>
        private PathGeometry? CreateCurrentPathDataFromStroke(Stroke stroke)
        {
            if (stroke.Points.Count < 2) return null;

            List<Vector2> simplifiedPoints = StrokeHelper.DouglasPeucker(stroke.Points, 1.0f);

            //var figure = GenerateBezierPathFigure(simplifiedPoints);

            return GenerateBezierPathFigure(simplifiedPoints);
        }

        /// <summary>
        /// 生成平滑的贝塞尔曲线路径
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private PathGeometry? GenerateBezierPathFigure(List<Vector2> points)
        {
            if (points.Count < 2) return null;

            List<int> corners = StrokeHelper.DetectCorners(points, 90.0f);

            var figure = new PathFigure { StartPoint = points[0].ToPoint() };
            var seg = new PolyQuadraticBezierSegment();
            for (int i = 1; i < points.Count - 1; i++)
            {
                Point mid;

                if (corners.Contains(i + 1))
                {
                    mid = points[i+1].ToPoint();
                }
                else
                {
                    mid = new Point(
                        (points[i].X + points[i + 1].X) / 2,
                        (points[i].Y + points[i + 1].Y) / 2);
                }

                seg.Points.Add(points[i].ToPoint());    // 控制点
                seg.Points.Add(mid);                    // 终点
            }
            figure.Segments.Add(seg);
            figure.Segments.Add(new LineSegment { Point = points[^1].ToPoint() });

            return new PathGeometry { Figures = { figure } };
        }
    }
}
