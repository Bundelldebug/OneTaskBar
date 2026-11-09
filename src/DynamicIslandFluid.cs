using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace OneTaskBar
{
    // The Fluid desktop style's look for the island: dark glass with warm light drifting under it, a thin lit rim,
    // a small orb ring and an elastic spring. None of it is active until the style is chosen, and the drifting
    // light and the orb only move while the music plays.
    internal sealed partial class DynamicIsland
    {
        static readonly Brush FluidBase=FluidPaint.Base();
        readonly Border fluidRim=new Border {BorderThickness=new Thickness(1),IsHitTestVisible=false,Visibility=Visibility.Collapsed,BorderBrush=FluidPaint.Rim()};
        readonly Grid fluidLight=new Grid {IsHitTestVisible=false,Visibility=Visibility.Collapsed};
        readonly Grid fluidOrb=new Grid {Width=18,Height=18,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false,Visibility=Visibility.Collapsed,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(1,1)};
        readonly TranslateTransform warmShift=new TranslateTransform(),coolShift=new TranslateTransform();
        // The light that pours across the island when something appears: a feathered white fill that runs left to right.
        readonly Rectangle pour=new Rectangle {IsHitTestVisible=false,Visibility=Visibility.Collapsed,Opacity=0};
        readonly GradientStop pourLit=new GradientStop(Color.FromArgb(0x4A,255,248,240),0),pourEdge=new GradientStop(Color.FromArgb(0x3C,255,248,240),-.4),pourEnd=new GradientStop(Color.FromArgb(0,255,248,240),-.05);
        // A burst of white around the orb at the left end, like the glow in the reference when the capsule fills.
        readonly Ellipse bloom=new Ellipse {Width=120,Height=120,IsHitTestVisible=false,Visibility=Visibility.Collapsed,Opacity=0,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(-44,0,0,0),RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(.4,.4),Fill=FluidPaint.Glow(Color.FromArgb(0xF0,255,250,244))};
        bool fluid,fluidMoving;
        Ellipse warmGlow;int glowKey;
        internal bool FluidActive {get{return fluid;}}
        internal bool FluidMoving {get{return fluidMoving;}}

        // Wraps the island content with the light underneath and the rim on top.
        Grid BuildFluid(Grid content)
        {
            warmGlow=new Ellipse {Width=120,Height=58,Fill=FluidPaint.Glow(Fluid.Glow(0xA0)),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(-34,0,0,-30),RenderTransform=warmShift};
            var cool=new Ellipse {Width=130,Height=62,Fill=FluidPaint.Glow(Color.FromArgb(0x8C,0x8C,0x6B,0xFF)),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,-38,-34),RenderTransform=coolShift};
            // The key light: a white highlight in the upper left, where the rim is brightest.
            var key=new Ellipse {Width=96,Height=44,Fill=FluidPaint.Glow(Color.FromArgb(0x58,255,255,255)),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(-26,-20,0,0)};
            fluidLight.Children.Add(warmGlow);
            fluidLight.Children.Add(cool);
            fluidLight.Children.Add(key);
            // The orb is a small grey glass sphere with a highlight and a soft halo, as in the reference.
            fluidOrb.Children.Add(new Ellipse {Width=18,Height=18,Stroke=FluidPaint.Halo(),StrokeThickness=2.2});
            fluidOrb.Children.Add(new Ellipse {Width=11,Height=11,Fill=FluidPaint.Sphere(),Stroke=FluidPaint.SphereRim(),StrokeThickness=.8});
            Grid.SetColumn(fluidOrb,1);
            compact.Children.Add(fluidOrb);
            var fill=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,0)};
            fill.GradientStops.Add(pourLit);
            fill.GradientStops.Add(pourEdge);
            fill.GradientStops.Add(pourEnd);
            pour.Fill=fill;
            var wrapper=new Grid();
            wrapper.Children.Add(fluidLight);
            wrapper.Children.Add(pour);
            wrapper.Children.Add(content);
            wrapper.Children.Add(bloom);
            wrapper.Children.Add(fluidRim);
            return wrapper;
        }

        // Called with every presentation; changes nothing unless the preference moved.
        void SetFluid(bool on)
        {
            if(on&&glowKey!=Fluid.StyleKey){glowKey=Fluid.StyleKey;warmGlow.Fill=FluidPaint.Glow(Fluid.Glow(0xA0));fluidRim.BorderBrush=FluidPaint.Rim();}
            if(on==fluid)return;
            fluid=on;
            var visible=on?Visibility.Visible:Visibility.Collapsed;
            fluidLight.Visibility=fluidRim.Visibility=fluidOrb.Visibility=visible;
            if(!on){pour.Visibility=bloom.Visibility=Visibility.Collapsed;}
            SetMaterial(on?FluidBase:Black,on?FluidBase:Black);
            // About one overshoot of a tenth: the shape stretches past its size and settles back.
            slide.Damping=on?.8:1;
            slide.Omega=on?30:38;
            if(!on)SetFluidMotion(false);
            if(IsVisible&&!current.IsEmpty)PlaceShape();
        }

        // The light drifts and the orb breathes only while something is playing and animations are on.
        void SetFluidMotion(bool moving)
        {
            moving=moving&&fluid&&Motion.Enabled;
            if(moving==fluidMoving)return;
            fluidMoving=moving;
            var scale=(ScaleTransform)fluidOrb.RenderTransform;
            if(!moving)
            {
                warmShift.BeginAnimation(TranslateTransform.XProperty,null);
                coolShift.BeginAnimation(TranslateTransform.XProperty,null);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
                fluidOrb.BeginAnimation(UIElement.OpacityProperty,null);
                return;
            }
            Drift(warmShift,0,70,5.2);
            Drift(coolShift,0,-84,6.4);
            Breathe(scale,ScaleTransform.ScaleXProperty,.86,1.1,2.4);
            Breathe(scale,ScaleTransform.ScaleYProperty,.86,1.1,2.4);
            var glow=new DoubleAnimation(.55,1,TimeSpan.FromSeconds(2.4)) {AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase()};
            Timeline.SetDesiredFrameRate(glow,30);
            fluidOrb.BeginAnimation(UIElement.OpacityProperty,glow);
        }

        static void Drift(TranslateTransform shift,double from,double to,double seconds)
        {
            var move=new DoubleAnimation(from,to,TimeSpan.FromSeconds(seconds)) {AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase()};
            Timeline.SetDesiredFrameRate(move,30);
            shift.BeginAnimation(TranslateTransform.XProperty,move);
        }

        static void Breathe(ScaleTransform scale,DependencyProperty property,double from,double to,double seconds)
        {
            var breathe=new DoubleAnimation(from,to,TimeSpan.FromSeconds(seconds)) {AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase()};
            Timeline.SetDesiredFrameRate(breathe,30);
            scale.BeginAnimation(property,breathe);
        }

        // The reference's arrival, in order: the glass is empty, light bursts around the orb, pours across and fills the
        // capsule with a soft leading edge, and the words come into focus out of a blur. It runs once per arrival, and
        // the blur is removed when it ends so nothing is left rendering.
        void Reveal(FrameworkElement content)
        {
            Motion.Fade(content,0,1,320);
            var blur=new BlurEffect {Radius=9,KernelType=KernelType.Gaussian};
            content.Effect=blur;
            var focus=new DoubleAnimation(9,0,TimeSpan.FromMilliseconds(420)) {BeginTime=TimeSpan.FromMilliseconds(140),EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};
            focus.Completed+=(s,e)=>{if(Object.ReferenceEquals(content.Effect,blur))content.Effect=null;};
            blur.BeginAnimation(BlurEffect.RadiusProperty,focus);
            Pour();
            Bloom();
        }

        void Pour()
        {
            pour.Visibility=Visibility.Visible;
            var ease=new CubicEase {EasingMode=EasingMode.EaseOut};
            pourEdge.BeginAnimation(GradientStop.OffsetProperty,new DoubleAnimation(-.4,1.05,TimeSpan.FromMilliseconds(520)) {EasingFunction=ease});
            pourEnd.BeginAnimation(GradientStop.OffsetProperty,new DoubleAnimation(-.05,1.4,TimeSpan.FromMilliseconds(520)) {EasingFunction=ease});
            var glow=new DoubleAnimationUsingKeyFrames();
            glow.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            glow.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
            glow.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420))));
            glow.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1250))));
            glow.Completed+=(s,e)=>pour.Visibility=Visibility.Collapsed;
            pour.BeginAnimation(UIElement.OpacityProperty,glow);
        }

        void Bloom()
        {
            bloom.Visibility=Visibility.Visible;
            var scale=(ScaleTransform)bloom.RenderTransform;
            var grow=new DoubleAnimation(.35,1.5,TimeSpan.FromMilliseconds(620)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,grow);
            var flash=new DoubleAnimationUsingKeyFrames();
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(.9,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110))));
            flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(760))));
            flash.Completed+=(s,e)=>bloom.Visibility=Visibility.Collapsed;
            bloom.BeginAnimation(UIElement.OpacityProperty,flash);
        }

        // The pill keeps its fluid corners while it overshoots, and never leaves the host window.
        double FluidShape(double radius)
        {
            var bounds=new Rect(0,0,host.Width,host.Height);
            if(!host.IsEmpty)current.Intersect(bounds);
            radius=Math.Min(radius,Math.Min(current.Width,current.Height)/2);
            fluidRim.CornerRadius=new CornerRadius(radius);
            return radius;
        }
    }

    // Frozen brushes of the fluid material, built once.
    internal static class FluidPaint
    {
        internal static Brush Base()
        {
            var brush=new LinearGradientBrush(Color.FromRgb(0x16,0x17,0x20),Color.FromRgb(0x27,0x1D,0x28),90);
            brush.Freeze();
            return brush;
        }
        internal static Brush Rim()
        {
            var brush=new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0xB4,0xFF,0xFF,0xFF),0),
                new GradientStop(Color.FromArgb(0x16,0xFF,0xFF,0xFF),.55),
                new GradientStop(Fluid.Pale(0x50),1)
            },90);
            brush.Freeze();
            return brush;
        }
        internal static Brush Glow(Color color)
        {
            var brush=new RadialGradientBrush(color,Color.FromArgb(0,color.R,color.G,color.B));
            brush.Freeze();
            return brush;
        }
        internal static Brush Sphere()
        {
            var brush=new RadialGradientBrush {GradientOrigin=new Point(.34,.28),Center=new Point(.45,.45),RadiusX=.7,RadiusY=.7};
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF,0xFF,0xFF),0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xC9,0xCD,0xD6),.38));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x6B,0x70,0x7C),1));
            brush.Freeze();
            return brush;
        }
        internal static Brush SphereRim()
        {
            var brush=new SolidColorBrush(Color.FromArgb(0xB4,255,255,255));
            brush.Freeze();
            return brush;
        }
        internal static Brush Halo()
        {
            var brush=new SolidColorBrush(Color.FromArgb(0x55,0xFF,0xFF,0xFF));
            brush.Freeze();
            return brush;
        }
    }
}
