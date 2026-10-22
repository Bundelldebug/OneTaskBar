using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed class DockLayout
    {
        internal double StartX,AppX,AppWidth,StatusX,StatusWidth,Y;
        internal int Capacity;
        internal static double StatusSize(double screenWidth,double powerWidth=58){return Math.Min(282,Math.Max(226,screenWidth*.25))+powerWidth-58;}
        internal static DockLayout Compute(System.Drawing.Rectangle screen,double scale,int count,double powerWidth=58)
        {
            double width=screen.Width/scale;
            double status=StatusSize(width,powerWidth),statusX=width-status-12;
            double available=Math.Max(74,statusX-78);
            int capacity=Math.Max(0,(int)Math.Floor((available-74)/46));
            double apps=Math.Min(count,capacity)*46+74;
            double x=Math.Max(66,Math.Min((width-apps)/2,statusX-apps-12));
            return new DockLayout { StartX=x-52,AppX=x,AppWidth=apps,StatusX=statusX,StatusWidth=status,Y=screen.Height/scale-56,Capacity=capacity };
        }
    }
    internal sealed class ShellController : IDisposable
    {
        internal readonly AudioService Audio=new AudioService();
        internal readonly bool Demo;
        internal bool Desktop;
        internal bool IsDisposing {get{return disposed;}}
        internal int Brightness=-1;
        internal PowerState Power=PowerState.Read();
        internal string Wifi;
        internal int WifiGeneration,WifiSignal=-1,WifiTraffic;long wifiSent=-1,wifiReceived=-1;
        internal List<AppEntry> Apps=new List<AppEntry>(),Running=new List<AppEntry>();
        internal List<AppEntry> Pins=new List<AppEntry>();
        readonly RuntimeDockOrder dockOrder=new RuntimeDockOrder();
        List<TaskbarItem> explorerItems=ExplorerBridge.ReadSaved();
        internal readonly SystemServices Services=new SystemServices();
        internal readonly QuickActions Toggles=new QuickActions();
        internal readonly NotificationService Notifications=new NotificationService();
        readonly List<DockHost> docks=new List<DockHost>();
        DockHost trayDock;
        bool trayBusy;
        readonly List<BatteryAnimation> charges=new List<BatteryAnimation>();
        readonly List<AppBarReservation> reservations=new List<AppBarReservation>();
        DesktopLease lease;
        readonly ChargeState chargeState=new ChargeState();
        readonly DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };
        readonly ShellEvents shellEvents;
        readonly VolumeKeys keys;
        readonly EventWaitHandle stop,show,notificationProbe,panelProbe,connectNotifications;
        readonly List<RegisteredWaitHandle> waits=new List<RegisteredWaitHandle>();
        readonly HwndSource messages;
        readonly IntPtr powerNotify,brightnessNotify,displayNotify;
        readonly BrightnessEvents brightnessEvents;
        internal bool BrightnessEventsReady {get{return brightnessEvents!=null&&brightnessEvents.Ready;}}
        readonly NativeOsdGate osdGate;
        readonly Forms.NotifyIcon tray;
        internal QuickWindow Quick;internal DrawerWindow Drawer;internal SearchWindow Finder;internal WelcomeWindow Welcome;internal NotificationWindow Center;internal DetailWindow Detail;
        internal NotificationConnectWindow NotificationLink;
        internal readonly WindowPreviewManager Previews=new WindowPreviewManager();
        HudWindow hud;
        DateTime hudUntil,queryAt,appsAt,commitBrightnessUntil,sourceUntil;
        bool brightnessBusy,runningBusy,disposed,servicesBusy,pinsBusy; bool? exactSource;
        bool guardStarted;
        int pinRevision;
        bool osdBrightnessBusy;string hudScreen;double hudScale=1;
        DateTime hudChangedAt;
        DateTime wifiAt,osdScanAt;bool wifiBusy;
        internal int OsdPrepared {get{return osdGate!=null?osdGate.Prepared:0;}}internal int OsdPrimedShows {get{return osdGate!=null?osdGate.PrimedShows:0;}}internal void PrepareNativeOsd(){if(osdGate!=null)osdGate.Refresh();}internal IntPtr[] OsdMasks {get{return osdGate!=null?osdGate.MaskedWindows:new IntPtr[0];}}
        internal int HudUpdates;internal string HudKind;internal bool HudVisible {get{return hud!=null&&hud.IsVisible;}}internal int OsdSuppressed {get{return osdGate!=null?osdGate.Suppressed:0;}}
        DateTime pinsAt;
        double lastAppRefreshMs;
        DateTime servicesAt;
        IntPtr lastForeground;
        internal IntPtr LastApplication;
        internal DateTime FocusUntil;
        internal bool DisplayAwake=true;
        int serviceWakeQueued,servicesDirty;DateTime liveStateAt;
        double previousVolume=-1; bool previousMuted;
        int iteration;readonly int ownPid=Process.GetCurrentProcess().Id;
        public ShellController(bool demo=false)
        {
            Demo=demo;
            if(demo) return;
            keys=new VolumeKeys();
            keys.InterceptStart=()=>Desktop&&!Preferences.Current.NativeStartKey;
            keys.OnStart=()=>Application.Current.Dispatcher.BeginInvoke(new Action(()=> {if(Desktop&&!disposed)ToggleDrawer(null);}));
            keys.OnKey=(key)=> {
                if(!Audio.Available) return false;
                Application.Current.Dispatcher.BeginInvoke(new Action(()=> {
                    Try(()=> { Audio.Read(); if(key==0xAD) Audio.Mute(!Audio.Muted); else { Audio.Mute(false); Audio.Set(Audio.Value+(key==0xAF ? 2 : -2)); }
                        Audio.Read();previousVolume=Audio.Value;previousMuted=Audio.Muted; ShowHud("volume",Audio.Value,Audio.Muted); });
                })); return true;
            };
            var p=new HwndSourceParameters("OneTaskBar.Events") { WindowStyle=0,ParentWindow=new IntPtr(-3) };
            messages=new HwndSource(p); messages.AddHook(Message);
            var source=new Guid("5D3E9A59-E9D5-4B00-A6BD-FF34FF516548");
            powerNotify=Native.RegisterPowerSettingNotification(messages.Handle,ref source,0);
            var brightSource=new Guid("8ffee2c6-2d01-46be-adb9-398addc5b4ff");brightnessNotify=Native.RegisterPowerSettingNotification(messages.Handle,ref brightSource,0);
            var displaySource=new Guid("2b84c20e-ad23-4ddf-93db-05ffbd7efca5");displayNotify=Native.RegisterPowerSettingNotification(messages.Handle,ref displaySource,0);
            Audio.Changed=Wake;brightnessEvents=new BrightnessEvents(QueueBrightness);
            Services.Changed=ServiceChanged;Notifications.Updated=ServiceChanged;
            osdGate=new NativeOsdGate(()=>Desktop&&!disposed,ReplaceNativeOsd,()=>Desktop&&!disposed&&Brightness>=0&&Audio.Available);
            Native.RegisterHotKey(messages.Handle,1,0x4000|2|1,0x51); // Ctrl+Alt+Q, no Win-key hook.
            Native.RegisterHotKey(messages.Handle,2,0x4000|2|1,0x48); // Ctrl+Alt+H.
            stop=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\OneTaskBar.Stop");
            show=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\OneTaskBar.Show");
            notificationProbe=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\OneTaskBar.ProbeNotifications");
            panelProbe=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\OneTaskBar.ProbePanels");
            connectNotifications=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\OneTaskBar.ConnectNotifications");
            waits.Add(ThreadPool.RegisterWaitForSingleObject(stop,(s,t)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>Application.Current.Shutdown())),null,Timeout.Infinite,false));
            waits.Add(ThreadPool.RegisterWaitForSingleObject(show,(s,t)=>Application.Current.Dispatcher.BeginInvoke(new Action(ShowWelcome)),null,Timeout.Infinite,false));
            waits.Add(ThreadPool.RegisterWaitForSingleObject(notificationProbe,(s,t)=>Application.Current.Dispatcher.BeginInvoke(new Action(ProbeNotifications)),null,Timeout.Infinite,false));
            waits.Add(ThreadPool.RegisterWaitForSingleObject(panelProbe,(s,t)=>Application.Current.Dispatcher.BeginInvoke(new Action(ProbePanels)),null,Timeout.Infinite,false));
            waits.Add(ThreadPool.RegisterWaitForSingleObject(connectNotifications,(s,t)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>ShowNotificationConnection())),null,Timeout.Infinite,false));
            tray=new Forms.NotifyIcon { Text=Program.AppName,Visible=true };
            string icon=Path.Combine(AppLog.Root,"assets","OneTaskBar.ico"); if(File.Exists(icon)) tray.Icon=new System.Drawing.Icon(icon);
            tray.DoubleClick+=(s,e)=>Application.Current.Dispatcher.BeginInvoke(new Action(ShowWelcome));
            var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("One TaskBar 열기",null,(s,e)=>Application.Current.Dispatcher.BeginInvoke(new Action(ShowWelcome)));
            menu.Items.Add("데스크톱 모드 전환",null,(s,e)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>SetDesktop(!Desktop))));
            menu.Items.Add("종료 · Windows 작업표시줄 복원",null,(s,e)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>Application.Current.Shutdown())));
            tray.ContextMenuStrip=menu;
            SystemEvents.DisplaySettingsChanged+=DisplayChanged;
            SystemEvents.PowerModeChanged+=PowerChanged;
            timer.Tick+=(s,e)=>Tick();
            shellEvents=new ShellEvents(Application.Current.Dispatcher,()=> {appsAt=DateTime.MinValue;Tick();},Tick);
            Preferences.Changed+=OptionsChanged;
            LayoutOrder.Changed+=LayoutChanged;DragReorder.Finished+=DragFinished;
        }
        internal void Start(bool desktop)
        {
            Audio.Read(); previousVolume=Audio.Value; previousMuted=Audio.Muted; chargeState.Observe(Power.Connected);
            Notifications.Check();
            BackgroundWork.Run(()=>Catalog.Read(false)).ContinueWith(t=>Application.Current.Dispatcher.BeginInvoke(new Action(()=> {
                if(disposed) return; if(t.Status==TaskStatus.RanToCompletion) Apps=t.Result; else AppLog.Write("apps",t.Exception.ToString());
                if(Drawer!=null&&Drawer.IsVisible)Drawer.Reload();if(Finder!=null&&Finder.IsVisible)Finder.Reload(); foreach(var dock in docks) dock.Rebuild();
            })));
            timer.Start(); SetDesktop(desktop);
            EnsureGuard();
            if(!desktop) ShowWelcome();
            AppLog.Write("startup","One TaskBar "+typeof(ShellController).Assembly.GetName().Version+"; Windows system font; native app icons; desktop="+desktop);
        }
        IntPtr Message(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled)
        {
            if(msg==0x218) {
                if(wp.ToInt64()==0x8013 && lp!=IntPtr.Zero) {
                    var guid=(Guid)Marshal.PtrToStructure(lp,typeof(Guid));
                    if(guid==new Guid("5D3E9A59-E9D5-4B00-A6BD-FF34FF516548") && Marshal.ReadInt32(lp,16)>=4) {
                        exactSource=Marshal.ReadInt32(lp,20)==0; sourceUntil=DateTime.UtcNow.AddSeconds(2);
                    }
                    if(guid==new Guid("8ffee2c6-2d01-46be-adb9-398addc5b4ff")&&Marshal.ReadInt32(lp,16)>=4)ApplyBrightness(Marshal.ReadInt32(lp,20));
                    if(guid==new Guid("2b84c20e-ad23-4ddf-93db-05ffbd7efca5")&&Marshal.ReadInt32(lp,16)>=4){DisplayAwake=Marshal.ReadInt32(lp,20)!=0;if(DisplayAwake){servicesAt=appsAt=wifiAt=queryAt=DateTime.MinValue;}else foreach(var dock in docks)dock.Refresh(Power);}
                }
                Tick();
            }
            if(msg==0x312) { if(wp.ToInt32()==1) ToggleQuick(docks.FirstOrDefault()); else if(wp.ToInt32()==2) ShowWelcome(); handled=true; }
            if(msg==0x7E || msg==0x2E0) Application.Current.Dispatcher.BeginInvoke(new Action(Relayout));
            return IntPtr.Zero;
        }
        void PowerChanged(object s,PowerModeChangedEventArgs e) { Application.Current.Dispatcher.BeginInvoke(new Action(()=> { Tick(); if(e.Mode==PowerModes.Resume) Relayout(); })); }
        void DisplayChanged(object s,EventArgs e) { Application.Current.Dispatcher.BeginInvoke(new Action(Relayout)); }
        internal void SetDesktop(bool value)
        {
            if(disposed) return;
            if(Desktop==value && (value ? docks.Count>0 : true)) return;
            Desktop=value;if(osdGate!=null)osdGate.Refresh();Preferences.Current.DesktopMode=value;Preferences.Save();if(keys!=null) keys.Enabled=value;
            if(value) {
                var imported=ExplorerBridge.ReadApps();if(imported.Count>0){explorerItems=imported;ExplorerBridge.Save(imported);}
                ExplorerBridge.CacheTray();
                Running=Catalog.Running();Pins=Catalog.ReadPins(Apps,explorerItems,Running);
                lease=DesktopLease.Acquire();
                TaskbarMask.Active=true;
                // Watch this process; recovery tokens protect newer desktop leases.
                using(var current=Process.GetCurrentProcess()){lease.OwnerPid=current.Id;lease.OwnerStarted=current.StartTime.ToUniversalTime().Ticks;lease.Save();}EnsureGuard();
                Native.HideTaskbars(); Relayout(); if(Welcome!=null) Welcome.Hide();
            } else {
                TaskbarMask.Active=false;
                foreach(var dock in docks) dock.Close(); docks.Clear();
                foreach(var reservation in reservations) reservation.Dispose(); reservations.Clear();
                foreach(var animation in charges) animation.Close(); charges.Clear();
                if(Quick!=null) Quick.Hide(); if(Drawer!=null) Drawer.Hide();if(Finder!=null)Finder.HideImmediately();if(Center!=null)Center.Hide();if(Detail!=null)Detail.Hide();
                if(lease!=null) DesktopLease.Recover(lease.Session); lease=null;
            }
            AppLog.Json("session.json",new { desktop=value,pid=Process.GetCurrentProcess().Id,time=DateTimeOffset.Now.ToString("o") });
            if(Welcome!=null) Welcome.UpdateMode();
        }
        void Relayout()
        {
            if(!Desktop || disposed) return;
            Previews.Close();
            string trayMonitor=trayDock!=null?trayDock.Screen.DeviceName:null;
            foreach(var animation in charges)animation.Close();charges.Clear();
            foreach(var reservation in reservations) reservation.Dispose(); reservations.Clear();
            foreach(var dock in docks) dock.Close(); docks.Clear();
            foreach(var screen in Forms.Screen.AllScreens) { var dock=new DockHost(this,screen); docks.Add(dock); dock.Show(); reservations.Add(new AppBarReservation(screen,dock.Scale)); }
            trayDock=trayMonitor==null?null:docks.FirstOrDefault(d=>d.Screen.DeviceName==trayMonitor)??docks.FirstOrDefault();
            if(Quick!=null) Quick.Hide(); if(Drawer!=null) Drawer.Hide();if(Finder!=null)Finder.HideImmediately();if(Center!=null)Center.Hide();if(Detail!=null)Detail.Hide();
        }
        internal List<AppEntry> DockApps()
        {
            return dockOrder.Arrange(LayoutOrder.Arrange("dock.apps",Catalog.Merge(Pins,Running),a=>a.Key));
        }
        internal void ResetDockOrder(){dockOrder.Reset();LayoutOrder.Reset("dock.apps");foreach(var dock in docks)dock.Rebuild();}
        internal bool MoveDock(string source,string target,bool after)
        {
            var entries=DockApps();var moving=entries.FirstOrDefault(a=>String.Equals(a.Key,source,StringComparison.OrdinalIgnoreCase));var anchor=entries.FirstOrDefault(a=>String.Equals(a.Key,target,StringComparison.OrdinalIgnoreCase));
            if(moving==null||anchor==null||String.Equals(source,target,StringComparison.OrdinalIgnoreCase))return false;
            if(moving.Pinned!=anchor.Pinned)SetPin(moving,anchor.Pinned);if(!dockOrder.Move(source,target,after))return false;LayoutOrder.Move("dock.apps",entries.Select(a=>a.Key),source,target,after);foreach(var dock in docks)dock.Rebuild();return true;
        }
        internal bool MoveDockGroup(string source,bool pinned)
        {
            var entries=DockApps();var moving=entries.FirstOrDefault(a=>String.Equals(a.Key,source,StringComparison.OrdinalIgnoreCase));if(moving==null)return false;
            var anchor=entries.LastOrDefault(a=>a.Pinned==pinned&&!String.Equals(a.Key,source,StringComparison.OrdinalIgnoreCase));bool changed=moving.Pinned!=pinned;if(changed)SetPin(moving,pinned);
            if(anchor!=null){changed|=dockOrder.Move(source,anchor.Key,true);LayoutOrder.Move("dock.apps",entries.Select(a=>a.Key),source,anchor.Key,true);}foreach(var dock in docks)dock.Rebuild();return changed;
        }
        void LayoutChanged(string scope) {if(scope=="dock.apps"&&!DragReorder.Active)foreach(var dock in docks)dock.Rebuild();}
        void DragFinished() {if(!disposed)Application.Current.Dispatcher.BeginInvoke(new Action(()=> {if(!disposed)foreach(var dock in docks)dock.Rebuild();}));}
        void OptionsChanged()
        {
            if(disposed)return;
            if(Preferences.Current.NativeSearch&&Finder!=null)Finder.Hide();
            if(Preferences.Current.NativeQuickSettings){if(Quick!=null)Quick.Hide();if(Detail!=null)Detail.Hide();}
            if(Preferences.Current.NativeNotifications&&Center!=null)Center.Hide();
            if(!Preferences.Current.Animations||!Preferences.Current.ChargeAnimation){foreach(var charge in charges)charge.Close();charges.Clear();}
            foreach(var dock in docks)dock.Rebuild();
        }
        void SetPin(AppEntry entry,bool pin)
        {
            Catalog.SetPinned(entry,pin);pinRevision++;Pins.RemoveAll(a=>Catalog.Same(a,entry));entry.Pinned=pin;
            if(pin){var saved=entry.Copy();saved.Pinned=true;Pins.Add(saved);}foreach(var item in Running.Concat(Apps))if(Catalog.Same(item,entry))item.Pinned=pin;pinsAt=appsAt=DateTime.MinValue;
        }
        internal void Pin(AppEntry entry,bool pin) {SetPin(entry,pin);foreach(var dock in docks)dock.Rebuild();}
        internal void ActivateApp(AppEntry entry,FrameworkElement anchor,IntPtr beforeClick=default(IntPtr))
        {
            var windows=entry.Windows.Where(Native.IsWindow).ToList();
            var foreground=beforeClick!=IntPtr.Zero&&Native.IsWindow(beforeClick)?beforeClick:Native.GetForegroundWindow();var root=Native.GetAncestor(foreground,3);var active=windows.FirstOrDefault(h=>h==foreground||h==root);
            if(active!=IntPtr.Zero&&!Native.IsIconic(active)){Native.ShowWindow(active,6);return;}
            if(windows.Count>1) {WindowPicker.ShowFor(entry,anchor,this); return;}
            if(windows.Count==1) {
                Native.Activate(windows[0]); return;
            }
            var launch=entry.Copy();launch.Windows.Clear();launch.Launch();appsAt=DateTime.MinValue;
        }
        internal async void Radio(string kind)
        {
            if(!await Services.ToggleRadio(kind))ShowDetail(kind);
            await Services.Refresh(); if(Quick!=null) Quick.Refresh(Power,Brightness,Audio.Value,Audio.Muted,Audio.Available,Brightness>=0);
        }
        internal async void Media(string command) {await Services.ControlMedia(command); servicesAt=DateTime.MinValue;}
        internal async Task<bool> SeekMedia(double percent){bool changed=await Services.SeekPercent(percent);if(changed)servicesAt=DateTime.MinValue;return changed;}
        internal void NowBarOpening(NowBar selected){foreach(var dock in docks)if(dock.MediaBar!=null&&dock.MediaBar!=selected)dock.MediaBar.SetExpanded(false);foreach(var panel in new GlassWindow[]{Quick,Center,Detail,Drawer,Finder})if(panel!=null)panel.HideImmediately();}
        internal void ShowNative(string kind,DockHost dock=null)
        {
            if(trayBusy||!Desktop||disposed)return;
            trayDock=dock??docks.FirstOrDefault();if(trayDock==null)return;
            var anchor=trayDock.TrayAnchor();trayBusy=true;
            if(Quick!=null) Quick.Hide(); if(Drawer!=null) Drawer.Hide();
            Task.Run(()=>ExplorerBridge.OpenTray(anchor)).ContinueWith(t=>Application.Current.Dispatcher.BeginInvoke(new Action(()=> {
                trayBusy=false;
                if(t.Status!=TaskStatus.RanToCompletion||!t.Result)AppLog.Write("native-panel","Windows tray invocation failed; native taskbar kept hidden");
            })));
        }
        int panelMotion;string pendingPanel;
        internal async void ToggleNotifications(DockHost dock){await TogglePanel(true,dock,false);}
        async Task TogglePanel(bool notifications,DockHost dock,bool forceCustom)
        {
            Previews.Close();
            int request=++panelMotion;string key=notifications?"notifications":"quick";bool pending=pendingPanel==key;pendingPanel=null;
            try {
                if(!forceCustom&&(notifications?Preferences.Current.NativeNotifications:Preferences.Current.NativeQuickSettings)){ShowWindowsPanel(notifications,dock,request);return;}
                if(notifications&&Center==null)Center=new NotificationWindow(this);if(!notifications&&Quick==null)Quick=new QuickWindow(this);GlassWindow target=notifications?(GlassWindow)Center:Quick;
                if(pending||target.IsVisible&&!target.IsDismissing){target.Hide();return;}pendingPanel=key;
                if(Finder!=null)Finder.HideImmediately();if(Drawer!=null)Drawer.HideImmediately();if(Detail!=null)Detail.Hide();foreach(var host in docks)if(host.MediaBar!=null)host.MediaBar.SetExpanded(false);
                GlassWindow other=notifications?(GlassWindow)Quick:Center;if(other!=null&&!await other.DismissAsync())return;
                if(disposed||request!=panelMotion)return;
                var screen=dock!=null?dock.Screen:Forms.Screen.FromPoint(Forms.Cursor.Position);double scale=dock!=null?dock.Scale:MonitorScale(screen,target),w=Math.Min(notifications?394:380,screen.Bounds.Width/scale-24),h=Math.Min(notifications?584:650,screen.Bounds.Height/scale-100);
                if(notifications)Center.Refresh();else Quick.Refresh(Power,Brightness,Audio.Value,Audio.Muted,Audio.Available,Brightness>=0);
                target.ShowAt(screen.Bounds,screen.Bounds.Width/scale-w-12,screen.Bounds.Height/scale-h-76,w,h);target.Pop();target.Activate();if(notifications)Center.RefreshNotifications();
            }catch(Exception error){AppLog.Write("panel-switch",error.Message);}finally{if(request==panelMotion)pendingPanel=null;}
        }
        internal void ShowDetail(string page)
        {
            Previews.Close();
            if(Finder!=null)Finder.HideImmediately();
            if(Detail!=null){Detail.Close();Detail=null;}if(Center!=null)Center.Hide();if(Quick!=null)Quick.Hide();if(Drawer!=null)Drawer.HideImmediately();Detail=new DetailWindow(this,page);
            var screen=Forms.Screen.FromPoint(Forms.Cursor.Position);double scale=MonitorScale(screen,Detail),w=Math.Min(380,screen.Bounds.Width/scale-24),h=Math.Min(540,screen.Bounds.Height/scale-100);
            Detail.ShowAt(screen.Bounds,screen.Bounds.Width/scale-w-12,screen.Bounds.Height/scale-h-76,w,h);Detail.Pop();Detail.Activate();
        }
        internal void ShowWelcome() { if(Welcome==null) Welcome=new WelcomeWindow(this); Welcome.UpdateMode(); Welcome.Show();Welcome.Pop(); Welcome.Activate(); }
        internal void ShowNotificationConnection(bool resume=false)
        {
            Previews.Close();
            if(NotificationLink==null)NotificationLink=new NotificationConnectWindow(this);if(Quick!=null)Quick.Hide();if(Center!=null)Center.Hide();if(Welcome!=null)Welcome.Hide();var screen=Forms.Screen.FromPoint(Forms.Cursor.Position);double scale=MonitorScale(screen,NotificationLink),w=Math.Min(490,screen.Bounds.Width/scale-32),h=Math.Min(414,screen.Bounds.Height/scale-100);NotificationLink.ShowAt(screen.Bounds,(screen.Bounds.Width/scale-w)/2,(screen.Bounds.Height/scale-h)/2,w,h);NotificationLink.Pop();NotificationLink.Activate();if(resume)Application.Current.Dispatcher.BeginInvoke(new Action(async()=>{await Task.Delay(350);if(!disposed&&NotificationLink.IsVisible)NotificationLink.Begin();}));
        }
        internal AppEntry SamsungSettings()
        {
            return Apps.FirstOrDefault(a=>(a.AppID??"").IndexOf("SamsungSettings",StringComparison.OrdinalIgnoreCase)>=0)??Apps.FirstOrDefault(a=>a.Name=="삼성 설정"||a.Name=="Samsung Settings");
        }
        bool externalLaunching;
        internal AppEntry SamsungDevices() {return Apps.FirstOrDefault(a=>(a.AppID??"").IndexOf("SamsungMyDevices",StringComparison.OrdinalIgnoreCase)>=0)??Apps.FirstOrDefault(a=>a.Name=="주변 기기 연결");}
        internal void OpenSamsungSettings()
        {
            var samsung=SamsungSettings();if(samsung==null){if(Quick!=null)Quick.Feedback("삼성 설정이 설치되어 있지 않습니다.");return;}OpenQuickApplication(samsung);
        }
        internal void OpenSamsungDevices() {var devices=SamsungDevices();if(devices==null){if(Quick!=null)Quick.Feedback("삼성 주변 기기 연결 앱이 설치되어 있지 않습니다.");return;}OpenQuickApplication(devices);}
        internal void OpenWindowsSettings() {OpenQuickApplication(new AppEntry {Target="ms-settings:"});}
        async void OpenQuickApplication(AppEntry app)
        {
            if(externalLaunching||disposed)return;externalLaunching=true;
            try {bool dismissed=Quick==null||await Quick.DismissAsync();if(dismissed&&!disposed)app.Launch();}
            catch(Exception error){if(Quick!=null)Quick.Feedback("앱을 열지 못했습니다: "+error.Message);AppLog.Write("quick-launch",error.Message);}
            finally {externalLaunching=false;}
        }
        internal async void OpenHomeSettings() {if(Quick!=null&&!await Quick.DismissAsync())return;if(!disposed)ShowWelcome();}
        internal async void ToggleQuick(DockHost dock,bool forceCustom=false){await TogglePanel(false,dock,forceCustom);}
        bool nativePanelBusy;
        internal async void ShowWindowsPanel(bool notifications,DockHost dock,int request=0)
        {
            if(nativePanelBusy||disposed)return;if(request==0)request=++panelMotion;pendingPanel=null;nativePanelBusy=true;
            try {
                var closing=new List<Task<bool>>();foreach(var panel in new GlassWindow[]{Quick,Center,Detail,Drawer,Finder})if(panel!=null)closing.Add(panel.DismissAsync());
                var completed=await Task.WhenAll(closing);if(disposed||request!=panelMotion||completed.Any(done=>!done))return;
                var screen=dock!=null?dock.Screen:Forms.Screen.FromPoint(Forms.Cursor.Position);
                bool opened=await Task.Run(()=>ExplorerBridge.OpenSystemPanel(notifications,screen.Bounds));
                if(!opened&&!disposed){ShowWelcome();Welcome.Feedback("Windows 기본 패널을 열지 못했습니다. 다시 눌러 주세요.");}
            }catch(Exception error){AppLog.Write("native-panel",error.Message);if(!disposed){ShowWelcome();Welcome.Feedback("Windows 기본 패널을 열지 못했습니다.");}}
            finally {nativePanelBusy=false;}
        }
        internal void ToggleDrawer(DockHost dock)
        {
            Previews.Close();
            if(Drawer==null)Drawer=new DrawerWindow(this);
            if(Drawer.IsVisible) { Drawer.Hide(); return; }
            if(Finder!=null)Finder.HideImmediately();if(Center!=null)Center.Hide();if(Detail!=null)Detail.Hide();
            if(Quick!=null) Quick.Hide();
            var screen=dock!=null ? dock.Screen : Forms.Screen.FromPoint(Forms.Cursor.Position); double scale=dock!=null ? dock.Scale : MonitorScale(screen,Drawer);
            var layout=LauncherLayout.Fit(screen.Bounds.Width/scale,screen.Bounds.Height/scale);
            Drawer.Search.Text="";Drawer.Configure(layout.Width,layout.Height);Drawer.Reload();Drawer.ShowAt(screen.Bounds,layout.X,layout.Y,layout.Width,layout.Height);Drawer.Pop();Drawer.Activate();Drawer.Search.Focus();
        }
        internal async void ToggleStartButton(DockHost dock)
        {
            if(!Preferences.Current.NativeStartButton){ToggleDrawer(dock);return;}
            foreach(var panel in new GlassWindow[]{Quick,Center,Detail,Drawer,Finder})if(panel!=null)panel.HideImmediately();
            if(!await Task.Run(()=>SystemPanels.Start())){ShowWelcome();Welcome.Feedback("Windows 시작 메뉴를 열지 못했습니다. 다시 눌러 주세요.");}
        }
        void EnsureGuard()
        {
            if(guardStarted||Demo)return;string helper=Path.Combine(AppLog.Root,"OneTaskBar.Guard.exe");if(!File.Exists(helper))return;
            guardStarted=true;GuardProcess.Child guard;long started;using(var current=Process.GetCurrentProcess()){started=current.StartTime.ToUniversalTime().Ticks;guard=GuardProcess.Start(current,Path.Combine(AppLog.Root,"OneTaskBar.exe"),AppLog.Data);}
            Application.Current.Dispatcher.BeginInvoke(new Action(async()=>{
                using(guard)try {
                    string ready=Path.Combine(AppLog.Data,"guard-"+ownPid+".ready"),expected="event-driven|"+started+"|packaged=False";bool confirmed=false;
                    for(int i=0;i<100&&!disposed&&!guard.HasExited;i++){try{confirmed=File.ReadAllText(ready)==expected;}catch(IOException){}if(confirmed)break;await Task.Delay(100);}
                    if(disposed)return;if(!confirmed)throw new InvalidOperationException("Recovery helper did not become ready outside the package runtime");
                }catch(Exception error){AppLog.Write("watchdog",error.Message);if(!disposed)Application.Current.Shutdown(1);}
            }));
        }
        internal async void ToggleSearch(DockHost dock,string query=null)
        {
            Previews.Close();
            if(Preferences.Current.NativeSearch){
                var closing=new List<Task<bool>>();foreach(var panel in new GlassWindow[]{Quick,Center,Detail,Drawer,Finder})if(panel!=null)closing.Add(panel.DismissAsync());
                var completed=await Task.WhenAll(closing);if(disposed||completed.Any(done=>!done))return;
                if(!await Task.Run(()=>SystemPanels.Search(query))){ShowWelcome();Welcome.Feedback("Windows 검색을 열지 못했습니다. 다시 눌러 주세요.");}return;
            }
            if(Finder==null)Finder=new SearchWindow(this);if(Finder.IsVisible){Finder.Hide();return;}
            foreach(var panel in new GlassWindow[]{Quick,Center,Detail,Drawer})if(panel!=null)panel.HideImmediately();
            var screen=dock!=null?dock.Screen:Forms.Screen.FromPoint(Forms.Cursor.Position);double scale=dock!=null?dock.Scale:MonitorScale(screen,Finder);var layout=LauncherLayout.Fit(screen.Bounds.Width/scale,screen.Bounds.Height/scale);
            Finder.Configure(layout.Width,layout.Height);Finder.Reset();if(!String.IsNullOrEmpty(query))Finder.Query.Text=query;Finder.ShowAt(screen.Bounds,layout.X,layout.Y,layout.Width,layout.Height);Finder.Pop();Finder.Activate();Finder.Query.Focus();Finder.Query.CaretIndex=Finder.Query.Text.Length;
        }
        internal async void OpenScreenCapture()
        {
            Previews.Close();var closing=new List<Task<bool>>();foreach(var panel in new GlassWindow[]{Quick,Center,Detail,Drawer,Finder})if(panel!=null)closing.Add(panel.DismissAsync());var done=await Task.WhenAll(closing);if(!disposed&&done.All(v=>v))Native.Open("ms-screenclip:");
        }
        internal async void OpenTaskManager()
        {
            var closing=new List<Task<bool>>();foreach(var panel in new GlassWindow[]{Quick,Center,Detail,Drawer,Finder})if(panel!=null)closing.Add(panel.DismissAsync());var done=await Task.WhenAll(closing);if(!disposed&&done.All(v=>v))Native.Open("taskmgr.exe");
        }
        internal void Volume(double value) { Try(()=> { Audio.Set(value); if(Audio.Muted && value>0) Audio.Mute(false); Audio.Read(); }); }
        internal void Mute() { Try(()=> { Audio.Mute(!Audio.Muted); Audio.Read(); }); }
        internal void Bright(double value)
        {
            Brightness=(int)value; commitBrightnessUntil=DateTime.UtcNow.AddSeconds(1);
            // Serialize writes and coalesce dragging so stale WMI requests cannot overtake the last value.
            lock(brightnessGate) { requestedBrightness=(int)value; if(brightnessWriting) return; brightnessWriting=true; }
            Task.Run(()=> {
                try { while(true) {
                    int applied; lock(brightnessGate) applied=requestedBrightness;
                    BrightnessService.Set(applied);
                    lock(brightnessGate) { if(requestedBrightness==applied) { brightnessWriting=false; break; } }
                } }
                catch(Exception e) { lock(brightnessGate) brightnessWriting=false; AppLog.Write("brightness-set",e.Message); }
            });
        }
        readonly object brightnessGate=new object(); int requestedBrightness; volatile bool brightnessWriting;
        void QueueBrightness(int value){if(!disposed&&!Application.Current.Dispatcher.HasShutdownStarted)Application.Current.Dispatcher.BeginInvoke(new Action(()=>ApplyBrightness(value)));}
        void ApplyBrightness(int value)
        {
            if(disposed||value<0||value>100||DateTime.UtcNow<commitBrightnessUntil)return;int before=Brightness;Brightness=value;
            if(osdGate!=null)osdGate.Refresh();if(before>=0&&before!=value&&DisplayAwake&&(Quick==null||!Quick.IsVisible))ShowHud("brightness",value,false);
        }
        void ServiceChanged(){Interlocked.Exchange(ref servicesDirty,1);Wake();}
        void Wake()
        {
            if(disposed||Interlocked.Exchange(ref serviceWakeQueued,1)!=0)return;
            Application.Current.Dispatcher.BeginInvoke(new Action(()=>{Interlocked.Exchange(ref serviceWakeQueued,0);if(!disposed)Tick();}));
        }
        async void RefreshServices()
        {
            try{await Task.WhenAll(Services.Refresh(),Notifications.Refresh());}
            catch(Exception error){AppLog.Once("services",error.Message);}
            finally{servicesBusy=false;}
        }
        void Tick()
        {
            if(disposed || Demo) return;
            var now=DateTime.UtcNow;
            if(Interlocked.Exchange(ref servicesDirty,0)!=0)servicesAt=DateTime.MinValue;
            if(FocusUntil!=DateTime.MinValue&&now>=FocusUntil){FocusUntil=DateTime.MinValue;System.Media.SystemSounds.Asterisk.Play();if(tray!=null)tray.ShowBalloonTip(5000,Program.AppName,"집중 타이머가 끝났습니다",Forms.ToolTipIcon.Info);}
            if(!DisplayAwake){double sleep=RuntimeCadence.Pulse(Desktop,true,false,false,false);if(FocusUntil!=DateTime.MinValue)sleep=Math.Min(sleep,Math.Max(100,(FocusUntil-now).TotalMilliseconds));timer.Interval=TimeSpan.FromMilliseconds(sleep);return;}
            bool interactive=(Quick!=null&&Quick.IsVisible)||(Center!=null&&Center.IsVisible)||(Detail!=null&&Detail.IsVisible)||(Drawer!=null&&Drawer.IsVisible)||(Finder!=null&&Finder.IsVisible)||(Welcome!=null&&Welcome.IsVisible)||docks.Any(d=>d.MediaBar!=null&&d.MediaBar.Expanded);
            if(now>=osdScanAt){osdScanAt=now.AddSeconds(10);if(osdGate!=null)osdGate.Refresh();}
            if(Desktop)ExplorerBridge.KeepTrayPosition(trayDock!=null?trayDock.TrayAnchor():null);
            var foreground=Native.GetForegroundWindow(); uint foregroundPid; Native.GetWindowThreadProcessId(foreground,out foregroundPid);
            if(foregroundPid!=ownPid) LastApplication=foreground;
            if(Desktop) foreach(var dock in docks) {dock.SetVisible(!Native.IsFullScreen(dock.Screen)); if(foreground!=lastForeground) dock.UpdateActive(LastApplication);}
            lastForeground=foreground;
            var power=PowerState.Read();
            if(exactSource.HasValue && now<sourceUntil) power.Connected=exactSource.Value;
            Power=power;bool battery=Power.Known&&!Power.Connected;double cadence=RuntimeCadence.Pulse(Desktop,battery,interactive,docks.Count>0&&docks.All(d=>!d.Apps.IsVisible),true);if(timer.Interval.TotalMilliseconds!=cadence)timer.Interval=TimeSpan.FromMilliseconds(cadence);
            if(Welcome!=null&&Welcome.IsVisible)Welcome.PreviewBattery.Update(Power);
            bool celebrate=chargeState.Observe(Power.Connected);
            if(celebrate && Power.Known&&Desktop&&Preferences.Current.ChargeAnimation&&Preferences.Current.Animations) PreviewCharge(false);
            Audio.Read();
            if(previousVolume>=0 && Audio.Available && (Math.Abs(Audio.Value-previousVolume)>=1 || Audio.Muted!=previousMuted) && (Quick==null || !Quick.IsVisible)) ShowHud("volume",Audio.Value,Audio.Muted);
            previousVolume=Audio.Value; previousMuted=Audio.Muted;
            Services.Interactive=interactive;Notifications.Interactive=Center!=null&&Center.IsVisible;
            if(now>=servicesAt&&!servicesBusy) {servicesAt=now.AddMilliseconds(interactive?500:2000);servicesBusy=true;RefreshServices();}
            if(Center!=null&&Center.IsVisible)Center.Refresh();
            if(Quick!=null && Quick.IsVisible) Quick.Refresh(Power,Brightness,Audio.Value,Audio.Muted,Audio.Available,Brightness>=0);
            foreach(var dock in docks) dock.Refresh(Power);
            for(int i=charges.Count-1;i>=0;i--) if(!Power.Connected&&!charges[i].AllowDisconnected || !charges[i].Frame(now)) { charges[i].Close(); charges.RemoveAt(i); }
            if(hud!=null && hud.IsVisible && now>hudUntil) hud.Hide();
            if(now>=wifiAt&&!wifiBusy){wifiAt=now.AddSeconds(interactive?1:5);wifiBusy=true;BackgroundWork.Run(()=>Native.ReadWifi()).ContinueWith(t=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>{wifiBusy=false;if(!disposed&&t.Status==TaskStatus.RanToCompletion){Wifi=t.Result.Name;WifiGeneration=t.Result.Generation;WifiSignal=t.Result.Signal;WifiTraffic=(wifiSent>=0&&t.Result.Sent>wifiSent?1:0)|(wifiReceived>=0&&t.Result.Received>wifiReceived?2:0);wifiSent=t.Result.Sent;wifiReceived=t.Result.Received;}})));}
            if(now>=queryAt && !brightnessBusy) {
                queryAt=now.AddSeconds(Brightness<0?60:interactive?5:battery?15:10); brightnessBusy=true;
                BackgroundWork.Run(()=>BrightnessService.Read()).ContinueWith(t=>Application.Current.Dispatcher.BeginInvoke(new Action(()=> {
                    brightnessBusy=false; if(disposed || t.Status!=TaskStatus.RanToCompletion) return;
                    if(DateTime.UtcNow<commitBrightnessUntil || brightnessWriting) return;
                    int next=t.Result;
                    if(Brightness>=0 && next>=0 && next!=Brightness && (Quick==null || !Quick.IsVisible)) ShowHud("brightness",next,false);
                    Brightness=next;
                })));
            }
            if(Desktop && now>=appsAt && !runningBusy) {
                appsAt=now.AddSeconds(5); runningBusy=true;
                BackgroundWork.Run(()=> {var watch=Stopwatch.StartNew();var running=Catalog.Running();return new {Running=running,Elapsed=watch.Elapsed.TotalMilliseconds};}).ContinueWith(t=>Application.Current.Dispatcher.BeginInvoke(new Action(()=> {
                    runningBusy=false; if(disposed || t.Status!=TaskStatus.RanToCompletion) return;
                    string old=String.Join("|",DockApps().Select(a=>a.Key+":"+String.Join(",",a.Windows)));Running=t.Result.Running;lastAppRefreshMs=t.Result.Elapsed;
                    string fresh=String.Join("|",DockApps().Select(a=>a.Key+":"+String.Join(",",a.Windows)));
                    if(old!=fresh) foreach(var dock in docks) dock.Rebuild();
                })));
                Native.HideTaskbars(); DesktopLease.SuppressCharging();
            }
            if(Desktop&&now>=pinsAt&&!pinsBusy){pinsAt=now.AddSeconds(30);pinsBusy=true;var installed=Apps;var running=Running;var previousMirror=explorerItems;int revision=pinRevision;
                BackgroundWork.Run(()=> {var mirror=ExplorerBridge.ReadApps();if(mirror.Count==0)mirror=previousMirror;return new {Mirror=mirror,Pins=Catalog.ReadPins(installed,mirror,running)};}).ContinueWith(t=>Application.Current.Dispatcher.BeginInvoke(new Action(()=> {
                    pinsBusy=false;if(disposed||t.Status!=TaskStatus.RanToCompletion)return;
                    if(revision!=pinRevision){pinsAt=DateTime.MinValue;return;}
                    var old=String.Join("|",Pins.Select(a=>a.Key));Pins=t.Result.Pins;explorerItems=t.Result.Mirror;ExplorerBridge.Save(explorerItems);
                    if(old!=String.Join("|",Pins.Select(a=>a.Key)))foreach(var dock in docks)dock.Rebuild();
                })));}
            ++iteration;if(now>=liveStateAt){liveStateAt=now.AddSeconds(30);AppLog.Json("live-state.json",new {time=DateTimeOffset.Now.ToString("o"),sequence=iteration,pulseMilliseconds=cadence,desktop=Desktop,battery=Power.Level,connected=Power.Connected,volume=Audio.Value,muted=Audio.Muted,brightness=Brightness,displays=docks.Count,apps=Apps.Count,pinned=Pins.Count,running=Running.Count,appRefreshMs=Math.Round(lastAppRefreshMs,1),wifi=Services.WifiOn,bluetooth=Services.BluetoothOn,media=Services.Media.Title,output=Services.OutputName,wifiSignal=WifiSignal,packaged=Notifications.Packaged,notificationsAllowed=Notifications.Allowed,cachedIcons=AppIcons.CachedCount,ecoWorkers=BackgroundWork.EcoWorkers,backgroundJobs=BackgroundWork.Completed,mediaMetadataReads=Services.MetadataReads,notificationSnapshotReads=Notifications.SnapshotReads,notificationCount=Notifications.Items.Count,inspectable=Program.Inspectable,quick=Quick!=null?Native.RegionInfo(Quick):null,center=Center!=null?Native.RegionInfo(Center):null,docks=docks.Select(d=>d.Inspect()).ToArray()});}
        }
        internal static double MonitorScale(Forms.Screen screen,Window window)
        {
            var h=new WindowInteropHelper(window).EnsureHandle(); Native.SetWindowPos(h,IntPtr.Zero,screen.Bounds.Left+12,screen.Bounds.Top+12,0,0,0x1|0x4|0x10);
            return Math.Max(96,Native.GetDpiForWindow(h))/96.0;
        }
        internal void ShowHud(string kind,double value,bool muted)
        {
            if(!Desktop&&!Demo)return;
            HudUpdates++;HudKind=kind;hudChangedAt=DateTime.UtcNow;
            if(hud==null)hud=new HudWindow();
            var screen=Forms.Screen.FromPoint(Forms.Cursor.Position);
            bool position=!hud.IsVisible||hudScreen!=screen.DeviceName;
            if(hudScreen!=screen.DeviceName){new WindowInteropHelper(hud).EnsureHandle();Native.SetWindowPos(new WindowInteropHelper(hud).Handle,IntPtr.Zero,screen.Bounds.Left+20,screen.Bounds.Top+20,0,0,0x1|0x4|0x10);hudScale=Math.Max(96,Native.GetDpiForWindow(new WindowInteropHelper(hud).Handle))/96.0;hudScreen=screen.DeviceName;}
            double scale=hudScale,bottom=(screen.Bounds.Bottom-screen.WorkingArea.Bottom)/scale;
            hud.Set(kind,value,muted);if(position)hud.ShowAt(screen.Bounds,(screen.Bounds.Width/scale-286)/2,screen.Bounds.Height/scale-Math.Max(bottom,64)-82,286,62);
            hudUntil=DateTime.UtcNow.AddSeconds(1.5);
        }
        bool ReplaceNativeOsd()
        {
            Audio.Read();if(previousVolume>=0&&(Math.Abs(Audio.Value-previousVolume)>=1||Audio.Muted!=previousMuted)){previousVolume=Audio.Value;previousMuted=Audio.Muted;ShowHud("volume",Audio.Value,Audio.Muted);return true;}
            if(HudVisible&&HudKind=="volume"&&(DateTime.UtcNow-hudChangedAt).TotalMilliseconds<350)return true;
            if(Brightness<0)return false;if(osdBrightnessBusy)return true;osdBrightnessBusy=true;
            Task.Run(()=>BrightnessService.Read()).ContinueWith(t=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>{osdBrightnessBusy=false;if(disposed||t.Status!=TaskStatus.RanToCompletion||t.Result<0)return;Brightness=t.Result;if(Quick==null||!Quick.IsVisible)ShowHud("brightness",Brightness,false);})));return true;
        }
        internal void PreviewCharge(bool preview=true)
        {
            if(!Power.Known)return;
            foreach(var charge in charges) charge.Close(); charges.Clear();
            foreach(var dock in docks)charges.Add(new BatteryAnimation(dock.Battery,preview));
            if(docks.Count==0&&Welcome!=null){Welcome.PreviewBattery.Update(Power);charges.Add(new BatteryAnimation(Welcome.PreviewBattery,preview));}
        }
        internal BatteryPill ProbeBattery {get{return docks.Count>0?docks[0].Battery:null;}}
        internal TrayPlacement ProbeTrayAnchor {get{return docks.Count>0?docks[0].TrayAnchor():null;}}
        async void ProbeNotifications()
        {
            string tag="RouteProbe",group="OneTaskBarQA",probeTitle="One TaskBar · 알림 연결 확인";bool temporary=false;
            try {
                await Notifications.Refresh();
                if(Notifications.Allowed&&Notifications.Items.Count==0){
                    var xml=new Windows.Data.Xml.Dom.XmlDocument();xml.LoadXml("<toast activationType='protocol' launch='ms-settings:about'><visual><binding template='ToastGeneric'><text>"+probeTitle+"</text><text>자동 검사 중 생성된 임시 알림입니다.</text></binding></visual></toast>");
                    var toast=new Windows.UI.Notifications.ToastNotification(xml){Tag=tag,Group=group,SuppressPopup=true,ExpirationTime=DateTimeOffset.Now.AddMinutes(2)};
                    Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier().Show(toast);temporary=true;
                    for(int i=0;i<10&&!Notifications.Items.Any(n=>n.Title==probeTitle);i++){await Task.Delay(150);await Notifications.Refresh();}
                }
                var item=Notifications.Items.FirstOrDefault();var route=item!=null?await Task.Run(()=>NotificationActivation.Open(item,true)):null;
                AppLog.Json("notification-route-probe.json",new {time=DateTimeOffset.Now.ToString("o"),temporary=temporary,allowed=Notifications.Allowed,count=Notifications.Items.Count,icons=Notifications.Items.Count(n=>n.Icon!=null),route=route});
            }catch(Exception e){AppLog.Json("notification-route-probe.json",new {error=e.Message});}
            finally {if(temporary){try{Windows.UI.Notifications.ToastNotificationManager.History.Remove(tag,group);}catch{}}}
            if(temporary)await Notifications.Refresh();
        }
        async void ProbePanels()
        {
            var dock=docks.FirstOrDefault();if(dock==null)return;if(Quick==null)Quick=new QuickWindow(this);if(Center==null)Center=new NotificationWindow(this);bool wasOpen=Quick.IsVisible,wasEditing=Quick.Editing,wasExpanded=Quick.Expanded;ContextMenu menu=null;DetailWindow detail=null;
            try {
                if(!wasOpen)ToggleQuick(dock,true);await Task.Delay(220);Quick.Refresh(Power,Brightness,Audio.Value,Audio.Muted,Audio.Available,Brightness>=0);
                Program.Save(Quick.Surface,380,650,Path.Combine(AppLog.Data,"quick-panel-preview.png"));
                Quick.EditLayout(false);Quick.SetExpanded(true);await Task.Delay(380);bool expandedFits=Quick.LayoutFits();var expandedLayout=Quick.LayoutSnapshot();Quick.SetExpanded(false);await Task.Delay(380);var foldedLayout=Quick.LayoutSnapshot();
                var audioStates=new List<object>();foreach(double level in new[]{0,18,75}){Quick.Refresh(Power,Brightness,level,false,true,Brightness>=0);audioStates.Add(new {level=level,icons=Quick.AudioSnapshot()});}Quick.Refresh(Power,Brightness,Audio.Value,Audio.Muted,Audio.Available,Brightness>=0);
                var closeWatch=Stopwatch.StartNew();bool closed=await Quick.DismissAsync();bool closePassed=closed&&!Quick.IsVisible;long closeMilliseconds=closeWatch.ElapsedMilliseconds;ToggleQuick(dock,true);await Task.Delay(380);
                Quick.EditLayout(true);await Task.Delay(220);bool editFits=Quick.LayoutFits();var editingLayout=Quick.LayoutSnapshot();Program.Save(Quick.Surface,380,650,Path.Combine(AppLog.Data,"quick-edit-preview.png"));Quick.EditLayout(wasEditing);await Task.Delay(220);
                menu=new ContextMenu {PlacementTarget=Quick.Surface,Placement=System.Windows.Controls.Primitives.PlacementMode.Center};menu.Items.Add(new MenuItem {Header="유리 메뉴 확인"});menu.IsOpen=true;await Task.Delay(300);bool menuBlur=MenuTheme.HasBackdrop(menu);menu.IsOpen=false;await Task.Delay(450);bool menuReleased=!MenuTheme.HasBackdrop(menu);
                detail=new DetailWindow(this,"power");detail.ShowAt(dock.Screen.Bounds,24,24,380,540);await Task.Delay(300);Program.Save(detail.Surface,380,540,Path.Combine(AppLog.Data,"power-panel-preview.png"));
                var samsung=SamsungSettings();var devices=SamsungDevices();AppLog.Json("panel-probe.json",new {time=DateTimeOffset.Now.ToString("o"),quickBlur=Quick.HasBlur,detailBlur=detail.HasBlur,sharedGray=Quick.Surface.Background==QuickPanel.Glass&&detail.Surface.Background==QuickPanel.Glass&&Center.Surface.Background==QuickPanel.Glass,menuBlur=menuBlur,menuReleased=menuReleased,samsungIcon=Quick.SamsungIconLoaded,samsungApp=samsung!=null?samsung.AppID:null,devicesApp=devices!=null?devices.AppID:null,outlineWidth=Quick.Surface.BorderThickness.Left,closePassed=closePassed,closeMilliseconds=closeMilliseconds,expandedFits=expandedFits,expandedLayout=expandedLayout,foldedLayout=foldedLayout,audioStates=audioStates.ToArray(),editFits=editFits,editingLayout=editingLayout,layout=Quick.LayoutSnapshot(),quick=Native.RegionInfo(Quick),detail=Native.RegionInfo(detail)});
            }catch(Exception e){AppLog.Json("panel-probe.json",new {error=e.ToString()});}
            finally {Quick.SetExpanded(wasExpanded);Quick.EditLayout(wasEditing);if(menu!=null)menu.IsOpen=false;if(detail!=null)detail.Close();if(!wasOpen)Quick.Hide();}
        }
        void Try(Action action) { try { action(); } catch(Exception e) { AppLog.Write("action",e.Message); } }
        public void Dispose()
        {
            if(disposed) return; disposed=true;
            Desktop=false;
            TaskbarMask.Active=false;
            timer.Stop(); SystemEvents.DisplaySettingsChanged-=DisplayChanged; SystemEvents.PowerModeChanged-=PowerChanged;
            Previews.Dispose();if(brightnessEvents!=null)brightnessEvents.Dispose();
            if(keys!=null) keys.Dispose();
            if(osdGate!=null)osdGate.Dispose();
            Toggles.Dispose();Services.Dispose();Notifications.Dispose();
            if(Finder!=null)Finder.Close();
            if(NotificationLink!=null)NotificationLink.Close();
            if(shellEvents!=null)shellEvents.Dispose();
            Preferences.Changed-=OptionsChanged;
            LayoutOrder.Changed-=LayoutChanged;DragReorder.Finished-=DragFinished;
            foreach(var dock in docks) dock.Close(); docks.Clear();
            foreach(var reservation in reservations) reservation.Dispose(); reservations.Clear();
            foreach(var c in charges) c.Close(); charges.Clear();
            if(lease!=null) DesktopLease.Recover(lease.Session); lease=null;
            if(Quick!=null)Quick.Close();if(Drawer!=null)Drawer.Close();if(hud!=null)hud.Close();if(Center!=null)Center.Close();if(Detail!=null)Detail.Close();if(Welcome!=null)Welcome.Close();
            if(tray!=null) { tray.Visible=false; tray.Dispose(); }
            foreach(var wait in waits) wait.Unregister(null);
            if(powerNotify!=IntPtr.Zero) Native.UnregisterPowerSettingNotification(powerNotify);
            if(brightnessNotify!=IntPtr.Zero)Native.UnregisterPowerSettingNotification(brightnessNotify);
            if(displayNotify!=IntPtr.Zero)Native.UnregisterPowerSettingNotification(displayNotify);
            if(messages!=null) { Native.UnregisterHotKey(messages.Handle,1); Native.UnregisterHotKey(messages.Handle,2); messages.Dispose(); }
            if(stop!=null) stop.Dispose(); if(show!=null) show.Dispose(); Audio.Dispose();
            if(notificationProbe!=null)notificationProbe.Dispose();
            if(connectNotifications!=null)connectNotifications.Dispose();
            if(panelProbe!=null)panelProbe.Dispose();
        }
    }
}
