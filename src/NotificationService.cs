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
        internal uint Id;internal string App,AppId,Title,Body;internal DateTimeOffset Created;internal ImageSource Icon;internal NotificationPayload Payload;internal Guid Activator;
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
        bool reading,subscribed,disposed;int dirty=1;DateTime nextRead;
        internal int SnapshotReads;
        void Changed(UserNotificationListener sender,UserNotificationChangedEventArgs args){Interlocked.Exchange(ref dirty,1);}
        public void Dispose(){disposed=true;if(subscribed&&listener!=null)try{listener.NotificationChanged-=Changed;}catch{}subscribed=false;}
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
            if(reading||disposed)return;if(Interlocked.Exchange(ref dirty,0)==0&&DateTime.UtcNow<nextRead)return;nextRead=DateTime.UtcNow.AddSeconds(3);Check();if(!Allowed){if(Items.Count>0)Items=new List<NotificationEntry>();return;}reading=true;
            try {
                var entries=await SystemServices.Wait(listener.GetNotificationsAsync(NotificationKinds.Toast));SnapshotReads++;
                if(!Preferences.Current.NotificationIntegration||listener.GetAccessStatus()!=UserNotificationListenerAccessStatus.Allowed){Allowed=false;Items=new List<NotificationEntry>();return;}
                var result=new List<NotificationEntry>();
                foreach(var item in entries) {
                    try {
                    var binding=item.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);if(binding==null)continue;
                    var text=binding.GetTextElements().Select(t=>t.Text).ToArray();
                    string appId=item.AppInfo.AppUserModelId??item.AppInfo.DisplayInfo.DisplayName;ImageSource icon;
                    if(!icons.TryGetValue(appId,out icon)){icon=await Logo(item.AppInfo);icons[appId]=icon;}
                    Guid activator;if(!activators.TryGetValue(appId,out activator)){activator=NotificationActivation.Callback(appId);activators[appId]=activator;}
                    var previous=Items.FirstOrDefault(n=>n.Id==item.Id&&n.AppId==appId&&n.Title==(text.FirstOrDefault()??"알림")&&n.Body==String.Join("\n",text.Skip(1)));
                    result.Add(new NotificationEntry {Id=item.Id,App=item.AppInfo.DisplayInfo.DisplayName,AppId=item.AppInfo.AppUserModelId,Icon=icon,Created=item.CreationTime,Title=text.FirstOrDefault()??"알림",Body=String.Join("\n",text.Skip(1)),Payload=previous!=null&&previous.Payload!=null?previous.Payload:NotificationPayload.Read(appId,text),Activator=activator});
                    }catch(Exception error){AppLog.Once("notification-item",error.Message);}
                }
                Items=result.OrderByDescending(n=>n.Created).ToList();
            }catch(Exception e){AppLog.Once("notification-read",e.Message);Status="Windows 알림을 읽지 못했습니다";}finally{reading=false;}
        }
        static async Task<ImageSource> Logo(Windows.ApplicationModel.AppInfo app)
        {
            try {using(var stream=await SystemServices.Wait(app.DisplayInfo.GetLogo(new Windows.Foundation.Size(96,96)).OpenReadAsync()))using(var reader=new DataReader(stream)) {
                uint length=(uint)Math.Min(stream.Size,2097152UL);await SystemServices.Wait(reader.LoadAsync(length));var bytes=new byte[length];reader.ReadBytes(bytes);
                using(var data=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=96;image.StreamSource=data;image.EndInit();image.Freeze();return AppIcons.Normalize(image);}
            }}catch{return null;}
        }
        internal void Dismiss(uint id) {try{if(Allowed){listener.RemoveNotification(id);Invalidate();}}catch(Exception e){AppLog.Write("notification-dismiss",e.Message);}}
        internal void Clear() {try{if(Allowed){listener.ClearNotifications();Invalidate();}}catch(Exception e){AppLog.Write("notification-clear",e.Message);}}
    }
}
