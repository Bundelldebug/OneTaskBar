using System;
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace OneTaskBar
{
    internal sealed class PreferenceData
    {
        public bool Blur=true,Animations=true,ChargeAnimation=true,Hover=true,RunningDots=true,Seconds=true,DesktopMode=true;
        public bool NativeNotifications=false,NativeQuickSettings=false;
        public bool NativeTaskView=false;
        public bool NativeTrayOverflow=false;
        public bool DynamicIsland=false;
        public bool ShowNowBar=true;
        public bool NotificationIntegration=true;
        public bool NotificationPopups=true;
        public bool NotificationSoundEnabled=false;
        public string NotificationSoundFile;
        public bool BudsConnectionPopups=true;
        public bool QuickGalaxyBuds=false;
        public int UIVersion=85;
        // Fluid UI only: the color of the glow behind the glass (0 orange, 1 pink, 2 violet, 3 blue, 4 green, 5 none, 6 custom).
        public int FluidGlow=0;
        public string FluidGlowColor="#FFFF8A3D";
        // Fluid UI only: a notification glows in the main color of its app icon.
        public bool FluidIconGlow=true;
        public int UITheme=0;
        public bool SeparateDockTheme=false;
        public bool PanelsUseDockColor=false;
        public int DockTheme=0;
        public bool DockSolidBackground=false;
        public int DockBackgroundKind=0,DockEdge=3;
        public int DockScalePercent=100;        public string DockMonitor;
        public string PresetDesktopWallpaper,PresetLockScreen;
        // Liquid glass, blur and bottom Now Bar as they were before the Mac style changed them; "lg,blur,nowbar" as 0/1.
        public string DesktopStyleBackup;
        public int DockClockMode=0;
        public string DockBackgroundColor="#FFBAC1C7",DockBackgroundImage;
        public bool DockMaterialCustom=false,StartMaterialCustom=false;
        public string DockMaterialColor="#FF52616D",StartMaterialColor="#FF3D4854";
        public int DockMaterialOpacity=30,StartMaterialOpacity=30;
        public int DockMaterialBlur=16,StartMaterialBlur=16;
        public bool NativeStartKey=true,NativeSearch=false;
        public bool NativeStartButton=false;
        public bool GroupAppsByStartMenu=false;
        public bool AutoFolderApps=false;
        public bool SquircleIcons=false;
        public bool MaximizedBehindDock=false;
        public bool NativeAltTab=false;
        public bool QuickSliderScroll=false;
        public bool DuoStatusIcon=false,SliderValueNumbers=false;
        public string[] DockStatusItems=new string[0];
        public bool DockWindowDrop=true;
        public int QuickToggleWidthPercent=100,QuickToggleCollapsedHeight=62;
        public int QuickToggleColumns,QuickToggleRows;
        public bool ChargeSoundEnabled=false;
        public string ChargeSoundFile;
        public bool MediaWindowAnimation=true;
        public bool NowBarSideCapsule=false;
        public bool NowBarLargeCover=false;
        public int FullscreenClockPercent=100;
        public bool DesktopWidgets=false;
        public int WidgetOpacity=100;
        public int WidgetBlur=0;
        public bool WidgetGlass=true;
        public bool LiquidGlass=false;
        public int TrayPanelPercent=100;
        public bool MediaFlightToIsland=true;
        public bool TopStatusBar=false;
        public bool FullscreenBand=true;
        public bool LightEffects=false;
        // Keeps animations at 60 frames per second on a faster display; read once at startup.
        public bool Limit60Hz=false;
        public bool WidgetsSeeded=false;
        public bool DesktopMenuEntry=true;
        public bool FullscreenWidgets=true;
        public string[] FullscreenTiles={"weather:2x2","moon:1x1","rain:1x1","gauges:2x1","calendar:2x2","todo:2x2"};
        public string[] FullscreenActions={"capture","mute"};
        public string FullscreenAppLeft,FullscreenAppRight;
        public string WeatherPlace;
        public double WeatherLatitude,WeatherLongitude;
        public bool WeatherAutoLocation=false;
        public string WeatherChannelKey;
        public bool SecondaryDockOpenWindowsOnly=false;
        public string DualClockZone="GMT Standard Time";
        public bool YouTubeHighResArtwork=false;
        public string YouTubeArtworkTrack;
        public int DesktopPowerWatts=0;
        public bool EmergencyExit=true;
    }
    internal static class Preferences
    {
        static readonly string file=Path.Combine(AppLog.Data,"settings.json");
        internal static readonly PreferenceData Current=Load();
        internal static event Action Changed;
        internal static event Action AppearanceChanged;
        internal static event Action MediaAppearanceChanged;
        internal static event Action DesktopWidgetsChanged;
        internal static event Action WidgetMaterialChanged;
        internal static PreferenceData Normalize(PreferenceData value){if(!ReleaseFeatures.GlassUI&&value.UIVersion>=95)value.UIVersion=90;value.UITheme=Math.Max(0,Math.Min(2,value.UITheme));value.DockTheme=Math.Max(0,Math.Min(2,value.DockTheme));value.DockScalePercent=DockSizing.Clamp(value.DockScalePercent);value.FullscreenClockPercent=Math.Max(50,Math.Min(200,value.FullscreenClockPercent));value.WidgetOpacity=Math.Max(10,Math.Min(100,value.WidgetOpacity));value.WidgetBlur=Math.Max(0,Math.Min(40,value.WidgetBlur));value.TrayPanelPercent=TrayPanelSizing.Clamp(value.TrayPanelPercent);SurfaceMaterial.Normalize(value);return value;}
        static PreferenceData Load(){try{if(File.Exists(file))return Normalize(new JavaScriptSerializer().Deserialize<PreferenceData>(File.ReadAllText(file))??new PreferenceData());}catch(Exception e){AppLog.Once("settings-read",e.Message);}return Normalize(Fresh());}
        // A first install starts on the newest interface this build offers; saved settings keep their own choice.
        static PreferenceData Fresh(){var value=new PreferenceData();if(!File.Exists(file)&&Environment.GetEnvironmentVariable("ONE_TASKBAR_DATA")==null)value.UIVersion=Fluid.Available&&ReleaseFeatures.GlassUI?Fluid.Version:ReleaseFeatures.GlassUI?95:90;return value;}
        internal static void Set(Action<PreferenceData> change)
        {
            change(Current);Normalize(Current);Save();var notify=Changed;if(notify!=null)notify();
        }
        // Material edits do not rescan devices, reload artwork, or rebuild dock icons.
        internal static void SetAppearance(Action<PreferenceData> change)
        {
            change(Current);Normalize(Current);Save();var notify=AppearanceChanged;if(notify!=null)notify();
        }
        internal static void SetAppIcons(bool enabled)
        {
            Current.SquircleIcons=enabled;Save();AppIconTheme.NotifyChanged();
        }
        internal static void SetMediaAppearance(Action<PreferenceData> change)
        {
            change(Current);
            Normalize(Current);
            Save();
            var notify=MediaAppearanceChanged;
            if(notify!=null)notify();
        }
        internal static void SetWidgetMaterial(Action<PreferenceData> change)
        {
            change(Current);
            Normalize(Current);
            Save();
            var notify=WidgetMaterialChanged;
            if(notify!=null)notify();
        }
        internal static void SetDesktopWidgets(bool enabled)
        {
            if(Current.DesktopWidgets==enabled)return;
            Current.DesktopWidgets=enabled;
            Save();
            var notify=DesktopWidgetsChanged;
            if(notify!=null)notify();
        }
        internal static void Save()
        {
            try{Directory.CreateDirectory(AppLog.Data);string temporary=file+".tmp";File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(Current));if(File.Exists(file))File.Replace(temporary,file,file+".previous");else File.Move(temporary,file);}
            catch(Exception e){AppLog.Write("settings-save",e.Message);}
        }
    }
    internal static class StartupPreference
    {
        const string key=@"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static bool Enabled
        {
            get {return LogonStartup.Enabled;}
        }
        internal static void Set(bool value)
        {
            LogonStartup.Set(value);
        }
    }
}
