using System;
using System.Collections.Generic;

namespace OneTaskBar
{
    internal sealed class NotificationAudio : IDisposable
    {
        readonly ChargeSound player=new ChargeSound();
        readonly HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> order=new Queue<string>();
        DateTime playedAt;
        internal bool Play(string file,Action<string> error){return player.Play(file,error);}
        internal void Notify(NotificationEntry entry)
        {
            if(!Preferences.Current.NotificationSoundEnabled)return;
            string key=NotificationBanners.Key(entry);
            if(!seen.Add(key))return;
            order.Enqueue(key);while(order.Count>128)seen.Remove(order.Dequeue());
            // Bursts share one audible cue. Baseline/history never calls Notify.
            var now=DateTime.UtcNow;if(now-playedAt<TimeSpan.FromMilliseconds(750))return;
            playedAt=now;player.Play(Preferences.Current.NotificationSoundFile,null);
        }
        internal void Refresh(){if(!Preferences.Current.NotificationSoundEnabled)player.Dispose();}
        public void Dispose(){player.Dispose();seen.Clear();order.Clear();}
    }
}
