using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace OneTaskBar
{
    internal sealed class NotificationEntry
    {
        internal uint Id;internal string App,AppId,Title,Body;internal DateTimeOffset Created;internal ImageSource Icon,Artwork;internal NotificationPayload Payload;internal Guid Activator;internal IntPtr NativeWindow;internal string NativeSignature;
    }
    internal sealed class NotificationService : IDisposable
    {
        internal List<NotificationEntry> Items=new List<NotificationEntry>();
        internal bool Allowed,Packaged;
        internal UserNotificationListenerAccessStatus Permission;
        Task<UserNotificationListenerAccessStatus> accessRequest;
        internal bool RequestPending {get{return accessRequest!=null&&!accessRequest.IsCompleted;}}
        internal string Status="Windows 알림을 연결하려면 접근을 허용해 주세요";
        UserNotificationListener listener;
        readonly Dictionary<string,ImageSource> icons=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,Guid> activators=new Dictionary<string,Guid>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,int> assetReads=new Dictionary<string,int>(StringComparer.Ordinal);
        bool reading,subscribed,disposed;int dirty=1;DateTime nextRead;
        internal int SnapshotReads;internal bool Interactive;internal Action Updated;internal Action<List<NotificationEntry>> SnapshotReady;
        internal Func<List<AppEntry>> AppCatalog;List<AppEntry> iconCatalog;
        void Changed(UserNotificationListener sender,UserNotificationChangedEventArgs args){Interlocked.Exchange(ref dirty,1);var updated=Updated;if(!disposed&&updated!=null)updated();}
        public void Dispose(){disposed=true;if(subscribed&&listener!=null)try{listener.NotificationChanged-=Changed;}catch{}subscribed=false;ClearSnapshot();iconCatalog=null;AppCatalog=null;Updated=null;SnapshotReady=null;}
        void ClearSnapshot(){Items.Clear();icons.Clear();activators.Clear();assetReads.Clear();}
        internal static void TrimCache<T>(Dictionary<string,T> cache,HashSet<string> activeApps){foreach(var key in cache.Keys.Where(k=>!activeApps.Contains(k)).ToArray())cache.Remove(key);}
        internal void Invalidate(){Interlocked.Exchange(ref dirty,1);}
        internal void Check()
        {
            Packaged=NotificationIdentity.IsCurrent;
            if(!Packaged){Allowed=false;Status="아래 연결 버튼으로 이 PC의 Windows 알림을 연결하세요.";return;}
            try{if(listener==null)listener=UserNotificationListener.Current;if(!subscribed){try{listener.NotificationChanged+=Changed;subscribed=true;}catch{}}Permission=listener.GetAccessStatus();Allowed=Preferences.Current.NotificationIntegration&&Permission==UserNotificationListenerAccessStatus.Allowed;Status=Allowed?"Windows 알림과 실시간 동기화 중":!Preferences.Current.NotificationIntegration?"알림 연동이 꺼져 있습니다":Permission==UserNotificationListenerAccessStatus.Denied?"Windows 권한 설정에서 알림 접근을 허용해 주세요":"Windows 알림 접근 허용이 필요합니다";}
            catch(Exception e){Allowed=false;Status="Windows 알림 서비스에 연결하지 못했습니다";AppLog.Once("notification-service",e.Message);}
        }
        internal async Task Request()
        {
            Preferences.Set(p=>p.NotificationIntegration=true);Check();if(!Packaged)return;
            try {if(accessRequest==null||accessRequest.IsCompleted)accessRequest=SystemServices.Wait(listener.RequestAccessAsync());var completed=await Task.WhenAny(accessRequest,Task.Delay(15000));if(completed!=accessRequest){Status="Windows 확인창을 기다리는 중입니다. 보이지 않으면 권한 설정에서 접근을 허용해 주세요.";return;}Permission=await accessRequest;Check();if(Allowed){Invalidate();await Refresh();}}
            catch(Exception e){Status="알림 접근 요청을 완료하지 못했습니다";AppLog.Write("notification-access",e.Message);}
        }
        internal async Task Refresh()
        {
            var app=System.Windows.Application.Current;
            if(app!=null&&!app.Dispatcher.CheckAccess()) {if(app.Dispatcher.HasShutdownStarted)return;await app.Dispatcher.InvokeAsync(new Func<Task>(Refresh)).Task.Unwrap();return;}
            if(reading||disposed)return;if(Interlocked.Exchange(ref dirty,0)==0&&DateTime.UtcNow<nextRead)return;nextRead=DateTime.UtcNow.AddSeconds(Interactive?3:30);Check();if(!Allowed){ClearSnapshot();return;}reading=true;
            try {
                var entries=await SystemServices.Wait(listener.GetNotificationsAsync(NotificationKinds.Toast));if(disposed)return;SnapshotReads++;
                if(!Preferences.Current.NotificationIntegration||listener.GetAccessStatus()!=UserNotificationListenerAccessStatus.Allowed){Allowed=false;ClearSnapshot();return;}
                var result=new List<NotificationEntry>();var catalog=AppCatalog!=null?AppCatalog():new List<AppEntry>();if(iconCatalog!=catalog){foreach(var key in icons.Where(p=>p.Value==null).Select(p=>p.Key).ToArray())icons.Remove(key);iconCatalog=catalog;}
                foreach(var item in entries) {
                    try {
                    var binding=item.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);if(binding==null)continue;
                    var text=binding.GetTextElements().Select(t=>t.Text).ToArray();
                    uint notificationId=item.Id;string appId=item.AppInfo.AppUserModelId??item.AppInfo.DisplayInfo.DisplayName;ImageSource icon;
                    if(!icons.TryGetValue(appId,out icon)){icon=await Logo(item.AppInfo);var bitmap=icon as BitmapSource;if(bitmap==null||Math.Min(bitmap.PixelWidth,bitmap.PixelHeight)<128){var known=catalog.FirstOrDefault(a=>String.Equals(a.AppID,appId,StringComparison.OrdinalIgnoreCase))??catalog.FirstOrDefault(a=>String.Equals(a.Name,item.AppInfo.DisplayInfo.DisplayName,StringComparison.OrdinalIgnoreCase));if(known!=null)icon=NotificationIcons.Best(icon,known.Icon??await BackgroundWork.Run(()=>AppIcons.Get(known)));}icons[appId]=icon;}
                    Guid activator;if(!activators.TryGetValue(appId,out activator)){activator=NotificationActivation.Callback(appId);activators[appId]=activator;}
                    var previous=Items.FirstOrDefault(n=>n.Id==item.Id&&n.AppId==appId&&n.Created==item.CreationTime&&n.Title==(text.FirstOrDefault()??"알림")&&n.Body==String.Join("\n",text.Skip(1)));
                    var payload=previous!=null?previous.Payload:NotificationPayload.Read(appId,text);ImageSource artwork=previous!=null?previous.Artwork:null,overrideIcon=previous!=null?previous.Icon:null;
                    string assetKey=appId+"|"+notificationId+"|"+item.CreationTime.UtcDateTime.Ticks;int attempts;assetReads.TryGetValue(assetKey,out attempts);
                    if(previous==null||artwork==null&&attempts<2){assetReads[assetKey]=attempts+1;var native=await BackgroundWork.Run(()=>NativeToastAssets.Read(appId,notificationId,text));if(native!=null&&payload!=null)native.SuppressPopup=payload.SuppressPopup;payload=native??payload;var assets=native??payload;if(assets!=null){overrideIcon=overrideIcon??await BackgroundWork.Run(()=>NativeToastAssets.Image(appId,assets.LogoSource,true));artwork=artwork??await BackgroundWork.Run(()=>NativeToastAssets.Image(appId,assets.ImageSource,false));}if(artwork==null&&BudsInfo.Toast(new NotificationEntry {AppId=appId,App=item.AppInfo.DisplayInfo.DisplayName,Title=text.FirstOrDefault(),Body=String.Join("\n",text.Skip(1))})!=null)artwork=await BackgroundWork.Run(()=>BudsDeviceData.Picture(text.FirstOrDefault()));}
                    result.Add(new NotificationEntry {Id=item.Id,App=item.AppInfo.DisplayInfo.DisplayName,AppId=appId,Icon=overrideIcon??icon,Artwork=artwork,Created=item.CreationTime,Title=text.FirstOrDefault()??"알림",Body=String.Join("\n",text.Skip(1)),Payload=payload,Activator=activator,NativeWindow=previous!=null?previous.NativeWindow:IntPtr.Zero,NativeSignature=previous!=null?previous.NativeSignature:null});
                    }catch(Exception error){AppLog.Once("notification-item",error.Message);}
                }
                if(disposed){ClearSnapshot();return;}
                Items=result.OrderByDescending(n=>n.Created).ToList();var activeApps=new HashSet<string>(Items.Select(n=>n.AppId),StringComparer.OrdinalIgnoreCase);TrimCache(icons,activeApps);TrimCache(activators,activeApps);
                var assetKeys=new HashSet<string>(Items.Select(n=>n.AppId+"|"+n.Id+"|"+n.Created.UtcDateTime.Ticks),StringComparer.Ordinal);foreach(var key in assetReads.Keys.Where(k=>!assetKeys.Contains(k)).ToArray())assetReads.Remove(key);
                var ready=SnapshotReady;if(!disposed&&ready!=null)ready(Items);
            }catch(Exception e){Allowed=false;AppLog.Once("notification-read",e.Message);Status="Windows 알림을 읽지 못했습니다";}finally{reading=false;}
        }
        static async Task<ImageSource> Logo(Windows.ApplicationModel.AppInfo app)
        {
            try {using(var stream=await SystemServices.Wait(app.DisplayInfo.GetLogo(new Windows.Foundation.Size(256,256)).OpenReadAsync()))using(var reader=new DataReader(stream)) {
                uint length=(uint)Math.Min(stream.Size,2097152UL);await SystemServices.Wait(reader.LoadAsync(length));var bytes=new byte[length];reader.ReadBytes(bytes);
                using(var data=new MemoryStream(bytes))return AppIcons.Normalize(BitmapDecode.Frozen(data,256),256);
            }}catch{return null;}
        }
        internal void Dismiss(uint id) {try{if(Allowed){listener.RemoveNotification(id);Invalidate();}}catch(Exception e){AppLog.Write("notification-dismiss",e.Message);}}
        internal void Clear() {try{if(Allowed){listener.ClearNotifications();Invalidate();}}catch(Exception e){AppLog.Write("notification-clear",e.Message);}}
    }
}
