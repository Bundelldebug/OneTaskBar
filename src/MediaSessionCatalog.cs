using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
using Session=Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using Manager=Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using Status=Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

namespace OneTaskBar
{
    internal static class MediaSessionPolicy
    {
        internal static int Select(IList<Status> states,int current,int previous)
        {
            foreach(var status in new[]{Status.Playing,Status.Paused})
            {
                if(current>=0&&current<states.Count&&states[current]==status)return current;
                if(previous>=0&&previous<states.Count&&states[previous]==status)return previous;
                for(int i=0;i<states.Count;i++)if(states[i]==status)return i;
            }
            return current>=0&&current<states.Count?current:previous>=0&&previous<states.Count?previous:states.Count>0?0:-1;
        }
        internal static void Check()
        {
            if(Select(new[]{Status.Paused,Status.Playing},0,0)!=1)throw new Exception("Playing browser must outrank a paused current app");
            if(Select(new[]{Status.Playing,Status.Playing},1,0)!=1)throw new Exception("Current playing provider retains priority");
            if(Select(new[]{Status.Stopped,Status.Playing},-1,0)!=1)throw new Exception("Playing session survives a missing current provider");
            if(Select(new[]{Status.Paused,Status.Paused},-1,1)!=1)throw new Exception("Paused fallback retains the previous session");
            if(Select(new Status[0],0,0)!=-1)throw new Exception("Empty session catalog must clear media");
        }
    }
    internal sealed class MediaSessionCandidate
    {
        internal Session Session;
        internal GlobalSystemMediaTransportControlsSessionPlaybackInfo Info;
    }
    internal sealed class MediaSessionCatalog : IDisposable
    {
        readonly Manager manager;
        readonly Action changed;
        readonly List<Session> observed=new List<Session>();
        int dirty=1;
        DateTime refreshAt;
        bool disposed;

        internal MediaSessionCatalog(Manager manager,Action changed)
        {
            this.manager=manager;
            this.changed=changed;
            manager.SessionsChanged+=SessionsChanged;
        }
        void SessionsChanged(Manager sender,SessionsChangedEventArgs args)
        {
            Interlocked.Exchange(ref dirty,1);
            if(!disposed)changed();
        }
        void PlaybackChanged(Session sender,PlaybackInfoChangedEventArgs args)
        {
            if(!disposed)changed();
        }
        void Refresh()
        {
            if(Interlocked.Exchange(ref dirty,0)==0&&DateTime.UtcNow<refreshAt)return;
            var next=manager.GetSessions().ToArray();
            foreach(var old in observed)
            {
                if(next.Contains(old))continue;
                try{old.PlaybackInfoChanged-=PlaybackChanged;}catch{}
            }
            foreach(var item in next)
            {
                if(observed.Contains(item))continue;
                try{item.PlaybackInfoChanged+=PlaybackChanged;}catch{}
            }
            observed.Clear();
            observed.AddRange(next);
            refreshAt=DateTime.UtcNow.AddSeconds(30);
        }
        internal MediaSessionCandidate Select(Session current,Session previous)
        {
            Refresh();
            var candidates=new List<MediaSessionCandidate>();
            foreach(var item in observed)
            {
                AddCandidate(candidates,item);
            }
            if(current!=null&&!candidates.Any(c=>c.Session==current))
            {
                AddCandidate(candidates,current);
            }
            int selected=MediaSessionPolicy.Select(candidates.Select(c=>c.Info.PlaybackStatus).ToArray(),candidates.FindIndex(c=>c.Session==current),candidates.FindIndex(c=>c.Session==previous));
            return selected>=0?candidates[selected]:null;
        }
        static void AddCandidate(List<MediaSessionCandidate> candidates,Session item)
        {
            try
            {
                var info=item.GetPlaybackInfo();
                if(info!=null)candidates.Add(new MediaSessionCandidate {Session=item,Info=info});
            }
            catch { /* One retired provider must not hide other apps. */ }
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            try{manager.SessionsChanged-=SessionsChanged;}catch{}
            foreach(var item in observed)try{item.PlaybackInfoChanged-=PlaybackChanged;}catch{}
            observed.Clear();
        }
        internal static int Probe()
        {
            try
            {
                return Task.Run(async()=>
                {
                    var manager=await SystemServices.Wait(Manager.RequestAsync());
                    using(var catalog=new MediaSessionCatalog(manager,()=>{}))
                    {
                        var current=manager.GetCurrentSession();
                        var selected=catalog.Select(current,null);
                        AppLog.Json("media-sessions-probe.json",new
                        {
                            currentApp=current==null?null:current.SourceAppUserModelId,
                            selectedApp=selected==null?null:selected.Session.SourceAppUserModelId,
                            selectedStatus=selected==null?null:selected.Info.PlaybackStatus.ToString(),
                            sessions=catalog.observed.Select(s=>Describe(s)).ToArray(),
                            browsingHistoryRead=false
                        });
                    }
                    return 0;
                }).GetAwaiter().GetResult();
            }
            catch(Exception error)
            {
                AppLog.Json("media-sessions-probe.json",new {error=error.ToString()});
                return 1;
            }
        }
        static object Describe(Session item)
        {
            try{return new {app=item.SourceAppUserModelId,status=item.GetPlaybackInfo().PlaybackStatus.ToString()};}
            catch{return new {app="unavailable",status="unavailable"};}
        }
    }
}
