using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal sealed class ShellEvents : IDisposable
    {
        delegate void WinEvent(IntPtr hook,uint eventId,IntPtr window,int objectId,int child,uint thread,uint time);
        [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEvent callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
        readonly WinEvent callback;
        readonly IntPtr windows;
        readonly Dispatcher dispatcher;
        readonly Action changed;
        readonly DispatcherTimer debounce;
        bool disposed;
        internal ShellEvents(Dispatcher dispatcher,Action changed)
        {
            this.dispatcher=dispatcher;this.changed=changed;
            debounce=new DispatcherTimer(DispatcherPriority.Normal,dispatcher){Interval=TimeSpan.FromMilliseconds(60)};
            debounce.Tick+=(s,e)=> {debounce.Stop();if(!disposed)this.changed();};
            callback=OnEvent;
            // Foreground selection is already tracked by the lightweight UI pulse.
            windows=SetWinEventHook(0x8000,0x8003,IntPtr.Zero,callback,0,0,2);
        }
        void OnEvent(IntPtr hook,uint id,IntPtr window,int objectId,int child,uint thread,uint time)
        {
            if(disposed||window==IntPtr.Zero||objectId!=0||child!=0)return;
            // Browser/XAML child-window churn is not an app launch; the one-second
            // fallback catches windows already destroyed before this callback.
            if(Native.GetAncestor(window,2)!=window)return;
            dispatcher.BeginInvoke(new Action(()=> {if(!disposed){debounce.Stop();debounce.Start();}}),DispatcherPriority.Normal);
        }
        public void Dispose(){disposed=true;debounce.Stop();if(windows!=IntPtr.Zero)UnhookWinEvent(windows);}
    }
}
