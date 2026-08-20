using LiteView.Contracts;
using LiteView.Models;
using LiteView.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;

namespace LiteView.Pages
{
    public sealed partial class PdfListPage : Page
    {
        public PdfListViewModel ViewModel { get; }

        public PdfListPage()
        {
            ViewModel = App.Host!.Services.GetRequiredService<PdfListViewModel>();
            InitializeComponent();
            DataContext = ViewModel;

            Unloaded += (s, e) => ViewModel.Cleanup();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PdfItem pdfItem)
            {
                ViewModel.OpenPdfCommand.Execute(pdfItem);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.Cleanup();
        }
    }
}
