using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace OneTaskBar
{
    internal static class TransparentInk
    {
        static readonly DependencyProperty OwnedProperty=DependencyProperty.RegisterAttached("Owned",typeof(bool),typeof(TransparentInk),new PropertyMetadata(false));
        static readonly Effect dark=Shadow(Colors.Black),light=Shadow(Colors.White);
        static Effect Shadow(Color color){var value=new DropShadowEffect {Color=color,BlurRadius=3,ShadowDepth=0,Opacity=.9,RenderingBias=RenderingBias.Performance};value.Freeze();return value;}
        internal static void Apply(DependencyObject root,bool enabled,bool darkInk)
        {
            if(root is MediaPlayerView)return;var element=root as UIElement;
            if(element!=null&&(element is TextBlock||element is Glyph)){
                bool owned=(bool)element.GetValue(OwnedProperty);if(enabled&&(owned||element.Effect==null)){element.Effect=darkInk?light:dark;element.SetValue(OwnedProperty,true);}else if(!enabled&&owned){element.ClearValue(UIElement.EffectProperty);element.ClearValue(OwnedProperty);}
            }
            foreach(object value in LogicalTreeHelper.GetChildren(root)){var child=value as DependencyObject;if(child!=null)Apply(child,enabled,darkInk);}
        }
    }
}
