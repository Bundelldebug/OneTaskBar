using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    // A layered glass window keeps the last picture it drew. Shown again, Windows puts that old picture up until WPF
    // draws the first frame of the new opening, so a Dex panel would flash at its full size before it grows (issue 30).
    // The window stays cloaked from Show until a frame of the new state has been drawn.
    internal static class FirstFrameCloak
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        const int Cloak=13;

        internal static bool Hide(Window window)
        {
            var handle=new WindowInteropHelper(window).Handle;
            if(handle==IntPtr.Zero)return false;
            int on=1;
            return DwmSetWindowAttribute(handle,Cloak,ref on,4)==0;
        }

        // Rendering runs before a frame is drawn, so the second call means one frame of the new state is on screen.
        internal static void RevealAfterFrame(Window window)
        {
            int frames=0;
            EventHandler tick=null;
            DispatcherTimer safety=new DispatcherTimer(DispatcherPriority.Normal,window.Dispatcher) {Interval=TimeSpan.FromMilliseconds(120)};
            Action reveal=()=>
            {
                CompositionTarget.Rendering-=tick;
                safety.Stop();
                Show(window);
            };
            tick=(s,e)=>
            {
                frames++;
                if(frames>=2)reveal();
            };
            safety.Tick+=(s,e)=>reveal();
            CompositionTarget.Rendering+=tick;
            safety.Start();
        }

        internal static void Show(Window window)
        {
            var handle=new WindowInteropHelper(window).Handle;
            if(handle==IntPtr.Zero)return;
            int off=0;
            DwmSetWindowAttribute(handle,Cloak,ref off,4);
        }
    }
}
