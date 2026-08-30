using System.Collections.Generic;
using System.Numerics;
using Windows.UI;

namespace LiteView.Models;

/// <summary>
/// An immutable snapshot of a single pen/eraser stroke on the annotation canvas.
/// </summary>
/// <param name="Points">Ordered control points in canvas coordinates.</param>
/// <param name="PenColor">Stroke color (Windows.UI.Color uses RGBA byte order, not BGRA).</param>
/// <param name="Thickness">Stroke width in canvas pixels.</param>
public record Stroke(List<Vector2> Points, Color PenColor, float Thickness);
