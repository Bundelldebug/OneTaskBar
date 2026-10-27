using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace OneTaskBar
{
    internal sealed class FullscreenCandidate
    {
        internal Native.RectI Bounds,Client;
        internal Rectangle Monitor;
        internal bool Maximized,Framed;
    }
    internal static class FullscreenVisibility
    {
        [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr window,out Native.RectI rect);
        [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr window,ref Native.PointI point);
        internal static bool PassiveSurface(int extended,bool foreground,string kind,bool titled)
        {
            if(kind=="Progman"||kind=="WorkerW"||kind=="DesktopWindowXamlSource"||kind.StartsWith("Shell_",StringComparison.Ordinal)||kind=="tooltips_class32")return true;
            if(foreground)return false;
            bool app=(extended&0x40000)!=0;
            return !app&&((extended&(0x08000000|0x20))!=0||!titled&&(extended&0x80)!=0);
        }
        internal static List<FullscreenCandidate> Capture()
        {
            var result=new List<FullscreenCandidate>();uint own=(uint)Process.GetCurrentProcess().Id;
            Native.EnumWindows((window,state)=>{uint pid;Native.GetWindowThreadProcessId(window,out pid);if(pid==own)return true;var candidate=Inspect(window);if(candidate!=null)result.Add(candidate);return true;},IntPtr.Zero);return result;
        }
        internal static FullscreenCandidate Inspect(IntPtr window)
        {
            if(!Native.IsWindowVisible(window)||Native.IsIconic(window))return null;
            int cloaked;if(Native.DwmGetWindowAttribute(window,14,out cloaked,4)>=0&&cloaked!=0)return null;
            var name=new StringBuilder(100);Native.GetClassName(window,name,100);string kind=name.ToString();
            var title=new StringBuilder(256);Native.GetWindowText(window,title,title.Capacity);
            bool foreground=Native.GetAncestor(Native.GetForegroundWindow(),3)==window;
            if(PassiveSurface(Native.GetWindowLong(window,-20),foreground,kind,title.Length>0))return null;
            Native.RectI bounds,client;if(!Native.GetWindowRect(window,out bounds)||!GetClientRect(window,out client)||bounds.Right<=bounds.Left||bounds.Bottom<=bounds.Top)return null;
            var first=new Native.PointI {X=client.Left,Y=client.Top};var last=new Native.PointI {X=client.Right,Y=client.Bottom};if(!ClientToScreen(window,ref first)||!ClientToScreen(window,ref last))return null;
            var candidate=new FullscreenCandidate {Bounds=bounds,Client=new Native.RectI {Left=first.X,Top=first.Y,Right=last.X,Bottom=last.Y},Monitor=System.Windows.Forms.Screen.FromHandle(window).Bounds,Maximized=Native.IsZoomed(window),Framed=(Native.GetWindowLong(window,-16)&0xC40000)!=0};if((Native.GetWindowLong(window,-20)&0x80)!=0&&!ClientCovers(candidate.Client,candidate.Monitor)&&!Native.CoversMonitor(candidate.Bounds,candidate.Monitor,candidate.Maximized,candidate.Framed))return null;return candidate;
        }
        static bool ClientCovers(Native.RectI rect,Rectangle screen){return rect.Left<=screen.Left+1&&rect.Top<=screen.Top+1&&rect.Right>=screen.Right-1&&rect.Bottom>=screen.Bottom-1;}
        internal static bool IsFullScreen(Rectangle screen,IEnumerable<FullscreenCandidate> windows)
        {
            // EnumWindows is in stacking order. An inactive fullscreen window on a
            // second monitor still hides that monitor's dock; a normal app above it does not.
            foreach(var window in windows){var overlap=Rectangle.Intersect(screen,Rectangle.FromLTRB(window.Bounds.Left,window.Bounds.Top,window.Bounds.Right,window.Bounds.Bottom));if(overlap.Width<=0||overlap.Height<=0)continue;if(ClientCovers(window.Client,screen)||Native.CoversMonitor(window.Bounds,screen,window.Maximized,window.Framed))return true;if(window.Monitor==screen)return false;}return false;
        }
    }
}
