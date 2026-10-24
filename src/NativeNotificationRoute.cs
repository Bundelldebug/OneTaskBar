using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Automation;
namespace OneTaskBar
{
    // Invoke the original Shell notification, preserving UWP Toast activation and
    // application-defined behavior which a generic app launch cannot reproduce.
    internal static class NativeNotificationRoute
    {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint color,byte alpha,uint flags);
        [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint color,out byte alpha,out uint flags);
        static readonly object actionLock=new object();static volatile HistoryMask current;static IntPtr lastCenter;
        static readonly string[] centerNames={"알림 센터","알림센터","Notification Center"};
        internal static bool Prepare(IntPtr window){var mask=current;return mask!=null&&mask.Acquire(window);}
        static bool Center(IntPtr window)
        {
            if(window==IntPtr.Zero||!Native.IsWindow(window))return false;var cls=new StringBuilder(128);Native.GetClassName(window,cls,128);if(cls.ToString()!="Windows.UI.Core.CoreWindow")return false;
            var title=new StringBuilder(128);Native.GetWindowText(window,title,128);if(!centerNames.Contains(title.ToString()))return false;uint pid;Native.GetWindowThreadProcessId(window,out pid);
            try{using(var process=Process.GetProcessById((int)pid))return process.ProcessName=="ShellExperienceHost"||process.ProcessName=="ShellHost";}catch{return false;}
        }
        static IntPtr FindCenter()
        {
            if(Center(lastCenter))return lastCenter;var foreground=Native.GetAncestor(Native.GetForegroundWindow(),2);if(Center(foreground)){lastCenter=foreground;return foreground;}
            IntPtr h=IntPtr.Zero;for(int i=0;i<80;i++){h=FindWindowEx(IntPtr.Zero,h,"Windows.UI.Core.CoreWindow",null);if(h==IntPtr.Zero)break;if(Center(h)){lastCenter=h;return h;}}return IntPtr.Zero;
        }
        internal static bool Toast(NotificationEntry item,bool inspectOnly)
        {
            if(item.NativeWindow==IntPtr.Zero||String.IsNullOrEmpty(item.NativeSignature))return false;
            try {var match=NativeToastView.Read(item.NativeWindow).FirstOrDefault(v=>v.Signature==item.NativeSignature&&v.Matches(item));if(match==null||match.Element==null)return false;return Invoke(match.Element,inspectOnly);}
            catch(ElementNotAvailableException){return false;}catch(InvalidOperationException){return false;}catch(Exception error){AppLog.Once("notification-native-toast",error.Message);return false;}
        }
        static bool Invoke(AutomationElement element,bool inspectOnly)
        {
            object pattern;if(!element.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))return false;if(!inspectOnly)((InvokePattern)pattern).Invoke();return true;
        }
        static bool InvokeButton(AutomationElement root,string label,bool inspectOnly){var buttons=root.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),new PropertyCondition(AutomationElement.NameProperty,label)));return buttons.Count==1&&Invoke(buttons[0],inspectOnly);}
        internal static bool ToastAction(NotificationEntry item,string label,bool inspectOnly){if(item.NativeWindow==IntPtr.Zero||String.IsNullOrEmpty(item.NativeSignature))return false;try{var match=NativeToastView.Read(item.NativeWindow).FirstOrDefault(v=>v.Signature==item.NativeSignature&&v.Matches(item));return match!=null&&match.Element!=null&&InvokeButton(match.Element,label,inspectOnly);}catch{return false;}}
        static string Text(AutomationElement root,string id){var element=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id));return element==null?"":element.Current.Name;}
        static List<AutomationElement> MatchHistory(AutomationElement root,NotificationEntry item)
        {
            foreach(AutomationElement group in root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.IsExpandCollapsePatternAvailableProperty,true))){try{string name=group.Current.Name;if(name==item.App+"의 알림"||name==item.App+" notifications"||name=="Notifications from "+item.App){object pattern;if(group.TryGetCurrentPattern(ExpandCollapsePattern.Pattern,out pattern)&&((ExpandCollapsePattern)pattern).Current.ExpandCollapseState==ExpandCollapseState.Collapsed)((ExpandCollapsePattern)pattern).Expand();}}catch(ElementNotAvailableException){}}
            var result=new List<AutomationElement>();var entries=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem));
            foreach(AutomationElement entry in entries){try {
                string label=entry.Current.Name??"";if(!String.IsNullOrEmpty(item.Title)&&label.IndexOf(item.Title,StringComparison.CurrentCultureIgnoreCase)<0&&label.IndexOf('…')<0&&!label.Contains("..."))continue;
                object virtualized;if(entry.TryGetCurrentPattern(VirtualizedItemPattern.Pattern,out virtualized))try{((VirtualizedItemPattern)virtualized).Realize();}catch(InvalidOperationException){}catch(NotSupportedException){}
                string title=Text(entry,"Title"),body=Text(entry,"Content");if(String.IsNullOrEmpty(body))body=Text(entry,"MessageText");
                if(NotificationPayload.TextKey(new[]{title})!=NotificationPayload.TextKey(new[]{item.Title})||NotificationPayload.TextKey(new[]{body})!=NotificationPayload.TextKey(new[]{item.Body}))continue;
                var group=TreeWalker.ControlViewWalker.GetParent(entry);string name=group==null?"":group.Current.Name;
                if(!String.IsNullOrEmpty(item.App)&&name.IndexOf(item.App,StringComparison.CurrentCultureIgnoreCase)<0)continue;result.Add(entry);
            }catch(ElementNotAvailableException){}catch(InvalidOperationException){}}
            if(result.Count>1&&item.Created.LocalDateTime.Date==DateTime.Today){string time=item.Created.LocalDateTime.ToString("t",System.Globalization.CultureInfo.CurrentCulture);var timed=result.Where(e=>Text(e,"TimeStamp")==time).ToList();if(timed.Count==1)return timed;}
            return result;
        }
        internal static NotificationOpenResult History(NotificationEntry item,bool inspectOnly,string actionLabel=null)
        {
            var result=new NotificationOpenResult {Method="native-notification-center"};lock(actionLock){
                IntPtr center=FindCenter();bool already=center!=IntPtr.Zero&&Native.GetAncestor(Native.GetForegroundWindow(),2)==center,opened=false,invoked=false;
                if(inspectOnly&&!already)return result;
                using(var mask=new HistoryMask(result))try {
                    if(!already){current=mask;if(center!=IntPtr.Zero)mask.Acquire(center);if(!SystemPanels.Toggle(true))return result;opened=true;}
                    for(int i=0;i<18;i++){
                        if(center==IntPtr.Zero)center=FindCenter();if(center!=IntPtr.Zero&&Native.IsWindowVisible(center)){
                            if(!already)mask.Acquire(center);var root=AutomationElement.FromHandle(center);if(root!=null){var matches=MatchHistory(root,item);if(matches.Count==1){result.Exact=actionLabel==null||InvokeButton(matches[0],actionLabel,true);result.Method=actionLabel==null?"native-toast-history":"native-history-button";if(!inspectOnly&&result.Exact){invoked=actionLabel==null?Invoke(matches[0],false):InvokeButton(matches[0],actionLabel,false);result.Invoked=invoked;}if(result.Exact)return result;}}
                        }
                        if(inspectOnly)break;Thread.Sleep(45);
                    }
                    // A missing/ambiguous entry is left for the user in the native
                    // centre instead of silently sending unrelated launch arguments.
                    result.Invoked=opened||already;return result;
                }catch(Exception error){AppLog.Once("notification-native-action",error.Message);result.Exact=false;result.Invoked=opened||already;return result;}
                finally {current=null;if(opened&&invoked&&center!=IntPtr.Zero&&Native.GetAncestor(Native.GetForegroundWindow(),2)==center)SystemPanels.Toggle(true);}
            }
        }
        sealed class HistoryMask : IDisposable
        {
            readonly object sync=new object();readonly OsdMaskLease lease=new OsdMaskLease {Session=Guid.NewGuid().ToString("N")};
            readonly NotificationOpenResult result;bool disposed;
            internal HistoryMask(NotificationOpenResult outcome){result=outcome;using(var process=Process.GetCurrentProcess()){lease.Owner=process.Id;lease.Started=process.StartTime.ToUniversalTime().Ticks;}Recover();}
            internal bool Acquire(IntPtr window)
            {
                lock(sync){if(disposed||!Center(window))return false;lastCenter=window;if(lease.Windows.Any(w=>w.Window==window.ToInt64()))return true;uint pid,color,flags;byte alpha;Native.GetWindowThreadProcessId(window,out pid);int style=Native.GetWindowLong(window,-20);bool layered=GetLayeredWindowAttributes(window,out color,out alpha,out flags);if((style&0x80000)!=0&&!layered)return false;
                    var record=new OsdMaskRecord {Window=window.ToInt64(),Pid=pid,Style=style,Color=color,Flags=flags,Alpha=layered?alpha:(byte)255};lease.Windows.Add(record);if(!Save(lease)){lease.Windows.Remove(record);return false;}
                    Native.SetWindowLong(window,-20,style|0x80020);if(SetLayeredWindowAttributes(window,0,0,2)){result.Masked=true;return true;}Restore(record);lease.Windows.Remove(record);return false;}
            }
            public void Dispose(){lock(sync){if(disposed)return;disposed=true;bool restored=true;foreach(var record in lease.Windows)restored&=Restore(record);result.Restored=restored;if(restored)Delete(lease);}}
        }
        static string LeaseFile {get{return Path.Combine(AppLog.Data,"native-notification-action-lease.json");}}
        static OsdMaskLease Read(){try{return new JavaScriptSerializer().Deserialize<OsdMaskLease>(File.ReadAllText(LeaseFile));}catch{return null;}}
        static bool Alive(OsdMaskLease lease){try{using(var process=Process.GetProcessById(lease.Owner))return !process.HasExited&&process.StartTime.ToUniversalTime().Ticks==lease.Started;}catch(ArgumentException){return false;}catch{return true;}}
        static bool Save(OsdMaskLease lease){try{var previous=Read();if(previous!=null&&previous.Session!=lease.Session&&Alive(previous))return false;Directory.CreateDirectory(AppLog.Data);string temp=LeaseFile+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(lease));if(File.Exists(LeaseFile))File.Replace(temp,LeaseFile,null);else File.Move(temp,LeaseFile);return true;}catch(Exception error){AppLog.Once("notification-action-mask",error.Message);return false;}}
        static bool Restore(OsdMaskRecord record){var window=new IntPtr(record.Window);if(!Native.IsWindow(window))return true;uint pid;Native.GetWindowThreadProcessId(window,out pid);if(pid!=record.Pid)return true;var cls=new StringBuilder(128);Native.GetClassName(window,cls,128);if(cls.ToString()!="Windows.UI.Core.CoreWindow")return true;bool restored=true;if((record.Style&0x80000)!=0)restored=SetLayeredWindowAttributes(window,record.Color,record.Alpha,record.Flags);Native.SetWindowLong(window,-20,(Native.GetWindowLong(window,-20)&~0x80020)|(record.Style&0x80020));return restored&&(Native.GetWindowLong(window,-20)&0x80020)==(record.Style&0x80020);}
        static void Delete(OsdMaskLease lease){var current=Read();if(current!=null&&current.Session==lease.Session)try{File.Delete(LeaseFile);}catch{}}
        internal static void Recover(){var lease=Read();if(lease==null||Alive(lease))return;bool restored=true;foreach(var record in lease.Windows??new List<OsdMaskRecord>())restored&=Restore(record);if(restored)Delete(lease);}
    }
}
