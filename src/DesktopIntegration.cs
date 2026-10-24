using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal static partial class Native
    {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls,string title);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr h,uint flags);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref uint length);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process,ref uint length,StringBuilder id);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll",EntryPoint="GetClassLongPtrW")] static extern IntPtr GetClassLongPtr(IntPtr h,int index);
        [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr h,ref Guid riid,[MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref Variant value);
        [DllImport("shell32.dll")] internal static extern UIntPtr SHAppBarMessage(uint message,ref AppBarData data);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(PointI point,uint flags);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorData data);
        [StructLayout(LayoutKind.Sequential)] struct MonitorData {public int Size;public RectI Monitor,Work;public uint Flags;}
        [StructLayout(LayoutKind.Sequential)] internal struct AppBarData { public uint Size; public IntPtr Window; public uint Callback,Edge; public RectI Rect; public IntPtr Param; }
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public uint Id; }
        [StructLayout(LayoutKind.Explicit,Size=24)] struct Variant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
        [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore {
            void GetCount(out uint count); void GetAt(uint i,out PropertyKey key);
            void GetValue(ref PropertyKey key,out Variant value); void SetValue(ref PropertyKey key,ref Variant value); void Commit();
        }
        [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellLink {
            void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int length,IntPtr data,uint flags);
            void GetIDList(out IntPtr id); void SetIDList(IntPtr id);
            void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder value,int size); void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string value);
            void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder value,int size); void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string value);
            void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder value,int size); void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string value);
            void GetHotkey(out short key);void SetHotkey(short key);void GetShowCmd(out int show);void SetShowCmd(int show);
            void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int size,out int index);
        }
        internal static string ProcessPath(uint pid,out string appId)
        {
            appId=null; var process=OpenProcess(0x1000,false,pid); if(process==IntPtr.Zero) return null;
            try {
                uint length=32768; var path=new StringBuilder((int)length); string result=QueryFullProcessImageName(process,0,path,ref length) ? path.ToString() : null;
                uint n=512; var id=new StringBuilder((int)n); if(GetApplicationUserModelId(process,ref n,id)==0 && id.Length>0) appId=id.ToString(); return result;
            } finally { CloseHandle(process); }
        }
        internal static string WindowAppId(IntPtr window)
        {
            IPropertyStore store=null;
            try { var id=typeof(IPropertyStore).GUID; if(SHGetPropertyStoreForWindow(window,ref id,out store)!=0) return null;
                var key=new PropertyKey {Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),Id=5}; Variant value; store.GetValue(ref key,out value);
                try { return value.Type==31 ? Marshal.PtrToStringUni(value.Pointer) : null; } finally { PropVariantClear(ref value); }
            } catch { return null; } finally { if(store!=null) Marshal.ReleaseComObject(store); }
        }
        internal static string ShortcutTarget(string file)
        {
            object link=null;
            try { link=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))); ((IPersistFile)link).Load(file,0);
                var path=new StringBuilder(32768); ((IShellLink)link).GetPath(path,path.Capacity,IntPtr.Zero,4); return path.ToString();
            } catch { return null; } finally { if(link!=null) Marshal.FinalReleaseComObject(link); }
        }
        internal static ImageSource WindowIcon(IntPtr h)
        {
            try { IntPtr icon; SendMessageTimeout(h,0x7F,new IntPtr(1),IntPtr.Zero,2,40,out icon);
                if(icon==IntPtr.Zero) icon=GetClassLongPtr(h,-14); if(icon==IntPtr.Zero) return null;
                var image=Imaging.CreateBitmapSourceFromHIcon(icon,System.Windows.Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); return image;
            } catch { return null; }
        }
        internal static string ShortcutAppId(string file)
        {
            var info=ShortcutInfo(file);return info!=null?info.Id:null;
        }
        internal static AppShortcut ShortcutInfo(string file)
        {
            object link=null;try{link=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046")));((IPersistFile)link).Load(file,0);StringBuilder target=new StringBuilder(32768),arguments=new StringBuilder(32768);((IShellLink)link).GetPath(target,target.Capacity,IntPtr.Zero,4);((IShellLink)link).GetArguments(arguments,arguments.Capacity);var result=new AppShortcut {File=file,Target=target.ToString(),Arguments=arguments.ToString()};try{result.Id=AppIdProperty((IPropertyStore)link,5);}catch{}return result;}catch{return null;}finally{if(link!=null)Marshal.FinalReleaseComObject(link);}
        }
        static string AppIdProperty(IPropertyStore store,uint property)
        {
            var key=new PropertyKey {Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),Id=property};Variant value;store.GetValue(ref key,out value);try{return value.Type==31?Marshal.PtrToStringUni(value.Pointer):null;}finally{PropVariantClear(ref value);}
        }
        internal static ImageSource WindowRelaunchIcon(IntPtr window)
        {
            IPropertyStore store=null;try{var id=typeof(IPropertyStore).GUID;if(SHGetPropertyStoreForWindow(window,ref id,out store)!=0)return null;string resource=AppIdProperty(store,3);return WebAppIcons.ReadResource(resource);}catch{return null;}finally{if(store!=null)Marshal.ReleaseComObject(store);}
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint PrivateExtractIcons(string file,int index,int width,int height,out IntPtr icon,out uint id,uint count,uint flags);
        [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
        internal static ImageSource ResourceIcon(string file,int index)
        {
            IntPtr icon=IntPtr.Zero;
            try {uint id;if(PrivateExtractIcons(Environment.ExpandEnvironmentVariables(file),index,256,256,out icon,out id,1,0)!=1||icon==IntPtr.Zero)return null;
                var result=Imaging.CreateBitmapSourceFromHIcon(icon,System.Windows.Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());result.Freeze();return result;
            }catch{return null;}finally{if(icon!=IntPtr.Zero)DestroyIcon(icon);}
        }
        internal static ImageSource ShortcutIcon(string file)
        {
            object link=null;
            try {link=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046")));((IPersistFile)link).Load(file,0);
                var path=new StringBuilder(32768);int index;((IShellLink)link).GetIconLocation(path,path.Capacity,out index);
                var icon=path.Length>0?ResourceIcon(path.ToString(),index):null;if(icon!=null)return icon;
                path.Clear();((IShellLink)link).GetPath(path,path.Capacity,IntPtr.Zero,4);return path.Length>0?ResourceIcon(path.ToString(),0):null;
            }catch{return null;}finally{if(link!=null)Marshal.FinalReleaseComObject(link);}
        }
        internal static bool IsFullScreen(Forms.Screen screen)
        {
            var h=GetForegroundWindow(); uint pid; GetWindowThreadProcessId(h,out pid);
            if(pid==Process.GetCurrentProcess().Id||!IsWindowVisible(h)||IsIconic(h))return false;
            var name=new StringBuilder(100); GetClassName(h,name,100); if(name.ToString()=="Progman" || name.ToString()=="WorkerW" || name.ToString().StartsWith("Shell_")) return false;
            RectI r; if(!GetWindowRect(h,out r)) return false;
            return CoversMonitor(r,screen.Bounds,IsZoomed(h),(GetWindowLong(h,-16)&0xC40000)!=0);
        }
        [DllImport("user32.dll")]static extern bool IsZoomed(IntPtr window);
        internal static bool CoversMonitor(RectI rect,System.Drawing.Rectangle screen,bool maximized,bool framed){return !(maximized&&framed)&&rect.Left<=screen.Left&&rect.Top<=screen.Top&&rect.Right>=screen.Right&&rect.Bottom>=screen.Bottom;}
        internal static bool TaskbarAutoHide(){if(TaskbarMask.TransientState)return TaskbarMask.SavedAutoHide;var data=new AppBarData {Size=(uint)Marshal.SizeOf(typeof(AppBarData))};return (SHAppBarMessage(4,ref data).ToUInt64()&1)!=0;}
        internal static void SetTaskbarAutoHide(bool value)
        {
            lock(TaskbarMask.Gate){var handle=FindWindow("Shell_TrayWnd",null);if(handle==IntPtr.Zero)throw new InvalidOperationException("Windows 작업표시줄을 찾지 못했습니다.");
                // ABM_GETSTATE ignores hWnd, but ABM_SETSTATE requires the taskbar HWND.
                var data=new AppBarData {Size=(uint)Marshal.SizeOf(typeof(AppBarData)),Window=handle};uint state=(uint)SHAppBarMessage(4,ref data).ToUInt64(),next=value?state|1:state&~1U;
                if(TaskbarMask.TransientState){TaskbarMask.SavedAutoHide=value;DesktopLease.RecordTemporaryState(next);}data.Param=new IntPtr(next);SHAppBarMessage(10,ref data);}
        }
        internal static RectI WorkArea(Forms.Screen screen) {var data=new MonitorData {Size=Marshal.SizeOf(typeof(MonitorData))};GetMonitorInfo(MonitorFromPoint(new PointI {X=screen.Bounds.Left+1,Y=screen.Bounds.Top+1},2),ref data);return data.Work;}
        internal static void Activate(IntPtr h)
        {
            if(!IsWindow(h)) return;
            // Restoring every window would unmaximize already maximized applications.
            if(IsIconic(h)) ShowWindow(h,9); SetForegroundWindow(h);
        }
    }

    internal sealed class TaskbarItem
    {
        public string Id,Name;internal double X;
        public bool Pinned;
        internal AutomationElement Element;
    }
    internal sealed class TrayPlacement
    {
        internal readonly Native.PointI Anchor;
        internal readonly System.Drawing.Rectangle Monitor;
        internal readonly double Scale;
        internal TrayPlacement(System.Windows.Point anchor,System.Drawing.Rectangle monitor,double scale)
        {
            Anchor=new Native.PointI {X=(int)Math.Round(anchor.X),Y=(int)Math.Round(anchor.Y)};Monitor=monitor;Scale=Math.Max(1,scale);
        }
        internal Native.PointI Target(int width,int height)
        {
            int inset=(int)Math.Round(8*Scale),gap=inset;
            int minX=Monitor.Left+inset,minY=Monitor.Top+inset;
            return new Native.PointI {
                X=Math.Max(minX,Math.Min(Anchor.X-width/2,Math.Max(minX,Monitor.Right-width-inset))),
                Y=Math.Max(minY,Math.Min(Anchor.Y-height-gap,Math.Max(minY,Monitor.Bottom-height-inset)))
            };
        }
    }
    internal static class ExplorerBridge
    {
        internal static bool OpenSystemPanel(bool notifications,System.Drawing.Rectangle? monitor=null)
        {
            return SystemPanels.Toggle(notifications);
        }
        internal static bool SystemPanelVisible(bool notifications)
        {
            foreach(string title in notifications?new[]{"알림 센터","알림센터","Notification Center"}:new[]{"빠른 설정","Quick Settings","Quick settings","Control center","제어 센터"}) {var h=Native.FindWindow(null,title);if(h!=IntPtr.Zero&&Native.IsWindowVisible(h))return true;}
            if(!notifications){var h=Native.FindWindow("ControlCenterWindow",null);if(h!=IntPtr.Zero&&Native.IsWindowVisible(h))return true;}return false;
        }
        static AutomationElement chevron;
        static IntPtr trayHost;
        static volatile TrayPlacement trayPlacement;
        internal static void CacheTray()
        {
            try {var handle=Native.FindWindow("Shell_TrayWnd",null);if(handle!=trayHost){trayHost=handle;chevron=null;}if(handle==IntPtr.Zero||chevron!=null)return;
                var root=AutomationElement.FromHandle(handle);
                foreach(AutomationElement button in root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button))){string id=button.Current.AutomationId??"",name=button.Current.Name??"";if(id.StartsWith("Appid: "))continue;
                    if(name.Contains("숨겨진")||name.Contains("오버플로")||name.Contains("더 많은 아이콘")||name.IndexOf("hidden",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("overflow",StringComparison.OrdinalIgnoreCase)>=0){chevron=button;break;}}
                if(chevron==null)chevron=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ClassNameProperty,"SystemTray.NormalButton"));
            }catch(Exception e){AppLog.Once("tray-cache",e.Message);}
        }
        static bool InvokeChevron()
        {
            if(!TaskbarMask.Active)return false;
            try {if(chevron==null)return false;object pattern;if(chevron.TryGetCurrentPattern(InvokePattern.Pattern,out pattern)){((InvokePattern)pattern).Invoke();return true;}}
            catch{chevron=null;}return false;
        }
        internal static bool TrayVisible()
        {
            return TrayWindow()!=IntPtr.Zero;
        }
        static IntPtr TrayWindow()
        {
            foreach(string name in new[]{"TopLevelWindowForOverflowXamlIsland","NotifyIconOverflowWindow"}) {
                var popup=Native.FindWindow(name,null);if(popup!=IntPtr.Zero&&Native.IsWindowVisible(popup))return popup;
            }return IntPtr.Zero;
        }
        internal static void KeepTrayPosition(TrayPlacement anchor=null)
        {
            if(!TaskbarMask.Active)return;
            var placement=trayPlacement;if(placement==null)return;
            var popup=TrayWindow();if(popup==IntPtr.Zero)return;
            if(anchor!=null)placement=trayPlacement=anchor;
            Native.RectI rect;if(!Native.GetWindowRect(popup,out rect))return;
            int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;if(width<=0||height<=0)return;
            var target=placement.Target(width,height);
            // Explorer may adjust the flyout again after its opening animation or a DPI change.
            // Reapply only when needed; do not resize, activate, or recreate its contents.
            if(rect.Left!=target.X||rect.Top!=target.Y)
                Native.SetWindowPos(popup,new IntPtr(-1),target.X,target.Y,0,0,0x1|0x10);
        }
        internal static object TrayPositionInfo(out bool aligned)
        {
            aligned=false;var popup=TrayWindow();var placement=trayPlacement;Native.RectI rect;
            if(popup==IntPtr.Zero||placement==null||!Native.GetWindowRect(popup,out rect))return null;
            var expected=placement.Target(rect.Right-rect.Left,rect.Bottom-rect.Top);
            aligned=rect.Left==expected.X&&rect.Top==expected.Y;
            return new {actual=rect,expected=expected,anchor=placement.Anchor,monitor=placement.Monitor,scale=placement.Scale,aligned=aligned};
        }
        internal static bool AnchorTray(TrayPlacement placement)
        {
            trayPlacement=placement;KeepTrayPosition();return TrayVisible();
        }
        static bool TrayOpened(IntPtr handle,bool wasOpen,TrayPlacement placement)
        {
            if(!wasOpen)AnchorTray(placement);else trayPlacement=null;
            return TaskbarMask.Protected(handle);
        }
        internal static bool OpenTray(TrayPlacement placement=null)
        {
            var handle=Native.FindWindow("Shell_TrayWnd",null);if(handle==IntPtr.Zero||!TaskbarMask.Hide(handle))return false;
            bool wasOpen=TrayVisible();CacheTray();bool cached=chevron!=null,invoked=InvokeChevron();
            if(invoked){for(int i=0;i<20;i++){if(wasOpen||TrayVisible())return TrayOpened(handle,wasOpen,placement);System.Threading.Thread.Sleep(15);}}
            var data=new Native.AppBarData {Size=(uint)Marshal.SizeOf(typeof(Native.AppBarData)),Window=handle};
            uint state=(uint)Native.SHAppBarMessage(4,ref data).ToUInt64();
            DesktopLease.RecordTemporaryState(state);
            try {
                lock(TaskbarMask.Gate){if(!TaskbarMask.Active)return false;TaskbarMask.SavedAutoHide=(state&1)!=0;TaskbarMask.TransientState=true;data.Param=new IntPtr(state&~1U);Native.SHAppBarMessage(10,ref data);}
                chevron=null;
                for(int i=0;i<40&&chevron==null;i++){CacheTray();if(chevron==null)System.Threading.Thread.Sleep(25);}
                if(chevron==null)AppLog.Json("tray-elements.json",SystemButtons());
                invoked=InvokeChevron();
            }finally {lock(TaskbarMask.Gate){if(TaskbarMask.TransientState){data.Param=new IntPtr((state&~1U)|(TaskbarMask.SavedAutoHide?1U:0));Native.SHAppBarMessage(10,ref data);}if(TaskbarMask.Active)TaskbarMask.Hide(handle);TaskbarMask.TransientState=false;}DesktopLease.RecordTemporaryState(null);}
            if(!invoked){AppLog.Write("tray-open","cached="+cached+"; found="+(chevron!=null)+"; invoke failed");return false;}
            for(int i=0;i<40;i++){if(wasOpen||TrayVisible())return TrayOpened(handle,wasOpen,placement);System.Threading.Thread.Sleep(15);}AppLog.Write("tray-open","invoked but overflow host not visible");return false;
        }
        internal static List<TaskbarItem> ReadSaved()
        {
            try {string file=Path.Combine(AppLog.Data,"explorer-pins.json");if(File.Exists(file))return new JavaScriptSerializer().Deserialize<List<TaskbarItem>>(File.ReadAllText(file))??new List<TaskbarItem>();}catch{}return new List<TaskbarItem>();
        }
        internal static void Save(List<TaskbarItem> items) {AppLog.Json("explorer-pins.json",items.Where(a=>a.Pinned).Select(a=>new {Id=a.Id,Name=a.Name,Pinned=a.Pinned}).ToArray());}
        internal static List<TaskbarItem> ReadApps()
        {
            var result=new List<TaskbarItem>();
            try {
                var h=Native.FindWindow("Shell_TrayWnd",null); if(h==IntPtr.Zero) return result;
                var root=AutomationElement.FromHandle(h);
                var elements=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));
                foreach(AutomationElement e in elements) {
                    string id=e.Current.AutomationId; if(id==null || !id.StartsWith("Appid: ",StringComparison.Ordinal)) continue;
                    id=id.Substring(7); if(result.Any(a=>String.Equals(a.Id,id,StringComparison.OrdinalIgnoreCase))) continue;
                    string name=e.Current.Name??"";bool pinned=name.Contains("고정됨")||name.IndexOf("pinned",StringComparison.OrdinalIgnoreCase)>=0&&name.IndexOf("not pinned",StringComparison.OrdinalIgnoreCase)<0;
                    result.Add(new TaskbarItem {Id=id,Name=name,Pinned=pinned,X=e.Current.BoundingRectangle.Left,Element=e});
                }
                return result.OrderBy(e=>e.X).ToList();
            } catch(Exception e) { AppLog.Once("explorer-apps",e.Message); return result; }
        }
        internal static object[] SystemButtons()
        {
            try {var h=Native.FindWindow("Shell_TrayWnd",null);if(h==IntPtr.Zero)return new object[0];var root=AutomationElement.FromHandle(h);var result=new List<object>();
                foreach(AutomationElement e in root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button))){string id=e.Current.AutomationId??"";if(id.StartsWith("Appid: "))continue;result.Add(new {id=id,name=e.Current.Name,cls=e.Current.ClassName,patterns=e.GetSupportedPatterns().Select(p=>p.ProgrammaticName).ToArray()});}return result.ToArray();
            }catch{return new object[0];}
        }
    }

    // The guard reads this lease after a crash. Explorer and the old Windhawk helper are restored together.
    internal sealed class DesktopLease
    {
        public bool Active {get;set;} public uint TaskbarState {get;set;} public bool ResumeCharging {get;set;}
        public bool? RestoreTaskbarState {get;set;}
        public uint? TemporaryTaskbarState {get;set;}
        public string ChargingExe {get;set;} public string Session {get;set;}
        public int OwnerPid {get;set;}public long OwnerStarted {get;set;}
        internal static string FileName {get {return Path.Combine(AppLog.Data,"desktop-lease.json");}}
        internal static readonly object Gate=new object();
        internal static DesktopLease Acquire()
        {
            Recover();
            var data=new Native.AppBarData {Size=(uint)Marshal.SizeOf(typeof(Native.AppBarData)),Window=Native.FindWindow("Shell_TrayWnd",null)};
            string helper=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex","OneUIBookRuntime","Charging","OneUI9_Charging.exe");
            bool wasRunning=false;
            try { using(var active=EventWaitHandleCompat.OpenCharging()) {wasRunning=active!=null;} } catch { }
            // The completed Windhawk project has its own logon task; do not delete or disable it.
            bool scheduled=false;
            try {scheduled=File.Exists(helper)&&File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex","OneUIBookAssets","power-state.json"));} catch { }
            var lease=new DesktopLease {Active=true,TaskbarState=(uint)Native.SHAppBarMessage(4,ref data).ToUInt64(),RestoreTaskbarState=false,ResumeCharging=wasRunning||scheduled,ChargingExe=helper,Session=Guid.NewGuid().ToString("N")};
            lease.Save();SuppressCharging();return lease;
        }
        internal void Save() {lock(Gate){Directory.CreateDirectory(AppLog.Data);string temporary=FileName+".tmp";File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(this));if(File.Exists(FileName))File.Replace(temporary,FileName,FileName+".previous");else File.Move(temporary,FileName);}}
        internal static void RecordTemporaryState(uint? state)
        {
            lock(Gate){try{if(!File.Exists(FileName))return;var lease=new JavaScriptSerializer().Deserialize<DesktopLease>(File.ReadAllText(FileName));if(lease==null||!lease.Active)return;lease.TemporaryTaskbarState=state;lease.Save();}catch(Exception error){AppLog.Once("tray-state-recovery",error.Message);}}
        }
        internal static void SuppressCharging() { try {using(var stop=EventWaitHandleCompat.OpenCharging()) if(stop!=null) stop.Set();} catch { } }
        internal static void Recover(string expectedSession=null)
        {
            lock(Gate) {
                DesktopLease lease=null;
                try { if(File.Exists(FileName)) lease=new JavaScriptSerializer().Deserialize<DesktopLease>(File.ReadAllText(FileName)); } catch(Exception e) { AppLog.Write("lease-read",e.Message); }
                if(lease==null || !lease.Active || expectedSession!=null && lease.Session!=expectedSession) return;
                var data=new Native.AppBarData {Size=(uint)Marshal.SizeOf(typeof(Native.AppBarData)),Window=Native.FindWindow("Shell_TrayWnd",null),Param=new IntPtr(lease.TemporaryTaskbarState??lease.TaskbarState)};
                if(lease.TemporaryTaskbarState.HasValue||lease.RestoreTaskbarState!=false)Native.SHAppBarMessage(10,ref data);Native.RestoreTaskbars();
                lease.Active=false; lease.Save();
                if(lease.ResumeCharging && File.Exists(lease.ChargingExe)) try {
                    bool resumed=false;
                    using(var task=Process.Start(new ProcessStartInfo("schtasks.exe","/Run /TN \"OneUIBook.Charging\"") {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})) {resumed=task.WaitForExit(2000)&&task.ExitCode==0;}
                    if(!resumed)Process.Start(new ProcessStartInfo(lease.ChargingExe,"--daemon") {UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(lease.ChargingExe)});
                } catch(Exception e) { AppLog.Write("charging-resume",e.Message); }
                AppLog.Write("desktop-restore","Explorer state restored; Windhawk charging resume="+lease.ResumeCharging);
            }
        }
    }
    internal static class EventWaitHandleCompat
    {
        internal static System.Threading.EventWaitHandle OpenCharging() { try {return System.Threading.EventWaitHandle.OpenExisting(@"Local\OneUIBookChargingStop");} catch(System.Threading.WaitHandleCannotBeOpenedException) {return null;} }
    }
    internal sealed class AppBarReservation : IDisposable
    {
        readonly HwndSource source; readonly Forms.Screen screen; readonly double scale;readonly Action stateChanged;bool busy,disposed,autoHide,positioned;Native.RectI lastPosition;
        internal AppBarReservation(Forms.Screen monitor,double dpiScale,bool hidden=false,Action changed=null)
        {
            screen=monitor;scale=dpiScale;autoHide=hidden;stateChanged=changed;
            var p=new HwndSourceParameters("OneTaskBar.WorkArea") {WindowStyle=unchecked((int)0x80000000),ExtendedWindowStyle=0x80|0x08000000,PositionX=screen.Bounds.Left,PositionY=screen.Bounds.Bottom-1,Width=1,Height=1};
            source=new HwndSource(p); source.AddHook(Message);
            var data=Data(); data.Callback=0x8053; Native.SHAppBarMessage(0,ref data); Update();
        }
        Native.AppBarData Data() {return new Native.AppBarData {Size=(uint)Marshal.SizeOf(typeof(Native.AppBarData)),Window=source.Handle,Edge=3};}
        IntPtr Message(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) {if(msg==0x8053){if(w.ToInt32()==1)Update();else if(w.ToInt32()==0&&stateChanged!=null)stateChanged();}return IntPtr.Zero;}
        internal void SetAutoHide(bool value){if(autoHide==value)return;autoHide=value;Update();}
        internal static int ReservationTop(System.Drawing.Rectangle screen,double scale,int availableBottom,bool hidden){return hidden?availableBottom:Math.Min(availableBottom,screen.Bottom-(int)Math.Round(68*scale));}
        void Update()
        {
            if(busy||disposed) return; busy=true;
            try { var b=screen.Bounds; var data=Data(); data.Rect=new Native.RectI {Left=b.Left,Top=b.Top,Right=b.Right,Bottom=b.Bottom};
                Native.SHAppBarMessage(2,ref data);data.Rect.Top=ReservationTop(b,scale,data.Rect.Bottom,autoHide);if(positioned&&data.Rect.Equals(lastPosition))return;Native.SHAppBarMessage(3,ref data);lastPosition=data.Rect;positioned=true;
            } finally {busy=false;}
        }
        public void Dispose() {if(disposed)return; disposed=true; var data=Data(); Native.SHAppBarMessage(1,ref data); source.Dispose();}
    }
}
