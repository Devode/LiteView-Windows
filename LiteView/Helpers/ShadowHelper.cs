using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Helpers
{
    public static class ShadowHelper
    {
        // 标记是否已初始化阴影
        private static readonly DependencyProperty IsInitializedProperty = DependencyProperty.RegisterAttached(
            "IsInitialized",
            typeof(bool),
            typeof(ShadowHelper),
            new PropertyMetadata(false));

        // 存储 DropShadow 实例的附加属性
        private static readonly DependencyProperty ShadowInstanceProperty = DependencyProperty.RegisterAttached(
            "ShadowInstance",
            typeof(DropShadow),
            typeof(ShadowHelper),
            null);

        // 可绑定的模糊半径属性
        public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.RegisterAttached(
            "BlurRadius",
            typeof(float),
            typeof(ShadowHelper),
            new PropertyMetadata(10f, OnBlurRadiusChanged));

        // 当目标控件加载时初始化阴影
        private static void OnBlurRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;

            if ((bool)element.GetValue(IsInitializedProperty))
            {
                UpdateShadowRadius(element, (float)e.NewValue);
                return;
            }

            //element.Lo
        }




        private static void UpdateShadowRadius(UIElement element, float radius)
        {
            if (element.GetValue(ShadowInstanceProperty) is DropShadow shadow)
            {
                shadow.BlurRadius = radius; // 直接修改实时生效
            }
        }
    }
}
