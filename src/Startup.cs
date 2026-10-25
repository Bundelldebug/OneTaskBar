using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class StartupTrace
    {
        static readonly Stopwatch clock=Stopwatch.StartNew();static readonly Dictionary<string,double> stages=new Dictionary<string,double>();
        internal static void Mark(string stage){stages[stage]=Math.Round(clock.Elapsed.TotalMilliseconds,1);}
        internal static void Save(){try{AppLog.Json("startup-timing.json",new {time=DateTimeOffset.Now.ToString("o"),stages=stages.ToDictionary(p=>p.Key,p=>p.Value),fromProcessStartMilliseconds=Math.Round((DateTime.UtcNow-Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds,1)});}catch(Exception error){AppLog.Once("startup-timing",error.GetType().Name);}}
    }
    // Start nonessential discovery only after the first owned window has rendered.
    // The recovery lease and watchdog are still established before replacing Explorer.
    internal sealed class StartupWork : IDisposable
    {
        readonly Window anchor;readonly Action action;readonly DispatcherTimer deadline;DispatcherOperation queued;bool done;
        internal bool Pending {get{return !done;}}
        internal StartupWork(Window window,Action initialize)
        {
            anchor=window;action=initialize;deadline=new DispatcherTimer(DispatcherPriority.ContextIdle){Interval=TimeSpan.FromSeconds(2)};deadline.Tick+=(s,e)=>Queue();
            if(anchor==null){Queue();return;}anchor.ContentRendered+=Rendered;anchor.Closed+=Closed;deadline.Start();
        }
        void Rendered(object sender,EventArgs args){Queue();}
        void Closed(object sender,EventArgs args){Queue();}
        void Queue(){if(done||queued!=null)return;queued=Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(done)return;Dispose();action();}),DispatcherPriority.ContextIdle);}
        public void Dispose(){if(done)return;done=true;deadline.Stop();if(anchor!=null){anchor.ContentRendered-=Rendered;anchor.Closed-=Closed;}if(queued!=null&&queued.Status==DispatcherOperationStatus.Pending)queued.Abort();queued=null;}
    }
    internal sealed class StartupDockCache
    {
        public int Version;public string Inputs;public List<Catalog.PinPreference> Pins;
        static string FileName {get{return Path.Combine(AppLog.Data,"startup-dock-cache.json");}}
        static string Stamp(string file){try{var info=new FileInfo(file);return info.Exists?info.Length+":"+info.LastWriteTimeUtc.Ticks:"missing";}catch{return "unavailable";}}
        internal static string Fingerprint()
        {
            string links=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Internet Explorer","Quick Launch","User Pinned","TaskBar");long changed=0;try{changed=Directory.GetLastWriteTimeUtc(links).Ticks;}catch{}
            return Stamp(Path.Combine(AppLog.Data,"dock-pins.json"))+"|"+Stamp(Path.Combine(AppLog.Data,"explorer-pins.json"))+"|"+Stamp(Path.Combine(AppLog.Data,"apps.json"))+"|"+changed;
        }
        internal static List<AppEntry> Read()
        {
            try{var info=new FileInfo(FileName);if(!info.Exists||info.Length>1048576)return null;var cache=new JavaScriptSerializer().Deserialize<StartupDockCache>(File.ReadAllText(FileName));if(cache==null||cache.Version!=1||cache.Inputs!=Fingerprint()||cache.Pins==null||cache.Pins.Count>256)return null;return cache.Pins.Where(p=>p!=null&&!p.Hidden&&!String.IsNullOrEmpty(p.Key)).GroupBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).Select(g=>{var p=g.First();return new AppEntry {Name=p.Name,AppID=p.AppID,Target=p.Target,Executable=p.Executable,StoredKey=p.Key,Pinned=true};}).ToList();}catch{return null;}
        }
        internal static void Save(IEnumerable<AppEntry> pins)
        {
            try{Directory.CreateDirectory(AppLog.Data);var cache=new StartupDockCache {Version=1,Inputs=Fingerprint(),Pins=pins.Take(256).Select(p=>new Catalog.PinPreference {Key=p.Key,Name=p.Name,AppID=p.AppID,Target=p.Target,Executable=p.Executable}).ToList()};string temp=FileName+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(cache));if(File.Exists(FileName))File.Replace(temp,FileName,null);else File.Move(temp,FileName);}catch(Exception error){AppLog.Once("startup-cache",error.GetType().Name);}
        }
    }
}
