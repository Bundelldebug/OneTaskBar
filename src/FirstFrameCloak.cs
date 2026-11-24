using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    // A layered glass window keeps the last picture it drew. Shown again, Windows puts that old picture up until WPF
    // draws the first frame of the new opening, so a Dex panel would flash at its full size before it grows (issue 30).
    // The window stays cloaked from Show until frames of the new state have had time to reach it.
    internal static class FirstFrameCloak
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        const int Cloak=13;
        // Rendering is raised in bursts, sometimes twice within a millisecond, and says nothing about when the picture
        // reaches a layered window. A few ticks and a little real time together are what the recordings needed.
        const int Ticks=3,Milliseconds=50,Safety=250;
        static readonly HashSet<Window> holding=new HashSet<Window>();

        // An opening animation waits while its window is still cloaked, so it starts on the first frame that is seen.
        internal static bool Holding(Window window){return holding.Contains(window);}

        internal static bool Hide(Window window)
        {
            var handle=new WindowInteropHelper(window).Handle;
            if(handle==IntPtr.Zero)return false;
            int on=1;
            return DwmSetWindowAttribute(handle,Cloak,ref on,4)==0;
        }

        internal static void RevealAfterFrame(Window window)
        {
            holding.Add(window);
            int ticks=0;
            var watch=Stopwatch.StartNew();
            EventHandler tick=null;
            DispatcherTimer safety=new DispatcherTimer(DispatcherPriority.Normal,window.Dispatcher) {Interval=TimeSpan.FromMilliseconds(Safety+150)};
            Action reveal=()=>
            {
                CompositionTarget.Rendering-=tick;
                safety.Stop();
                holding.Remove(window);
                Show(window);
            };
            tick=(s,e)=>
            {
                ticks++;
                if(ticks>=Ticks&&watch.ElapsedMilliseconds>=Milliseconds)reveal();
            };
            safety.Tick+=(s,e)=>reveal();
            safety.Start();
            // The caller still lays the panel out, activates and focuses it after Show, and Rendering is raised inside
            // that work too. The count starts once that is over, when the frames are of the finished opening state.
            window.Dispatcher.BeginInvoke(new Action(()=>
            {
                if(!holding.Contains(window))return;
                watch.Restart();
                CompositionTarget.Rendering+=tick;
                safety.Interval=TimeSpan.FromMilliseconds(Safety);
            }),DispatcherPriority.Background);
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
