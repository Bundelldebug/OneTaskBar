using System;
using System.Runtime.InteropServices;
using System.Threading;
namespace OneTaskBar
{
    internal static class WindowActivation
    {
        [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]static extern bool AttachThreadInput(uint first,uint second,bool attach);
        [DllImport("user32.dll")]static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
        [DllImport("user32.dll")]static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
        static int links;
        internal static int AttachedQueues {get{return Volatile.Read(ref links);}}
        static bool Responsive(IntPtr window,uint message=0,uint timeout=60){IntPtr result;return SendMessageTimeout(window,message,IntPtr.Zero,IntPtr.Zero,3,timeout,out result)!=IntPtr.Zero;}
        static bool ForegroundIs(IntPtr window)
        {
            var foreground=Native.GetForegroundWindow();return foreground==window||foreground!=IntPtr.Zero&&Native.GetAncestor(foreground,3)==Native.GetAncestor(window,3);
        }
        internal static void Activate(IntPtr window)
        {
            if(!TryActivate(window)&&Native.IsWindow(window))AppLog.Once("switcher-focus","The requested window did not confirm foreground activation.");
        }
        // Only called for an explicit committed switch. Do not borrow input queues for
        // discovery/hover, attach to an unresponsive application, or retain an attachment.
        internal static bool TryActivate(IntPtr window)
        {
            if(window==IntPtr.Zero||!Native.IsWindow(window))return false;
            var original=Native.GetForegroundWindow();if(original==IntPtr.Zero)return false;
            if(Native.IsIconic(window))ShowWindowAsync(window,9);
            bool requested=Native.SetForegroundWindow(window);
            if(requested){Responsive(window);if(ForegroundIs(window)){BringWindowToTop(window);return true;}}
            var foreground=Native.GetForegroundWindow();if(foreground==IntPtr.Zero||foreground!=original&&!ForegroundIs(window))return false;
            if(!Responsive(foreground)||!Responsive(window))return false;
            // Alt release can still be closing the previous app's menu. Finish that
            // work before joining queues, with a timeout and without generating keys.
            if(!Responsive(foreground,0x1F,40)||Native.GetForegroundWindow()!=foreground)return false;
            uint pid;uint current=GetCurrentThreadId(),source=Native.GetWindowThreadProcessId(foreground,out pid),target=Native.GetWindowThreadProcessId(window,out pid);
            if(source==0||target==0)return false;bool joinedSource=false,joinedTarget=false;
            try{
                if(source!=current){joinedSource=AttachThreadInput(current,source,true);if(!joinedSource)return false;Interlocked.Increment(ref links);}
                if(target!=current&&target!=source){joinedTarget=AttachThreadInput(current,target,true);if(!joinedTarget)return false;Interlocked.Increment(ref links);}
                if(!Native.IsWindow(window))return false;
                // Keep the normal/maximized bounds and topmost policy. Only minimized
                // windows are restored; no always-on-top toggle or synthetic Alt key.
                BringWindowToTop(window);Native.SetForegroundWindow(window);return ForegroundIs(window);
            }finally{
                if(joinedTarget){AttachThreadInput(current,target,false);Interlocked.Decrement(ref links);}
                if(joinedSource){AttachThreadInput(current,source,false);Interlocked.Decrement(ref links);}
            }
        }
    }
}
