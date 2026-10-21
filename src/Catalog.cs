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
        internal string Executable;
        internal bool Pinned;
        internal TaskbarItem ExplorerItem;
        public string Key { get { return !String.IsNullOrEmpty(AppID) ? AppID : Target; } }
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
            var result=new AppEntry {Name=Name,AppID=AppID,Target=Target,Icon=Icon,Executable=Executable,Pinned=Pinned,ExplorerItem=ExplorerItem};
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
        public static List<AppEntry> Read()
        {
            string cache=Path.Combine(AppLog.Data,"apps.json");
            try {
                var p=new Process { StartInfo=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+Path.Combine(AppLog.Root,"Read-Apps.ps1")+"\" -OutputPath \""+cache+"\"") {
                    UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden } };
                using(p) { p.Start(); if(!p.WaitForExit(25000)) { p.Kill(); AppLog.Write("catalog","Get-StartApps timed out"); } }
                if(File.Exists(cache)) {
                    var rows=new JavaScriptSerializer().Deserialize<List<AppEntry>>(File.ReadAllText(cache,Encoding.UTF8));
                    if(rows!=null && rows.Count>0) { Parallel.ForEach(rows,new ParallelOptions {MaxDegreeOfParallelism=4},item=>item.LoadIcon()); return rows.OrderBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).ToList(); }
                }
            } catch(Exception e) { AppLog.Write("catalog",e.Message); }
            var fallback=new List<AppEntry> { new AppEntry { Name="파일 탐색기",Target="explorer.exe" },new AppEntry { Name="설정",Target="ms-settings:" },new AppEntry { Name="메모장",Target="notepad.exe" } };
            foreach(var item in fallback) item.LoadIcon(); return fallback;
        }
        public static List<AppEntry> Running()
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
                        entry.LoadIcon(); if(entry.Icon==null) entry.Icon=Native.WindowIcon(handle); entries.Add(key,entry); }
                    entry.Windows.Add(handle);
                } } catch { }
                return true;
            },IntPtr.Zero);
            return entries.Values.ToList();
        }
        internal static bool Same(AppEntry a,AppEntry b)
        {
            if(a==null||b==null) return false;
            if(!String.IsNullOrEmpty(a.AppID) && !String.IsNullOrEmpty(b.AppID)) return String.Equals(a.AppID,b.AppID,StringComparison.OrdinalIgnoreCase);
            string x=a.Executable??a.Target,y=b.Executable??b.Target;
            return !String.IsNullOrEmpty(x)&&!String.IsNullOrEmpty(y)&&String.Equals(x,y,StringComparison.OrdinalIgnoreCase);
        }
        internal static List<AppEntry> Merge(IEnumerable<AppEntry> pinned,IEnumerable<AppEntry> running)
        {
            var result=new List<AppEntry>();
            foreach(var item in pinned) if(!result.Any(a=>Same(a,item))) {var copy=item.Copy(); copy.Pinned=true; result.Add(copy);}
            foreach(var item in running) {
                var match=result.FirstOrDefault(a=>Same(a,item));
                if(match==null) result.Add(item.Copy());
                else { foreach(var w in item.Windows) if(!match.Windows.Contains(w)) match.Windows.Add(w); if(match.Icon==null) match.Icon=item.Icon; }
            }
            return result;
        }
        internal static List<AppEntry> ReadPins(List<AppEntry> installed,List<TaskbarItem> mirror,List<AppEntry> running)
        {
            var result=new List<AppEntry>();
            string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Internet Explorer","Quick Launch","User Pinned","TaskBar");
            if(Directory.Exists(dir)) foreach(string file in Directory.GetFiles(dir,"*.lnk")) {
                string target=Native.ShortcutTarget(file); var item=new AppEntry {Name=Path.GetFileNameWithoutExtension(file),Target=file,Executable=target,Pinned=true};
                if(String.IsNullOrEmpty(target)&&(item.Name=="File Explorer"||item.Name=="파일 탐색기")) {target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");item.Executable=target;item.AppID="Microsoft.Windows.Explorer";}
                item.AppID=item.AppID??installed.Where(a=>!String.IsNullOrEmpty(target)&&(String.Equals(a.AppID,target,StringComparison.OrdinalIgnoreCase)||String.Equals(a.Name,item.Name,StringComparison.CurrentCultureIgnoreCase))).Select(a=>a.AppID).FirstOrDefault();
                item.LoadIcon(); result.Add(item);
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
                item.AppID=button.Id;item.ExplorerItem=button;item.Pinned=true;item.LoadIcon();ordered.Add(item);
            }
            foreach(var pin in result) if(!ordered.Any(a=>Same(a,pin))) ordered.Add(pin);
            string custom=Path.Combine(AppLog.Data,"dock-pins.json");
            try { if(File.Exists(custom)) {
                var preferences=new JavaScriptSerializer().Deserialize<List<PinPreference>>(File.ReadAllText(custom));
                foreach(var preference in preferences.Where(p=>p.Hidden)) ordered.RemoveAll(a=>String.Equals(a.Key,preference.Key,StringComparison.OrdinalIgnoreCase));
                foreach(var preference in preferences.Where(p=>!p.Hidden)) if(!ordered.Any(a=>String.Equals(a.Key,preference.Key,StringComparison.OrdinalIgnoreCase))) {
                    var found=installed.FirstOrDefault(a=>String.Equals(a.Key,preference.Key,StringComparison.OrdinalIgnoreCase));
                    var entry=found!=null ? found.Copy() : new AppEntry {Name=preference.Name,Target=preference.Target,AppID=preference.AppID,Executable=preference.Executable};
                    entry.Pinned=true; entry.LoadIcon(); ordered.Add(entry);
                }
            }} catch(Exception e) {AppLog.Write("pins",e.Message);}
            return ordered;
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
