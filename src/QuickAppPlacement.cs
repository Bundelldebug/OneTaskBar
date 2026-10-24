using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace OneTaskBar
{
    // A bounded, click-triggered launch placement. There is no idle window scan.
    internal static class QuickAppPlacement
    {
        internal static System.Drawing.Rectangle Rectangle(Native.RectI r){return System.Drawing.Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);}
        internal static System.Drawing.Rectangle Fit(System.Drawing.Rectangle anchor,System.Drawing.Rectangle work,int width,int height)
        {
            int inset=Math.Min(12,Math.Min(work.Width,work.Height)/8),w=Math.Max(1,Math.Min(width,work.Width-2*inset)),h=Math.Max(1,Math.Min(height,work.Height-2*inset));
            return new System.Drawing.Rectangle(Math.Max(work.Left+inset,Math.Min(anchor.Right-w,work.Right-inset-w)),Math.Max(work.Top+inset,Math.Min(anchor.Bottom-h,work.Bottom-inset-h)),w,h);
        }
        static IntPtr Find(AppEntry app,IEnumerable<AppEntry> running)
        {
            var known=app.Windows.Concat(running.Where(a=>Catalog.Same(a,app)).SelectMany(a=>a.Windows)).FirstOrDefault(h=>Native.IsWindow(h)&&Native.IsWindowVisible(h));if(known!=IntPtr.Zero)return known;
            if(String.IsNullOrEmpty(app.AppID)||!app.AppID.Contains("!"))return IntPtr.Zero;IntPtr found=IntPtr.Zero;
            Native.EnumWindows((h,state)=>{if(!Native.IsWindowVisible(h)||Native.GetWindow(h,4)!=IntPtr.Zero||(Native.GetWindowLong(h,-20)&0x80)!=0)return true;string id=Native.WindowAppId(h);if(String.IsNullOrEmpty(id)){uint pid;Native.GetWindowThreadProcessId(h,out pid);Native.ProcessPath(pid,out id);}if(!String.Equals(id,app.AppID,StringComparison.OrdinalIgnoreCase))return true;var box=Native.VisibleArrangeBounds(h);if(box.Right-box.Left<100||box.Bottom-box.Top<100)return true;found=h;return false;},IntPtr.Zero);return found;
        }
        internal static async Task<bool> Position(AppEntry app,System.Drawing.Rectangle anchor,System.Drawing.Rectangle work,Func<bool> cancelled,Func<IEnumerable<AppEntry>> running)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();IntPtr window=IntPtr.Zero;
            while(!cancelled()&&watch.ElapsedMilliseconds<6500){window=Find(app,running());if(window!=IntPtr.Zero)break;await Task.Delay(150);}
            if(window==IntPtr.Zero||cancelled())return false;await Task.Delay(180);if(cancelled()||!Native.IsWindow(window))return false;var box=Native.VisibleArrangeBounds(window);var target=Fit(anchor,work,box.Right-box.Left,box.Bottom-box.Top);
            await WindowPlacement.Arrange(window,target,CancellationToken.None);if(cancelled())return false;Native.Activate(window);return true;
        }
    }
}
