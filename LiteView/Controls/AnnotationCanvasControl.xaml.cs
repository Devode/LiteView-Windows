using LiteView.Helpers;
using LiteView.Models;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using Windows.Foundation;
using Windows.UI;

namespace LiteView.Controls
{
    /// <summary>
    /// Transparent canvas overlay for freehand pen/eraser annotations.
    /// Strokes are stored as <see cref="Stroke"/> records and rendered as
    /// XAML Path elements with Bezier-smoothed geometry.
    /// </summary>
    public sealed partial class AnnotationCanvasControl : UserControl, INotifyPropertyChanged
    {
        private Color _currentPenColor = Microsoft.UI.Colors.Red;
        private double _currentStrokeThickness = 1.0;

        private readonly List<Stroke> _strokes = new();
        private readonly Dictionary<uint, DrawingState> _activeDrawings = new();
        //private Stroke _currentDrawingStroke;
        //private Microsoft.UI.Xaml.Shapes.Path _currentDrawingPath;

        public bool IsEraser = false;

        public float SimplifiedTolerance
        {
            get => (float)GetValue(SimplifiedToleranceProperty);
            set => SetValue(SimplifiedToleranceProperty, value);
        }
        public static readonly DependencyProperty SimplifiedToleranceProperty = DependencyProperty.Register(
            "SimplifiedThreshold", typeof(float), typeof(AnnotationCanvasControl), new PropertyMetadata(null));

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public AnnotationCanvasControl()
        {
            InitializeComponent();
        }

        /// <summary>Set the color for subsequent pen strokes.</summary>
        public void SetPenColor(Color penColor) => _currentPenColor = penColor;

        /// <summary>Set the thickness for subsequent pen strokes.</summary>
        public void SetStrokeThickness(double strokeThickness) => _currentStrokeThickness = strokeThickness;

        /// <summary>Remove all strokes and clear the canvas.</summary>
        public void ClearStrokes()
        {
            _strokes.Clear();
            DrawingCanvas.Children.Clear();
        }

        private void DrawingCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(DrawingCanvas).Position;
            e.Handled = true;

            if (IsEraser)
            {
                Erase(point);
                return;
            }

            var stroke = new Stroke(new List<Vector2>(), _currentPenColor, (float)_currentStrokeThickness);
            stroke.Points.Add(new Vector2((float)point.X, (float)point.Y));

            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Name = "Current",
                Stroke = new SolidColorBrush(stroke.PenColor),
                StrokeThickness = stroke.Thickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };

            DrawingCanvas.CapturePointer(e.Pointer);
            DrawingCanvas.Children.Add(path);
            _activeDrawings[e.Pointer.PointerId] = new DrawingState(stroke, path);
        }

        private void DrawingCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(DrawingCanvas).Position;

            if (IsEraser)
            {
                Erase(point);
                return;
            }

            if (!_activeDrawings.TryGetValue(e.Pointer.PointerId, out var state)) return;

            state.Stroke.Points.Add(new Vector2((float)point.X, (float)point.Y));
            state.Path.Data = CreateCurrentPathDataFromStroke(state.Stroke);
        }

        private void CanvasContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ContainerClipRect.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
        }

        private void DrawingCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            FinalizeStroke(e.Pointer);
        }

        private void DrawingCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            FinalizeStroke(e.Pointer);
        }

        private void DrawingCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            FinalizeStroke(e.Pointer);
        }

        private void FinalizeStroke(Pointer pointer)
        {
            if (!_activeDrawings.TryGetValue(pointer.PointerId, out var state)) 
                return;

            // Threshold: >= 1 point means a click was registered (even without drag).
            // However, CreatePathFromStroke requires >= 2 points to produce geometry.
            // Single-point clicks silently add an invisible stroke to _strokes — it has no
            // visual but participates in eraser hit-testing and consumes a name slot.
            if (state.Stroke.Points.Count >= 1)
            {
                var finalStroke = CreatePathFromStroke(state.Stroke);
                if (finalStroke != null)
                {
                    DrawingCanvas.Children.Add(finalStroke);
                }
                // Remove the temporary path used during drawing, even if finalStroke is null (single-point click).
                DrawingCanvas.Children.Remove(state.Path);
                _strokes.Add(state.Stroke); // added even when finalStroke is null
            }
            else
            {
                DrawingCanvas.Children.Remove(state.Path);
            }

            _activeDrawings.Remove(pointer.PointerId);
            DrawingCanvas.ReleasePointerCapture(pointer);
        }

        /// <summary>
        /// Erase any stroke whose control points are within 1px of the pointer.
        /// Uses a brute-force proximity search — acceptable for typical annotation counts.
        ///
        /// IMPORTANT: Path elements are named by their index at creation time (see CreatePathFromStroke).
        /// After the first _strokes.Remove() call in the loop below, subsequent indices shift,
        /// so FindName may return the wrong Path or null for later strokes in the same erase pass.
        /// This is a known limitation — in practice, erasing multiple strokes in one gesture
        /// is rare, and the worst case is a stale visual (Path left on canvas).
        /// </summary>
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

        /// <summary>
        /// Build a finalized Path element from a completed stroke.
        /// Applies Douglas-Peucker simplification then generates Bezier geometry.
        ///
        /// The Path's Name is set to the current _strokes.Count (i.e., its future index).
        /// This coupling is used by Erase() to find and remove the corresponding Path via FindName.
        /// If strokes are ever removed out-of-order, the name-to-index mapping breaks.
        /// </summary>
        private Microsoft.UI.Xaml.Shapes.Path? CreatePathFromStroke(Stroke stroke)
        {
            if (stroke.Points.Count < 2) return null;

            List<Vector2> simplifiedPoints = StrokeHelper.DouglasPeucker(stroke.Points, SimplifiedTolerance); // At first, SimplifiedThreshold is 0.5.
            var figure = GenerateBezierPathFigure(simplifiedPoints);

            int count = _strokes.Count;

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

        private PathGeometry? CreateCurrentPathDataFromStroke(Stroke stroke)
        {
            if (stroke.Points.Count < 2) return null;

            List<Vector2> simplifiedPoints = StrokeHelper.DouglasPeucker(stroke.Points, SimplifiedTolerance); // At first, SimplifiedThreshold is 0.5.
            return GenerateBezierPathFigure(simplifiedPoints);
        }

        /// <summary>
        /// Generate a smoothed PathGeometry using quadratic Bezier segments.
        /// Corner points (detected by StrokeHelper.DetectCorners) are used as-is
        /// to preserve sharp angles, while non-corner points use midpoints as
        /// endpoints to produce smooth curves through the original control points.
        /// </summary>
        private PathGeometry? GenerateBezierPathFigure(List<Vector2> points)
        {
            if (points.Count < 2) return null;

            List<int> corners = StrokeHelper.DetectCorners(points, 128.0f);

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

                seg.Points.Add(points[i].ToPoint());
                seg.Points.Add(mid);
            }
            figure.Segments.Add(seg);
            figure.Segments.Add(new LineSegment { Point = points[^1].ToPoint() });

            return new PathGeometry { Figures = { figure } };
        }

    }
}
