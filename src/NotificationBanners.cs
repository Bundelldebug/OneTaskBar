using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    // Snapshot differences are consumed once. There is no polling while the banner stack is empty.
    internal sealed class NotificationBanners : IDisposable
    {
        [DllImport("shell32.dll")]static extern int SHQueryUserNotificationState(out int state);
        readonly ShellController shell;
        readonly NativeToastGate gate;
        readonly List<NotificationBanner> visible=new List<NotificationBanner>();
        readonly HashSet<string> known=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,NotificationEntry> pending=new Dictionary<string,NotificationEntry>();
        readonly List<NativeToastView> presentations=new List<NativeToastView>();
        readonly Dictionary<string,DateTime> nativeSeen=new Dictionary<string,DateTime>();
        readonly DispatcherTimer expiry=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(500)};
        bool baseline,enabled,disposed,nativeReading,draining;int revision;IntPtr queuedNative;
        internal int Shown {get;private set;}internal int VisibleCount {get{return visible.Count;}}
        internal int NativeSuppressed {get{return gate==null?0:gate.Suppressed;}}
        internal NotificationBanner Last {get{return visible.LastOrDefault();}}
        internal object State {get{return new {enabled=Enabled,baseline=baseline,pending=pending.Count,presentations=presentations.Count,readingNative=nativeReading,gate=gate!=null?gate.State:null};}}
        internal bool TimerActive {get{return expiry.IsEnabled;}}
        internal bool Enabled {get{return !disposed&&shell.Desktop&&!shell.IsDisposing&&shell.Notifications.Allowed&&Preferences.Current.NotificationPopups;}}
        internal NotificationBanners(ShellController owner)
        {
            shell=owner;if(!owner.Demo){gate=new NativeToastGate(()=>Enabled&&baseline);gate.Presented=NativePresented;}
            expiry.Tick+=(s,e)=>Expire();shell.Notifications.SnapshotReady=Observe;
        }
        internal void Refresh()
        {
            bool current=Enabled;if(current!=enabled){enabled=current;baseline=false;++revision;known.Clear();pending.Clear();presentations.Clear();nativeSeen.Clear();Clear();if(gate!=null)gate.Refresh();}
        }
        internal static string Key(NotificationEntry item){return item.AppId+"|"+item.Id;}
        internal static bool Fresh(NotificationEntry item,DateTimeOffset now){return item!=null&&(now-item.Created).TotalSeconds>=-5&&(now-item.Created).TotalSeconds<=25&&!(item.Payload!=null&&item.Payload.SuppressPopup);}
        internal static bool AppAllows(string appId)
        {
            try {
                using(var global=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\PushNotifications")){if(global!=null&&Convert.ToInt32(global.GetValue("ToastEnabled",1))==0)return false;}
                if(String.IsNullOrWhiteSpace(appId)||appId.IndexOfAny(new[]{'\\','/'})>=0)return true;
                using(var app=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\"+appId)){return app==null||Convert.ToInt32(app.GetValue("Enabled",1))!=0&&Convert.ToInt32(app.GetValue("ShowBanners",1))!=0;}
            }catch{return true;}
        }
        internal void Observe(List<NotificationEntry> items)
        {
            Refresh();if(!Enabled)return;
            var current=new HashSet<string>(items.Select(Key),StringComparer.Ordinal);var fresh=items.Where(n=>!known.Contains(Key(n))&&Fresh(n,DateTimeOffset.Now)).OrderByDescending(n=>n.Created).Take(3).Reverse().ToList();
            foreach(var banner in visible.ToArray())if(!current.Contains(Key(banner.Entry)))Close(banner,false);
            known.Clear();known.UnionWith(current);if(!baseline){baseline=true;if(gate!=null)gate.Refresh();return;}
            foreach(string key in pending.Keys.Where(k=>!current.Contains(k)||!Fresh(pending[k],DateTimeOffset.Now)).ToArray())pending.Remove(key);
            foreach(var item in fresh)pending[Key(item)]=item;while(pending.Count>12)pending.Remove(pending.OrderBy(p=>p.Value.Created).First().Key);Drain();
        }
        // The listener history does not preserve SuppressPopup reliably. A popup
        // must also have appeared in the native toast view before it can be replaced.
        async void NativePresented(IntPtr window)
        {
            if(nativeReading){queuedNative=window;return;}nativeReading=true;int token=revision;
            try {
                await Task.Delay(45);var views=await BackgroundWork.Run(()=>NativeToastView.Read(window));
                if(views.Count==0){await Task.Delay(100);views=await BackgroundWork.Run(()=>NativeToastView.Read(window));}
                if(disposed||token!=revision||!Enabled)return;
                if(views.Count==0){if(gate!=null)gate.Fallback(window);return;}
                foreach(string key in nativeSeen.Where(p=>(DateTime.UtcNow-p.Value).TotalSeconds>40).Select(p=>p.Key).ToArray())nativeSeen.Remove(key);
                foreach(var view in views)if(!nativeSeen.ContainsKey(view.Signature)){nativeSeen[view.Signature]=DateTime.UtcNow;presentations.Add(view);}
                while(nativeSeen.Count>64)nativeSeen.Remove(nativeSeen.OrderBy(p=>p.Value).First().Key);while(presentations.Count>8)presentations.RemoveAt(0);Drain();
            }catch(Exception error){AppLog.Once("notification-native-view",error.Message);if(!disposed&&gate!=null)gate.Fallback(window);}
            finally {nativeReading=false;var again=queuedNative;queuedNative=IntPtr.Zero;if(again!=IntPtr.Zero&&Enabled)NativePresented(again);}
        }
        async void Drain()
        {
            if(draining||!Enabled)return;presentations.RemoveAll(v=>(DateTime.UtcNow-v.Created).TotalSeconds>10);
            if(!presentations.Any(v=>pending.Values.Any(v.Matches)))return;draining=true;int token=revision;
            try {
                var quiet=await shell.Toggles.Disturb.RefreshAsync();if(disposed||token!=revision||!Enabled)return;
                int state;bool allowed=shell.DisplayAwake&&quiet.Available&&!quiet.Enabled&&!(shell.Center!=null&&shell.Center.IsVisible)&&SHQueryUserNotificationState(out state)==0&&state==5;
                foreach(var view in presentations.ToArray()){
                    var item=pending.Values.Where(view.Matches).OrderByDescending(n=>String.Equals(n.App,view.App,StringComparison.OrdinalIgnoreCase)).ThenByDescending(n=>n.Created).FirstOrDefault();if(item==null)continue;
                    item.NativeWindow=view.Window;item.NativeSignature=view.Signature;pending.Remove(Key(item));presentations.Remove(view);if(allowed&&Fresh(item,DateTimeOffset.Now)&&AppAllows(item.AppId))Show(item);
                }
            }catch(Exception error){AppLog.Once("notification-banner",error.Message);}finally{draining=false;}
        }
        internal void Show(NotificationEntry item)
        {
            if(disposed)return;var existing=visible.FirstOrDefault(b=>Key(b.Entry)==Key(item));if(existing!=null){existing.Update(item);existing.Expires=DateTime.UtcNow.AddSeconds(7);return;}
            while(visible.Count>=3)Close(visible[0],false);
            var banner=new NotificationBanner(item,Open,Close);visible.Add(banner);Shown++;Layout();banner.Pop();expiry.Start();
        }
        void Layout()
        {
            if(visible.Count==0)return;var screen=Forms.Screen.PrimaryScreen??Forms.Screen.FromPoint(Forms.Cursor.Position);double scale=visible[0].IsVisible?Math.Max(96,Native.GetDpiForWindow(new WindowInteropHelper(visible[0]).Handle))/96.0:ShellController.MonitorScale(screen,visible[0]),width=Math.Min(370,screen.WorkingArea.Width/scale-24),y=Math.Max(12,(screen.WorkingArea.Top-screen.Bounds.Top)/scale+12);
            foreach(var banner in visible){double h=banner.DesiredHeight;if(y+h>screen.WorkingArea.Height/scale-24){Close(banner,false);break;}banner.ShowAt(screen.Bounds,(screen.WorkingArea.Right-screen.Bounds.Left)/scale-width-12,y,width,h);y+=h+9;}
        }
        async void Open(NotificationBanner banner)
        {
            if(banner.Opening)return;banner.Opening=true;var entry=banner.Entry;Close(banner,true);
            try {var result=await Task.Run(()=>NotificationActivation.Open(entry,false));if(result.Invoked&&result.Exact)shell.Notifications.Dismiss(entry.Id);}
            catch(Exception error){AppLog.Write("notification-banner-open",error.Message);}
        }
        void Expire()
        {
            foreach(var banner in visible.ToArray()){if(banner.IsMouseOver||banner.IsKeyboardFocusWithin){banner.Expires=DateTime.UtcNow.AddSeconds(2);continue;}if(DateTime.UtcNow>=banner.Expires)Close(banner,true);}if(visible.Count==0)expiry.Stop();
        }
        internal async void Close(NotificationBanner banner,bool animate)
        {
            if(!visible.Remove(banner))return;if(visible.Count==0)expiry.Stop();
            if(animate)await banner.DismissAsync();if(!banner.ClosedFlag)banner.Close();if(!disposed)Layout();
        }
        internal void Clear(){foreach(var banner in visible.ToArray())Close(banner,false);expiry.Stop();}
        public void Dispose(){if(disposed)return;disposed=true;++revision;shell.Notifications.SnapshotReady=null;Clear();if(gate!=null)gate.Dispose();}
    }
    internal sealed class NotificationBanner : GlassWindow
    {
        readonly TextBlock sender=Paint.TextBlock("",11,Paint.Muted),title=Paint.TextBlock("",14,Paint.Text),body=Paint.TextBlock("",12,Paint.Muted);
        readonly Border icon=new Border {Width=35,Height=35,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,4,10,0)};
        readonly Image artwork=new Image {Height=118,Stretch=Stretch.Uniform,Margin=new Thickness(0,9,0,0),Visibility=Visibility.Collapsed};
        readonly ColumnDefinition iconColumn=new ColumnDefinition();
        internal bool HasIcon {get{return icon.Visibility==Visibility.Visible;}}
        internal bool HasArtwork {get{return artwork.Visibility==Visibility.Visible;}}
        internal NotificationEntry Entry;internal DateTime Expires;internal bool Opening,ClosedFlag;
        internal double DesiredHeight {get{return (String.IsNullOrWhiteSpace(Entry.Body)?98:126)+(Entry.Artwork!=null?127:0);}}
        internal NotificationBanner(NotificationEntry entry,Action<NotificationBanner> open,Action<NotificationBanner,bool> close)
        {
            Title="One TaskBar · 알림 팝업";ShowActivated=false;Focusable=false;Radius=27;StableSurface=true;Dark=true;Surface.CornerRadius=new CornerRadius(Radius);Surface.Background=QuickPanel.Glass;Surface.BorderBrush=Paint.Brush("#36FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var content=new Grid();content.ColumnDefinitions.Add(iconColumn);content.ColumnDefinitions.Add(new ColumnDefinition());content.Children.Add(icon);
            var text=new StackPanel();sender.TextTrimming=TextTrimming.CharacterEllipsis;text.Children.Add(sender);title.FontWeight=FontWeights.SemiBold;title.TextWrapping=TextWrapping.Wrap;title.TextTrimming=TextTrimming.CharacterEllipsis;title.MaxHeight=39;title.Margin=new Thickness(0,5,12,3);text.Children.Add(title);body.TextWrapping=TextWrapping.Wrap;body.TextTrimming=TextTrimming.CharacterEllipsis;body.MaxHeight=34;body.Margin=new Thickness(0,0,4,0);text.Children.Add(body);Grid.SetColumn(text,1);content.Children.Add(text);
            text.Children.Add(artwork);var row=new Grid();var hit=Paint.Button(content,()=>open(this),"알림 열기");hit.Background=Brushes.Transparent;hit.Padding=new Thickness(17,14,33,13);hit.HorizontalContentAlignment=HorizontalAlignment.Stretch;row.Children.Add(hit);var dismiss=Paint.Button(new Glyph("close",13),()=>close(this,true),"팝업 닫기 · 알림 기록 유지");dismiss.Background=Brushes.Transparent;dismiss.Width=dismiss.Height=28;dismiss.Padding=new Thickness(6);dismiss.HorizontalAlignment=HorizontalAlignment.Right;dismiss.VerticalAlignment=VerticalAlignment.Top;dismiss.Margin=new Thickness(0,7,7,0);row.Children.Add(dismiss);Surface.Child=row;TransitionContent=row;
            SourceInitialized+=(s,e)=> {var h=new WindowInteropHelper(this).Handle;Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)|0x08000000);};
            Closed+=(s,e)=>ClosedFlag=true;Update(entry);
        }
        internal void Update(NotificationEntry entry){Entry=entry;sender.Text=entry.App;title.Text=entry.Title;body.Text=entry.Body;body.Visibility=String.IsNullOrWhiteSpace(entry.Body)?Visibility.Collapsed:Visibility.Visible;icon.Child=entry.Icon!=null?AppIcons.Visual(entry.Icon,32):null;icon.Visibility=entry.Icon!=null?Visibility.Visible:Visibility.Collapsed;iconColumn.Width=new GridLength(entry.Icon!=null?45:0);artwork.Source=entry.Artwork;artwork.Visibility=entry.Artwork!=null?Visibility.Visible:Visibility.Collapsed;Expires=DateTime.UtcNow.AddSeconds(7);}
    }
}
