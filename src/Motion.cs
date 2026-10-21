using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    internal static class Motion
    {
        internal static bool Enabled {get{return Preferences.Current.Animations&&SystemParameters.ClientAreaAnimation;}}
        internal static DoubleAnimation Tween(double from,double to,int milliseconds)
        {
            return new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(milliseconds)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        }
        internal static void Fade(FrameworkElement element,double from,double to,int milliseconds)
        {
            element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=to;
            if(Enabled&&element.IsVisible)element.BeginAnimation(UIElement.OpacityProperty,Tween(from,to,milliseconds));
        }
        internal static void Button(Button button)
        {
            Action<double,double> apply=(scale,opacity)=> {
                button.ApplyTemplate();var part=button.Template.FindName("p",button) as Border;if(part==null)return;
                var transform=part.RenderTransform as ScaleTransform;if(transform==null){transform=new ScaleTransform(1,1);part.RenderTransform=transform;part.RenderTransformOrigin=new Point(.5,.5);}
                double x=transform.ScaleX,y=transform.ScaleY;transform.BeginAnimation(ScaleTransform.ScaleXProperty,null);transform.BeginAnimation(ScaleTransform.ScaleYProperty,null);
                transform.ScaleX=transform.ScaleY=scale;
                if(Enabled){transform.BeginAnimation(ScaleTransform.ScaleXProperty,Tween(x,scale,150));transform.BeginAnimation(ScaleTransform.ScaleYProperty,Tween(y,scale,150));}
                Fade(part,part.Opacity,opacity,130);
            };
            button.MouseEnter+=(s,e)=>apply(Preferences.Current.Hover?1.025:1,.88);button.MouseLeave+=(s,e)=>apply(1,1);
            button.PreviewMouseLeftButtonDown+=(s,e)=>apply(.94,.72);button.PreviewMouseLeftButtonUp+=(s,e)=>apply(button.IsMouseOver&&Preferences.Current.Hover?1.025:1,button.IsMouseOver?.88:1);
            button.LostMouseCapture+=(s,e)=> {bool over=button.IsMouseOver&&!DragReorder.Active;apply(over&&Preferences.Current.Hover?1.025:1,over?.88:1);};
        }
        sealed class ColorState {internal Color Target;internal bool Set;}
        static readonly ConditionalWeakTable<Border,ColorState> colors=new ConditionalWeakTable<Border,ColorState>();
        internal static void Background(Border border,string color)
        {
            Color target=(Color)ColorConverter.ConvertFromString(color);var state=colors.GetValue(border,b=>new ColorState());if(state.Set&&state.Target==target)return;state.Set=true;state.Target=target;
            var previous=border.Background as SolidColorBrush;Color from=previous!=null?previous.Color:target;var brush=new SolidColorBrush(target);border.Background=brush;
            if(Enabled&&border.IsVisible)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(from,target,TimeSpan.FromMilliseconds(200)){FillBehavior=FillBehavior.Stop});
        }
    }
    internal static class SoundIcon
    {
        internal static string For(double volume,bool muted,bool available) {volume=PillSlider.Clamp(volume);return !available||muted||volume<=0?"mute":volume<=33?"volume-low":"volume";}
    }
}
