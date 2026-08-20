using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    public interface IMessageDialogService
    {
        Task<ContentDialogResult> ShowAsync(string title, object content,
                                            string primaryText, string closeText);
    }
}
