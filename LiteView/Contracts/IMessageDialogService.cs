using System.Threading.Tasks;

namespace LiteView.Contracts
{
    public interface IMessageDialogService
    {
        Task<DialogResult> ShowAsync(string title, string content,
                                     string primaryText, string closeText);
    }
}
