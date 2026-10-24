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
        public bool NativeStartKey=true,NativeSearch=false;
        public bool NativeStartButton=false;
        public bool QuickSliderScroll=false;
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
        static PreferenceData Load(){try{if(File.Exists(file))return new JavaScriptSerializer().Deserialize<PreferenceData>(File.ReadAllText(file))??new PreferenceData();}catch(Exception e){AppLog.Once("settings-read",e.Message);}return new PreferenceData();}
        internal static void Set(Action<PreferenceData> change)
        {
            change(Current);Save();var notify=Changed;if(notify!=null)notify();
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
            get {try{using(var entry=Registry.CurrentUser.OpenSubKey(key)){string value=entry==null?"":entry.GetValue("OneTaskBar","") as string;return value!=null&&(value.IndexOf("OneTaskBar.exe",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("OneTaskBar.Desktop_",StringComparison.OrdinalIgnoreCase)>=0);}}catch(Exception e){AppLog.Once("startup-read",e.Message);return false;}}
        }
        internal static void Set(bool value)
        {
            using(var entry=Registry.CurrentUser.CreateSubKey(key)) {
                if(!value){if(Enabled)entry.DeleteValue("OneTaskBar",false);return;}
                string command="\""+NotificationIdentity.PhysicalFile(Path.Combine(AppLog.Root,"OneTaskBar.exe"))+"\" --desktop --logon";
                try{if(NotificationIdentity.IsCurrent){var family=Windows.ApplicationModel.Package.Current.Id.FamilyName;command="explorer.exe \"shell:AppsFolder\\"+family+"!OneTaskBar\"";}}catch{}
                entry.SetValue("OneTaskBar",command,RegistryValueKind.String);
            }
        }
    }
}
