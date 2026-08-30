namespace LiteView.Contracts
{
    /// <summary>
    /// Indicates which button the user clicked in a dialog shown by
    /// <see cref="IMessageDialogService.ShowAsync"/>.
    /// </summary>
    public enum DialogResult
    {
        /// <summary>The user clicked the primary (confirm) button.</summary>
        Primary,

        /// <summary>The user clicked the close (cancel) button.</summary>
        Close
    }
}
