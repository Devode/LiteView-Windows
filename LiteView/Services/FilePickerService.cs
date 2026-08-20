using LiteView.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Services
{
    public class FilePickerService : IFilePickerService
    {
        private readonly IServiceProvider _serviceProvider;

        public FilePickerService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

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
