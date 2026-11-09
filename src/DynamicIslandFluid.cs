using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        bool fluid,fluidMoving;
        internal bool FluidActive {get{return fluid;}}
        internal bool FluidMoving {get{return fluidMoving;}}

        // Wraps the island content with the light underneath and the rim on top.
        Grid BuildFluid(Grid content)
        {
            var warm=new Ellipse {Width=120,Height=58,Fill=FluidPaint.Glow(Color.FromArgb(0xA0,0xFF,0x8A,0x3D)),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(-34,0,0,-30),RenderTransform=warmShift};
            var cool=new Ellipse {Width=130,Height=62,Fill=FluidPaint.Glow(Color.FromArgb(0x8C,0x8C,0x6B,0xFF)),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,-38,-34),RenderTransform=coolShift};
            fluidLight.Children.Add(warm);
            fluidLight.Children.Add(cool);
            fluidOrb.Children.Add(new Ellipse {Width=18,Height=18,Stroke=FluidPaint.Halo(),StrokeThickness=3});
            fluidOrb.Children.Add(new Ellipse {Width=10,Height=10,Stroke=Brushes.White,StrokeThickness=1.7});
            Grid.SetColumn(fluidOrb,1);
            compact.Children.Add(fluidOrb);
            var wrapper=new Grid();
            wrapper.Children.Add(fluidLight);
            wrapper.Children.Add(content);
            wrapper.Children.Add(fluidRim);
            return wrapper;
        }

        // Called with every presentation; changes nothing unless the preference moved.
        void SetFluid(bool on)
        {
            if(on==fluid)return;
            fluid=on;
            var visible=on?Visibility.Visible:Visibility.Collapsed;
            fluidLight.Visibility=fluidRim.Visibility=fluidOrb.Visibility=visible;
            SetMaterial(on?FluidBase:Black,on?FluidBase:Black);
            // About one overshoot of a tenth: the shape stretches past its size and settles back.
            slide.Damping=on?.6:1;
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
                new GradientStop(Color.FromArgb(0x70,0xFF,0xFF,0xFF),0),
                new GradientStop(Color.FromArgb(0x16,0xFF,0xFF,0xFF),.55),
                new GradientStop(Color.FromArgb(0x40,0xFF,0xB0,0x7A),1)
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
        internal static Brush Halo()
        {
            var brush=new SolidColorBrush(Color.FromArgb(0x55,0xFF,0xFF,0xFF));
            brush.Freeze();
            return brush;
        }
    }
}
