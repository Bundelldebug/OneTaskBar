using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Devices.Radios;
using Windows.Media.Control;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace OneTaskBar
{
    internal sealed class MediaState
    {
        internal string Title="재생 중인 미디어 없음",Artist="음악이나 동영상을 재생해 보세요",AppId;
        internal bool Available,Playing,CanPlay,CanNext,CanPrevious;
        internal BitmapSource Artwork;
    }
    internal sealed class SystemServices
    {
        internal static async Task<BitmapSource> AppLogo(string appId)
        {
            try {
                var app=Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(appId);if(app==null)return null;
                using(var stream=await Wait(app.DisplayInfo.GetLogo(new Windows.Foundation.Size(192,192)).OpenReadAsync()))using(var reader=new DataReader(stream)) {
                    uint size=(uint)Math.Min(8388608UL,stream.Size);await Wait(reader.LoadAsync(size));var bytes=new byte[size];reader.ReadBytes(bytes);
                    using(var data=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=192;image.StreamSource=data;image.EndInit();image.Freeze();return image;}
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
        internal bool? WifiPresent,BluetoothPresent;internal bool NetworkConnected;DateTime radioProbeAt;
        internal MediaState Media=new MediaState();
        string artworkKey;
        internal async Task Refresh()
        {
            bool radioFailed=false;
            try {
                if(DateTime.UtcNow>=radioProbeAt) {
                    radioProbeAt=DateTime.UtcNow.AddSeconds(4);
                    var radios=await Wait(Radio.GetRadiosAsync());wifi=radios.FirstOrDefault(r=>r.Kind==RadioKind.WiFi);bluetooth=radios.FirstOrDefault(r=>r.Kind==RadioKind.Bluetooth);
                    WifiPresent=wifi!=null;BluetoothPresent=bluetooth!=null;
                }
                WifiOn=wifi!=null ? (bool?)(wifi.State==RadioState.On) : null;
                BluetoothOn=bluetooth!=null ? (bool?)(bluetooth.State==RadioState.On) : null;
            } catch(Exception e) {AppLog.Once("radios",e.Message);radioFailed=true;}
            if(radioFailed){
                // Unpackaged desktop installs may not have radio control permission. Hardware
                // absence is still detectable without requesting or changing that permission.
                try{WifiPresent=System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Any(n=>n.NetworkInterfaceType==System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211);}catch{}
                try{BluetoothPresent=await Wait(Windows.Devices.Bluetooth.BluetoothAdapter.GetDefaultAsync())!=null;}catch{}
            }
            try{NetworkConnected=System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();}catch{}
            try {
                if(manager==null)manager=await Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                session=manager.GetCurrentSession(); if(session==null) {Media=new MediaState(); artworkKey=null; return;}
                var info=session.GetPlaybackInfo();var properties=await Wait(session.TryGetMediaPropertiesAsync());
                string key=session.SourceAppUserModelId+"|"+properties.Title+"|"+properties.Artist;
                var state=new MediaState {Available=true,Title=String.IsNullOrWhiteSpace(properties.Title) ? "미디어" : properties.Title,Artist=properties.Artist,AppId=session.SourceAppUserModelId,
                    Playing=info.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    CanPlay=info.Controls.IsPlayPauseToggleEnabled,CanNext=info.Controls.IsNextEnabled,CanPrevious=info.Controls.IsPreviousEnabled,Artwork=Media.Artwork};
                if(key!=artworkKey) {
                    artworkKey=key; state.Artwork=null;
                    if(properties.Thumbnail!=null)using(var stream=await Wait(properties.Thumbnail.OpenReadAsync()))using(var reader=new DataReader(stream)) {
                        uint size=(uint)Math.Min(8388608UL,stream.Size);await Wait(reader.LoadAsync(size));var bytes=new byte[size];reader.ReadBytes(bytes);
                        using(var managed=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=320;image.StreamSource=managed;image.EndInit();image.Freeze();state.Artwork=image;}
                    }
                }
                Media=state;
            } catch(Exception e) {AppLog.Once("media",e.Message); Media=new MediaState();}
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
        internal async Task ControlMedia(string command)
        {
            try {if(session==null) return;
                if(command=="play")await Wait(session.TryTogglePlayPauseAsync());
                else if(command=="next")await Wait(session.TrySkipNextAsync());
                else if(command=="previous")await Wait(session.TrySkipPreviousAsync());
            } catch(Exception e) {AppLog.Write("media-control",e.Message);}
        }
    }
}
