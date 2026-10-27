using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class AppLog
    {
        internal static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
        internal static readonly string Data=Environment.GetEnvironmentVariable("ONE_TASKBAR_DATA")??InstalledData();
        static string InstalledData(){string sibling=Path.GetFullPath(Path.Combine(Root,"..","Data"));return String.Equals(Path.GetFileName(Root.TrimEnd(Path.DirectorySeparatorChar)),"App",StringComparison.OrdinalIgnoreCase)&&File.Exists(Path.Combine(sibling,"installation.json"))?sibling:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OneTaskBar","Data");}
        static readonly object gate=new object(); static readonly HashSet<string> once=new HashSet<string>();
        internal static void Write(string area,string message) { try { lock(gate) { Directory.CreateDirectory(Data); File.AppendAllText(Path.Combine(Data,"app.log"),DateTimeOffset.Now.ToString("o")+" ["+area+"] "+message+Environment.NewLine); } } catch { } }
        internal static void Once(string area,string message) { lock(gate) { if(!once.Add(area)) return; } Write(area,message); }
        internal static void Json(string file,object value) { Directory.CreateDirectory(Data); File.WriteAllText(Path.Combine(Data,file),new JavaScriptSerializer().Serialize(value)); }
    }
    internal static class Program
    {
        internal const string AppName="One TaskBar";
        internal static bool Inspectable;
        internal static bool CaptureReview;
        [STAThread] static int Main(string[] args){int code=Run(args);Environment.Exit(code);return code;}
        static int Run(string[] args)
        {
            StartupTrace.Mark("entry");
            Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
            CaptureReview=args.Contains("--review-ui");Inspectable=CaptureReview||args.Contains("--qa")||File.Exists(Path.Combine(AppLog.Data,"ui-inspection.flag"));
            if(args.Length>0 && args[0]=="--guard") { Guard(args); return 0; }
            if(args.Contains("--stop")) { Signal("Stop"); return 0; }
            if(args.Length==2&&args[0]=="--open-panel"&&new[]{"notifications","quick","start","search","desktops"}.Contains(args[1])){Signal("Panel-"+args[1]);return 0;}
            if(args.Length==2&&args[0]=="--review-edge"&&new[]{"0","1","2","3"}.Contains(args[1])){Signal("Panel-edge"+args[1]);return 0;}
            if(args.Length==2&&args[0]=="--review-background"&&new[]{"0","1","2","3"}.Contains(args[1])){Signal("Panel-background"+args[1]);return 0;}
            if(args.Length==3&&args[0]=="--register-notifications")return NotificationIdentity.RegisterHelper(args[1],args[2]);
            if(args.Length>0&&args[0]=="--notification-restart")return NotificationIdentity.RestartHelper(args);
            if(args.Contains("--notification-probe")) { Signal("ProbeNotifications"); return 0; }
            if(args.Contains("--notification-banner-probe")) { Signal("ProbeBanners"); return 0; }
            if(args.Contains("--notification-action-probe")) { Signal("ProbeNotificationAction"); return 0; }
            if(args.Contains("--phone-companion-probe")){var phone=PhoneCompanion.Read();int reads=PhoneCompanion.Reads,decodes=PhoneCompanion.Decodes;var again=PhoneCompanion.Read();bool cached=Object.ReferenceEquals(phone,again)&&reads==PhoneCompanion.Reads&&decodes==PhoneCompanion.Decodes;bool passed=phone.Available&&cached&&phone.Actions.ContainsKey("open");AppLog.Json("phone-companion-probe.json",new {passed=passed,available=phone.Available,connectionPresent=!String.IsNullOrEmpty(phone.Status),batteryPresent=!String.IsNullOrEmpty(phone.Battery),phoneImage=phone.Image!=null,genericActions=phone.Actions.Count,unchangedMetadataAndArtworkReused=cached,backgroundPolling=false,phoneCommandsExecuted=false});return passed?0:1;}
            if(args.Contains("--panel-probe")) { Signal("ProbePanels"); return 0; }
            if(args.Contains("--hud-probe")) { Signal("ProbeHud"); return 0; }
            if(args.Contains("--native-panels-probe")) {
                TaskbarMask.Active=TaskbarMask.Protected(Native.FindWindow("Shell_TrayWnd",null));
                var results=new List<object>();bool passed=true;
                foreach(bool notifications in new[]{false,true}){bool opened=false;try {if(ExplorerBridge.SystemPanelVisible(notifications)){ExplorerBridge.OpenSystemPanel(notifications);Thread.Sleep(300);}bool invoked=ExplorerBridge.OpenSystemPanel(notifications);Thread.Sleep(700);opened=ExplorerBridge.SystemPanelVisible(notifications);results.Add(new {notifications=notifications,invoked=invoked,visible=opened});passed=passed&&invoked&&opened;}finally {if(opened){ExplorerBridge.OpenSystemPanel(notifications);Thread.Sleep(250);}}}
                AppLog.Json("native-panels-probe.json",new {passed=passed,results=results.ToArray()});return passed?0:1;
            }
            if(args.Contains("--theme-probe")) {
                try {var before=WindowsTheme.Read(true);if(!before.Available)return 1;WindowsTheme.Apply(before.AppsLight,before.SystemLight);var after=WindowsTheme.Read(true);bool same=after.Available&&before.AppsLight==after.AppsLight&&before.SystemLight==after.SystemLight;AppLog.Json("theme-probe.json",new {passed=same,appsLight=after.AppsLight,systemLight=after.SystemLight});return same?0:1;}
                catch(Exception error){AppLog.Json("theme-probe.json",new {passed=false,error=error.Message});return 1;}
            }
            if(args.Contains("--power-probe")) {var before=Native.ReadPerformanceMode();if(!before.Available)return 1;try {Native.SetPerformanceMode(before.Selected);var after=Native.ReadPerformanceMode();bool same=after.Available&&before.Selected==after.Selected;AppLog.Json("power-mode-probe.json",new {passed=same,before=before,after=after});return same?0:1;}catch(Exception e){AppLog.Json("power-mode-probe.json",new {passed=false,error=e.Message,before=before});return 1;}}
            if(args.Contains("--launcher-probe"))return LauncherChecks.Probe();
            if(args.Contains("--launcher-keyboard-probe"))return LauncherInputChecks.Run();
            if(args.Contains("--admin-targets-probe"))return AdminLaunch.Probe();
            if(args.Contains("--window-drop-probe"))return WindowDropChecks.Run();
            if(args.Contains("--quick9-probe"))return QuickWidgetChecks.Run();
            if(args.Contains("--glass95-probe"))return Glass95Checks.Run(args.Contains("--inspect"));
            if(args.Contains("--beta-feedback-probe"))return BetaFeedbackChecks.Run();
            if(args.Contains("--panel-theme-probe"))return PanelThemeChecks.Run();
            if(args.Contains("--media9-probe"))return MediaModernChecks.Run();
            if(args.Contains("--launcher-motion-probe"))return LauncherMotionChecks.Run();
            if(args.Contains("--deferred-icon-probe"))return DeferredIconChecks.Run();
            if(args.Contains("--settings-visibility-probe"))return SettingsVisibilityChecks.Run();
            if(args.Contains("--window-switcher-probe"))return WindowSwitcherChecks.Run(args.Contains("--inspect"));
            if(args.Contains("--desktop-overview-probe"))return DesktopOverviewChecks.Run(args.Contains("--inspect"));
            if(args.Contains("--window-activation-probe"))return WindowActivationChecks.Run();
            if(args.Contains("--startup-probe"))return StartupChecks.Run();
            if(args.Contains("--startup-registration-probe"))return LogonStartup.ProbeRegistration();
            if(args.Length==2&&args[0]=="--configure-startup"&&(args[1]=="on"||args[1]=="off")){try{LogonStartup.Set(args[1]=="on");return 0;}catch(Exception error){AppLog.Write("startup-configure",error.ToString());return 1;}}
            if(args.Length==2&&args[0]=="--window-focus-source")return WindowActivationChecks.Source(args[1]);
            if(args.Contains("--start-size-probe"))return StartSizeChecks.Run();
            if(args.Contains("--app-metadata-live"))return AppMetadataChecks.Live();
            if(args.Contains("--feature-857-probe"))return Feature857Checks.Run();
            if(args.Contains("--feature-857-live"))return Feature857Checks.Live();
            if(args.Contains("--desktop-behavior-probe"))return DesktopBehaviorChecks.Run();
            if(args.Contains("--taskbar-state-probe"))return DesktopBehaviorChecks.NativeState();
            if(args.Contains("--taskbar-logo-probe"))return TaskbarLogoChecks.Probe();
            if(args.Contains("--app-screen-probe"))return AppScreenChecks.Run();
            if(args.Contains("--notification-assets-probe"))return NotificationAssetChecks.Run();
            if(args.Contains("--notification-stack-probe"))return NotificationStackChecks.Run();
            if(args.Contains("--wireless-scan-probe"))return WirelessScanChecks.Run();
            if(args.Contains("--wireless-live-probe"))return WirelessScanChecks.Live();
            if(args.Contains("--quick-size-probe"))return QuickSizeChecks.Run(args.Contains("--qa"));
            if(args.Contains("--samsung-mode-probe"))return SamsungPerformanceChecks.Probe();
            if(args.Contains("--compact-dock-probe"))return CompactDockChecks.Run();
            if(args.Contains("--network-route-probe"))return NetworkRouteInfo.Probe();
            if(args.Contains("--memory-lifecycle-probe"))return MemoryChecks.Run();
            if(args.Contains("--media-source-probe"))return MediaArtwork.Probe();
            if(args.Contains("--artwork-resolver-probe"))return YouTubeArtworkChecks.Run();
            if(args.Contains("--start-menu-probe"))return StartChecks.Run();
            if(args.Contains("--start-key-probe"))return StartKeyChecks.Run();
            if(args.Contains("--notification-ui-probe"))return BannerChecks.Run();
            if(args.Contains("--ui-probe"))return UiChecks.Run(args.Contains("--optimized")?"optimized":"baseline");
            if(args.Contains("--reference-probe"))return ReferenceChecks.Run();
            if(args.Contains("--osd-probe"))return OsdChecks.Run();
            if(args.Contains("--power-menu-probe"))return SystemPower.Probe();
            if(args.Contains("--quick-feature-probe"))return QuickFeatureChecks.Run();
            if(args.Contains("--nowbar-probe"))return NowBarChecks.Run();
            if(args.Contains("--nowbar9-probe"))return NowBarChecks.Run(true);
            if(args.Contains("--media-activation-probe"))return MediaActivationChecks.Run();
            if(args.Contains("--energy-probe"))return EnergyChecks.Run();
            if(args.Contains("--panel-transition-probe"))return PanelTransitionChecks.Run();
            if(args.Contains("--window-preview-probe"))return WindowPreviewChecks.Run(args.Contains("--inspect"));
            if(args.Contains("--popup-probe"))return PopupChecks.Run();
            if(args.Contains("--identity-package-probe"))return IdentityPackage.Probe();
            if(args.Contains("--dock-motion-probe"))return DockMotionChecks.Run();
            if(args.Contains("--dock-overflow-probe"))return DockOverflowChecks.Run();
            if(args.Contains("--osd-mask-fixture")){var mask=new NativeOsdGate(()=>true,()=>false,()=>true);mask.Refresh();AppLog.Json("osd-mask-fixture.json",new {prepared=mask.Prepared,windows=mask.MaskedWindows.Select(h=>h.ToInt64()).ToArray()});return mask.Prepared>0?0:1;}
            if(args.Contains("--osd-recovery-probe"))return OsdChecks.Recovery();
            if(args.Contains("--native-start-probe")){var watch=Stopwatch.StartNew();bool sent=SystemPanels.Start();Thread.Sleep(900);bool visible=LauncherChecks.StartVisible();AppLog.Json("native-start-probe.json",new {passed=sent&&visible,sent=sent,nativeStart=visible,foreground=ReferenceChecks.FocusName(),milliseconds=watch.ElapsedMilliseconds});if(visible)SystemPanels.Start();else SystemPanels.CloseSearchProbe();return sent&&visible?0:1;}
            if(args.Contains("--recovery-probe"))return RecoveryChecks.Run();
            if(args.Contains("--desktop-recovery-probe"))return RecoveryChecks.Desktop();
            if(args.Contains("--typing-probe"))return RecoveryChecks.Typing();
            if(args.Contains("--native-search-probe"))return RecoveryChecks.NativeSearch();
            if(args.Contains("--launcher-input-probe"))return LauncherChecks.InputProbe();
            if(args.Contains("--self-test")) return SelfTest();
            if(args.Contains("--update950-probe"))return Update950Checks.Run();
            if(args.Contains("--workspace-probe"))return WorkspaceChecks.Run();
            if(args.Contains("--diagnose")) return Diagnose();
            if(args.Contains("--desktop-probe")) return DesktopProbe();
            if(args.Contains("--tray-probe")) return DesktopProbe(true);
            if(args.Contains("--recover")) {int index=Array.IndexOf(args,"--session");DesktopLease.Recover(index>=0&&index+1<args.Length?args[index+1]:null);NativeOsdGate.Recover();NativeToastGate.Recover();NativeNotificationRoute.Recover();return 0;}
            if(args.Contains("--guard-fixture")){Thread.Sleep(Timeout.Infinite);return 0;}
            if(args.Contains("--render")) return Render(args);
            if(args.Contains("--logon")&&DesktopInstanceRunning())return 0;
            if(NotificationIdentity.Bootstrap(args))return 0;
            bool created;
            using(var mutex=new Mutex(true,@"Local\OneTaskBar.Desktop",out created)) {
                if(!created) { if(!args.Contains("--logon"))Signal(args.Contains("--connect-notifications")?"ConnectNotifications":"Show"); return 0; }
                ShellController shell=null;
                try {
                    var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                    app.DispatcherUnhandledException+=(s,e)=> { AppLog.Write("ui-error",e.Exception.ToString()); Native.RestoreTaskbars(); if(shell!=null) shell.Dispose(); app.Shutdown(1); e.Handled=true; };
                    shell=new ShellController();
                    app.Exit+=(s,e)=>shell.Dispose();
                    bool packaged=NotificationIdentity.IsCurrent;
                    shell.Start(args.Contains("--desktop")||packaged&&Preferences.Current.DesktopMode&&!args.Contains("--settings"));
                    if(args.Contains("--connect-notifications"))app.Dispatcher.BeginInvoke(new Action(()=>shell.ShowNotificationConnection(true)));
                    int exitCode=app.Run();Environment.Exit(exitCode);return exitCode;
                } catch(Exception e) { AppLog.Write("fatal",e.ToString()); if(shell!=null) shell.Dispose(); Native.RestoreTaskbars(); return 1; }
            }
        }
        internal static void Signal(string name) { try { using(var handle=EventWaitHandle.OpenExisting(@"Local\OneTaskBar."+name)) handle.Set(); } catch(WaitHandleCannotBeOpenedException) { } }
        static bool DesktopInstanceRunning()
        {
            try{using(var mutex=Mutex.OpenExisting(@"Local\OneTaskBar.Desktop")){bool acquired=false;try{try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}return !acquired;}finally{if(acquired)mutex.ReleaseMutex();}}}catch(WaitHandleCannotBeOpenedException){return false;}catch(UnauthorizedAccessException){return false;}
        }
        static void Guard(string[] args)
        {
            int pid; long started;
            if(args.Length<3 || !Int32.TryParse(args[1],out pid) || !Int64.TryParse(args[2],out started)) return;
            try { using(var p=Process.GetProcessById(pid)) { if(p.StartTime.ToUniversalTime().Ticks!=started) return; p.WaitForExit(); } } catch(ArgumentException) { }
            // Restore Explorer even after Task Manager termination or a native crash.
            DesktopLease.Recover(args.Length>3?args[3]:null);
        }
        static int SelfTest()
        {
            var checks=new List<string>();
            try {
                Update950Checks.Validate(checks);WindowSwitchGesture.Check();checks.Add("alt-tab-shortcuts-window-filter-monitor-layout");StartMenuLayout.Check();checks.Add("responsive-start-work-area");MediaWave.Check();checks.Add("bounded-media-waves");BatteryPill.Check();checks.Add("low-battery-red-excludes-charging");
                EfficiencyChecks.Validate(checks);RuntimeCadence.Check(checks);RuntimeDockOrder.Check();checks.Add("stable-launch-order");LauncherLayout.Check();checks.Add("compact-launcher-layout");NowBarLayout.Check();checks.Add("nowbar-monitor-layout");ArtworkPalette.Check();checks.Add("artwork-color-contrast");MediaArtwork.Check(checks);StartDataChecks.Validate(checks);NotificationAssetChecks.Validate(checks);NotificationStackChecks.Validate(checks);QuickSizeChecks.Validate(checks);SamsungPerformanceChecks.Validate(checks);NetworkRouteInfo.Validate(checks);LauncherInputChecks.Validate(checks);DesktopBehaviorChecks.Validate(checks);TaskbarLogoChecks.Validate(checks);Feature856Checks.Validate(checks);Feature857Checks.Validate(checks);QuickWidgetChecks.Validate(checks);AppMetadataChecks.Validate(checks);Glass95Checks.Validate(checks);BetaFeedbackChecks.Validate(checks);
                foreach(double width in new double[] { 1,200,288,344,600 }) foreach(double value in new double[] { -20,0,1,33,99,100,120 }) {
                    double f=PillSlider.FillWidth(width,value);
                    if(f<0 || f>width || (value==100 && f!=width)) throw new Exception("Slider endpoint "+width+" "+value);
                } checks.Add("Slider fill has exact 0/100 endpoints, clamped inputs and responsive widths");
                foreach(double width in new[]{20.0,90,260,600})foreach(double level in new[]{0.0,1,3,9.99,10,50,100}) {var cap=PillSlider.CapsuleFillBounds(width,44,level);if(cap.Width>width||cap.Height>44||level==0&&cap.Width!=0||level<10&&cap.Width!=cap.Height||level==100&&cap.Width!=width)throw new Exception("Slider low-value morph bounds");}checks.Add("Slider shape shrinks in both axes below ten and empties at zero; semantic values remain 0–100");
                if(PillSlider.Clamp(Double.NaN)!=0 || PillSlider.Clamp(Double.PositiveInfinity)!=0) throw new Exception("Invalid scalar accepted");
                if(!PowerState.From(true,1,8,70).Connected || !PowerState.From(true,1,8,70).Charging) throw new Exception("Charging flag");
                if(!PowerState.From(true,1,1,100).Connected || PowerState.From(true,1,1,100).Charging) throw new Exception("Full plugged state");
                if(PowerState.From(true,0,8,55).Charging || PowerState.From(true,1,128,100).Known) throw new Exception("Power classification");
                checks.Add("Lightning appears for any connected AC state, including charge protection/full battery");
                var animation=new ChargeState();
                if(animation.Observe(false) || !animation.Observe(true) || animation.Observe(true) || animation.Observe(false) || !animation.Observe(true)) throw new Exception("Connection edge replay");
                var startup=new ChargeState(); if(startup.Observe(true)) throw new Exception("Startup must not replay a fake cable connection");
                checks.Add("One animation per connection; no startup animation and no dropped reconnect edge");
                foreach(double logicalWidth in new double[]{480,640,800,1280,1920}) foreach(double scale in new double[] {1,1.25,1.5,2,2.5,3}) foreach(int x in new int[]{-3840,0,2880}) {
                    var area=new System.Drawing.Rectangle(x,-1080,(int)(logicalWidth*scale),(int)(800*scale));
                    var layout=DockLayout.Compute(area,scale,14);
                    if(layout.StartX<0 || layout.AppX<0 || layout.StatusX+layout.StatusWidth>logicalWidth || layout.AppX+layout.AppWidth+12>layout.StatusX) throw new Exception("Dock collision at "+logicalWidth);
                } checks.Add("Dock remains inside each monitor and clear of status capsule at mixed DPI/negative origins");
                foreach(double scale in new[]{1,1.25,1.5,2,2.5,3})foreach(int origin in new[]{-3840,0,2880})foreach(double horizontal in new[]{.01,.5,.99}) {
                    var monitor=new System.Drawing.Rectangle(origin,-1080,(int)(1440*scale),(int)(900*scale));
                    var anchor=new Point(monitor.Left+monitor.Width*horizontal,monitor.Bottom-56*scale);
                    var placement=new TrayPlacement(anchor,monitor,scale);int w=(int)(220*scale),h=(int)(260*scale);
                    var target=placement.Target(w,h);
                    if(target.X<monitor.Left||target.X+w>monitor.Right||target.Y<monitor.Top||target.Y+h>anchor.Y-7*scale)throw new Exception("Tray anchor outside monitor/dock at "+scale);
                    if(horizontal==.5&&Math.Abs(target.X+w/2-placement.Anchor.X)>1)throw new Exception("Tray not centered over button");
                }checks.Add("Native tray anchors above its button and clamps to mixed-DPI monitors including negative origins");
                var pin=new AppEntry {Name="native shortcut",Target="app.lnk",Executable=@"C:\test\app.exe"};
                var running=new AppEntry {Name="real window",Target=@"C:\test\app.exe",Executable=@"C:\test\app.exe"};
                if(Catalog.Merge(new[]{pin},new[]{running}).Count!=1)throw new Exception("Shortcut/executable duplicate");
                if(Catalog.Same(new AppEntry {AppID="app!one",Executable="host.exe"},new AppEntry {AppID="app!two",Executable="host.exe"}))throw new Exception("Packaged-app identity collision");
                if(Catalog.Merge(new[]{pin,pin},new[]{running,running}).Count!=1)throw new Exception("Repeated pin/window duplicate");
                checks.Add("Native shortcuts and running executables merge once; packaged apps retain distinct identities");
                var pixels=new byte[16*16*4];for(int y=5;y<11;y++)for(int x=5;x<11;x++){int i=(y*16+x)*4;pixels[i]=40;pixels[i+1]=80;pixels[i+2]=150;pixels[i+3]=255;}
                var padded=BitmapSource.Create(16,16,96,96,PixelFormats.Bgra32,null,pixels,64);padded.Freeze();var trimmed=AppIcons.Normalize(padded);
                if(trimmed.PixelWidth!=8||trimmed.PixelHeight!=8)throw new Exception("Icon transparent padding retained or artwork cropped");
                var center=new byte[4];trimmed.CopyPixels(new Int32Rect(1,1,1,1),center,4,0);if(center[2]!=150||center[3]!=255)throw new Exception("Native icon colors changed");
                checks.Add("Transparent icon padding is removed without recoloring original artwork");
                var serializer=new JavaScriptSerializer();var options=serializer.Deserialize<PreferenceData>("{\"Seconds\":false,\"ChargeAnimation\":false}");
                var restored=serializer.Deserialize<PreferenceData>(serializer.Serialize(options));
                if(restored.Seconds||restored.ChargeAnimation||!restored.Blur||!restored.Animations||!restored.Hover)throw new Exception("Settings round trip/default migration");
                checks.Add("Toggle settings preserve false values and keep defaults for newly added options");
                if(restored.NativeNotifications||restored.NativeQuickSettings)throw new Exception("Old settings must keep custom panels");
                var panelOptions=serializer.Deserialize<PreferenceData>(serializer.Serialize(new PreferenceData {NativeNotifications=true,NativeQuickSettings=false}));
                if(!panelOptions.NativeNotifications||panelOptions.NativeQuickSettings)throw new Exception("Panel choices must persist independently");
                checks.Add("Native/custom panel choices persist independently and migrate existing users to their current custom panels");
                LauncherChecks.Validate(checks);
                string layoutFolder=Path.Combine(AppLog.Data,"layout-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(layoutFolder);
                try {
                    string layoutFile=Path.Combine(layoutFolder,"layout.json");var layoutStore=new LayoutStore(layoutFile);var apps=new[]{"A","B","C","D"};
                    if(!layoutStore.Move("dock.apps",apps,"D","B",false))throw new Exception("Dock reorder rejected");
                    layoutStore=new LayoutStore(layoutFile);var order=layoutStore.Arrange("dock.apps",apps,a=>a);
                    if(String.Join("",order)!="ADBC")throw new Exception("Reordered apps did not survive restart");
                    if(!layoutStore.Move("dock.apps",new[]{"A","D","C"},"C","A",false))throw new Exception("Closed app prevented reorder");
                    layoutStore=new LayoutStore(layoutFile);
                    if(String.Join("",layoutStore.Arrange("dock.apps",new[]{"a","b","c","d","E"},a=>a))!="cabdE")throw new Exception("Closed/new/case-insensitive app identities lost their positions");
                    if(layoutStore.Move("dock.apps",apps,"missing","A",false)||layoutStore.Move("dock.apps",apps,"A","A",true))throw new Exception("Invalid drop changed layout");
                    if(!layoutStore.Move("dock.after",apps,"A","D",true)||String.Join("",new LayoutStore(layoutFile).Arrange("dock.after",apps,a=>a))!="BCDA")throw new Exception("Drop at the last icon lost an app");
                    if(!layoutStore.Move("quick.sections",new[]{"brightness","volume","media"},"volume","brightness",false))throw new Exception("Quick section reorder rejected");
                    if(String.Join(",",new LayoutStore(layoutFile).Arrange("quick.sections",new[]{"brightness","volume","media"},a=>a))!="volume,brightness,media")throw new Exception("Quick section order did not persist");
                    layoutStore.Reset("quick.");layoutStore=new LayoutStore(layoutFile);
                    if(String.Join("",layoutStore.Arrange("dock.apps",apps,a=>a))!="CABD"||String.Join(",",layoutStore.Arrange("quick.sections",new[]{"brightness","volume","media"},a=>a))!="brightness,volume,media")throw new Exception("Scoped layout reset changed another panel");
                }finally {foreach(string path in Directory.GetFiles(layoutFolder))File.Delete(path);Directory.Delete(layoutFolder);}
                checks.Add("Dock and quick-setting orders survive reload; missing/new apps, case-insensitive IDs, invalid drops and scoped resets preserve all items");
                var payload=NotificationPayload.Parse("<toast activationType='protocol' launch='https://example.invalid/post?id=12&amp;view=알림'><visual><binding template='ToastGeneric'><text>알림 제목</text><text>알림 내용</text></binding></visual></toast>",new[]{"알림 제목","알림 내용"});
                if(payload==null||payload.Type!="protocol"||payload.Arguments!="https://example.invalid/post?id=12&view=알림")throw new Exception("Original notification launch arguments were changed");
                if(NotificationPayload.Parse("<toast launch='other'><visual><binding template='ToastGeneric'><text>다른 알림</text></binding></visual></toast>",new[]{"알림 제목"})!=null)throw new Exception("Unrelated notification payload matched");
                var rich=NotificationPayload.Parse("<toast launch='conversation=42'><visual><binding template='ToastGeneric'><text>메시지</text><group><subgroup><text>새로운 내용</text></subgroup></group></binding></visual></toast>",new[]{"메시지","새로운 내용"});if(rich==null||rich.Arguments!="conversation=42")throw new Exception("Adaptive notification launch payload was lost");
                checks.Add("Notification activation retains original escaped/Unicode arguments and rejects unrelated content");
                using(var audio=new AudioService()) { audio.Read(); checks.Add("Audio read: "+(audio.Available ? "available, value="+audio.Value+", muted="+audio.Muted : "no default device")); }
                checks.Add("Brightness read: "+BrightnessService.Read());
                AppLog.Json("self-test.json",new { passed=true,checks=checks.ToArray(),time=DateTimeOffset.Now.ToString("o") }); return 0;
            } catch(Exception e) { AppLog.Json("self-test.json",new { passed=false,error=e.ToString(),checks=checks.ToArray() }); return 1; }
        }
        static int Diagnose()
        {
            try {
                var services=new SystemServices();var refresh=System.Threading.Tasks.Task.Run(async()=>await services.Refresh());bool complete=refresh.Wait(12000);
                var installed=Catalog.Read();var running=Catalog.Running();var mirror=ExplorerBridge.ReadApps();var pins=Catalog.ReadPins(installed,mirror,running);var notifications=new NotificationService();notifications.Check();
                AppLog.Json("integration-check.json",new {time=DateTimeOffset.Now.ToString("o"),servicesComplete=complete,wifi=services.WifiOn,bluetooth=services.BluetoothOn,mediaAvailable=services.Media.Available,mediaTitle=services.Media.Title,
                    installed=installed.Count,systemButtons=ExplorerBridge.SystemButtons(),mirrored=mirror.Select(a=>new {id=a.Id,name=a.Name}).ToArray(),pinned=pins.Select(a=>new {name=a.Name,id=a.AppID,path=a.Executable,icon=a.Icon!=null}).ToArray(),running=running.Select(a=>new {name=a.Name,id=a.AppID,path=a.Executable,windows=a.Windows.Count,icon=a.Icon!=null}).ToArray(),
                    font=SystemFonts.MessageFontFamily.Source,packaged=notifications.Packaged,notificationsAllowed=notifications.Allowed,outputs=Native.AudioOutputs().Select(o=>new {name=o.Name,current=o.Current}).ToArray(),powerPlans=Native.PowerPlans().Select(p=>new {name=p.Name,current=p.Current}).ToArray(),
                    displays=System.Windows.Forms.Screen.AllScreens.Select(s=>new {name=s.DeviceName,bounds=s.Bounds.ToString(),workArea=s.WorkingArea.ToString()}).ToArray()});return 0;
            }catch(Exception e){AppLog.Write("diagnose",e.ToString());return 1;}
        }
        static int DesktopProbe(bool testTray=false)
        {
            ShellController shell=null;
            try {
                var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
                var before=System.Windows.Forms.Screen.AllScreens.Select(s=>Native.WorkArea(s)).ToArray();
                shell=new ShellController();shell.Start(true);
                var baseline=shell.ProbeBattery;Point? center=null;double maxScale=0,maxShift=0;int windowCount=0,maxWindows=0;
                bool trayStarted=false,trayComplete=false,traySuccess=false,trayMasked=true,trayInitiallyOpen=ExplorerBridge.TrayVisible(),trayAligned=false;
                object trayPosition=null;int alignedSamples=0;var traySamples=new List<object>();
                Func<int> countWindows=()=> {int count=0;uint own=(uint)Process.GetCurrentProcess().Id;Native.EnumWindows((h,l)=> {uint pid;Native.GetWindowThreadProcessId(h,out pid);if(pid==own)count++;return true;},IntPtr.Zero);return count;};
                var pulse=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};int samples=0;
                pulse.Tick+=(s,e)=> {samples++;if(baseline==null)return;
                    var current=baseline.PointToScreen(new Point(baseline.ActualWidth,baseline.ActualHeight/2));
                    if(samples==3){center=current;windowCount=countWindows();if(!testTray)shell.PreviewCharge();}
                    if(testTray&&samples==10){trayStarted=true;var anchor=shell.ProbeTrayAnchor;System.Threading.Tasks.Task.Run(()=> {
                        if(trayInitiallyOpen){ExplorerBridge.OpenTray();for(int i=0;i<25&&ExplorerBridge.TrayVisible();i++)Thread.Sleep(20);}
                        return ExplorerBridge.OpenTray(anchor);
                    }).ContinueWith(t=>app.Dispatcher.BeginInvoke(new Action(()=> {trayComplete=true;traySuccess=t.Status==System.Threading.Tasks.TaskStatus.RanToCompletion&&t.Result&&ExplorerBridge.TrayVisible();})));}
                    if(testTray&&trayStarted)trayMasked=trayMasked&&TaskbarMask.Protected(Native.FindWindow("Shell_TrayWnd",null));
                    if(testTray&&trayComplete&&traySuccess){bool aligned;var position=ExplorerBridge.TrayPositionInfo(out aligned);if(position!=null)trayPosition=position;if(samples>20){if(aligned)alignedSamples++;trayAligned=alignedSamples>=10&&aligned;}if(traySamples.Count<50)traySamples.Add(new {sample=samples,visible=ExplorerBridge.TrayVisible(),position=position});}
                    if(samples>=3&&center.HasValue){maxScale=Math.Max(maxScale,baseline.Morph);maxShift=Math.Max(maxShift,(current-center.Value).Length);maxWindows=Math.Max(maxWindows,countWindows());}
                };pulse.Start();
                var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromSeconds(7)};
                timer.Tick+=(s,e)=> {timer.Stop();pulse.Stop();bool effect=testTray||maxScale>.8&&Math.Abs(baseline.Morph)<.001&&maxShift<=1&&maxWindows==windowCount;
                    if(testTray&&ExplorerBridge.TrayVisible())ExplorerBridge.OpenTray();
                    var during=System.Windows.Forms.Screen.AllScreens.Select(m=>Native.WorkArea(m)).ToArray();
                    shell.Dispose();
                    var after=System.Windows.Forms.Screen.AllScreens.Select(m=>Native.WorkArea(m)).ToArray();
                    AppLog.Json("desktop-probe.json",new {before=before,during=during,after=after,pins=shell.Pins.Count,running=shell.Running.Count,brightness=shell.Brightness,wifi=shell.Services.WifiOn,bluetooth=shell.Services.BluetoothOn,media=shell.Services.Media.Available,batteryMorphPassed=testTray?(bool?)null:effect,batteryMorphMax=maxScale,batteryRightEdgeShift=maxShift,extraAnimationWindows=maxWindows-windowCount,trayTest=testTray,trayInitiallyOpen=trayInitiallyOpen,trayOpened=trayComplete&&traySuccess,taskbarStayedTransparent=trayMasked,trayPositionPassed=trayAligned,trayAlignedSamples=alignedSamples,trayPosition=trayPosition,traySamples=traySamples.ToArray(),leaseActive=File.Exists(DesktopLease.FileName)?File.ReadAllText(DesktopLease.FileName):null});app.Shutdown(effect&&(!testTray||traySuccess&&trayMasked&&trayAligned)?0:1);};timer.Start();return app.Run();
            }catch(Exception e){AppLog.Write("desktop-probe",e.ToString());return 1;}finally{if(shell!=null)shell.Dispose();}
        }
        static int Render(string[] args)
        {
            try {
                var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                var shell=new ShellController(true);
                string output=args.Length>1 ? args[1] : Path.Combine(AppLog.Root,"previews"); Directory.CreateDirectory(output);
                shell.Apps=Catalog.Read();var quick=new QuickWindow(shell);quick.Refresh(new PowerState {Known=true,Level=73,Connected=true},62,37,false,true,true);
                Save(quick.Surface,380,650,Path.Combine(output,"quick-settings.png"));
                var editingQuick=new QuickWindow(shell);editingQuick.Height=840;editingQuick.EditLayout(true);editingQuick.Refresh(new PowerState {Known=true,Level=73,Connected=true},62,37,false,true,true);Save(editingQuick.Surface,380,840,Path.Combine(output,"quick-settings-edit.png"));AppLog.Json("layout-preview.json",editingQuick.LayoutSnapshot());editingQuick.Close();
                var center=new NotificationWindow(shell);shell.Notifications.Allowed=true;shell.Notifications.Packaged=true;
                shell.Notifications.Items=new List<NotificationEntry>{new NotificationEntry {Id=1,App="One TaskBar",Title="알림 미리 보기",Body="알림을 누르면 원래 알림의 동작으로 연결됩니다.",Created=DateTimeOffset.Now,Icon=new BitmapImage(new Uri(Path.Combine(AppLog.Root,"assets","OneTaskBar.png")))}};
                center.Refresh();Save(center.Surface,394,584,Path.Combine(output,"notifications.png"));
                var settings=new WelcomeWindow(shell);Save(settings.Surface,700,650,Path.Combine(output,"settings.png"));
                var finder=new SearchWindow(shell);finder.Configure(720,680);finder.Reset();Save(finder.Surface,720,680,Path.Combine(output,"launcher-search.png"));finder.Query.Text="ChatGPT";Save(finder.Surface,720,680,Path.Combine(output,"launcher-search-results.png"));finder.Query.Text="One UI";finder.Query.Text="소리";Save(finder.Surface,800,650,Path.Combine(output,"launcher-search-settings.png"));finder.Close();
                var context=WindowPicker.Menu(new AppEntry {Name="One TaskBar"},shell);context.Visibility=Visibility.Visible;Save(context,300,124,Path.Combine(output,"context-menu.png"));
                var demoDock=new DockHost(shell,System.Windows.Forms.Screen.PrimaryScreen);var charging=new PowerState {Known=true,Connected=true,Level=28};demoDock.Status.Surface.Background=Paint.Brush("#FFE0E5E2");demoDock.Refresh(charging);
                foreach(double morph in new[]{0,.4,1.0}) {demoDock.Battery.Celebration=morph>0;demoDock.Battery.Reveal=1;demoDock.Battery.Morph=morph;demoDock.Battery.Phase=.2;demoDock.Battery.InvalidateVisual();Save(demoDock.Status.Surface,282,44,Path.Combine(output,"charging-morph-"+morph.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"));}
                demoDock.Battery.Morph=0;demoDock.Battery.Celebration=false;shell.Audio.Available=true;
                shell.Services.WifiOn=false;shell.Wifi=null;shell.Audio.Value=0;demoDock.Refresh(charging);Save(demoDock.Status.Surface,282,44,Path.Combine(output,"status-off-zero.png"));
                shell.Services.WifiOn=true;shell.Audio.Value=15;demoDock.Refresh(charging);Save(demoDock.Status.Surface,282,44,Path.Combine(output,"status-disconnected-low.png"));
                shell.Wifi="Wi-Fi";shell.Audio.Value=75;demoDock.Refresh(charging);Save(demoDock.Status.Surface,282,44,Path.Combine(output,"status-connected-volume.png"));demoDock.Close();
                var drawer=new DrawerWindow(shell);drawer.Configure(720,680);drawer.Reload();Save(drawer.Surface,720,680,Path.Combine(output,"app-drawer.png"));
                var hud=new HudWindow(); hud.Set("volume",37,false); Save(hud.Surface,286,62,Path.Combine(output,"keyboard-volume.png"));
                hud.Set("brightness",100,false); Save(hud.Surface,286,62,Path.Combine(output,"keyboard-brightness.png"));
                foreach(int level in new int[]{0,1,25,50,99,100}) {
                    var panel=new System.Windows.Controls.StackPanel { Background=Paint.Brush("#FF8E9692"),Margin=new Thickness(0) };
                    foreach(string kind in new string[]{"brightness","volume"}) panel.Children.Add(new PillSlider(kind) { Width=300,Height=44,Value=level,Margin=new Thickness(12,10,12,10) });
                    panel.Children.Add(new BatteryPill { Width=70,Height=28,State=new PowerState {Known=true,Level=level,Connected=true},Margin=new Thickness(12,10,12,10) });
                    Save(panel,324,190,Path.Combine(output,"endpoints-"+level+".png"));
                }
                shell.Dispose(); return 0;
            } catch(Exception e) { AppLog.Write("render",e.ToString()); return 1; }
        }
        internal static void Save(FrameworkElement element,int width,int height,string file)
        {
            element.Measure(new Size(width,height)); element.Arrange(new Rect(0,0,width,height)); element.UpdateLayout();
            var image=new RenderTargetBitmap(width*2,height*2,192,192,PixelFormats.Pbgra32); image.Render(element);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using(var stream=File.Create(file)) encoder.Save(stream);
        }
    }
    internal sealed class ChargeState
    {
        bool? last;
        public bool Observe(bool connected) { bool play=last.HasValue && !last.Value && connected; last=connected; return play; }
    }
}
