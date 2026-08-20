using LiteView.Services;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    public interface INavigationService
    {
        event EventHandler<NavigatedEventArgs> Navigated;

        void Initialize(Frame frame);

        bool CanGoBack { get; }
        void GoBack();
        void NavigateTo<T>(object patameter = null) where T : Page;
        void NavigateToSettings();
    }
}
