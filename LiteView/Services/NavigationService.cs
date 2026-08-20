using LiteView.Contracts;
using LiteView.Pages;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Services
{
    public class NavigatedEventArgs : EventArgs
    {
        public bool CanGoBack { get; }
        public NavigatedEventArgs(bool canGoBack) => CanGoBack = canGoBack;
    }

    public class NavigationService : INavigationService
    {
        private Frame _frame;

        public event EventHandler<NavigatedEventArgs> Navigated;

        public void Initialize(Frame frame) => _frame = frame;

        public bool CanGoBack => _frame?.CanGoBack ?? false;
        public void GoBack()
        {
            _frame?.GoBack();
            Navigated.Invoke(this, new NavigatedEventArgs(CanGoBack));
        }


        public void NavigateTo<T>(object parameter = null) where T : Page 
        {
            _frame?.Navigate(typeof(T), parameter);
            Navigated.Invoke(this, new NavigatedEventArgs(CanGoBack));
        }

        public void NavigateToSettings()
        {
            _frame?.Navigate(typeof(SettingsPage));
            Navigated.Invoke(this, new NavigatedEventArgs(CanGoBack));
        }
    }
}
