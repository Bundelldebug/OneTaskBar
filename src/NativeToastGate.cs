using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Automation;
namespace OneTaskBar
{
    internal sealed class NativeToastView
    {
        internal string Title,Body,App,Signature;internal DateTime Created=DateTime.UtcNow;internal IntPtr Window;internal AutomationElement Element;
        internal bool Matches(NotificationEntry item){return NotificationPayload.TextKey(new[]{item.Title})==NotificationPayload.TextKey(new[]{Title})&&NotificationPayload.TextKey(new[]{item.Body})==NotificationPayload.TextKey(new[]{Body});}
        static string Text(AutomationElement root,string id){var element=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id));return element==null?"":element.Current.Name;}
        internal static List<NativeToastView> Read(IntPtr window)
        {
            var result=new List<NativeToastView>();try {
                var root=AutomationElement.FromHandle(window);if(root==null)return result;var views=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NormalToastView"));
                for(int i=Math.Max(0,views.Count-3);i<views.Count;i++){
                    var element=views[i];var title=Text(element,"Title");if(String.IsNullOrEmpty(title))continue;string body=Text(element,"MessageText"),app=Text(element,"SenderName");
                    result.Add(new NativeToastView {Title=title,Body=body,App=app,Window=window,Element=element,Signature=String.Join(",",element.GetRuntimeId())+"|"+app+"|"+title+"|"+body});
                }
            }catch(ElementNotAvailableException){}return result;
        }
    }
    // Only the Shell's dedicated toast HWND is masked. Notification history and
    // the notification centre remain owned by Windows. Hooks sleep between events.
    internal sealed class NativeToastGate : IDisposable
    {
        delegate void EventCallback(IntPtr hook,uint id,IntPtr window,int objectId,int child,uint thread,uint time);
        [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,EventCallback callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")]static extern bool GetWindowBand(IntPtr window,out uint band);
        [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint color,byte alpha,uint flags);
        [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint color,out byte alpha,out uint flags);
        readonly Func<bool> enabled;readonly EventCallback callback;IntPtr lifecycle,names;bool disposed;
        readonly OsdMaskLease lease=new OsdMaskLease {Session=Guid.NewGuid().ToString("N")};
        internal int Suppressed {get;private set;}
        int events,activeEvents;internal object State {get{return new {events=events,activeEvents=activeEvents,presented=Suppressed,masks=lease.Windows.Count,hooks=lifecycle!=IntPtr.Zero&&names!=IntPtr.Zero};}}
        internal Action<IntPtr> Presented;
        static string LeaseFile {get{return Path.Combine(AppLog.Data,"native-toast-lease.json");}}
        internal NativeToastGate(Func<bool> active)
        {
            enabled=active;using(var process=Process.GetCurrentProcess()){lease.Owner=process.Id;lease.Started=process.StartTime.ToUniversalTime().Ticks;}Recover();NativeNotificationRoute.Recover();callback=Event;
            lifecycle=SetWinEventHook(0x8000,0x8003,IntPtr.Zero,callback,0,0,2);names=SetWinEventHook(0x800C,0x800C,IntPtr.Zero,callback,0,0,2);
        }
        internal static bool Candidate(string cls,string process,string title,uint band)
        {
            bool shell=String.Equals(process,"ShellExperienceHost",StringComparison.OrdinalIgnoreCase)||String.Equals(process,"ShellHost",StringComparison.OrdinalIgnoreCase);
            if(!shell||cls!="Windows.UI.Core.CoreWindow")return false;
            // Other shell CoreWindows (Start/Action Centre/desktop XAML sources)
            // are deliberately excluded; NAMECHANGE prepares a newly created toast.
            return title=="New notification"||title=="새 알림"||title=="새로운 알림";
        }
        static bool Eligible(IntPtr window)
        {
            if(!Native.IsWindow(window)||Native.GetAncestor(window,2)!=window)return false;var cls=new StringBuilder(128);Native.GetClassName(window,cls,128);if(cls.ToString()!="Windows.UI.Core.CoreWindow")return false;
            uint pid,band;Native.GetWindowThreadProcessId(window,out pid);GetWindowBand(window,out band);var title=new StringBuilder(256);Native.GetWindowText(window,title,256);
            try {using(var process=Process.GetProcessById((int)pid))return Candidate(cls.ToString(),process.ProcessName,title.ToString(),band);}catch{return false;}
        }
        static OsdMaskLease Read(){try{return new JavaScriptSerializer().Deserialize<OsdMaskLease>(File.ReadAllText(LeaseFile));}catch{return null;}}
        static bool Alive(OsdMaskLease state){try{using(var process=Process.GetProcessById(state.Owner))return !process.HasExited&&process.StartTime.ToUniversalTime().Ticks==state.Started;}catch(ArgumentException){return false;}catch{return true;}}
        bool Save()
        {
            try {var previous=Read();if(previous!=null&&previous.Session!=lease.Session&&Alive(previous))return false;Directory.CreateDirectory(AppLog.Data);string temp=LeaseFile+"."+lease.Session+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(lease));if(File.Exists(LeaseFile))File.Replace(temp,LeaseFile,null);else File.Move(temp,LeaseFile);return true;}
            catch(Exception error){AppLog.Once("toast-mask-lease",error.Message);return false;}
        }
        bool Acquire(IntPtr window)
        {
            if(!enabled()||!Eligible(window))return false;uint pid;Native.GetWindowThreadProcessId(window,out pid);var record=lease.Windows.FirstOrDefault(w=>w.Window==window.ToInt64()&&w.Pid==pid);
            if(record==null){int style=Native.GetWindowLong(window,-20);uint color,flags;byte alpha;bool layered=GetLayeredWindowAttributes(window,out color,out alpha,out flags);if((style&0x80000)!=0&&!layered)return false;
                record=new OsdMaskRecord {Window=window.ToInt64(),Pid=pid,Mode=2,Style=style,Color=color,Flags=flags,Alpha=layered?alpha:(byte)255};lease.Windows.Add(record);if(!Save()){lease.Windows.Remove(record);return false;}}
            Native.SetWindowLong(window,-20,Native.GetWindowLong(window,-20)|0x80020);
            if(SetLayeredWindowAttributes(window,0,0,2))return true;
            Restore(record);lease.Windows.Remove(record);Save();return false;
        }
        void Event(IntPtr hook,uint id,IntPtr window,int objectId,int child,uint thread,uint time)
        {
            if(disposed||window==IntPtr.Zero||objectId!=0||child!=0)return;
            events++;
            if((id==0x8000||id==0x8002||id==0x800C)&&NativeNotificationRoute.Prepare(window))return;
            if(id==0x8001){if(lease.Windows.RemoveAll(w=>w.Window==window.ToInt64())>0)Save();return;}
            if(!enabled())return;activeEvents++;if(id==0x8000||id==0x8002||id==0x800C)if(Acquire(window)&&(id==0x8002||id==0x800C)){Suppressed++;var presented=Presented;if(presented!=null)presented(window);}
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        internal void Refresh(){if(disposed)return;if(!enabled()){Release();return;}IntPtr window=IntPtr.Zero;for(int i=0;i<80;i++){window=FindWindowEx(IntPtr.Zero,window,"Windows.UI.Core.CoreWindow",null);if(window==IntPtr.Zero)break;Acquire(window);}}
        internal void Fallback(IntPtr window){var records=lease.Windows.Where(w=>w.Window==window.ToInt64()).ToList();foreach(var record in records)if(Restore(record))lease.Windows.Remove(record);Save();}
        static bool Restore(OsdMaskRecord record)
        {
            var window=new IntPtr(record.Window);if(!Native.IsWindow(window))return true;uint pid;Native.GetWindowThreadProcessId(window,out pid);if(pid!=record.Pid)return true;var cls=new StringBuilder(128);Native.GetClassName(window,cls,128);if(cls.ToString()!="Windows.UI.Core.CoreWindow")return true;
            bool restored=true;if((record.Style&0x80000)!=0)restored=SetLayeredWindowAttributes(window,record.Color,record.Alpha,record.Flags);int current=Native.GetWindowLong(window,-20);Native.SetWindowLong(window,-20,(current&~0x80020)|(record.Style&0x80020));return restored&&(Native.GetWindowLong(window,-20)&0x80020)==(record.Style&0x80020);
        }
        void Release(){var failed=lease.Windows.Where(w=>!Restore(w)).ToList();lease.Windows.Clear();lease.Windows.AddRange(failed);var saved=Read();if(saved==null||saved.Session!=lease.Session)return;if(failed.Count>0){Save();return;}try{File.Delete(LeaseFile);}catch{}}
        internal static void Recover(){var state=Read();if(state==null||Alive(state))return;bool restored=true;foreach(var item in state.Windows??new List<OsdMaskRecord>())restored&=Restore(item);if(restored)try{File.Delete(LeaseFile);}catch{}}
        public void Dispose(){if(disposed)return;disposed=true;if(lifecycle!=IntPtr.Zero)UnhookWinEvent(lifecycle);if(names!=IntPtr.Zero)UnhookWinEvent(names);lifecycle=names=IntPtr.Zero;Release();}
    }
}
