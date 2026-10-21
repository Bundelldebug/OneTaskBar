using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OneTaskBar
{
    // Only Explorer's dedicated AboveLockUX OSD host is eligible. No shell process
    // is killed, injected into, or permanently restyled. Hidden OSDs can show again on exit.
    internal sealed class NativeOsdGate : IDisposable
    {
        delegate void EventCallback(IntPtr hook,uint id,IntPtr window,int objectId,int child,uint thread,uint time);
        [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,EventCallback callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")]static extern bool GetWindowBand(IntPtr window,out uint band);
        [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        readonly EventCallback callback;readonly Func<bool> enabled,replace;IntPtr hook;bool disposed;
        internal int Suppressed {get;private set;}
        internal NativeOsdGate(Func<bool> enabled,Func<bool> replace)
        {this.enabled=enabled;this.replace=replace;callback=OnEvent;hook=SetWinEventHook(0x8002,0x8002,IntPtr.Zero,callback,0,0,2);}
        internal static bool Candidate(string cls,string process,uint band,bool child)
        {return process=="explorer"&&band==18&&child&&(cls=="XamlExplorerHostIslandWindow"||cls=="NativeHWNDHost");}
        internal static bool IsOsd(IntPtr window)
        {
            if(window==IntPtr.Zero||!Native.IsWindow(window))return false;var text=new StringBuilder(128);Native.GetClassName(window,text,text.Capacity);string cls=text.ToString();if(cls!="XamlExplorerHostIslandWindow"&&cls!="NativeHWNDHost")return false;
            try{uint band;if(!GetWindowBand(window,out band)||band!=18)return false;uint pid;Native.GetWindowThreadProcessId(window,out pid);using(var process=Process.GetProcessById((int)pid)){bool child=cls=="NativeHWNDHost"?FindWindowEx(window,IntPtr.Zero,"DirectUIHWND",null)!=IntPtr.Zero:FindWindowEx(window,IntPtr.Zero,"Windows.UI.Composition.DesktopWindowContentBridge","DesktopWindowXamlSource")!=IntPtr.Zero;return Candidate(cls,process.ProcessName.ToLowerInvariant(),band,child);}}catch{return false;}
        }
        void OnEvent(IntPtr source,uint id,IntPtr window,int objectId,int child,uint thread,uint time)
        {if(!disposed&&objectId==0&&child==0&&enabled()&&IsOsd(window)&&replace()){ShowWindowAsync(window,0);Suppressed++;}}
        public void Dispose(){disposed=true;if(hook!=IntPtr.Zero){UnhookWinEvent(hook);hook=IntPtr.Zero;}}
    }
}
