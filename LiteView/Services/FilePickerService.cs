using LiteView.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Threading.Tasks;

namespace LiteView.Services
{
    /// <summary>
    /// Shows the WinUI <see cref="FileOpenPicker"/> using the main window's AppWindow handle.
    /// Resolves <see cref="MainWindow"/> lazily to break the circular DI dependency.
    /// </summary>
    public class FilePickerService : IFilePickerService
    {
        private readonly IServiceProvider _serviceProvider;

        public FilePickerService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public async Task<string?> PickSingleFileAsync(string[] fileTypes, PickerLocationId startLocation = PickerLocationId.DocumentsLibrary)
        {
            var window = _serviceProvider.GetRequiredService<MainWindow>();
            var picker = new FileOpenPicker(window.AppWindow.Id);
            picker.SuggestedStartLocation = startLocation;
            foreach (var type in fileTypes)
                picker.FileTypeFilter.Add(type);

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
    }
}
