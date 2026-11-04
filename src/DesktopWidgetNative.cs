using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class DesktopWidgetNative
    {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string kind,string name);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
        [DllImport("user32.dll")] static extern int MapWindowPoints(IntPtr from,IntPtr to,ref Native.PointI point,int count);
        [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out Native.RectI bounds);
        internal static IntPtr FindDesktop()
        {
            IntPtr result=IntPtr.Zero;
            Native.EnumWindows((window,data)=>
            {
                var kind=new StringBuilder(80);
                Native.GetClassName(window,kind,kind.Capacity);
                if(kind.ToString()!="Progman"&&kind.ToString()!="WorkerW")return true;
                result=FindWindowEx(window,IntPtr.Zero,"SHELLDLL_DefView",null);
                return result==IntPtr.Zero;
            },IntPtr.Zero);
            return result;
        }
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint relation);
        [DllImport("user32.dll")] internal static extern bool ScreenToClient(IntPtr window,ref Native.PointI point);
        [DllImport("user32.dll")] internal static extern IntPtr ChildWindowFromPointEx(IntPtr parent,Native.PointI point,uint flags);
        // The icon list is transparent, so a widget beneath it is visible but receives no mouse input.
        internal static bool IconsOnTop(IntPtr desktopView)
        {
            IntPtr list=FindWindowEx(desktopView,IntPtr.Zero,"SysListView32",null);
            return list!=IntPtr.Zero&&GetWindow(desktopView,5)==list;
        }
        // The child of the desktop view that receives a click at this screen point.
        internal static IntPtr ChildAt(IntPtr desktopView,int x,int y)
        {
            var point=new Native.PointI {X=x,Y=y};
            ScreenToClient(desktopView,ref point);
            return ChildWindowFromPointEx(desktopView,point,0);
        }
        internal static IntPtr EnterDpi(IntPtr parent)
        {
            try{return SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(parent));}
            catch(EntryPointNotFoundException){return IntPtr.Zero;}
        }
        internal static void ExitDpi(IntPtr previous)
        {
            if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);
        }
        internal static Native.PointI ParentPoint(IntPtr parent,int x,int y)
        {
            var point=new Native.PointI {X=x,Y=y};
            MapWindowPoints(IntPtr.Zero,parent,ref point,1);
            return point;
        }
        internal static bool Available(IntPtr parent)
        {
            Native.RectI rect;
            return parent!=IntPtr.Zero&&IsWindow(parent)&&GetClientRect(parent,out rect)&&rect.Right>rect.Left&&rect.Bottom>rect.Top;
        }
    }

    // A small child surface is created directly in the existing desktop view.
    // Explorer is never patched, reparented, sent WorkerW messages or restyled.
    internal sealed class DesktopWidgetHost : IDisposable
    {
        internal readonly HwndSource Source;
        internal readonly DesktopWidgetView View;
        internal readonly IntPtr Parent;
        internal Rect Pixels;
        double scale;
        bool shown,disposed;
        internal bool Alive {get{return !disposed&&!Source.IsDisposed&&DesktopWidgetNative.IsWindow(Source.Handle);}}
        internal DesktopWidgetHost(IntPtr parent,DesktopWidgetView view)
        {
            Parent=parent;
            View=view;
            IntPtr previous=DesktopWidgetNative.EnterDpi(parent);
            try
            {
                var options=new HwndSourceParameters("OneTaskBar.DesktopWidget")
                {
                    ParentWindow=parent,
                    WindowStyle=unchecked((int)0x40000000),
                    ExtendedWindowStyle=0x08000080,
                    Width=1,Height=1,
                    UsesPerPixelTransparency=true,
                    HwndSourceHook=Message
                };
                Source=new HwndSource(options);
                Source.CompositionTarget.BackgroundColor=Colors.Transparent;
                Source.RootVisual=view;
            }
            finally
            {
                DesktopWidgetNative.ExitDpi(previous);
            }
            Raise();
        }
        internal void Raise()
        {
            if(Alive)Native.SetWindowPos(Source.Handle,IntPtr.Zero,0,0,0,0,0x0013);
        }
        IntPtr Message(IntPtr window,int message,IntPtr wp,IntPtr lp,ref bool handled)
        {
            if(message==0x21)
            {
                handled=true;
                return new IntPtr(3); // MA_NOACTIVATE; clicking a toggle does not focus Explorer.
            }
            return IntPtr.Zero;
        }
        internal void Place(Rect pixels,double dpiScale)
        {
            if(!Alive)return;
            bool changed=Pixels!=pixels||Math.Abs(scale-dpiScale)>.001;
            if(!changed)return;
            Pixels=pixels;
            scale=dpiScale;
            var target=Source.CompositionTarget.TransformToDevice;
            View.LayoutTransform=new ScaleTransform(dpiScale/target.M11,dpiScale/target.M22);
            View.Width=pixels.Width/dpiScale;
            View.Height=pixels.Height/dpiScale;
            var point=DesktopWidgetNative.ParentPoint(Parent,(int)Math.Round(pixels.X),(int)Math.Round(pixels.Y));
            Native.SetWindowPos(Source.Handle,IntPtr.Zero,point.X,point.Y,(int)Math.Ceiling(pixels.Width),(int)Math.Ceiling(pixels.Height),0x0014);
        }
        internal void Present(bool visible)
        {
            if(!Alive||shown==visible)return;
            shown=visible;
            View.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
            Native.ShowWindow(Source.Handle,visible?5:0);
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            View.Retire();
            if(!Source.IsDisposed)
            {
                Source.RootVisual=null;
                Source.Dispose();
            }
        }
    }
}
