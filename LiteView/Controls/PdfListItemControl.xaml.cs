using LiteView.Contracts;
using LiteView.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Windows.Input;

namespace LiteView.Controls
{
    /// <summary>
    /// Custom list item for the PDF reading list. Exposes a <see cref="RemoveCommand"/>
    /// and a <see cref="Model"/> property for data binding, with hover visual states.
    ///
    /// NOTE: RemoveCommand DP exists for XAML binding but is never wired up.
    /// The actual remove action is handled by RemoveItem_Click, which uses the service
    /// locator to call IPdfDataService.RemovePdf directly. This is an inconsistency —
    /// the DP is dead weight and could be removed.
    /// </summary>
    public sealed partial class PdfListItemControl : UserControl
    {
        public PdfListItemControl()
        {
            InitializeComponent();
        }

        public ICommand RemoveCommand
        {
            get => (ICommand)GetValue(RemoveCommandProperty);
            set => SetValue(RemoveCommandProperty, value);
        }
        public static readonly DependencyProperty RemoveCommandProperty =
            DependencyProperty.Register(nameof(RemoveCommand), typeof(ICommand), typeof(PdfListItemControl), new PropertyMetadata(null));

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }
        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(PdfListItemControl), new PropertyMetadata(null));

        public PdfItem Model
        {
            get { return (PdfItem)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }
        public static readonly DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(PdfItem), typeof(PdfListItemControl), new PropertyMetadata(null));

        private void CardRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "PointerOver", true);
        }

        private void CardRoot_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "Normal", true);
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (Model == null) return;
            App.Host!.Services.GetRequiredService<IPdfDataService>().RemovePdf(Model);
        }
    }
}
