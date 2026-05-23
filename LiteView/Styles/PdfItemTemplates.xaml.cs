// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace LiteView.Styles;

public sealed partial class PdfItemTemplates : ResourceDictionary
{

    public PdfItemTemplates()
    {
        this.InitializeComponent();
    }


    private void CardRoot_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            //border.Background = (SolidColorBrush)border.Resources["CardBackgroundFillColorSecondaryBrush"];

            //var visual = ElementCompositionPreview.GetElementVisual(border);
            //visual.Properties.InsertVector3("Translation", new System.Numerics.Vector3(0, 4, 16));
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out var brush)) {
                border.Background = brush as Brush;
            }
            
            border.Translation = new System.Numerics.Vector3(0, -4, 16);
        }
    }

    private void CardRoot_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            //border.Background = (SolidColorBrush)border.Resources["CardBackgroundFillColorDefaultBrush"];

            //var visual = ElementCompositionPreview.GetElementVisual(border);
            //visual.Properties.InsertVector3("Translation", new System.Numerics.Vector3(0, 0, 4));
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var brush))
            {
                border.Background = brush as Brush;
            }

            border.Translation = new System.Numerics.Vector3(0, 0, 4);
        }
    }
}
