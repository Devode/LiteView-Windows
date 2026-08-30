using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Helpers
{
    /// <summary>
    /// DEAD CODE — This helper is incomplete and non-functional.
    /// OnBlurRadiusChanged contains a stub (commented-out `element.Lo` call) that does nothing.
    /// BlurRadiusProperty has a callback that routes to UpdateShadowRadius, but no shadow is
    /// ever created because the initialization path is broken. This file can be removed entirely.
    /// Kept only to document its existence before deletion.
    /// </summary>
    public static class ShadowHelper
    {
        // Mark whether shadow has been initialized
        private static readonly DependencyProperty IsInitializedProperty = DependencyProperty.RegisterAttached(
            "IsInitialized",
            typeof(bool),
            typeof(ShadowHelper),
            new PropertyMetadata(false));

        // Attached property storing the DropShadow instance
        private static readonly DependencyProperty ShadowInstanceProperty = DependencyProperty.RegisterAttached(
            "ShadowInstance",
            typeof(DropShadow),
            typeof(ShadowHelper),
            null);

        // Bindable blur radius property
        public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.RegisterAttached(
            "BlurRadius",
            typeof(float),
            typeof(ShadowHelper),
            new PropertyMetadata(10f, OnBlurRadiusChanged));

        // Called when the target control loads — supposed to initialize shadow
        private static void OnBlurRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;

            if ((bool)element.GetValue(IsInitializedProperty))
            {
                UpdateShadowRadius(element, (float)e.NewValue);
                return;
            }

            //element.Lo // STUB: incomplete — shadow creation never happens
        }




        private static void UpdateShadowRadius(UIElement element, float radius)
        {
            if (element.GetValue(ShadowInstanceProperty) is DropShadow shadow)
            {
                shadow.BlurRadius = radius; // Live update — but shadow is never created
            }
        }
    }
}
