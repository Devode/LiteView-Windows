using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LiteView.Helpers
{
    public static class StrokeHelper
    {
        /// <summary>
        /// Simplify a polyline using the Douglas-Peucker algorithm.
        /// </summary>
        /// <param name="points">Input points to simplify.</param>
        /// <param name="epsilon">Maximum perpendicular distance tolerance.</param>
        /// <returns>Simplified point list.</returns>
        public static List<Vector2> DouglasPeucker(List<Vector2> points, float epsilon)
        {
            if (points.Count < 3) return points;

            Stack<(int start, int end)> stack = new Stack<(int, int)>();
            bool[] keep = new bool[points.Count];
            keep[0] = true;
            keep[points.Count - 1] = true;

            stack.Push((0, points.Count - 1));

            while (stack.Count > 0)
            {
                var (start, end) = stack.Pop();

                if (end - start <= 1) continue;

                float maxDist = -1;
                int maxIndex = start;
                Vector2 startPoint = points[start];
                Vector2 endPoint = points[end];

                for (int i = start + 1; i < end; i++)
                {
                    float dist = PointToLineDistance(points[i], startPoint, endPoint);
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        maxIndex = i;
                    }
                }

                if (maxDist > epsilon)
                {
                    keep[maxIndex] = true;
                    stack.Push((start, maxIndex));
                    stack.Push((maxIndex, end));
                }
            }

            return points.Where((p, i) => keep[i]).ToList();
        }

        /// <summary>
        /// Detect sharp corners in a polyline by measuring the angle between consecutive segments.
        /// </summary>
        /// <param name="points">Input points.</param>
        /// <param name="angleThresholdDeg">Angle threshold in degrees; segments forming a sharper angle are marked as corners.</param>
        /// <returns>Indices of points that are corners.</returns>
        public static List<int> DetectCorners(List<Vector2> points, float angleThresholdDeg = 150f)
        {
            List<int> corners = new List<int>();
            if (points.Count < 3) return corners;

            float thresholdRad = (float)(angleThresholdDeg * Math.PI / 180.0);

            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 a = points[i - 1];
                Vector2 b = points[i];
                Vector2 c = points[i + 1];

                float v1x = a.X - b.X;
                float v1y = a.Y - b.Y;
                float v2x = c.X - b.X;
                float v2y = c.Y - b.Y;

                float dot = v1x * v2x + v1y * v2y;
                float len1 = (float)Math.Sqrt(v1x * v1x + v1y * v1y);
                float len2 = (float)Math.Sqrt(v2x * v2x + v2y * v2y);

                if (len1 == 0 || len2 == 0) continue;

                float cosAngle = dot / (len1 * len2);
                cosAngle = Math.Clamp(cosAngle, -1f, 1f);
                float angle = (float)Math.Acos(cosAngle);

                if (angle < thresholdRad)
                {
                    corners.Add(i);
                }
            }
            return corners;
        }

        /// <summary>
        /// Compute the perpendicular distance from point p to line segment a-b using the cross product.
        /// </summary>
        private static float PointToLineDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;

            // Degenerate segment (point)
            if (dx == 0 && dy == 0)
                return (float)Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

            float area = Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx);
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            return area / len;
        }
    }
}
