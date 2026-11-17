using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // A short white glow laid over a piece of the dock for half a second. The dock's own windows have no rim layer to
    // draw on, so the Fluid notification borrows this click-through window to light the bar the card rises from.
    internal sealed class GlowCue : Window
    {
        const double Milliseconds=520;
        static GlowCue shared;
        readonly Border glow=new Border {Opacity=0,IsHitTestVisible=false};
        readonly RadialGradientBrush brush=new RadialGradientBrush {RadiusX=.55,RadiusY=1.4};
        GlowCue()
        {
            WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;
            Topmost=true;ResizeMode=ResizeMode.NoResize;Focusable=false;IsHitTestVisible=false;Title="One TaskBar · 알림 글로우";
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(235,255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(120,214,232,255),.45));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0,214,232,255),1));
            glow.Background=brush;Content=glow;
            // Never takes input or focus and stays out of Alt+Tab.
            SourceInitialized+=(s,e)=>{var handle=new WindowInteropHelper(this).Handle;Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x80|0x20|0x08000000);};
        }
        // rect and radius describe the bar piece in device pixels; anchor is where the glow is brightest (0..1).
        internal static void Play(System.Drawing.Rectangle screen,Native.RectI rect,double radius,Point anchor)
        {
            if(!Motion.Enabled||Performance.Light)return;
            if(shared==null)shared=new GlowCue();
            var window=shared;
            double scale=Native.PrepareMonitor(window,screen);
            double width=(rect.Right-rect.Left)/scale,height=(rect.Bottom-rect.Top)/scale;
            if(width<8||height<8)return;
            window.glow.CornerRadius=new CornerRadius(radius);
            window.brush.Center=window.brush.GradientOrigin=anchor;
            window.Width=width;window.Height=height;
            new WindowInteropHelper(window).EnsureHandle();
            Native.Place(window,screen,(rect.Left-screen.Left)/scale,(rect.Top-screen.Top)/scale,width,height);
            if(!window.IsVisible)window.Show();
            Native.Place(window,screen,(rect.Left-screen.Left)/scale,(rect.Top-screen.Top)/scale,width,height);
            var fade=new DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(Milliseconds)),SwiftEase.Instance));
            fade.Completed+=(s,e)=>{if(window.glow.Opacity<.01&&window.IsVisible)window.Hide();};
            window.glow.BeginAnimation(UIElement.OpacityProperty,fade);
        }
        internal static void Release()
        {
            if(shared==null)return;
            var window=shared;shared=null;
            try{window.glow.BeginAnimation(UIElement.OpacityProperty,null);window.Close();}catch(Exception){}
        }
        internal static bool Showing {get{return shared!=null&&shared.IsVisible&&shared.glow.Opacity>.01;}}
    }
}
