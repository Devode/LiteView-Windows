using LiteView.Services;
using Microsoft.UI.Xaml.Controls;
using System;

namespace LiteView.Contracts
{
    /// <summary>
    /// Provides frame-based navigation for the application.
    /// Wraps a WinUI <see cref="Frame"/> and exposes back-stack awareness.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Raised after every navigation so that the shell can update back-button visibility.
        /// </summary>
        event EventHandler<NavigatedEventArgs> Navigated;

        /// <summary>
        /// Bind the service to a <see cref="Frame"/> instance. Must be called before any navigation.
        /// </summary>
        void Initialize(Frame frame);

        /// <summary>
        /// Whether the frame's back stack contains at least one entry.
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// Navigate to the previous page in the back stack.
        /// </summary>
        void GoBack();

        /// <summary>
        /// Navigate to a page of type <typeparamref name="T"/> with an optional parameter.
        /// </summary>
        void NavigateTo<T>(object patameter = null) where T : Page;

        /// <summary>
        /// Navigate to the dedicated settings page.
        /// </summary>
        void NavigateToSettings(object parameter = null);
    }
}
