using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
namespace OneTaskBar
{
    internal sealed class OsdMaskRecord {public long Window;public uint Pid;public int Mode,Style;public uint Color,Flags;public byte Alpha=255;}
    internal sealed class OsdMaskLease {public string Session;public int Owner;public long Started;public List<OsdMaskRecord> Windows=new List<OsdMaskRecord>();}
    // Prepare Explorer's dedicated OSD while hidden, so a SHOW event cannot paint a
    // native frame first. The lease lets the crash watchdog restore the original opacity.
    internal sealed class NativeOsdGate : IDisposable
    {
        delegate void EventCallback(IntPtr hook,uint id,IntPtr window,int objectId,int child,uint thread,uint time);
        [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,EventCallback callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")]static extern bool GetWindowBand(IntPtr window,out uint band);
        [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint color,byte alpha,uint flags);
        [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint color,out byte alpha,out uint flags);
        readonly EventCallback callback;readonly Func<bool> enabled,replace,prepare;IntPtr hook;bool disposed;
        readonly OsdMaskLease lease=new OsdMaskLease {Session=Guid.NewGuid().ToString("N")};
        static string LeaseFile {get{return Path.Combine(AppLog.Data,"native-osd-lease.json");}}
        internal int Suppressed {get;private set;}internal int PrimedShows {get;private set;}
        internal IntPtr[] MaskedWindows {get{return lease.Windows.Select(w=>new IntPtr(w.Window)).ToArray();}}
        internal int Prepared {get{return MaskedWindows.Count(IsMasked);}}
        internal static bool IsMasked(IntPtr window){int cloak;if(Native.DwmGetWindowAttribute(window,14,out cloak,4)==0&&(cloak&1)!=0)return true;uint color,flags;byte alpha;return (Native.GetWindowLong(window,-20)&0x80000)!=0&&GetLayeredWindowAttributes(window,out color,out alpha,out flags)&&(flags&2)!=0&&alpha==0;}
        internal NativeOsdGate(Func<bool> enabled,Func<bool> replace,Func<bool> prepare)
        {
            this.enabled=enabled;this.replace=replace;this.prepare=prepare;using(var process=Process.GetCurrentProcess()){lease.Owner=process.Id;lease.Started=process.StartTime.ToUniversalTime().Ticks;}Recover();callback=OnEvent;hook=SetWinEventHook(0x8000,0x8002,IntPtr.Zero,callback,0,0,2);
        }
        internal static bool Candidate(string cls,string process,uint band,bool child)
        {return process=="explorer"&&band==18&&child&&(cls=="XamlExplorerHostIslandWindow"||cls=="NativeHWNDHost");}
        internal static bool IsOsd(IntPtr window){return Eligible(window,true);}
        static bool Eligible(IntPtr window,bool requireChild)
        {
            if(window==IntPtr.Zero||!Native.IsWindow(window))return false;var text=new StringBuilder(128);Native.GetClassName(window,text,text.Capacity);string cls=text.ToString();if(cls!="XamlExplorerHostIslandWindow"&&cls!="NativeHWNDHost")return false;
            try{uint band;if(!GetWindowBand(window,out band)||band!=18)return false;uint pid;Native.GetWindowThreadProcessId(window,out pid);using(var process=Process.GetProcessById((int)pid)){bool child=cls=="NativeHWNDHost"?FindWindowEx(window,IntPtr.Zero,"DirectUIHWND",null)!=IntPtr.Zero:FindWindowEx(window,IntPtr.Zero,"Windows.UI.Composition.DesktopWindowContentBridge","DesktopWindowXamlSource")!=IntPtr.Zero;return Candidate(cls,process.ProcessName.ToLowerInvariant(),band,child||!requireChild);}}catch{return false;}
        }
        static OsdMaskLease ReadLease(){try{return new JavaScriptSerializer().Deserialize<OsdMaskLease>(File.ReadAllText(LeaseFile));}catch{return null;}}
        static bool OwnerAlive(OsdMaskLease state){try{using(var process=Process.GetProcessById(state.Owner))return process.StartTime.ToUniversalTime().Ticks==state.Started&&!process.HasExited;}catch(ArgumentException){return false;}catch{return true;}}
        bool Save()
        {
            try{var existing=ReadLease();if(existing!=null&&existing.Session!=lease.Session&&OwnerAlive(existing))return false;Directory.CreateDirectory(AppLog.Data);string temp=LeaseFile+"."+lease.Session+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(lease));if(File.Exists(LeaseFile))File.Replace(temp,LeaseFile,null);else File.Move(temp,LeaseFile);return true;}catch(Exception error){AppLog.Once("osd-mask-lease",error.Message);return false;}
        }
        void Acquire(IntPtr window)
        {
            if(!prepare()||!Eligible(window,false))return;var record=lease.Windows.FirstOrDefault(w=>w.Window==window.ToInt64());
            if(record!=null&&IsMasked(window))return;
            if(record==null){if(IsMasked(window))return;uint pid;Native.GetWindowThreadProcessId(window,out pid);int style=Native.GetWindowLong(window,-20);uint color,flags;byte alpha;bool layered=GetLayeredWindowAttributes(window,out color,out alpha,out flags);if((style&0x80000)!=0&&!layered)return;
                record=new OsdMaskRecord {Window=window.ToInt64(),Pid=pid,Mode=2,Style=style,Color=color,Flags=flags,Alpha=layered?alpha:(byte)255};lease.Windows.Add(record);if(!Save()){lease.Windows.Remove(record);return;}}
            Native.SetWindowLong(window,-20,Native.GetWindowLong(window,-20)|0x80000);if(!SetLayeredWindowAttributes(window,0,0,2)){Restore(record);lease.Windows.Remove(record);Save();AppLog.Once("osd-opacity","Native host did not accept a temporary alpha mask");}
        }
        internal void Refresh()
        {
            if(disposed)return;if(!enabled()||!prepare()){Release();return;}foreach(string cls in new[]{"XamlExplorerHostIslandWindow","NativeHWNDHost"}){IntPtr window=IntPtr.Zero;for(int i=0;i<64;i++){window=FindWindowEx(IntPtr.Zero,window,cls,null);if(window==IntPtr.Zero)break;Acquire(window);}}
        }
        void OnEvent(IntPtr source,uint id,IntPtr window,int objectId,int child,uint thread,uint time)
        {
            if(disposed||window==IntPtr.Zero||objectId!=0||child!=0)return;if(id==0x8001){lease.Windows.RemoveAll(w=>w.Window==window.ToInt64());return;}if(!enabled())return;var root=Native.GetAncestor(window,2);if(root==IntPtr.Zero)root=window;
            if(id==0x8000){Acquire(root);return;}if(id!=0x8002||root!=window||!IsOsd(window))return;if(IsMasked(window))PrimedShows++;Acquire(window);if(replace()){ShowWindowAsync(window,0);Suppressed++;}
        }
        static bool Restore(OsdMaskRecord record)
        {
            var window=new IntPtr(record.Window);if(!Native.IsWindow(window))return true;uint pid;Native.GetWindowThreadProcessId(window,out pid);if(pid!=record.Pid||!Eligible(window,false))return true;if(record.Mode==2){if((record.Style&0x80000)==0){Native.SetWindowLong(window,-20,Native.GetWindowLong(window,-20)&~0x80000);return (Native.GetWindowLong(window,-20)&0x80000)==0;}return SetLayeredWindowAttributes(window,record.Color,record.Alpha,record.Flags);}int cloak=0;return DwmSetWindowAttribute(window,13,ref cloak,4)==0;
        }
        void Release()
        {
            var failed=lease.Windows.Where(w=>!Restore(w)).ToList();lease.Windows.Clear();lease.Windows.AddRange(failed);var current=ReadLease();if(current==null||current.Session!=lease.Session)return;if(failed.Count>0){Save();return;}try{File.Delete(LeaseFile);}catch{}
        }
        internal static void Recover()
        {
            var previous=ReadLease();if(previous==null||OwnerAlive(previous))return;bool restored=true;foreach(var record in previous.Windows??new List<OsdMaskRecord>())restored&=Restore(record);if(restored)try{File.Delete(LeaseFile);}catch{}
        }
        public void Dispose(){if(disposed)return;disposed=true;if(hook!=IntPtr.Zero){UnhookWinEvent(hook);hook=IntPtr.Zero;}Release();}
    }
}
