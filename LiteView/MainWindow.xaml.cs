using LiteView.Contracts;
using LiteView.Models;
using LiteView.Pages;
using LiteView.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

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

            ViewModel.NavigateCommand?.Execute(navView.SelectedItem as NavigationViewItem);

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
                ViewModel.NavigateCommand.Execute(args.SelectedItem as NavigationViewItem);
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            ViewModel.SearchText = sender.Text;
        }
    }
}
