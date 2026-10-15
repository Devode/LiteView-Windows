using LiteView.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Pdf;

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

        /// <summary>
        /// Binary search to find the page index at a given vertical position.
        /// </summary>
        public static int FindPageByPosition(List<PdfPageViewModel>? PdfPages, double positionY)
        {
            if (PdfPages == null || !PdfPages.Any()) return -1;

            int left = 0;
            int right = PdfPages.Count - 1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                var page = PdfPages[mid];

                double pageBottom = (page.DocumentTop + page.PageHeight);

                //if ((positionY >= page.DocumentTop) && positionY < pageBottom)
                if ((positionY > page.DocumentTop || MathHelper.IsClose(positionY, page.DocumentTop, 0.001)) && positionY < pageBottom)
                {
                    return mid;
                }
                else if (positionY < page.DocumentTop)
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }

            if (right < 0) return 0;
            if (left >= PdfPages.Count) return PdfPages.Count - 1;

            return -1;
        }
    }
}
