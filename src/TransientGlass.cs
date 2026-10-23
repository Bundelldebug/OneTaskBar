using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    // One finite clock drives both the WPF surface and its separate blur HWND.
    internal class TransientGlass : GlassWindow
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool EnableWindow(IntPtr window,bool enabled);
        readonly DispatcherTimer clock=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(16)};
        readonly Stopwatch watch=new Stopwatch();double from,to;int duration;bool closeAfter,closed;
        internal bool Fading {get{return clock.IsEnabled;}}
        internal TransientGlass(){Opacity=0;ShowActivated=false;Focusable=false;IsHitTestVisible=false;Surface.BorderThickness=new Thickness(1);SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)|0x080000A0);};clock.Tick+=(s,e)=>Frame();Closed+=(s,e)=>{closed=true;clock.Stop();};}
        public new void ShowAt(System.Drawing.Rectangle screen,double x,double y,double width,double height){new WindowInteropHelper(this).EnsureHandle();Native.BlurOpacity(this,0);base.ShowAt(screen,x,y,width,height);EnableWindow(new WindowInteropHelper(this).Handle,false);}
        internal void Reveal(){clock.Stop();Opacity=0;Native.BlurOpacity(this,0);Fade(1,150,false);}
        internal void FadeAway(){if(closed||closeAfter)return;Fade(0,110,true);}
        void Fade(double value,int milliseconds,bool finish){from=Opacity;to=value;duration=milliseconds;closeAfter=finish;if(!Motion.Enabled){Apply(value);if(finish)Close();return;}watch.Restart();clock.Start();}
        void Frame(){if(closed){clock.Stop();return;}double t=Math.Min(1,watch.Elapsed.TotalMilliseconds/duration);Apply(from+(to-from)*(1-Math.Pow(1-t,3)));if(t>=1){clock.Stop();if(closeAfter)Close();}}
        void Apply(double opacity){Opacity=opacity;Native.BlurOpacity(this,opacity);}
    }
    internal static class ActionNotice
    {
        static TransientGlass current;
        internal static void Show(string message)
        {
            if(current!=null)current.Close();var screen=Forms.Screen.FromPoint(Forms.Cursor.Position);var notice=new TransientGlass {Title="One TaskBar · 안내",Width=330,Height=68,Radius=24};current=notice;notice.Surface.CornerRadius=new CornerRadius(24);notice.Surface.Background=QuickPanel.Glass;var text=Paint.TextBlock(message,13,Paint.Text);text.TextWrapping=TextWrapping.Wrap;text.TextAlignment=TextAlignment.Center;text.Margin=new Thickness(15,8,15,8);notice.Surface.Child=text;
            double scale=Native.PrepareMonitor(notice,screen.Bounds),width=Math.Min(330,screen.Bounds.Width/scale-24);notice.ShowAt(screen.Bounds,(screen.Bounds.Width/scale-width)/2,Math.Max(12,(screen.WorkingArea.Bottom-screen.Bounds.Top)/scale-100),width,68);notice.Reveal();var expiry=new DispatcherTimer {Interval=TimeSpan.FromSeconds(2.5)};expiry.Tick+=(s,e)=>{expiry.Stop();notice.FadeAway();};notice.Closed+=(s,e)=>{expiry.Stop();if(current==notice)current=null;};expiry.Start();
        }
    }
}
