using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class DockShellChecks
    {
        internal static async Task Run(DockHost dock)
        {
            foreach(var window in new[]{dock.Start,dock.Apps,dock.Status})
            {
                var handle=new WindowInteropHelper(window).Handle;
                if(window.ShowInTaskbar||(Native.GetWindowLong(handle,-20)&0x40080)!=0x80)
                    throw new Exception("A dock surface entered the shell application list after Show");
                Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x40000)&~0x80);
                ShellWindowPolicy.Apply(window,true);
                if((Native.GetWindowLong(handle,-20)&0x40080)!=0x80)
                    throw new Exception("Task View exclusion did not recover from a changed native style");
            }
            // Simulate an external shell moving a dock after its initial layout.
            await Task.Delay(2100);
            var app=new WindowInteropHelper(dock.Apps).Handle;
            Native.RectI expected;
            Native.GetWindowRect(app,out expected);
            Native.SetWindowPos(app,IntPtr.Zero,expected.Left,expected.Top-120,0,0,0x15);
            dock.UpdateVisibility(false,false);
            Native.RectI actual;
            Native.GetWindowRect(app,out actual);
            if(!actual.Equals(expected))throw new Exception("Externally moved dock did not return to its screen edge");
            foreach(int edge in new[]{0,1,2,3})
            {
                var screen=new System.Drawing.Rectangle(-1920,-360,1920,1080);
                var anchor=new Point(edge==0?screen.Left+22:edge==2?screen.Right-22:screen.Left+960,edge==1?screen.Top+22:edge==3?screen.Bottom-22:screen.Top+540);
                var placement=new TrayPlacement(anchor,screen,1,edge);
                var point=placement.Target(240,260);
                bool inward=edge==0?point.X>anchor.X:edge==2?point.X+240<anchor.X:edge==1?point.Y>anchor.Y:point.Y+260<anchor.Y;
                if(!inward||point.X<screen.Left||point.Y<screen.Top||point.X+240>screen.Right||point.Y+260>screen.Bottom)
                    throw new Exception("Tray popup did not open inward from the selected dock edge");
                int invoked=0;
                using(var reveal=new DockRevealEdge(screen,()=>invoked++,edge))
                {
                    reveal.Show(true);
                    var area=DockGeometry.Reveal(screen,edge);
                    bool ignored=!reveal.ObservePointer(new Native.PointI {X=screen.Left+200,Y=screen.Top+200});
                    bool entered=reveal.ObservePointer(new Native.PointI {X=area.Left,Y=area.Top});
                    if(!ignored||!entered||invoked!=1||reveal.Visible)
                        throw new Exception("Auto-hide fallback must reveal only at its own monitor edge");
                }
            }
            bool blur=Preferences.Current.Blur;
            try
            {
                Preferences.Current.Blur=false;
                if(Glass95.Enabled&&(Glass95.Rim!=Brushes.Transparent||Glass95.Card is GradientBrush))
                    throw new Exception("Solid panels still contain glass rim or card gradients");
            }
            finally {Preferences.Current.Blur=blur;}
            if(SystemPanelPlacement.Supported("SamsungBrowser","quick")||SystemPanelPlacement.Supported("explorer","start"))
                throw new Exception("Native panel placement may not move application windows");
        }
    }
}
