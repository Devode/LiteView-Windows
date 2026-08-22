using LiteView.Contracts;
using LiteView.Pages;
using Microsoft.UI.Xaml.Controls;
using System;

namespace LiteView.Services
{
    /// <summary>
    /// Event args raised after a navigation, carrying the updated back-stack state.
    /// </summary>
    public class NavigatedEventArgs : EventArgs
    {
        public bool CanGoBack { get; }
        public NavigatedEventArgs(bool canGoBack) => CanGoBack = canGoBack;
    }

    /// <summary>
    /// Frame-based navigation service. Wraps a WinUI <see cref="Frame"/>
    /// and fires <see cref="Navigated"/> so the shell can react to back-stack changes.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private Frame _frame;

        /// <inheritdoc/>
        public event EventHandler<NavigatedEventArgs> Navigated;

        /// <inheritdoc/>
        public void Initialize(Frame frame) => _frame = frame;

        /// <inheritdoc/>
        public bool CanGoBack => _frame?.CanGoBack ?? false;

        /// <inheritdoc/>
        public void GoBack()
        {
            _frame?.GoBack();
            Navigated.Invoke(this, new NavigatedEventArgs(CanGoBack));
        }

        /// <inheritdoc/>
        public void NavigateTo<T>(object parameter = null) where T : Page 
        {
            _frame?.Navigate(typeof(T), parameter);
            Navigated.Invoke(this, new NavigatedEventArgs(CanGoBack));
        }

        /// <inheritdoc/>
        public void NavigateToSettings()
        {
            _frame?.Navigate(typeof(SettingsPage));
            Navigated.Invoke(this, new NavigatedEventArgs(CanGoBack));
        }
    }
}
