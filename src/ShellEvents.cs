using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal sealed class ShellEvents : IDisposable
    {
        delegate void WinEvent(IntPtr hook,uint eventId,IntPtr window,int objectId,int child,uint thread,uint time);
        [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEvent callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
        readonly WinEvent callback;
        readonly IntPtr windows,foreground,location;
        readonly WinEvent locationCallback;
        bool covering;
        readonly WinEvent foregroundCallback;
        readonly Dispatcher dispatcher;
        readonly Action changed;
        readonly DispatcherTimer debounce;
        bool disposed;int queued;
        internal ShellEvents(Dispatcher dispatcher,Action changed,Action focusChanged=null)
        {
            this.dispatcher=dispatcher;this.changed=changed;
            debounce=new DispatcherTimer(DispatcherPriority.Normal,dispatcher){Interval=TimeSpan.FromMilliseconds(60)};
            debounce.Tick+=(s,e)=> {debounce.Stop();if(!disposed)this.changed();};
            callback=OnEvent;
            windows=SetWinEventHook(0x8000,0x8003,IntPtr.Zero,callback,0,0,2);
            foregroundCallback=(hook,id,window,objectId,child,thread,time)=>
            {
                if(disposed)return;
                if(focusChanged!=null)dispatcher.BeginInvoke(focusChanged,DispatcherPriority.Normal);
            };
            foreground=SetWinEventHook(3,3,IntPtr.Zero,foregroundCallback,0,0,2);
            // The foreground window growing to the whole monitor (F11, a player going fullscreen) raises no event of its
            // own. Only the moment it starts or stops covering the monitor is reported, so drags stay free.
            locationCallback=(hook,id,window,objectId,child,thread,time)=>
            {
                if(disposed||objectId!=0||child!=0||focusChanged==null)return;
                if(window!=Native.GetForegroundWindow()||Native.GetAncestor(window,2)!=window)return;
                Native.RectI rect;
                if(!Native.GetWindowRect(window,out rect))return;
                var monitor=System.Windows.Forms.Screen.FromHandle(window).Bounds;
                bool now=rect.Left<=monitor.Left+1&&rect.Top<=monitor.Top+1&&rect.Right>=monitor.Right-1&&rect.Bottom>=monitor.Bottom-1;
                if(now==covering)return;
                covering=now;
                dispatcher.BeginInvoke(focusChanged,DispatcherPriority.Normal);
            };
            location=SetWinEventHook(0x800B,0x800B,IntPtr.Zero,locationCallback,0,0,2);
        }
        void OnEvent(IntPtr hook,uint id,IntPtr window,int objectId,int child,uint thread,uint time)
        {
            if(disposed||window==IntPtr.Zero||objectId!=0||child!=0)return;
            // Browser/XAML child-window churn is not an app launch; the one-second
            // fallback catches windows already destroyed before this callback.
            if(Native.GetAncestor(window,2)!=window)return;
            if(Interlocked.Exchange(ref queued,1)!=0)return;
            dispatcher.BeginInvoke(new Action(()=> {Interlocked.Exchange(ref queued,0);if(!disposed){debounce.Stop();debounce.Start();}}),DispatcherPriority.Normal);
        }
        public void Dispose(){disposed=true;debounce.Stop();if(windows!=IntPtr.Zero)UnhookWinEvent(windows);if(foreground!=IntPtr.Zero)UnhookWinEvent(foreground);if(location!=IntPtr.Zero)UnhookWinEvent(location);}
    }
}
