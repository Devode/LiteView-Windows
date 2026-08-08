using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace LiteView.Helpers
{
    public static class StrokeHelper
    {
        /// <summary>
        /// Douglas-Peucker 算法简化点集
        /// </summary>
        /// <param name="points">待简化的点集</param>
        /// <param name="epsilon">容差</param>
        /// <returns>简化后的点集</returns>
        public static List<Vector2> DouglasPeucker(List<Vector2> points, float epsilon)
        {
            if (points.Count < 3) return points;

            // 使用栈模拟递归
            Stack<(int start, int end)> stack = new Stack<(int, int)>();
            // 标记哪些点最终需要保留（默认只保留首尾）
            bool[] keep = new bool[points.Count];
            keep[0] = true;
            keep[points.Count - 1] = true;

            stack.Push((0, points.Count - 1));

            while (stack.Count > 0)
            {
                var (start, end) = stack.Pop();

                if (end - start <= 1) continue;

                // 找当前段中最远的点
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

                // 判断是否保留该点
                if (maxDist > epsilon)
                {
                    keep[maxIndex] = true;
                    // 将新生成的两段压入栈中继续处理
                    stack.Push((start, maxIndex));
                    stack.Push((maxIndex, end));
                }
            }

            // 收集保留的点
            return points.Where((p, i) => keep[i]).ToList();
        }

        /// <summary>
        /// 检测尖角
        /// </summary>
        /// <param name="points">点集</param>
        /// <param name="angleThresholdDeg">角度阈值度数</param>
        /// <returns>返回需要强制断开的点索引列表</returns>
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

                // 向量
                float v1x = a.X - b.X;
                float v1y = a.Y - b.Y;
                float v2x = c.X - b.X;
                float v2y = c.Y - b.Y;

                // 计算点积和模长
                float dot = v1x * v2x + v1y * v2y;
                float len1 = (float)Math.Sqrt(v1x * v1x + v1y * v1y);
                float len2 = (float)Math.Sqrt(v2x * v2x + v2y * v2y);

                if (len1 == 0 || len2 == 0) continue;

                float cosAngle = dot / (len1 * len2);
                // 防止浮点数溢出范围
                cosAngle = Math.Clamp(cosAngle, -1f, 1f);
                float angle = (float)Math.Acos(cosAngle);

                // 如果夹角小于阈值（即比较尖锐），标记为角点
                if (angle < thresholdRad)
                {
                    corners.Add(i);
                }
            }
            return corners;
        }

        /// <summary>
        /// 计算点到线段的垂直距离。使用叉积法，效率最高
        /// </summary>
        /// <param name="p"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static float PointToLineDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;

            // 如果线段退化成点
            if (dx == 0 && dy == 0)
                return (float)Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

            // 叉积求面积，除以底边得到高（即点到直线的垂直距离）
            float area = Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx);
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            return area / len;
        }
    }
}
