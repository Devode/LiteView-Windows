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
    public class NavigationService : INavigationService
    {
        private Frame _frame;

        public void Initialize(Frame frame) => _frame = frame;

        public bool CanGoBack => _frame?.CanGoBack ?? false;
        public void GoBack() => _frame?.GoBack();


        public void NavigateTo<T>(object parameter = null) where T : Page 
            => _frame?.Navigate(typeof(T), parameter);

        public void NavigateToSettings() => _frame?.Navigate(typeof(SettingsPage));
    }
}
