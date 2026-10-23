using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace OneTaskBar
{
    // Native mouse messages reveal an auto-hidden dock without an idle polling timer.
    internal sealed class DockRevealEdge : IDisposable
    {
        readonly HwndSource source;readonly System.Drawing.Rectangle screen;readonly Action reveal;bool visible,disposed;
        internal IntPtr Handle {get{return source.Handle;}}
        internal bool Visible {get{return visible;}}
        internal DockRevealEdge(System.Drawing.Rectangle bounds,Action entered)
        {
            screen=bounds;reveal=entered;var p=new HwndSourceParameters("OneTaskBar.RevealEdge") {WindowStyle=unchecked((int)0x80000000),ExtendedWindowStyle=0x08000088,PositionX=bounds.Left,PositionY=bounds.Bottom-2,Width=bounds.Width,Height=2,UsesPerPixelOpacity=true};
            source=new HwndSource(p);source.CompositionTarget.BackgroundColor=Colors.Transparent;source.RootVisual=new Border {Background=Paint.Brush("#01000000")};source.AddHook(Message);
        }
        IntPtr Message(IntPtr h,int message,IntPtr wp,IntPtr lp,ref bool handled)
        {
            if(message==0x21){handled=true;return new IntPtr(3);}if(message==0x200&&visible){Show(false);reveal();}return IntPtr.Zero;
        }
        internal void Show(bool value)
        {
            if(disposed||visible==value)return;visible=value;if(value)Native.SetWindowPos(source.Handle,new IntPtr(-1),screen.Left,screen.Bottom-2,screen.Width,2,0x10|0x40);else Native.ShowWindow(source.Handle,0);
        }
        public void Dispose(){if(disposed)return;Show(false);disposed=true;source.RootVisual=null;source.Dispose();}
    }
}
