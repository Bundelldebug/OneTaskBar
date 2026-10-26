using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
namespace OneTaskBar
{
    internal sealed class BudsInfo
    {
        internal string Name,Detail="연결됨";internal ImageSource Image;internal NotificationEntry Notification;internal Guid? Container;internal bool RichImage;
        internal List<NotificationAction> Actions {get{return Notification!=null&&Notification.Payload!=null?Notification.Payload.Actions:new List<NotificationAction>();}}
        internal static bool IsBuds(string name){return Regex.IsMatch(name??"",@"Galaxy\s*Buds|\bBuds\s*(?:[0-9]|Pro|Live|FE|Plus)|버즈",RegexOptions.IgnoreCase);}
        internal static BudsInfo Toast(NotificationEntry entry)
        {
            if(entry==null)return null;string app=entry.AppId??"",sender=entry.App??"",title=entry.Title??"",body=entry.Body??"";
            bool samsung=app.IndexOf("SamsungSettings",StringComparison.OrdinalIgnoreCase)>=0||app.IndexOf("GalaxyBuds",StringComparison.OrdinalIgnoreCase)>=0||sender=="삼성 설정"||sender.Equals("Samsung Settings",StringComparison.OrdinalIgnoreCase);
            if(!samsung||!IsBuds(title+" "+body)||Regex.IsMatch(title+" "+body,@"disconnected|not connected|연결 해제|연결 끊|연결되지",RegexOptions.IgnoreCase))return null;
            bool connection=Regex.IsMatch(title+" "+body,@"connected|연결됨|연결되었|연결됐|연결되었습니다|L\s*[·:]|케이스|case|[0-9]\s*%",RegexOptions.IgnoreCase);
            if(!connection&&!(IsBuds(title)&&String.IsNullOrWhiteSpace(body)))return null;
            return new BudsInfo {Name=title,Detail=String.IsNullOrWhiteSpace(body)?"연결됨":body,Image=entry.Artwork,Notification=entry,RichImage=entry.Artwork!=null&&entry.Payload!=null&&!String.IsNullOrEmpty(entry.Payload.ImageSource)};
        }
        internal static int? Battery(IReadOnlyDictionary<string,object> properties){object raw;try{if(properties.TryGetValue("System.Devices.BatteryLife",out raw)&&raw!=null){int value=Convert.ToInt32(raw);if(value>=0&&value<=100)return value;}}catch{}return null;}
    }
    internal sealed class BudsConnectionTracker
    {
        sealed class Endpoint {internal string Name,Identity;internal bool Connected;}
        readonly Dictionary<string,Endpoint> states=new Dictionary<string,Endpoint>(StringComparer.Ordinal);
        internal bool Observe(string id,string name,bool connected,bool initial,Guid? container=null)
        {
            string normal=Normalize(name),identity=container.HasValue?container.Value.ToString("N"):normal;bool was=states.Values.Any(p=>p.Identity==identity&&p.Connected);states[id]=new Endpoint {Name=normal,Identity=identity,Connected=connected};
            return !initial&&!was&&connected&&BudsInfo.IsBuds(name);
        }
        internal bool Connected(string name,Guid? container=null){string identity=container.HasValue?container.Value.ToString("N"):Normalize(name);return states.Values.Any(p=>p.Identity==identity&&p.Connected);}
        internal void Remove(string id){states.Remove(id);}
        internal void Clear(){states.Clear();}
        internal static string Normalize(string name){return Regex.Replace(Regex.Replace(name??"",@"^(LE[_ -])","",RegexOptions.IgnoreCase),@"\s+","").ToUpperInvariant();}
    }
    // Paired endpoints only: observes OS connection changes, never starts an
    // unpaired-device discovery scan, GATT session or Bluetooth connection.
    internal sealed class BudsConnections : IDisposable
    {
        [DllImport("shell32.dll")]static extern int SHQueryUserNotificationState(out int state);
        readonly ShellController shell;readonly Dispatcher dispatcher;readonly BudsConnectionTracker tracker=new BudsConnectionTracker();
        readonly Dictionary<string,DeviceInformation> devices=new Dictionary<string,DeviceInformation>(StringComparer.Ordinal);
        readonly DispatcherTimer expiry=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(500)};
        DeviceWatcher watcher;bool baseline,disposed;int generation;DateTime retryAfter,lastShown;string lastName;
        internal BudsConnectionPopup Popup {get;private set;}
        internal int Shown {get;private set;}internal bool Watching {get{return watcher!=null;}}internal bool TimerActive {get{return expiry.IsEnabled;}}
        internal bool Enabled {get{return !disposed&&shell.Desktop&&shell.DisplayAwake&&Preferences.Current.NotificationPopups&&Preferences.Current.BudsConnectionPopups;}}
        bool ShouldWatch {get{return !disposed&&shell.Desktop&&shell.DisplayAwake;}}
        internal string ConnectedCaption {get;private set;}
        internal bool HasConnectedBuds {get;private set;}
        internal string BudsCaption {get;private set;}
        internal BudsConnections(ShellController owner){shell=owner;dispatcher=Application.Current.Dispatcher;expiry.Tick+=(s,e)=>{if(Popup==null){expiry.Stop();return;}if(Popup.IsMouseOver)Popup.Expires=DateTime.UtcNow.AddSeconds(2);else if(DateTime.UtcNow>=Popup.Expires)Hide(true);};}
        internal void Refresh()
        {
            if(!ShouldWatch){Stop();Hide(false);return;}if(!Enabled)Hide(false);if(shell.Demo||watcher!=null||DateTime.UtcNow<retryAfter)return;
            try{
                watcher=PairedWatcher();
                baseline=false;watcher.Added+=Added;watcher.Updated+=Updated;watcher.Removed+=Removed;watcher.EnumerationCompleted+=Complete;watcher.Stopped+=Stopped;watcher.Start();
            }catch(Exception error){AppLog.Once("buds-watch",error.GetType().Name);Stop();retryAfter=DateTime.UtcNow.AddMinutes(1);}
        }
        internal static DeviceWatcher PairedWatcher(){string query="("+BluetoothDevice.GetDeviceSelectorFromPairingState(true)+") OR ("+BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)+")";return DeviceInformation.CreateWatcher(query,new[]{"System.Devices.Aep.IsConnected","System.Devices.BatteryLife","System.Devices.Aep.ContainerId"},DeviceInformationKind.AssociationEndpoint);}
        void Queue(DeviceWatcher sender,Action action){if(disposed||dispatcher.HasShutdownStarted)return;try{dispatcher.BeginInvoke(new Action(()=>{if(!disposed&&watcher==sender&&ShouldWatch)action();}));}catch(InvalidOperationException){}}
        void Added(DeviceWatcher sender,DeviceInformation device){Queue(sender,()=>{if(devices.Count>=128&&!devices.ContainsKey(device.Id))return;devices[device.Id]=device;Observe(device);});}
        void Updated(DeviceWatcher sender,DeviceInformationUpdate update){Queue(sender,()=>{DeviceInformation device;if(devices.TryGetValue(update.Id,out device)){device.Update(update);Observe(device);}});}
        void Removed(DeviceWatcher sender,DeviceInformationUpdate update){Queue(sender,()=>{devices.Remove(update.Id);tracker.Remove(update.Id);UpdateCaption(devices.Values.Select(BluetoothPeer.Read));});}
        void Complete(DeviceWatcher sender,object args){Queue(sender,()=>baseline=true);}
        void Stopped(DeviceWatcher sender,object args){Queue(sender,()=>{Stop();retryAfter=DateTime.UtcNow.AddMinutes(1);});}
        async void Observe(DeviceInformation device)
        {
            object value;bool connected=device.Properties.TryGetValue("System.Devices.Aep.IsConnected",out value)&&value is bool&&(bool)value;
            Guid? identity=BluetoothPeer.ContainerId(device.Properties);UpdateCaption(devices.Values.Select(BluetoothPeer.Read));if(!tracker.Observe(device.Id,device.Name,connected,!baseline,identity)||!Enabled)return;int token=generation;string name=device.Name;
            try{await Task.Delay(400);if(token!=generation||!Enabled||!tracker.Connected(name,identity))return;
                int state;var quiet=await shell.Toggles.Disturb.RefreshAsync();if(token!=generation||!Enabled||!tracker.Connected(name,identity)||!quiet.Available||quiet.Enabled||SHQueryUserNotificationState(out state)!=0||state!=5)return;
                var peers=devices.Values.Select(BluetoothPeer.Read).Where(p=>p.Connected&&(identity.HasValue?p.Container==identity:BudsConnectionTracker.Normalize(p.Name)==BudsConnectionTracker.Normalize(name))).ToArray();if(peers.Length>0)name=BluetoothDeviceView.ConnectedCaption(peers);
                int? battery=BudsInfo.Battery(device.Properties);var info=await BackgroundWork.Run(()=>BudsDeviceData.Enrich(new BudsInfo {Name=name,Container=identity,Detail=battery.HasValue?"연결됨 · 배터리 "+battery+"%":"연결됨"}));if(token==generation&&Enabled&&tracker.Connected(name,identity))Show(info,true);
            }catch(Exception error){AppLog.Once("buds-connection",error.GetType().Name);}
        }
        internal void UpdateCaption(IEnumerable<BluetoothPeer> peers){var list=peers.ToArray();var buds=list.Where(p=>BudsInfo.IsBuds(p.Name)).ToArray();string text=BluetoothDeviceView.ConnectedCaption(list),caption=BluetoothDeviceView.ConnectedCaption(buds);bool connected=buds.Any(p=>p.Connected);if(text==ConnectedCaption&&caption==BudsCaption&&connected==HasConnectedBuds)return;ConnectedCaption=text;BudsCaption=caption;HasConnectedBuds=connected;if(shell.Quick!=null&&shell.Quick.IsVisible)shell.Quick.Refresh(shell.Power,shell.Brightness,shell.Audio.Value,shell.Audio.Muted,shell.Audio.Available,shell.Brightness>=0);}
        internal bool ShowNative(NotificationEntry entry){var info=BudsInfo.Toast(entry);if(info==null||!Enabled)return false;var device=devices.Values.FirstOrDefault(d=>BudsConnectionTracker.Normalize(d.Name)==BudsConnectionTracker.Normalize(info.Name));if(device!=null)info.Container=BluetoothPeer.ContainerId(device.Properties);if(shell.Demo)Show(info,false);else EnrichNative(info,generation);return true;}
        async void EnrichNative(BudsInfo info,int token){try{info=await BackgroundWork.Run(()=>BudsDeviceData.Enrich(info));if(token==generation&&Enabled)Show(info,false);}catch(Exception error){AppLog.Once("buds-native-data",error.GetType().Name);if(token==generation&&Enabled)Show(info,false);}}
        internal void Show(BudsInfo info,bool connection=false)
        {
            if(disposed||info==null)return;string name=BudsConnectionTracker.Normalize(info.Name);
            if(Popup==null&&!connection&&name==lastName&&(DateTime.UtcNow-lastShown).TotalSeconds<15)return;
            bool same=Popup!=null&&(BudsConnectionTracker.Normalize(Popup.Info.Name)==name||info.Container.HasValue&&Popup.Info.Container==info.Container);if(same&&Popup.Info.Notification!=null&&info.Notification==null){Popup.Expires=DateTime.UtcNow.AddSeconds(7);return;}if(Popup!=null&&!same)Hide(false);
            bool fresh=Popup==null;if(fresh){Popup=new BudsConnectionPopup(Open,()=>Hide(true),OpenAction);Shown++;}Popup.Update(info);lastName=name;lastShown=DateTime.UtcNow;
            Popup.Position();if(fresh)Popup.Appear();expiry.Start();
        }
        async void Open()
        {
            var info=Popup!=null?Popup.Info:null;Hide(true);if(info==null)return;
            if(info.Notification!=null){try{await Task.Run(()=>NotificationActivation.Open(info.Notification,false));}catch(Exception error){AppLog.Write("buds-open",error.Message);}}
            else shell.ShowDetail("bluetooth");
        }
        internal Action<NotificationEntry,NotificationAction> ActionDispatch;
        async void OpenAction(NotificationAction action){var info=Popup!=null?Popup.Info:null;if(info==null||info.Notification==null)return;Hide(true);if(ActionDispatch!=null){ActionDispatch(info.Notification,action);return;}try{await BackgroundWork.Run(()=>NotificationActivation.Action(info.Notification,action,false));}catch(Exception error){AppLog.Write("buds-action",error.Message);}}
        internal async void Hide(bool animate)
        {
            var popup=Popup;Popup=null;expiry.Stop();if(popup==null)return;if(animate)await popup.Disappear();if(!popup.ClosedFlag)popup.Close();
        }
        void Stop(){generation++;baseline=false;tracker.Clear();devices.Clear();ConnectedCaption=null;var current=watcher;watcher=null;if(current==null)return;current.Added-=Added;current.Updated-=Updated;current.Removed-=Removed;current.EnumerationCompleted-=Complete;current.Stopped-=Stopped;try{if(current.Status==DeviceWatcherStatus.Started||current.Status==DeviceWatcherStatus.EnumerationCompleted)current.Stop();}catch{}}
        public void Dispose(){if(disposed)return;disposed=true;Stop();Hide(false);}
    }
}
