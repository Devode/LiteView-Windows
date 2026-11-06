using LiteView.Helpers;
using LiteView.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
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
        private const float D_MIN = 1f;      // 慢写时的典型点间距
        private const float D_MAX = 5f;     // 快写时的典型点间距
        private const float W_MIN = 0.4f;    // 最细线宽
        private const float W_MAX = 2f;      // 最粗线宽
        private const float MIN_MOVE = 0.5f; // 小于此距离视为原地抖动，跳过
        private const float PALM_CONTACT_THRESHOLD = 40f; // 手掌过滤阈值(px @96dpi)
        private const float ERASER_BASE_RADIUS = 8f;   // DIP
        private const float MAX_DPI_SCALE = 10f;


        private readonly Dictionary<uint, StrokeState> _activeStrokes = new();

        private readonly List<BakedStroke> _bakedStrokes = new();


        private bool _isEraser = false;
        public bool IsEraser
        {
            get => _isEraser;
            set
            {
                if (_isEraser == value) return;
                _isEraser = value;

                if (value)
                {
                    // 切到橡皮擦：丢弃所有正在写的笔画（工具切换 = 会话结束）
                    _activeStrokes.Clear();
                    DynamicCanvas.Invalidate();
                }

                _eraserCursorPos = null;
            }
        }

        private readonly HashSet<uint> _erasingPointers = new();
        private Vector2? _eraserCursorPos = null;

        public float SimplifiedTolerance
        {
            get => (float)GetValue(SimplifiedToleranceProperty);
            set => SetValue(SimplifiedToleranceProperty, value);
        }
        public static readonly DependencyProperty SimplifiedToleranceProperty = DependencyProperty.Register(
            nameof(SimplifiedTolerance), typeof(float), typeof(AnnotationCanvasControl), new PropertyMetadata(0.5f));

        public Color PenColor
        {
            get => (Color)GetValue(PenColorProperty);
            set => SetValue(PenColorProperty, value);
        }
        public static readonly DependencyProperty PenColorProperty = DependencyProperty.Register(
            nameof(PenColor), typeof(Color), typeof(AnnotationCanvasControl), new PropertyMetadata(Colors.Red));

        public double PenThickness
        {
            get => (double)GetValue(PenThicknessProperty);
            set => SetValue(PenThicknessProperty, value);
        }
        public static readonly DependencyProperty PenThicknessProperty = DependencyProperty.Register(
            nameof(PenThickness), typeof(double), typeof(AnnotationCanvasControl), new PropertyMetadata(1.0));

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public AnnotationCanvasControl()
        {
            InitializeComponent();

            StaticCanvas.SizeChanged += (_, __) => StaticCanvas.Invalidate();
            DynamicCanvas.SizeChanged += (_, __) => DynamicCanvas.Invalidate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Dispose 所有缓存的几何
            foreach (var s in _bakedStrokes)
                s.CachedGeometry?.Dispose();
            _bakedStrokes.Clear();
            _activeStrokes.Clear();

            DynamicCanvas?.RemoveFromVisualTree();
            StaticCanvas?.RemoveFromVisualTree();
        }

        /// <summary>Remove all strokes and clear the canvas.</summary>
        public void ClearStrokes()
        {
            foreach (var s in _bakedStrokes)
                s.CachedGeometry?.Dispose();
            _bakedStrokes.Clear();

            StaticCanvas.Invalidate();
            DynamicCanvas.Invalidate();
        }

        /// <summary>
        /// 根据 ScrollViewer 的缩放比调整渲染表面的物理像素密度。
        /// DpiScale 变化会触发 CreateResources，进而清空 CachedGeometry 并用新设备重建。
        /// </summary>
        public void UpdateRenderDpi(float zoomFactor)
        {
            float clamped = Math.Clamp(zoomFactor, 0.5f, MAX_DPI_SCALE);

            if (Math.Abs(StaticCanvas.DpiScale - clamped) < 0.05f) return;

            StaticCanvas.DpiScale = clamped;
            DynamicCanvas.DpiScale = clamped;
        }

        private void CanvasContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ContainerClipRect.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
        }



        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var cp = e.GetCurrentPoint(DynamicCanvas);
            uint id = e.Pointer.PointerId;

            if (_activeStrokes.ContainsKey(id)) return;

            var deviceType = cp.PointerDeviceType;
            if (deviceType == PointerDeviceType.Mouse && !cp.Properties.IsPrimary) return;
            if (deviceType == PointerDeviceType.Touch)
            {
                var rect = cp.Properties.ContactRect;
                if (rect.Width > PALM_CONTACT_THRESHOLD || rect.Height > PALM_CONTACT_THRESHOLD) return;
            }
            if (deviceType == PointerDeviceType.Pen && cp.Properties.IsBarrelButtonPressed) return;

            // ===== 橡皮擦分支 =====
            if (IsEraser)
            {
                DynamicCanvas.CapturePointer(e.Pointer);
                _erasingPointers.Add(id);

                var pos = new Vector2((float)cp.Position.X, (float)cp.Position.Y);
                _eraserCursorPos = pos;
                EraseAt(pos);

                DynamicCanvas.Invalidate();
                e.Handled = true;
                return;
            }

            // ===== 笔分支 =====
            DynamicCanvas.CapturePointer(e.Pointer);

            float thicknessScale = Math.Clamp((float)PenThickness, 0.1f, 10f);
            var startPos = new Vector2((float)cp.Position.X, (float)cp.Position.Y);
            var state = new StrokeState
            {
                LastPos = startPos,
                SmoothedDist = D_MIN,
                Color = PenColor,
                ThicknessScale = thicknessScale,
                Points = { new InkPoint(startPos, W_MAX * 0.8f * thicknessScale) }
            };
            _activeStrokes[id] = state;

            DynamicCanvas.Invalidate();
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            uint id = e.Pointer.PointerId;

            // ===== 橡皮擦分支 =====
            if (IsEraser)
            {
                if (!_erasingPointers.Contains(id)) return;

                var cpErase = e.GetCurrentPoint(DynamicCanvas);
                var erasePos = new Vector2((float)cpErase.Position.X, (float)cpErase.Position.Y);
                _eraserCursorPos = erasePos;
                EraseAt(erasePos);

                DynamicCanvas.Invalidate();
                e.Handled = true;
                return;
            }

            // ===== 笔分支 =====
            if (!_activeStrokes.TryGetValue(id, out var state)) return;

            var cp = e.GetCurrentPoint(DynamicCanvas);
            var pos = new Vector2((float)cp.Position.X, (float)cp.Position.Y);

            float dx = pos.X - state.LastPos.X;
            float dy = pos.Y - state.LastPos.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < MIN_MOVE) return;

            state.SmoothedDist = state.SmoothedDist * 0.75f + dist * 0.25f;
            float t = MathF.Sqrt(Math.Clamp(
                (state.SmoothedDist - D_MIN) / (D_MAX - D_MIN), 0f, 1f));
            float baseWidth = W_MAX - (W_MAX - W_MIN) * t;
            float width = baseWidth * state.ThicknessScale;

            state.Points.Add(new InkPoint(pos, width));
            state.LastPos = pos;

            DynamicCanvas.Invalidate();
            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            uint id = e.Pointer.PointerId;

            if (_erasingPointers.Remove(id))
            {
                try { DynamicCanvas.ReleasePointerCapture(e.Pointer); } catch { }
                if (_erasingPointers.Count == 0) _eraserCursorPos = null;
                DynamicCanvas.Invalidate();
                e.Handled = true;
                return;
            }

            if (!_activeStrokes.TryGetValue(id, out _)) return;
            try { DynamicCanvas.ReleasePointerCapture(e.Pointer); } catch { }
            FinishStroke(id, commit: true);
            e.Handled = true;
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            uint id = e.Pointer.PointerId;

            if (_erasingPointers.Remove(id))
            {
                if (_erasingPointers.Count == 0) _eraserCursorPos = null;
                DynamicCanvas.Invalidate();
                e.Handled = true;
                return;
            }

            if (!_activeStrokes.TryGetValue(id, out _)) return;
            FinishStroke(id, commit: true);
            e.Handled = true;
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            uint id = e.Pointer.PointerId;

            if (_erasingPointers.Remove(id))
            {
                if (_erasingPointers.Count == 0) _eraserCursorPos = null;
                DynamicCanvas.Invalidate();
                return;
            }

            if (!_activeStrokes.TryGetValue(id, out _)) return;
            FinishStroke(id, commit: true);
        }

        private void FinishStroke(uint id, bool commit)
        {
            if (!_activeStrokes.TryGetValue(id, out var state)) return;
            _activeStrokes.Remove(id);

            if (commit && state.Points.Count > 0)
                BakeStroke(state.Points, state.Color);

            DynamicCanvas.Invalidate();
        }

        /// <summary>橡皮擦视觉半径（DIP），与当前笔宽联动。</summary>
        private float EraserRadius() => Math.Max(ERASER_BASE_RADIUS, (float)PenThickness * 4f);

        /// <summary>在 point 处擦除所有命中笔迹。命中判定基于相邻采样点之间的线段，而非仅采样点本身。</summary>
        private void EraseAt(Vector2 point)
        {
            float radius = EraserRadius();
            float radiusSq = radius * radius;
            bool anyErased = false;

            // 倒序遍历，删除时索引不失效
            for (int i = _bakedStrokes.Count - 1; i >= 0; i--)
            {
                var stroke = _bakedStrokes[i];
                if (!HitTest(stroke, point, radiusSq)) continue;

                stroke.CachedGeometry?.Dispose();
                _bakedStrokes.RemoveAt(i);
                anyErased = true;
            }

            if (anyErased) StaticCanvas.Invalidate();
        }

        private static bool HitTest(BakedStroke stroke, Vector2 point, float radiusSq)
        {
            var pts = stroke.Points;
            int n = pts.Count;
            if (n == 0) return false;
            if (n == 1) return Vector2.DistanceSquared(pts[0].Position, point) <= radiusSq;

            for (int i = 0; i < n - 1; i++)
            {
                if (DistancePointToSegmentSq(point, pts[i].Position, pts[i + 1].Position) <= radiusSq)
                    return true;
            }
            return false;
        }

        private static float DistancePointToSegmentSq(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 1e-6f) return Vector2.DistanceSquared(p, a);
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            Vector2 closest = a + ab * t;
            return Vector2.DistanceSquared(p, closest);
        }


        private void DynamicCanvas_RegionsInvalidated(CanvasVirtualControl sender, CanvasRegionsInvalidatedEventArgs args)
        {
            if (!sender.ReadyToDraw) return;

            foreach (var region in args.InvalidatedRegions)
            {
                using var ds = sender.CreateDrawingSession(region);

                // ===== 橡皮擦光标预览 =====
                if (IsEraser && _eraserCursorPos.HasValue)
                {
                    float r = EraserRadius();
                    ds.DrawCircle(_eraserCursorPos.Value, r, Colors.Gray, 1.5f);
                    // 中心小点，强化"擦除点"的视觉
                    ds.FillCircle(_eraserCursorPos.Value, 1.5f, Colors.Gray);
                }

                // ===== 笔画绘制 =====
                foreach (var kv in _activeStrokes)
                {
                    var pts = kv.Value.Points;
                    if (pts.Count == 0) continue;

                    if (pts.Count == 1)
                    {
                        var p = pts[0];
                        ds.FillCircle(p.Position, p.Width * 0.5f, kv.Value.Color);
                        continue;
                    }

                    using var geo = BuildStrokeGeometry(sender, pts, taperEnd: true);
                    if (geo != null) ds.FillGeometry(geo, kv.Value.Color);
                }
            }
        }

        private void DynamicCanvas_CreateResources(CanvasVirtualControl sender, CanvasCreateResourcesEventArgs args)
        {
            if (args.Reason == CanvasCreateResourcesReason.NewDevice)
            {
                _activeStrokes.Clear();
                DynamicCanvas.Invalidate();
            }
        }


        private void StaticCanvas_CreateResources(CanvasVirtualControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            foreach (var s in _bakedStrokes)
            {
                s.CachedGeometry?.Dispose();
                s.CachedGeometry = null;
            }

            if (args.Reason == CanvasCreateResourcesReason.NewDevice)
                sender.Invalidate();
        }

        private void StaticCanvas_RegionsInvalidated(CanvasVirtualControl sender, CanvasRegionsInvalidatedEventArgs args)
        {
            if (!sender.ReadyToDraw) return;

            foreach (var region in args.InvalidatedRegions)
            {
                using var ds = sender.CreateDrawingSession(region);

                foreach (var stroke in _bakedStrokes)
                {
                    if (stroke.Points.Count == 1)
                    {
                        var p = stroke.Points[0];
                        ds.FillCircle(p.Position, p.Width * 0.5f, stroke.Color);
                        continue;
                    }

                    // 在绘制时构建并缓存
                    if (stroke.CachedGeometry == null)
                        stroke.CachedGeometry = BuildStrokeGeometry(sender, stroke.Points, taperEnd: true);

                    // 直接使用已缓存的几何图形
                    if (stroke.CachedGeometry != null)
                        ds.FillGeometry(stroke.CachedGeometry, stroke.Color);
                }
            }
        }

        private void BakeStroke(List<InkPoint> points, Color color)
        {
            var simplified = SimplifyStroke(points, SimplifiedTolerance);

            _bakedStrokes.Add(new BakedStroke
            {
                Points = simplified,
                Color = color,
                CachedGeometry = null,
            });

            StaticCanvas.Invalidate();
        }

        /// <summary>
        /// 对已完成的笔画做 Douglas-Peucker 简化，只保留位置，宽度跟着原始点走。
        /// SimplifiedTolerance ≤ 0 时跳过简化。
        /// </summary>
        private static List<InkPoint> SimplifyStroke(List<InkPoint> points, float tolerance)
        {
            int n = points.Count;
            if (n < 8 || tolerance <= 0f) return points;

            // 首尾各保留 taperPts 个点，与 BuildStrokeGeometry 的 taper 区一致
            int taperZone = Math.Min(6, n / 4);

            var result = new List<InkPoint>(n);

            // 起点 taper 区原样保留
            for (int i = 0; i < taperZone; i++)
                result.Add(points[i]);

            // 中间段做 DP 简化
            var middleIndices = DouglasPeuckerIndices(points, taperZone, n - 1 - taperZone, tolerance);
            for (int i = 0; i < middleIndices.Count - 1; i++)   // 跳过最后一个，taper 区会补
                result.Add(points[middleIndices[i]]);

            // 终点 taper 区原样保留
            for (int i = n - taperZone; i < n; i++)
                result.Add(points[i]);

            return result;
        }

        private static List<int> DouglasPeuckerIndices(List<InkPoint> points, int start, int end, float tolerance)
        {
            if (end - start < 2)
                return new List<int> { start, end };

            Vector2 startPos = points[start].Position;
            Vector2 endPos = points[end].Position;
            Vector2 dir = endPos - startPos;
            float dirLen = dir.Length();
            if (dirLen < 0.001f) dirLen = 0.001f;
            Vector2 normal = new Vector2(-dir.Y, dir.X) / dirLen;

            float maxDist = 0f;
            int maxIdx = start;
            for (int i = start + 1; i < end; i++)
            {
                float d = MathF.Abs(Vector2.Dot(points[i].Position - startPos, normal));
                if (d > maxDist) { maxDist = d; maxIdx = i; }
            }

            if (maxDist > tolerance)
            {
                var left = DouglasPeuckerIndices(points, start, maxIdx, tolerance);
                var right = DouglasPeuckerIndices(points, maxIdx, end, tolerance);
                var result = new List<int>(left);
                for (int i = 1; i < right.Count; i++)   // 跳过首元素（等于 left 的尾）
                    result.Add(right[i]);
                return result;
            }
            return new List<int> { start, end };
        }


        private static CanvasGeometry? BuildStrokeGeometry(
            ICanvasResourceCreator resourceCreator,
            List<InkPoint> points,
            bool taperStart = false,
            bool taperEnd = false)
        {
            int n = points.Count;
            if (n < 2) return null;

            // ===== 位置平滑，抑制采样抖动 =====
            var positions = SmoothPositions(points, iterations: 2);

            // ===== 切线基于平滑后的位置 =====
            var tangents = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = positions[Math.Max(i - 1, 0)];
                Vector2 next = positions[Math.Min(i + 1, n - 1)];
                Vector2 diff = next - prev;
                float len = diff.Length();
                tangents[i] = len > 1e-6f ? diff / len : new Vector2(1f, 0f);
            }

            // 位置已平滑，切线只做 1 次轻量平滑即可
            tangents = SmoothTangents(tangents, iterations: 1);

            // ===== 计算 halfWidths（taper + 双向 EMA，保持不变）=====
            var halfWidths = new float[n];
            for (int i = 0; i < n; i++)
                halfWidths[i] = points[i].Width * 0.5f;

            int taperPts = n < 6 ? 0 : Math.Min(12, Math.Max(4, n / 4));
            if (taperPts > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (taperStart && i < taperPts)
                    {
                        float t = (float)i / (taperPts - 1);
                        halfWidths[i] *= SmoothStep(t);
                    }
                    else if (taperEnd && i >= n - taperPts)
                    {
                        float t = (float)(i - (n - taperPts)) / (taperPts - 1);
                        halfWidths[i] *= SmoothStep(1f - t);
                    }
                }
            }

            const float WIDTH_EMA = 0.45f;
            for (int i = 1; i < n; i++)
                halfWidths[i] = halfWidths[i - 1] * WIDTH_EMA + halfWidths[i] * (1f - WIDTH_EMA);
            for (int i = n - 2; i >= 0; i--)
                halfWidths[i] = halfWidths[i + 1] * WIDTH_EMA + halfWidths[i] * (1f - WIDTH_EMA);

            const float MIN_HALF_WIDTH = 0.15f;
            for (int i = 0; i < n; i++)
                if (halfWidths[i] < MIN_HALF_WIDTH) halfWidths[i] = MIN_HALF_WIDTH;

            // ===== 左右边界（用 positions 而非 points.Position）=====
            var left = new Vector2[n];
            var right = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 normal = new Vector2(-tangents[i].Y, tangents[i].X);
                left[i] = positions[i] + normal * halfWidths[i];
                right[i] = positions[i] - normal * halfWidths[i];
            }

            // ===== 路径构建（半圆中心也用 positions）=====
            using var builder = new CanvasPathBuilder(resourceCreator);
            builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Winding);

            builder.BeginFigure(left[0]);

            for (int i = 0; i < n - 1; i++)
                AddSmoothSegment(builder, left[i], left[i + 1], tangents[i], tangents[i + 1]);

            {
                Vector2 t = tangents[n - 1];
                Vector2 nrm = new Vector2(-t.Y, t.X);
                AddSemicircle(builder, positions[n - 1], nrm, t, halfWidths[n - 1]);
            }

            for (int i = n - 1; i > 0; i--)
                AddSmoothSegment(builder, right[i], right[i - 1], -tangents[i], -tangents[i - 1]);

            {
                Vector2 t = tangents[0];
                Vector2 nrm = new Vector2(-t.Y, t.X);
                AddSemicircle(builder, positions[0], -nrm, -t, halfWidths[0]);
            }

            builder.EndFigure(CanvasFigureLoop.Closed);
            return CanvasGeometry.CreatePath(builder);
        }

        /// <summary>
        /// 3 点加权移动平均，端点保持不动（防止笔迹首尾缩进）。
        /// iterations 通常 1~2：1 次已能消除大部分抖动，2 次曲线更柔但不失细节。
        /// </summary>
        private static Vector2[] SmoothPositions(List<InkPoint> points, int iterations)
        {
            int n = points.Count;
            var current = new Vector2[n];
            for (int i = 0; i < n; i++) current[i] = points[i].Position;

            if (n < 3 || iterations <= 0) return current;

            for (int iter = 0; iter < iterations; iter++)
            {
                var next = new Vector2[n];
                next[0] = current[0];
                next[n - 1] = current[n - 1];
                for (int i = 1; i < n - 1; i++)
                {
                    // [1, 2, 1] / 4 加权，中心点占主导，保留真实走向
                    next[i] = (current[i - 1] + current[i] * 2f + current[i + 1]) * 0.25f;
                }
                current = next;
            }
            return current;
        }

        private static float SmoothStep(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static Vector2[] SmoothTangents(Vector2[] tangents, int iterations)
        {
            int n = tangents.Length;
            if (n < 3 || iterations <= 0) return tangents;

            var current = (Vector2[])tangents.Clone();
            for (int iter = 0; iter < iterations; iter++)
            {
                var next = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = current[Math.Max(i - 1, 0)];
                    Vector2 b = current[i];
                    Vector2 c = current[Math.Min(i + 1, n - 1)];
                    Vector2 acc = a + b * 2f + c;
                    float len = acc.Length();
                    next[i] = len > 1e-6f ? acc / len : current[i];
                }
                current = next;
            }
            return current;
        }

        private static void AddSmoothSegment(
            CanvasPathBuilder b,
            Vector2 start, Vector2 end,
            Vector2 startTangent, Vector2 endTangent)
        {
            Vector2 diff = end - start;
            float dist = diff.Length();
            if (dist < 0.01f)
            {
                b.AddLine(end);
                return;
            }

            float handle = dist / 3f;
            Vector2 c1 = start + startTangent * handle;
            Vector2 c2 = end - endTangent * handle;

            b.AddCubicBezier(c1, c2, end);
        }

        /// <summary>
        /// 用 4 段三次贝塞尔近似半圆。调用者必须保证路径当前末尾 = P(0)，
        /// 即 center + radius * startDir。
        /// 参数化：p(θ) = center + radius*(startDir*cosθ + aroundDir*sinθ), θ∈[0,π]
        /// </summary>
        private static void AddSemicircle(CanvasPathBuilder b,
                                          Vector2 center,
                                          Vector2 startDir,
                                          Vector2 aroundDir,
                                          float radius)
        {
            const int segments = 4;
            float step = MathF.PI / segments;
            float factor = (4f / 3f) * MathF.Tan(step / 4f);

            for (int s = 0; s < segments; s++)
            {
                float theta0 = s * step;
                float theta1 = (s + 1) * step;

                float cos0 = MathF.Cos(theta0), sin0 = MathF.Sin(theta0);
                float cos1 = MathF.Cos(theta1), sin1 = MathF.Sin(theta1);

                Vector2 p0 = center + radius * (startDir * cos0 + aroundDir * sin0);
                Vector2 p1 = center + radius * (startDir * cos1 + aroundDir * sin1);
                Vector2 dp0 = radius * (-startDir * sin0 + aroundDir * cos0);
                Vector2 dp1 = radius * (-startDir * sin1 + aroundDir * cos1);

                Vector2 cp1 = p0 + factor * dp0;
                Vector2 cp2 = p1 - factor * dp1;

                b.AddCubicBezier(cp1, cp2, p1);
            }
        }


        private readonly struct InkPoint
        {
            public readonly Vector2 Position;
            public readonly float Width;
            public InkPoint(Vector2 pos, float width)
            {
                Position = pos;
                Width = width;
            }
        }

        /// <summary>每个 PointerId 一份独立笔画状态，多指互不干扰。</summary>
        private sealed class StrokeState
        {
            public List<InkPoint> Points = new();
            public Vector2 LastPos;
            public float SmoothedDist;
            public Color Color;
            public float ThicknessScale = 1f;
        }

        private sealed class BakedStroke
        {
            public List<InkPoint> Points = null!;
            public Color Color;
            public CanvasGeometry? CachedGeometry;
        }

        
    }

    
}
