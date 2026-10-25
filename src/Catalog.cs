using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class AppEntry
    {
        public string Name {get;set;} public string AppID {get;set;} public string Target {get;set;}
        public ImageSource Icon;
        public readonly List<IntPtr> Windows=new List<IntPtr>();
        internal string Executable,StoredKey;internal long StartedUtcTicks;
        internal bool Pinned;
        internal TaskbarItem ExplorerItem;
        public string Key { get { return StoredKey??(!String.IsNullOrEmpty(AppID) ? AppID : Target); } }
        public void Launch()
        {
            if(Windows.Count>0) { Native.Activate(Windows[0]); return; }
            if(!String.IsNullOrEmpty(Target)) Native.Open(Target);
            else if(!String.IsNullOrEmpty(AppID)) {
                try { Process.Start(new ProcessStartInfo("explorer.exe","\"shell:AppsFolder\\"+AppID+"\"") { UseShellExecute=true }); }
                catch(Exception e) { AppLog.Write("launch",e.Message); }
            }
        }
        internal AppEntry Copy()
        {
            var result=new AppEntry {Name=Name,AppID=AppID,Target=Target,Icon=Icon,Executable=Executable,StoredKey=StoredKey,Pinned=Pinned,ExplorerItem=ExplorerItem,StartedUtcTicks=StartedUtcTicks};
            result.Windows.AddRange(Windows.Where(Native.IsWindow)); return result;
        }
        public void LoadIcon()
        {
            if(Icon!=null)return;
            Icon=AppIcons.Get(this);
        }
    }
    internal static class Catalog
    {
        internal static List<AppEntry> ReadCached()
        {
            try{string file=Path.Combine(AppLog.Data,"apps.json");if(!File.Exists(file)||new FileInfo(file).Length>8388608)return new List<AppEntry>();var rows=new JavaScriptSerializer {MaxJsonLength=8388608}.Deserialize<List<AppEntry>>(File.ReadAllText(file,Encoding.UTF8));return rows==null?new List<AppEntry>():rows.Where(a=>a!=null&&!String.IsNullOrEmpty(a.Name)&&!String.IsNullOrEmpty(a.Key)).Take(4096).ToList();}catch{return new List<AppEntry>();}
        }
        public static List<AppEntry> Read(bool loadIcons=true)
        {
            Directory.CreateDirectory(AppLog.Data);string cache=Path.Combine(AppLog.Data,"apps.json");
            try {
                var p=new Process { StartInfo=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(AppLog.Root,"Read-Apps.ps1")+"\" -OutputPath \""+cache+"\"") {
                    UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden } };
                using(p) { p.Start(); BackgroundWork.HintChild(p); if(!p.WaitForExit(25000)) { p.Kill(); AppLog.Write("catalog","Get-StartApps timed out"); } }
                if(File.Exists(cache)) {
                    var rows=new JavaScriptSerializer().Deserialize<List<AppEntry>>(File.ReadAllText(cache,Encoding.UTF8));
                    if(rows!=null && rows.Count>0) {foreach(var item in rows.Where(a=>!String.IsNullOrEmpty(a.AppID)&&!a.AppID.Contains("!"))){bool direct=Path.IsPathRooted(item.AppID)&&File.Exists(item.AppID);if(direct)item.Target=item.Executable=item.AppID;var shortcut=WebAppIcons.LaunchShortcut(item.AppID,item.Name);if(shortcut!=null&&shortcut.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(shortcut.Target)&&(!direct||String.Equals(shortcut.Target,item.AppID,StringComparison.OrdinalIgnoreCase))){item.Target=shortcut.File;item.Executable=shortcut.Target;}}if(loadIcons)Parallel.ForEach(rows,new ParallelOptions {MaxDegreeOfParallelism=3},item=>item.LoadIcon()); return rows.OrderBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).ToList(); }
                }
            } catch(Exception e) { AppLog.Write("catalog",e.Message); }
            var fallback=new List<AppEntry> { new AppEntry { Name="파일 탐색기",Target="explorer.exe" },new AppEntry { Name="설정",Target="ms-settings:" },new AppEntry { Name="메모장",Target="notepad.exe" } };
            if(loadIcons)foreach(var item in fallback) item.LoadIcon(); return fallback;
        }
        public static List<AppEntry> Running(IEnumerable<AppEntry> installed=null)
        {
            var entries=new Dictionary<string,AppEntry>(StringComparer.OrdinalIgnoreCase); int own=Process.GetCurrentProcess().Id;
            Native.EnumWindows((handle,state)=> {
                if(!Native.IsWindowVisible(handle) || Native.GetWindow(handle,4)!=IntPtr.Zero || (Native.GetWindowLong(handle,-20)&0x80)!=0) return true;
                var title=new StringBuilder(512); Native.GetWindowText(handle,title,512); if(title.Length==0) return true;
                int cloaked; if(Native.DwmGetWindowAttribute(handle,14,out cloaked,4)==0 && cloaked!=0) return true;
                uint pid; Native.GetWindowThreadProcessId(handle,out pid); if(pid==own) return true;
                try { using(var p=Process.GetProcessById((int)pid)) {
                    string processId; string path=Native.ProcessPath(pid,out processId);
                    string appId=Native.WindowAppId(handle)??processId;
                    if(String.IsNullOrEmpty(path)) return true;
                    if(String.Equals(path,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),StringComparison.OrdinalIgnoreCase))appId="Microsoft.Windows.Explorer";
                    string key=appId??path;
                    AppEntry entry;
                    if(!entries.TryGetValue(key,out entry)) { entry=new AppEntry { Name=title.ToString(),Target=String.IsNullOrEmpty(appId) ? path : null,AppID=appId,Executable=path };
                        entry.Windows.Add(handle);entry.LoadIcon(); if(entry.Icon==null) entry.Icon=Native.WindowIcon(handle); entries.Add(key,entry); }
                    try{long started=p.StartTime.ToUniversalTime().Ticks;if(entry.StartedUtcTicks==0||started<entry.StartedUtcTicks)entry.StartedUtcTicks=started;}catch{}
                    if(!entry.Windows.Contains(handle))entry.Windows.Add(handle);
                } } catch { }
                return true;
            },IntPtr.Zero);
            if(installed==null)return entries.Values.ToList();var nativeApps=installed.ToList();return entries.Values.Select(entry=>{var resolved=ResolveInstalled(entry,nativeApps);resolved.LoadIcon();return resolved;}).ToList();
        }
        internal static bool Same(AppEntry a,AppEntry b)
        {
            if(a==null||b==null) return false;
            if(!String.IsNullOrEmpty(a.AppID) && !String.IsNullOrEmpty(b.AppID)) return String.Equals(a.AppID,b.AppID,StringComparison.OrdinalIgnoreCase);
            string x=a.Executable??a.Target,y=b.Executable??b.Target;
            return !String.IsNullOrEmpty(x)&&!String.IsNullOrEmpty(y)&&String.Equals(x,y,StringComparison.OrdinalIgnoreCase);
        }
        internal static AppEntry ResolveInstalled(AppEntry saved,IEnumerable<AppEntry> installed)
        {
            var apps=installed.ToList();var known=apps.FirstOrDefault(a=>String.Equals(a.Key,saved.Key,StringComparison.OrdinalIgnoreCase))??apps.FirstOrDefault(a=>Same(a,saved));
            string old=saved.Executable??saved.Target;
            if(known==null&&saved.Windows.Count>0&&!String.IsNullOrEmpty(old)&&!String.IsNullOrEmpty(saved.AppID)&&!saved.AppID.Contains("!")&&!WebAppIcons.IsWebApp(saved.AppID)){var exact=apps.Where(a=>String.Equals(a.Executable,old,StringComparison.OrdinalIgnoreCase)&&!String.IsNullOrEmpty(a.AppID)&&String.Equals(a.AppID,a.Executable,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();if(exact.Length==1)known=exact[0];}
            string folder=ExecutableFolder(old,true);
            if(known==null&&folder!=null){
                var aliases=apps.Where(a=>String.Equals(ExecutableFolder(a.Executable??a.Target??a.AppID,false),folder,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();if(aliases.Length==1)known=aliases[0];
            }
            if(known==null)return saved.Copy();var result=known.Copy();result.StoredKey=saved.Key;result.Pinned=saved.Pinned;result.ExplorerItem=saved.ExplorerItem;result.StartedUtcTicks=saved.StartedUtcTicks;foreach(var window in saved.Windows)if(!result.Windows.Contains(window))result.Windows.Add(window);return result;
        }
        internal static bool PresentationChanged(IList<AppEntry> before,IList<AppEntry> after){return before.Count!=after.Count||before.Where((a,i)=>!String.Equals(a.Key,after[i].Key,StringComparison.OrdinalIgnoreCase)||a.Name!=after[i].Name||a.Target!=after[i].Target||a.AppID!=after[i].AppID||!Object.ReferenceEquals(a.Icon,after[i].Icon)).Any();}
        static string ExecutableFolder(string path,bool electron)
        {
            try{if(String.IsNullOrEmpty(path)||!Path.IsPathRooted(path)||!path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetFileNameWithoutExtension(path),"electron",StringComparison.OrdinalIgnoreCase)!=electron)return null;return Path.GetDirectoryName(Path.GetFullPath(path));}catch(ArgumentException){return null;}catch(NotSupportedException){return null;}catch(PathTooLongException){return null;}
        }
        internal static List<AppEntry> Merge(IEnumerable<AppEntry> pinned,IEnumerable<AppEntry> running)
        {
            var result=new List<AppEntry>();
            foreach(var item in pinned) if(!result.Any(a=>Same(a,item))) {var copy=item.Copy(); copy.Pinned=true; result.Add(copy);}
            foreach(var item in running) {
                var match=result.FirstOrDefault(a=>Same(a,item));
                if(match==null) {var copy=item.Copy();copy.Pinned=false;result.Add(copy);}
                else { foreach(var w in item.Windows) if(!match.Windows.Contains(w)) match.Windows.Add(w); if(match.Icon==null) match.Icon=item.Icon; }
            }
            return result;
        }
        internal static List<AppEntry> ReadPins(List<AppEntry> installed,List<TaskbarItem> mirror,List<AppEntry> running,bool loadIcons=true,bool resolveLinks=true)
        {
            var result=new List<AppEntry>();
            string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Internet Explorer","Quick Launch","User Pinned","TaskBar");
            if(Directory.Exists(dir)) foreach(string file in Directory.GetFiles(dir,"*.lnk")) {
                string target=resolveLinks?Native.ShortcutTarget(file):null; var item=new AppEntry {Name=Path.GetFileNameWithoutExtension(file),Target=file,Executable=target,Pinned=true};
                if(!resolveLinks){var cached=installed.FirstOrDefault(a=>String.Equals(a.Name,item.Name,StringComparison.CurrentCultureIgnoreCase));if(cached!=null){item.AppID=cached.AppID;item.Executable=cached.Executable;}}
                if(String.IsNullOrEmpty(target)&&(item.Name=="File Explorer"||item.Name=="파일 탐색기")) {target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");item.Executable=target;item.AppID="Microsoft.Windows.Explorer";}
                item.AppID=item.AppID??installed.Where(a=>!String.IsNullOrEmpty(target)&&(String.Equals(a.AppID,target,StringComparison.OrdinalIgnoreCase)||String.Equals(a.Name,item.Name,StringComparison.CurrentCultureIgnoreCase))).Select(a=>a.AppID).FirstOrDefault();
                if(loadIcons)item.LoadIcon(); result.Add(item);
            }
            // Explorer supplies the real taskbar order and packaged-app identity. Keep shortcut pins as fallback.
            var ordered=new List<AppEntry>();
            foreach(var button in mirror) {
                var known=installed.FirstOrDefault(a=>String.Equals(a.AppID,button.Id,StringComparison.OrdinalIgnoreCase));
                var pin=result.FirstOrDefault(a=>String.Equals(a.AppID,button.Id,StringComparison.OrdinalIgnoreCase)||String.Equals(a.Executable,button.Id,StringComparison.OrdinalIgnoreCase)||known!=null&&String.Equals(a.Name,known.Name,StringComparison.CurrentCultureIgnoreCase)||button.Name.StartsWith(a.Name+" ",StringComparison.CurrentCultureIgnoreCase));
                // A running-only taskbar button is not silently converted into a permanent pin.
                if(pin==null&&!button.Pinned)continue;
                string display=button.Name.Replace(" 고정됨","").Replace(" Pinned","");int split=display.IndexOf(" - ",StringComparison.Ordinal);if(split>0)display=display.Substring(0,split);
                var item=pin??(known!=null?known.Copy():new AppEntry {Name=display,AppID=button.Id});
                item.AppID=button.Id;item.ExplorerItem=button;item.Pinned=true;if(loadIcons)item.LoadIcon();ordered.Add(item);
            }
            foreach(var pin in result) if(!ordered.Any(a=>Same(a,pin))) ordered.Add(pin);
            string custom=Path.Combine(AppLog.Data,"dock-pins.json");
            try { if(File.Exists(custom)) {
                var preferences=new JavaScriptSerializer().Deserialize<List<PinPreference>>(File.ReadAllText(custom));
                foreach(var preference in preferences.Where(p=>p.Hidden)) ordered.RemoveAll(a=>String.Equals(a.Key,preference.Key,StringComparison.OrdinalIgnoreCase));
                foreach(var preference in preferences.Where(p=>!p.Hidden)) if(!ordered.Any(a=>String.Equals(a.Key,preference.Key,StringComparison.OrdinalIgnoreCase))) {
                    var found=installed.FirstOrDefault(a=>String.Equals(a.Key,preference.Key,StringComparison.OrdinalIgnoreCase));
                    var entry=found!=null ? found.Copy() : ResolveInstalled(new AppEntry {Name=preference.Name,Target=preference.Target,AppID=preference.AppID,Executable=preference.Executable,StoredKey=preference.Key},installed);
                    entry.Pinned=true; if(loadIcons)entry.LoadIcon(); ordered.Add(entry);
                }
            }} catch(Exception e) {AppLog.Write("pins",e.Message);}
            for(int i=0;i<ordered.Count;i++){ordered[i]=ResolveInstalled(ordered[i],installed);if(loadIcons)ordered[i].LoadIcon();}return ordered;
        }
        internal static void SetPinned(AppEntry entry,bool pinned)
        {
            string file=Path.Combine(AppLog.Data,"dock-pins.json"); var rows=new List<PinPreference>();
            try {if(File.Exists(file)) rows=new JavaScriptSerializer().Deserialize<List<PinPreference>>(File.ReadAllText(file))??rows;} catch { }
            rows.RemoveAll(r=>String.Equals(r.Key,entry.Key,StringComparison.OrdinalIgnoreCase));
            rows.Add(new PinPreference {Key=entry.Key,Name=entry.Name,AppID=entry.AppID,Target=entry.Target,Executable=entry.Executable,Hidden=!pinned}); AppLog.Json("dock-pins.json",rows);
        }
        internal sealed class PinPreference {public string Key,Name,AppID,Target,Executable; public bool Hidden;}
    }
}
