// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Numerics;

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
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out var brush)) {
                border.Background = brush as Brush;
            }
            
            border.Translation = new Vector3(0, -1, 16);
        }
    }

    private void CardRoot_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var brush))
            {
                border.Background = brush as Brush;
            }

            border.Translation = new Vector3(0, 0, 4);
        }
    }
}
