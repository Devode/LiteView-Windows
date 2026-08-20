using LiteView.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace LiteView.Services
{
    public class MessageDialogService : IMessageDialogService
    {
        private readonly IServiceProvider _serviceProvider;

        public MessageDialogService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        public async Task<ContentDialogResult> ShowAsync(string title, object content,
                                                         string primaryText, string closeText)
        {
            var window = _serviceProvider.GetRequiredService<MainWindow>();
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryText,
                CloseButtonText = closeText,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = window.Content.XamlRoot
            };
            return await dialog.ShowAsync();
        }
    }
}
