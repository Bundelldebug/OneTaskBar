using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Media.Imaging;
using Windows.Devices.Radios;
using Windows.Media.Control;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.Networking.Connectivity;

namespace OneTaskBar
{
    internal sealed class MediaState
    {
        internal string Title="재생 중인 미디어 없음",Artist="음악이나 동영상을 재생해 보세요",AppId;
        internal bool Available,Active,Playing,CanPlay,CanNext,CanPrevious,CanSeek,CanShuffle,Shuffling;
        internal double Position,Duration;internal DateTime SampledUtc=DateTime.UtcNow;
        internal double CurrentPosition(DateTime now){return Math.Max(0,Math.Min(Duration,Position+(Playing?Math.Max(0,(now-SampledUtc).TotalSeconds):0)));}
        internal BitmapSource Artwork;
    }
    internal sealed class SystemServices : IDisposable
    {
        internal string OutputName="오디오 출력";DateTime outputAt;
        int outputRevision,outputDirty=1;
        internal void InvalidateOutput(int revision)
        {
            if(Interlocked.Exchange(ref outputRevision,revision)!=revision)Interlocked.Exchange(ref outputDirty,1);
        }
        internal static async Task<BitmapSource> AppLogo(string appId)
        {
            try {
                var app=Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(appId);if(app==null)return null;
                using(var stream=await Wait(app.DisplayInfo.GetLogo(new Windows.Foundation.Size(160,160)).OpenReadAsync()))using(var reader=new DataReader(stream)) {
                    uint size=(uint)Math.Min(8388608UL,stream.Size);await Wait(reader.LoadAsync(size));var bytes=new byte[size];reader.ReadBytes(bytes);
                    using(var data=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=160;image.StreamSource=data;image.EndInit();image.Freeze();return image;}
                }
            }catch{return null;}
        }
        // Framework 4.8 can project the OS split metadata directly; bridge its async ABI explicitly.
        internal static Task<T> Wait<T>(IAsyncOperation<T> operation)
        {
            var source=new TaskCompletionSource<T>();
            operation.Completed=(result,status)=> {
                try {if(status==AsyncStatus.Completed)source.TrySetResult(result.GetResults());else if(status==AsyncStatus.Canceled)source.TrySetCanceled();else source.TrySetException(result.ErrorCode??new Exception("Windows operation failed"));}
                catch(Exception e){source.TrySetException(e);}finally{result.Close();}
            };return source.Task;
        }
        Radio wifi,bluetooth;
        GlobalSystemMediaTransportControlsSessionManager manager;
        GlobalSystemMediaTransportControlsSession session;
        internal bool? WifiOn,BluetoothOn;
        internal bool? WifiPresent,BluetoothPresent;internal bool NetworkConnected;internal NetworkRoute NetworkRoute;DateTime radioProbeAt,networkAt;int networkDirty=1;
        internal SystemServices(){try{NetworkInformation.NetworkStatusChanged+=NetworkChanged;}catch{}}
        void NetworkChanged(object sender){Interlocked.Exchange(ref networkDirty,1);var changed=Changed;if(!disposed&&changed!=null)changed();}
        internal MediaState Media=new MediaState();
        readonly MediaArtwork artwork=new MediaArtwork();int propertiesDirty=1,playbackDirty=1;DateTime metadataAt,playbackAt;bool disposed;
        readonly MediaArtwork highArtwork=new MediaArtwork();readonly ArtworkUpgradeSchedule artworkUpgrade=new ArtworkUpgradeSchedule();string highAttempt;bool highResolution=YouTubeArtwork.Enabled;string artworkConsent=ConsentState();
        static string ConsentState(){return Preferences.Current.YouTubeHighResArtwork+"|"+Preferences.Current.YouTubeArtworkTrack;}
        internal void ArtworkModeChanged(){string consent=ConsentState();if(artworkConsent==consent)return;artworkConsent=consent;highResolution=YouTubeArtwork.Enabled;highAttempt=null;artworkUpgrade.Clear();highArtwork.Clear();Interlocked.Exchange(ref propertiesDirty,1);Notify();}
        bool interactive;internal bool Interactive {get{return interactive;}set{if(interactive==value)return;interactive=value;if(value){outputAt=radioProbeAt=playbackAt=DateTime.MinValue;Interlocked.Exchange(ref playbackDirty,1);}}}internal Action Changed;
        internal int MetadataReads;
        internal int ArtworkReads {get{return artwork.Reads;}}internal int ArtworkDecodes {get{return artwork.Decodes;}}
        void Notify(){Interlocked.Exchange(ref playbackDirty,1);var changed=Changed;if(!disposed&&changed!=null)changed();}
        void PropertiesChanged(GlobalSystemMediaTransportControlsSession sender,MediaPropertiesChangedEventArgs args){Interlocked.Exchange(ref propertiesDirty,1);Notify();}
        void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender,PlaybackInfoChangedEventArgs args){Notify();}
        void TimelineChanged(GlobalSystemMediaTransportControlsSession sender,TimelinePropertiesChangedEventArgs args){Notify();}
        void CurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender,CurrentSessionChangedEventArgs args){Interlocked.Exchange(ref propertiesDirty,1);Notify();}
        void SelectSession(GlobalSystemMediaTransportControlsSession value)
        {
            if(Object.ReferenceEquals(session,value))return;
            if(session!=null)try{session.MediaPropertiesChanged-=PropertiesChanged;session.PlaybackInfoChanged-=PlaybackChanged;session.TimelinePropertiesChanged-=TimelineChanged;}catch{}
            session=value;artwork.Clear();highArtwork.Clear();artworkUpgrade.Clear();highAttempt=null;Interlocked.Exchange(ref propertiesDirty,1);metadataAt=DateTime.MinValue;
            if(session!=null)try{session.MediaPropertiesChanged+=PropertiesChanged;session.PlaybackInfoChanged+=PlaybackChanged;session.TimelinePropertiesChanged+=TimelineChanged;}catch{}
        }
        public void Dispose(){disposed=true;try{NetworkInformation.NetworkStatusChanged-=NetworkChanged;}catch{}SelectSession(null);if(manager!=null)try{manager.CurrentSessionChanged-=CurrentChanged;}catch{}manager=null;}
        internal async Task Refresh()
        {
            if(disposed)return;if(Interlocked.Exchange(ref outputDirty,0)!=0||DateTime.UtcNow>=outputAt){outputAt=DateTime.UtcNow.AddSeconds(Interactive?10:60);try{OutputName=await BackgroundWork.Run(()=>Native.DefaultAudioOutputName());}catch{OutputName="오디오 출력";}}
            bool radioFailed=false,probeRadio=DateTime.UtcNow>=radioProbeAt;
            try {
                if(DateTime.UtcNow>=radioProbeAt) {
                    radioProbeAt=DateTime.UtcNow.AddSeconds(Interactive?4:30);
                    var radios=await Wait(Radio.GetRadiosAsync());wifi=radios.FirstOrDefault(r=>r.Kind==RadioKind.WiFi);bluetooth=radios.FirstOrDefault(r=>r.Kind==RadioKind.Bluetooth);
                    WifiPresent=wifi!=null;BluetoothPresent=bluetooth!=null;
                }
                WifiOn=wifi!=null ? (bool?)(wifi.State==RadioState.On) : null;
                BluetoothOn=bluetooth!=null ? (bool?)(bluetooth.State==RadioState.On) : null;
            } catch(Exception e) {AppLog.Once("radios",e.Message);radioFailed=true;}
            if(radioFailed&&probeRadio){
                // Unpackaged desktop installs may not have radio control permission. Hardware
                // absence is still detectable without requesting or changing that permission.
                try{WifiPresent=System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Any(n=>n.NetworkInterfaceType==System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211);}catch{}
                try{BluetoothPresent=await Wait(Windows.Devices.Bluetooth.BluetoothAdapter.GetDefaultAsync())!=null;}catch{}
            }
            if(Interlocked.Exchange(ref networkDirty,0)!=0||DateTime.UtcNow>=networkAt){networkAt=DateTime.UtcNow.AddSeconds(30);try{NetworkRoute=NetworkRouteInfo.Read();NetworkConnected=NetworkRoute!=NetworkRoute.None;}catch{try{NetworkConnected=System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();NetworkRoute=NetworkConnected?NetworkRoute.Other:NetworkRoute.None;}catch{}}}
            try {
                if(manager==null){manager=await Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());manager.CurrentSessionChanged+=CurrentChanged;}
                if(Interlocked.Exchange(ref playbackDirty,0)==0&&Volatile.Read(ref propertiesDirty)==0&&DateTime.UtcNow<playbackAt&&DateTime.UtcNow<metadataAt&&!artwork.Due(DateTime.UtcNow)&&!artworkUpgrade.Pending(DateTime.UtcNow))return;
                playbackAt=DateTime.UtcNow.AddSeconds(Interactive?2:30);
                if(disposed)return;SelectSession(manager.GetCurrentSession()); if(session==null) {if(Media.Available)Media=new MediaState();artwork.Clear();Interlocked.Exchange(ref propertiesDirty,0);metadataAt=DateTime.UtcNow.AddSeconds(60);return;}
                var info=session.GetPlaybackInfo();var previous=Media;
                var state=new MediaState {Available=true,Title=previous.Title,Artist=previous.Artist,AppId=session.SourceAppUserModelId,
                    Playing=info.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,Active=info.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing||info.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused,
                    CanPlay=info.Controls.IsPlayPauseToggleEnabled,CanNext=info.Controls.IsNextEnabled,CanPrevious=info.Controls.IsPreviousEnabled,CanShuffle=info.Controls.IsShuffleEnabled,Shuffling=info.IsShuffleActive??false,Artwork=highArtwork.Image??artwork.Image};
                try {var timeline=session.GetTimelineProperties();state.Duration=Math.Max(0,(timeline.EndTime-timeline.StartTime).TotalSeconds);state.Position=Math.Max(0,(timeline.Position-timeline.StartTime).TotalSeconds);state.SampledUtc=timeline.LastUpdatedTime.UtcDateTime;if(Math.Abs((DateTime.UtcNow-state.SampledUtc).TotalDays)>1)state.SampledUtc=DateTime.UtcNow;state.CanSeek=info.Controls.IsPlaybackPositionEnabled&&state.Duration>0;}catch{}
                // Track changes arrive as events; retain a slow fallback for incomplete media providers.
                if(Interlocked.Exchange(ref propertiesDirty,0)!=0||DateTime.UtcNow>=metadataAt||artwork.Due(DateTime.UtcNow)||artworkUpgrade.Pending(DateTime.UtcNow)) {
                    try {
                        var properties=await Wait(session.TryGetMediaPropertiesAsync());MetadataReads++;metadataAt=DateTime.UtcNow.AddSeconds(60);
                        state.Title=String.IsNullOrWhiteSpace(properties.Title)?"미디어":properties.Title;state.Artist=properties.Artist;
                        string key=state.AppId+"|"+properties.Title+"|"+properties.Artist;
                        state.Artwork=await artwork.Read(key,()=>ReadThumbnail(properties.Thumbnail),DateTime.UtcNow);
                        bool permitted=highResolution&&state.Active&&!String.IsNullOrWhiteSpace(properties.Title)&&YouTubeArtwork.Allowed(state.AppId,properties.Title);if(!permitted||highAttempt!=key){highArtwork.Clear();artworkUpgrade.Clear();highAttempt=permitted?key:null;}
                        if(permitted&&(state.Artwork==null||Math.Max(state.Artwork.PixelWidth,state.Artwork.PixelHeight)<1200)&&highArtwork.Image==null&&artworkUpgrade.Due(DateTime.UtcNow)){artworkUpgrade.Attempt(DateTime.UtcNow);string source=state.AppId,title=state.Title;var bytes=await BackgroundWork.Run(()=>YouTubeArtwork.Read(source,title));if(disposed)return;if(bytes!=null){var sharp=await highArtwork.Read(key,()=>Task.FromResult(bytes),DateTime.UtcNow);if(sharp!=null){state.Artwork=sharp;artworkUpgrade.Complete();}}}
                        else if(permitted&&highArtwork.Image!=null)state.Artwork=highArtwork.Image;
                    }catch(Exception error){artwork.RetryFailed(DateTime.UtcNow);metadataAt=DateTime.UtcNow.AddSeconds(10);AppLog.Once("media-properties",error.Message);}
                }
                Media=state;
            } catch(Exception e) {AppLog.Once("media",e.Message); Media=new MediaState();artwork.Clear();highArtwork.Clear();artworkUpgrade.Clear();highAttempt=null;Interlocked.Exchange(ref propertiesDirty,1);}
        }
        static async Task<byte[]> ReadThumbnail(IRandomAccessStreamReference reference)
        {
            if(reference==null)return null;
            using(var stream=await Wait(reference.OpenReadAsync()))using(var reader=new DataReader(stream)) {
                if(stream.Size==0||stream.Size>8388608UL)return null;uint size=(uint)stream.Size;
                if(await Wait(reader.LoadAsync(size))!=size)return null;var bytes=new byte[size];reader.ReadBytes(bytes);return bytes;
            }
        }
        internal async Task<bool> ToggleRadio(string kind)
        {
            try {
                var access=await Wait(Radio.RequestAccessAsync());if(access!=RadioAccessStatus.Allowed)return false;
                var radio=kind=="wifi" ? wifi : bluetooth; if(radio==null) {await Refresh(); radio=kind=="wifi" ? wifi : bluetooth;}
                if(radio==null) return false;
                return await Wait(radio.SetStateAsync(radio.State==RadioState.On?RadioState.Off:RadioState.On))==RadioAccessStatus.Allowed;
            } catch(Exception e) {AppLog.Write("radio-set",e.Message); return false;}
        }
        internal async Task<bool> SeekPercent(double percent)
        {
            try {var current=session;if(current==null||!Media.CanSeek)return false;var timeline=current.GetTimelineProperties();long ticks=timeline.StartTime.Ticks+(long)((timeline.EndTime-timeline.StartTime).Ticks*PillSlider.Clamp(percent)/100);return await Wait(current.TryChangePlaybackPositionAsync(ticks));}
            catch(Exception error){AppLog.Write("media-seek",error.Message);return false;}
        }
        internal async Task ControlMedia(string command)
        {
            try {if(session==null) return;
                if(command=="play")await Wait(session.TryTogglePlayPauseAsync());
                else if(command=="next")await Wait(session.TrySkipNextAsync());
                else if(command=="previous")await Wait(session.TrySkipPreviousAsync());
                else if(command=="shuffle"){var info=session.GetPlaybackInfo();if(info.Controls.IsShuffleEnabled)await Wait(session.TryChangeShuffleActiveAsync(!(info.IsShuffleActive??false)));}
            } catch(Exception e) {AppLog.Write("media-control",e.Message);}
        }
    }
}
