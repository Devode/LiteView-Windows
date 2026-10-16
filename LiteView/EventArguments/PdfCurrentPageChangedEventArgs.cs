using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.EventArguments
{
    public class PdfCurrentPageChangedEventArgs
    {
        public int CurrentPage { get; }
        public PdfCurrentPageChangedEventArgs(int currentPage) => CurrentPage = currentPage;
    }
}
