using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Helpers
{
    public static class MathHelper
    {
        public static bool IsClose(
            double a,
            double b,
            double relTol = 1e-9,
            double absTol = 1e-12)
        {
            if (double.IsNaN(a) || double.IsNaN(b))
                return double.IsNaN(a) && double.IsNaN(b);

            if (double.IsInfinity(a) || double.IsInfinity(b))
                return a == b;

            if (a == b)
                return true;

            double diff = Math.Abs(a - b);

            if (diff <= absTol)
                return true;

            double largest = Math.Max(Math.Abs(a), Math.Abs(b));
            return diff <= largest * relTol;
        }
    }
}
