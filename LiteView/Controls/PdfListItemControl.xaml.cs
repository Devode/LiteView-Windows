using LiteView.Contracts;
using LiteView.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteView.Controls
{
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
            DependencyProperty.Register("Model", typeof(PdfItem), typeof(PdfListItemControl), new PropertyMetadata(null, OnModelChanged));

        public static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PdfListItemControl;
            var model = e.NewValue as PdfItem;
            //Debug.WriteLine($"Name: {model.FileName}");
        }

        private void CardRoot_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                //border.Background = (SolidColorBrush)border.Resources["CardBackgroundFillColorSecondaryBrush"];

                //var visual = ElementCompositionPreview.GetElementVisual(border);
                //visual.Properties.InsertVector3("Translation", new System.Numerics.Vector3(0, 4, 16));
                //    if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out var brush))
                //    {
                //        border.Background = brush as Brush;
                //    }

                //    border.Translation = new System.Numerics.Vector3(0, -1, 16);

                VisualStateManager.GoToState(this, "PointerOver", true);
            }
        }

        private void CardRoot_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                //border.Background = (SolidColorBrush)border.Resources["CardBackgroundFillColorDefaultBrush"];

                //var visual = ElementCompositionPreview.GetElementVisual(border);
                //visual.Properties.InsertVector3("Translation", new System.Numerics.Vector3(0, 0, 4));
                //if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var brush))
                //{
                //    border.Background = brush as Brush;
                //}

                //border.Translation = new System.Numerics.Vector3(0, 0, 4);
                VisualStateManager.GoToState(this, "Normal", true);
            }
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            (App.Host!.Services.GetRequiredService<IPdfDataService>() as LiteView.Services.PdfDataService).RemovePdf(Model);
        }
    }
}
