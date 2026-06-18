using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace LiteView.Models;

public record Stroke(List<Vector2> Points, Color PenColor, float Thickness);
