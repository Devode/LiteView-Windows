using LiteView.Contracts;
using LiteView.Models;
using LiteView.Pages;
using LiteView.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace LiteView
{
    public sealed partial class MainWindow : Window
    {
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
