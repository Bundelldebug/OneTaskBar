using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal sealed class RecentAppUse {public string Key;public long UsedUtcTicks;}
    internal sealed class RecentAppUsage : IDisposable
    {
        const int Limit=64;readonly string file;readonly Dictionary<string,long> times=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        readonly DispatcherTimer save=new DispatcherTimer {Interval=TimeSpan.FromSeconds(2)};bool dirty,disposed;int failures;
        internal RecentAppUsage(string path){file=path;try{if(File.Exists(file))foreach(var item in new JavaScriptSerializer().Deserialize<List<RecentAppUse>>(File.ReadAllText(file))??new List<RecentAppUse>())if(!String.IsNullOrWhiteSpace(item.Key)&&item.Key.Length<=32768&&item.UsedUtcTicks>0&&item.UsedUtcTicks<=DateTime.MaxValue.Ticks)times[item.Key]=item.UsedUtcTicks;Trim();}catch(Exception error){AppLog.Once("recent-apps-read",error.Message);}save.Tick+=(s,e)=>Flush();}
        internal void Record(AppEntry entry){Record(entry,DateTime.UtcNow);}
        internal void Record(AppEntry entry,DateTime utc){if(disposed||entry==null||String.IsNullOrWhiteSpace(entry.Key)||!ApplicationEntry(entry))return;string key=entry.StoredKey==null?entry.Key:entry.AppID??entry.Target;if(String.IsNullOrWhiteSpace(key))return;times[key]=utc.Ticks;Trim();dirty=true;save.Stop();save.Start();}
        static bool ApplicationEntry(AppEntry entry){if(!String.IsNullOrEmpty(entry.AppID))return true;try{return String.Equals(Path.GetExtension(entry.Executable??entry.Target),".exe",StringComparison.OrdinalIgnoreCase);}catch{return false;}}
        void Trim(){foreach(string key in times.OrderByDescending(p=>p.Value).Skip(Limit).Select(p=>p.Key).ToArray())times.Remove(key);}
        internal List<AppEntry> Recommend(IEnumerable<AppEntry> installed,IEnumerable<AppEntry> running,int count=8)
        {
            var active=running.Where(a=>!String.IsNullOrEmpty(a.Key)).ToList();var available=installed.Concat(active).Where(a=>!String.IsNullOrEmpty(a.Key)).GroupBy(a=>a.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();var result=new List<AppEntry>();
            foreach(var used in times.OrderByDescending(p=>p.Value)){var entry=available.FirstOrDefault(a=>String.Equals(a.Key,used.Key,StringComparison.OrdinalIgnoreCase));if(entry!=null)Add(result,entry,active);if(result.Count>=count)return result;}
            // On first use, running apps are the only observed recent applications.
            // Do not pad the list with fixed pins or alphabetically chosen apps.
            foreach(var entry in active.OrderByDescending(a=>a.StartedUtcTicks)){Add(result,entry,active);if(result.Count>=count)break;}return result;
        }
        static void Add(List<AppEntry> result,AppEntry entry,List<AppEntry> active){if(result.Any(a=>Catalog.Same(a,entry)))return;var copy=entry.Copy();foreach(var window in active.Where(a=>Catalog.Same(a,entry)).SelectMany(a=>a.Windows))if(!copy.Windows.Contains(window))copy.Windows.Add(window);result.Add(copy);}
        internal void Flush()
        {
            save.Stop();if(!dirty||disposed)return;try{Directory.CreateDirectory(Path.GetDirectoryName(file));string temporary=file+".tmp",backup=file+".previous";File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(times.OrderByDescending(p=>p.Value).Select(p=>new RecentAppUse {Key=p.Key,UsedUtcTicks=p.Value}).ToArray()));
                if(File.Exists(file))File.Replace(temporary,file,backup);else File.Move(temporary,file);dirty=false;failures=0;try{if(File.Exists(backup))File.Delete(backup);}catch(IOException){}catch(UnauthorizedAccessException){}
            }catch(Exception error){AppLog.Write("recent-apps-save",error.Message);if(++failures<=3)save.Start();}
        }
        public void Dispose(){if(disposed)return;Flush();disposed=true;save.Stop();}
    }
}
