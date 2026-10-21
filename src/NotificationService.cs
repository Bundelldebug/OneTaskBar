using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    internal sealed class NotificationService
    {
        internal List<NotificationEntry> Items=new List<NotificationEntry>();
        internal bool Allowed,Packaged;
        internal string Status="Windows 알림을 연결하려면 접근을 허용해 주세요";
        UserNotificationListener listener;
        readonly Dictionary<string,ImageSource> icons=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,Guid> activators=new Dictionary<string,Guid>(StringComparer.OrdinalIgnoreCase);
        bool reading;
        internal void Check()
        {
            try{Packaged=Windows.ApplicationModel.Package.Current!=null;}catch{Packaged=false;}
            if(!Packaged){Allowed=false;Status="Windows 알림은 아래 버튼으로 확인하세요. 자체 알림 연결에는 별도 앱 등록이 필요합니다.";return;}
            try{listener=UserNotificationListener.Current;Allowed=listener.GetAccessStatus()==UserNotificationListenerAccessStatus.Allowed;Status=Allowed?"Windows 알림과 실시간 동기화 중":"Windows 알림에 접근할 권한이 필요합니다";}
            catch(Exception e){Allowed=false;Status="Windows 알림 서비스에 연결하지 못했습니다";AppLog.Once("notification-service",e.Message);}
        }
        internal async Task Request()
        {
            Check();if(!Packaged)return;
            try {var access=await SystemServices.Wait(listener.RequestAccessAsync());Allowed=access==UserNotificationListenerAccessStatus.Allowed;await Refresh();}
            catch(Exception e){Status="알림 접근 요청을 완료하지 못했습니다";AppLog.Write("notification-access",e.Message);}
        }
        internal async Task Refresh()
        {
            var app=System.Windows.Application.Current;
            if(app!=null&&!app.Dispatcher.CheckAccess()) {if(app.Dispatcher.HasShutdownStarted)return;await app.Dispatcher.InvokeAsync(new Func<Task>(Refresh)).Task.Unwrap();return;}
            if(reading)return;Check();if(!Allowed){Items=new List<NotificationEntry>();return;}reading=true;
            try {
                var entries=await SystemServices.Wait(listener.GetNotificationsAsync(NotificationKinds.Toast));
                var result=new List<NotificationEntry>();
                foreach(var item in entries) {
                    var binding=item.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);if(binding==null)continue;
                    var text=binding.GetTextElements().Select(t=>t.Text).ToArray();
                    string appId=item.AppInfo.AppUserModelId??item.AppInfo.DisplayInfo.DisplayName;ImageSource icon;
                    if(!icons.TryGetValue(appId,out icon)){icon=await Logo(item.AppInfo);icons[appId]=icon;}
                    Guid activator;if(!activators.TryGetValue(appId,out activator)){activator=NotificationActivation.Callback(appId);activators[appId]=activator;}
                    var previous=Items.FirstOrDefault(n=>n.Id==item.Id&&n.AppId==appId&&n.Title==(text.FirstOrDefault()??"알림")&&n.Body==String.Join("\n",text.Skip(1)));
                    result.Add(new NotificationEntry {Id=item.Id,App=item.AppInfo.DisplayInfo.DisplayName,AppId=item.AppInfo.AppUserModelId,Icon=icon,Created=item.CreationTime,Title=text.FirstOrDefault()??"알림",Body=String.Join("\n",text.Skip(1)),Payload=previous!=null&&previous.Payload!=null?previous.Payload:NotificationPayload.Read(appId,text),Activator=activator});
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
        internal void Dismiss(uint id) {try{if(Allowed)listener.RemoveNotification(id);}catch(Exception e){AppLog.Write("notification-dismiss",e.Message);}}
        internal void Clear() {try{if(Allowed)listener.ClearNotifications();}catch(Exception e){AppLog.Write("notification-clear",e.Message);}}
    }
}
