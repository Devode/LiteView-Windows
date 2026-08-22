using System.Threading.Tasks;

namespace LiteView.Contracts
{
    /// <summary>
    /// Abstracts the WinUI <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> so that
    /// ViewModels can show dialogs without a direct UI dependency.
    /// </summary>
    public interface IMessageDialogService
    {
        /// <summary>
        /// Show a modal content dialog and return which button the user clicked.
        /// </summary>
        /// <param name="title">Dialog title text.</param>
        /// <param name="content">Body content (plain string).</param>
        /// <param name="primaryText">Label for the primary (confirm) button. Pass null to hide it.</param>
        /// <param name="closeText">Label for the close (cancel) button. Pass null to hide it.</param>
        /// <returns><see cref="DialogResult.Primary"/> or <see cref="DialogResult.Close"/>.</returns>
        Task<DialogResult> ShowAsync(string title, string content,
                                     string primaryText, string closeText);
    }
}
