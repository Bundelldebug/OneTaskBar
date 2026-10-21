using System;
using System.Linq;
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
        internal static DoubleAnimationUsingKeyFrames PanelTween(double from,double to,int milliseconds)
        {
            var animation=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromMilliseconds(milliseconds),FillBehavior=FillBehavior.Stop};
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(to,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds)),new KeySpline(.22,.72,.2,1)));
            return animation;
        }
        static DoubleAnimationUsingKeyFrames Spring(double from,double peak,double target,int milliseconds)
        {
            var motion=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromMilliseconds(milliseconds),FillBehavior=FillBehavior.Stop};
            motion.KeyFrames.Add(new LinearDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            motion.KeyFrames.Add(new SplineDoubleKeyFrame(peak,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds*.74)),new KeySpline(.18,.8,.16,1)));
            motion.KeyFrames.Add(new SplineDoubleKeyFrame(target,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds)),new KeySpline(.25,0,.3,1)));return motion;
        }
        internal static DoubleAnimation Launcher(FrameworkElement content,bool opening)
        {
            var old=content.RenderTransform as TransformGroup;var oldScale=old==null?null:old.Children.OfType<ScaleTransform>().FirstOrDefault();var oldShift=old==null?null:old.Children.OfType<TranslateTransform>().FirstOrDefault();
            double fromScale=oldScale!=null?oldScale.ScaleX:opening?.965:1,fromY=oldShift!=null?oldShift.Y:opening?24:0,fromOpacity=old!=null?content.Opacity:opening?.12:content.Opacity;
            content.BeginAnimation(UIElement.OpacityProperty,null);var scale=new ScaleTransform(opening?1:.985,opening?1:.985);var shift=new TranslateTransform(0,opening?0:12);var group=new TransformGroup();group.Children.Add(scale);group.Children.Add(shift);content.RenderTransformOrigin=new Point(.5,1);content.RenderTransform=group;
            if(opening){scale.BeginAnimation(ScaleTransform.ScaleXProperty,Spring(fromScale,1.005,1,340));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Spring(fromScale,1.005,1,340));shift.BeginAnimation(TranslateTransform.YProperty,Spring(fromY,-1.2,0,340));}
            else {scale.BeginAnimation(ScaleTransform.ScaleXProperty,Tween(fromScale,.985,145));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Tween(fromScale,.985,145));shift.BeginAnimation(TranslateTransform.YProperty,Tween(fromY,12,145));}
            content.Opacity=opening?1:0;var fade=Tween(fromOpacity,opening?1:0,opening?135:145);if(opening)content.BeginAnimation(UIElement.OpacityProperty,fade);return fade;
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
        internal static void Dock(Button button,FrameworkElement icon)
        {
            var scale=new ScaleTransform(1,1);var shift=new TranslateTransform();var group=new TransformGroup();group.Children.Add(scale);group.Children.Add(shift);icon.RenderTransform=group;icon.RenderTransformOrigin=new Point(.5,.5);bool bouncing=false;
            Action<double> settle=value=>{if(bouncing)return;double from=scale.ScaleX;scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);scale.ScaleX=scale.ScaleY=value;if(Enabled){scale.BeginAnimation(ScaleTransform.ScaleXProperty,Tween(from,value,140));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Tween(from,value,140));}};
            button.MouseEnter+=(s,e)=>settle(Preferences.Current.Hover?1.08:1);button.MouseLeave+=(s,e)=>settle(1);
            button.PreviewMouseLeftButtonDown+=(s,e)=>settle(.88);
            button.LostMouseCapture+=(s,e)=>settle(button.IsMouseOver&&Preferences.Current.Hover?1.08:1);
            button.Click+=(s,e)=>{
                if(!Enabled||DragReorder.Active)return;bouncing=true;double target=button.IsMouseOver&&Preferences.Current.Hover?1.08:1;
                var pulse=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromMilliseconds(360),FillBehavior=FillBehavior.Stop};pulse.KeyFrames.Add(new LinearDoubleKeyFrame(.88,KeyTime.FromTimeSpan(TimeSpan.Zero)));pulse.KeyFrames.Add(new SplineDoubleKeyFrame(1.12,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(125)),new KeySpline(.15,.65,.25,1)));pulse.KeyFrames.Add(new SplineDoubleKeyFrame(target,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)),new KeySpline(.2,.6,.25,1)));
                scale.ScaleX=scale.ScaleY=target;scale.BeginAnimation(ScaleTransform.ScaleXProperty,pulse);scale.BeginAnimation(ScaleTransform.ScaleYProperty,pulse);
                var lift=new DoubleAnimationUsingKeyFrames {Duration=pulse.Duration,FillBehavior=FillBehavior.Stop};lift.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));lift.KeyFrames.Add(new SplineDoubleKeyFrame(-1.5,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(125)),new KeySpline(.15,.65,.25,1)));lift.KeyFrames.Add(new SplineDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)),new KeySpline(.2,.6,.25,1)));lift.Completed+=(sender,args)=>{bouncing=false;settle(button.IsMouseOver&&Preferences.Current.Hover?1.08:1);};shift.BeginAnimation(TranslateTransform.YProperty,lift);
            };
            button.Unloaded+=(s,e)=>{bouncing=false;scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);shift.BeginAnimation(TranslateTransform.YProperty,null);scale.ScaleX=scale.ScaleY=1;shift.Y=0;};
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
