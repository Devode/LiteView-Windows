using LiteView.Contracts;
using LiteView.Models;
using LiteView.Pages;
using LiteView.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace LiteView
{
    /// <summary>
    /// Application shell. Hosts the NavigationView, TitleBar, and Frame.
    /// Resolves <see cref="MainViewModel"/> from DI and wires up navigation
    /// selection changes to the ViewModel's NavigateCommand.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        /// <summary>Static reference used by <see cref="Helpers.ThemeHelper"/> for theme application.</summary>
        public static MainWindow current;

        public MainViewModel ViewModel;
        private readonly INavigationService _navigationService;

        public MainWindow(MainViewModel viewModel, INavigationService navigationService)
        {
            InitializeComponent();
            current = this;

            ViewModel = viewModel;
            _navigationService = navigationService;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;

            _navigationService?.Initialize(navFrame);

            // Double navigation is intentional:
            // 1. Setting navView.SelectedItem highlights the default item (PDF list icon).
            // 2. NavigateCommand.Execute performs the actual Frame.Navigate via the ViewModel.
            // Both are needed because SelectionChanged alone doesn't fire for programmatic
            // SelectedItem assignment when it's already the default selection.
            navView.SelectedItem = navView.MenuItems[0];

            var tag = (navView.SelectedItem as NavigationViewItem)?.Tag?.ToString();
            ViewModel.NavigateCommand?.Execute(tag);

            this.Closed += (s, e) => ViewModel.Cleanup();
        }

        public NavigationView GetNavView()
        {
            return navView;
        }

        private void titleBar_BackRequested(TitleBar sender, object args)
            => ViewModel.GoBackCommand.Execute(null);

        private void titleBar_PaneToggleRequested(TitleBar sender, object args)
            => ViewModel.TogglePaneCommand.Execute(null);

        private void navView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ViewModel.NavigateCommand.Execute(null);
            }
            else
            {
                var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();
                ViewModel.NavigateCommand.Execute(tag);
            }
        }
    }
}
