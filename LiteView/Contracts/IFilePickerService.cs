using Microsoft.Windows.Storage.Pickers;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    /// <summary>
    /// Abstracts the WinUI file picker so that ViewModels can prompt for files
    /// without a direct window dependency.
    /// </summary>
    public interface IFilePickerService
    {
        /// <summary>
        /// Show a single-file open picker filtered to the given file extensions.
        /// </summary>
        /// <param name="fileTypes">Extensions to filter by (e.g. [".pdf"]).</param>
        /// <param name="startLocation">Initial folder for the picker.</param>
        /// <returns>The selected file path, or null if the user cancelled.</returns>
        Task<string?> PickSingleFileAsync(string[] fileTypes, PickerLocationId startLocation = PickerLocationId.DocumentsLibrary);
    }
}
