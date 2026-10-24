using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
namespace OneTaskBar
{
    // Windows-maintained recent-document records, read only when Start opens.
    // This is not an export of Windows 11's complete Recommended ranking.
    internal static class StartRecentFeed
    {
        internal static List<StartRecent> Read()
        {
            return ReadFolder(Environment.GetFolderPath(Environment.SpecialFolder.Recent),Enabled(),24);
        }
        internal static bool Enabled()
        {
            try{return Enabled((hive,path,name)=>{using(var root=RegistryKey.OpenBaseKey(hive,RegistryView.Default))using(var key=root.OpenSubKey(path,false))return key==null?null:key.GetValue(name);});}
            catch{return false;}
        }
        internal static bool Enabled(Func<RegistryHive,string,string,object> read)
        {
            object tracking=read(RegistryHive.CurrentUser,@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced","Start_TrackDocs");
            if(tracking is int&&(int)tracking==0)return false;
            foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(string name in new[]{"NoRecentDocsHistory","NoRecentDocsMenu"}){
                object policy=read(hive,@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",name);if(policy is int&&(int)policy!=0)return false;
            }
            return true;
        }
        internal static List<StartRecent> ReadFolder(string folder,bool enabled,int limit)
        {
            var result=new Dictionary<string,StartRecent>(StringComparer.OrdinalIgnoreCase);
            if(!enabled||!Local(folder)||limit<=0)return result.Values.ToList();
            var watch=Stopwatch.StartNew();
            try{
                var links=new DirectoryInfo(folder).EnumerateFiles("*.lnk").Take(2048).OrderByDescending(f=>f.LastWriteTimeUtc).Take(192);
                foreach(var link in links){
                    if(watch.ElapsedMilliseconds>700||result.Count>=Math.Min(24,limit))break;
                    try{
                        // GetPath does not resolve a missing shortcut or activate its target.
                        // Open the document itself, never commands/arguments from a Recent link.
                        string target=Native.ShortcutTarget(link.FullName);if(!String.IsNullOrWhiteSpace(target))target=Environment.ExpandEnvironmentVariables(target);
                        AddRecord(result,target,link.FullName,link.LastWriteTimeUtc);
                    }catch{}
                }
            }catch(Exception error){AppLog.Once("start-windows-recent",error.GetType().Name);}
            return result.Values.OrderByDescending(r=>r.Time.ToUniversalTime()).ThenBy(r=>r.App.Name,StringComparer.CurrentCultureIgnoreCase).Take(Math.Min(24,limit)).ToList();
        }
        internal static bool Local(string path)
        {
            return !String.IsNullOrWhiteSpace(path)&&path.Length>3&&Char.IsLetter(path[0])&&path[1]==':'&&(path[2]=='\\'||path[2]=='/');
        }
        internal static void AddRecord(IDictionary<string,StartRecent> records,string target,string link,DateTime recordedUtc)
        {
            if(!Local(target)||!Local(link)||recordedUtc.Year<1980||recordedUtc>DateTime.UtcNow.AddMinutes(5))return;
            try{
                string full=Path.GetFullPath(target),extension=Path.GetExtension(full);
                if(extension.Equals(".exe",StringComparison.OrdinalIgnoreCase)||extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase)||Inside(full,AppLog.Data)||Inside(full,AppLog.Root))return;
                var file=new FileInfo(full);if(!file.Exists||(file.Attributes&(FileAttributes.Hidden|FileAttributes.System|FileAttributes.Directory))!=0)return;
                StartRecent previous;if(records.TryGetValue(full,out previous)&&previous.Time.ToUniversalTime()>=recordedUtc)return;
                records[full]=new StartRecent {App=new AppEntry {Name=file.Name,Target=full,Executable=full},Time=recordedUtc.ToLocalTime(),RecentLink=link};
            }catch{}
        }
        static bool Inside(string file,string folder){return !String.IsNullOrEmpty(folder)&&file.StartsWith(Path.GetFullPath(folder).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}
    }
}
