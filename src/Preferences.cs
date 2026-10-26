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
        public bool NotificationIntegration=true;
        public bool NotificationPopups=true;
        public bool BudsConnectionPopups=true;
        public bool QuickGalaxyBuds=false;
        public int UIVersion=85;
        public int UITheme=0;
        public bool SeparateDockTheme=false;
        public int DockTheme=0;
        public bool DockSolidBackground=false;
        public int DockBackgroundKind=0,DockEdge=3;
        public string DockBackgroundColor="#FFBAC1C7",DockBackgroundImage;
        public bool NativeStartKey=true,NativeSearch=false;
        public bool NativeStartButton=false;
        public bool NativeAltTab=false;
        public bool QuickSliderScroll=false;
        public bool DuoStatusIcon=true,SliderValueNumbers=false;
        public bool DockWindowDrop=true;
        public int QuickToggleWidthPercent=100,QuickToggleCollapsedHeight=62;
        public int QuickToggleColumns,QuickToggleRows;
        public bool ChargeSoundEnabled=false;
        public string ChargeSoundFile;
        public bool NowBarLargeCover=false;
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
        internal static PreferenceData Normalize(PreferenceData value){if(!ReleaseFeatures.GlassUI&&value.UIVersion>=95)value.UIVersion=90;value.UITheme=Math.Max(0,Math.Min(2,value.UITheme));value.DockTheme=Math.Max(0,Math.Min(2,value.DockTheme));return value;}
        static PreferenceData Load(){try{if(File.Exists(file))return Normalize(new JavaScriptSerializer().Deserialize<PreferenceData>(File.ReadAllText(file))??new PreferenceData());}catch(Exception e){AppLog.Once("settings-read",e.Message);}return Normalize(new PreferenceData());}
        internal static void Set(Action<PreferenceData> change)
        {
            change(Current);Normalize(Current);Save();var notify=Changed;if(notify!=null)notify();
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
