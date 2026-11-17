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
            // The small bottom popups do not wait for the native toast to be seen: some of these apps post history-only toasts.
            var direct=baseline?items.Where(n=>!known.Contains(Key(n))&&IsGalaxyConnect(n)&&(DateTimeOffset.Now-n.Created).TotalSeconds>=-5&&(DateTimeOffset.Now-n.Created).TotalSeconds<=25).OrderBy(n=>n.Created).Take(3).ToList():new List<NotificationEntry>();
            if(shell.Island!=null)shell.Island.RetainNotification(current);
            foreach(var banner in visible.ToArray())if(!banner.Retain(current))Close(banner,false);Layout();
            known.Clear();known.UnionWith(current);if(!baseline){baseline=true;if(gate!=null)gate.Refresh();return;}
            foreach(string key in pending.Keys.Where(k=>!current.Contains(k)||!Fresh(pending[k],DateTimeOffset.Now)).ToArray())pending.Remove(key);
            foreach(var item in fresh)pending[Key(item)]=item;while(pending.Count>12)pending.Remove(pending.OrderBy(p=>p.Value.Created).First().Key);Drain();
            foreach(var item in direct)ShowCompact(item);
        }
        async void ShowCompact(NotificationEntry item)
        {
            int token=revision;
            try
            {
                await Task.Delay(1500);
                if(disposed||token!=revision||!Enabled||!pending.Remove(Key(item)))return;
                var quiet=await shell.Toggles.Disturb.RefreshAsync();
                if(disposed||token!=revision||!Enabled)return;
                int state;
                bool allowed=shell.DisplayAwake&&quiet.Available&&!quiet.Enabled&&!(shell.Center!=null&&shell.Center.IsVisible)&&SHQueryUserNotificationState(out state)==0&&state==5;
                if(allowed&&AppAllows(item.AppId))Show(item);
            }
            catch(Exception error){AppLog.Once("notification-compact",error.Message);}
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
            if(disposed)return;
            shell.NotificationSound.Notify(item);
            bool connect=IsGalaxyConnect(item);
            if(!connect&&(shell.Buds.ShowNative(item)||shell.IslandNotification(item,OpenEntry)))return;
            var existing=visible.FirstOrDefault(b=>String.Equals(NotificationStack.AppKey(b.Entry),NotificationStack.AppKey(item),StringComparison.OrdinalIgnoreCase));
            if(existing!=null){existing.Update(item);Layout();return;}
            while(visible.Count>=3)Close(visible[0],false);
            var banner=new NotificationBanner(item,Open,Close,connect);visible.Add(banner);Shown++;Layout();banner.DockCue=()=>GlowDock(banner);banner.Appear();expiry.Start();
        }
        // Galaxy Connect's phone notifications are small and rise from the bottom centre instead of the top-right stack.
        internal static bool IsGalaxyConnect(NotificationEntry item)
        {
            if(item==null)return false;
            string id=item.AppId??"",app=item.App??"";
            if(id.IndexOf("SamsungContinuityService",StringComparison.OrdinalIgnoreCase)>=0||app=="갤럭시 커넥트"||String.Equals(app,"Galaxy Connect",StringComparison.OrdinalIgnoreCase))return true;
            // Storage sharing and camera sharing from the phone arrive from other Samsung packages but belong in the same place.
            if(id.IndexOf("SAMSUNGELECTRONICS",StringComparison.OrdinalIgnoreCase)>=0||app.IndexOf("Samsung",StringComparison.OrdinalIgnoreCase)>=0||app.IndexOf("삼성",StringComparison.Ordinal)>=0||app.IndexOf("갤럭시",StringComparison.Ordinal)>=0)
            {
                string text=(item.Title??"")+" "+(item.Body??"");
                if(text.IndexOf("저장공간",StringComparison.Ordinal)>=0||text.IndexOf("카메라 공유",StringComparison.Ordinal)>=0||text.IndexOf("카메라를 공유",StringComparison.Ordinal)>=0||text.IndexOf("Storage sharing",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("Camera sharing",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("Share storage",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("Share camera",StringComparison.OrdinalIgnoreCase)>=0)return true;
            }
            // Only Samsung Internet's helper reports through the same small bottom popup. The browser's own notifications use the normal path.
            return id.IndexOf("SamsungAccountPluginforSa",StringComparison.OrdinalIgnoreCase)>=0||app.IndexOf("Browser Helper",StringComparison.OrdinalIgnoreCase)>=0||id.IndexOf("SamsungBrowserHelper",StringComparison.OrdinalIgnoreCase)>=0;
        }
        void Layout()
        {
            if(visible.Count==0)return;var screen=Forms.Screen.PrimaryScreen??Forms.Screen.FromPoint(Forms.Cursor.Position);double scale=visible[0].IsVisible?Math.Max(96,Native.GetDpiForWindow(new WindowInteropHelper(visible[0]).Handle))/96.0:ShellController.MonitorScale(screen,visible[0]),width=Math.Min(370,screen.WorkingArea.Width/scale-24);
            // Like Windows' own notifications, banners sit in the bottom-right corner beside the status area and stack upward.
            double x=(screen.WorkingArea.Right-screen.Bounds.Left)/scale-width-12,bottom=(screen.WorkingArea.Bottom-screen.Bounds.Top)/scale-12,top=(screen.WorkingArea.Top-screen.Bounds.Top)/scale+12;
            var dock=shell.DockFor(screen);
            Native.RectI apps;
            if(dock!=null&&dock.Apps.IsVisible&&Native.GetWindowRect(new WindowInteropHelper(dock.Apps).Handle,out apps)&&apps.Top>screen.Bounds.Top+screen.Bounds.Height/2&&apps.Right>screen.Bounds.Left+x*scale)bottom=Math.Min(bottom,(apps.Top-screen.Bounds.Top)/scale-10);
            foreach(var banner in visible.Where(b=>!b.Compact).Reverse().ToArray()){double h=banner.DesiredHeight;if(bottom-h<top){Close(banner,false);continue;}banner.PlaceBanner(screen.Bounds,x,bottom-h,width,h);bottom-=h+9;}            LayoutCompact(screen,scale);
        }
        // Before a Fluid banner rises, a glow flares on the top of the bar below it, so the card seems to leave the bar.
        // Before a Fluid banner rises, the bar piece below it flares with a glow and the card leaves from that piece.
        // Returns the piece's bounds in device pixels, or null when there is no bar to launch from.
        // Fixtures stand in for the dock with any window.
        internal static Func<Native.RectI?> BarForFixture;
        Native.RectI? GlowDock(NotificationBanner banner)
        {
            try
            {
                var screen=Forms.Screen.PrimaryScreen??Forms.Screen.FromPoint(Forms.Cursor.Position);
                if(Program.Inspectable&&BarForFixture!=null)
                {
                    var fake=BarForFixture();
                    if(fake.HasValue){GlowCue.Play(screen.Bounds,fake.Value,(fake.Value.Bottom-fake.Value.Top)/2.0,new Point(.5,0));return fake;}
                }
                var dock=shell.DockFor(screen);Native.RectI card;
                if(dock==null||!Native.GetWindowRect(new WindowInteropHelper(banner).Handle,out card))return null;
                double center=(card.Left+card.Right)/2.0;GlassWindow best=null;Native.RectI bestRect=new Native.RectI();double bestGap=Double.MaxValue;
                // The bar piece (start, apps or status) closest below the card.
                foreach(var piece in new GlassWindow[]{dock.Apps,dock.Status,dock.Start})
                {
                    Native.RectI rect;
                    if(piece==null||!piece.IsVisible||!Native.GetWindowRect(new WindowInteropHelper(piece).Handle,out rect)||rect.Right<=rect.Left)continue;
                    double gap=center<rect.Left?rect.Left-center:center>rect.Right?center-rect.Right:0;
                    if(gap<bestGap){bestGap=gap;best=piece;bestRect=rect;}
                }
                if(best==null)return null;
                double x=(center-bestRect.Left)/(bestRect.Right-bestRect.Left);
                GlowCue.Play(screen.Bounds,bestRect,best.Surface.CornerRadius.TopLeft*Native.GetDpiForWindow(new WindowInteropHelper(best).Handle)/96.0,new Point(Math.Max(.08,Math.Min(.92,x)),0));
                return bestRect;
            }
            catch(Exception error){AppLog.Once("banner-dock-glow",error.GetType().Name);return null;}
        }        void LayoutCompact(Forms.Screen screen,double scale)
        {
            double width=Math.Min(330,screen.Bounds.Width/scale-24),bottom=(screen.WorkingArea.Bottom-screen.Bounds.Top)/scale-14;
            // The dock floats over the work area, so the popup rises from just above it.
            var dock=shell.DockFor(screen);
            Native.RectI apps;
            if(dock!=null&&dock.Apps.IsVisible&&Native.GetWindowRect(new WindowInteropHelper(dock.Apps).Handle,out apps)&&apps.Top>screen.Bounds.Top+screen.Bounds.Height/2)bottom=Math.Min(bottom,(apps.Top-screen.Bounds.Top)/scale-10);
            foreach(var banner in visible.Where(b=>b.Compact).Reverse().ToArray())
            {
                double h=banner.DesiredHeight;
                if(bottom-h<12){Close(banner,false);continue;}
                banner.PlaceBanner(screen.Bounds,(screen.Bounds.Width/scale-width)/2,bottom-h,width,h);
                bottom-=h+8;
            }
        }
        async void Open(NotificationBanner banner)
        {
            if(banner.Opening)return;banner.Opening=true;var entry=banner.Entry;Close(banner,true);
            await ActivateEntry(entry);
        }
        async void OpenEntry(NotificationEntry entry)
        {
            await ActivateEntry(entry);
        }
        async Task ActivateEntry(NotificationEntry entry)
        {
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
            if(animate)await banner.Disappear();if(!banner.ClosedFlag)banner.Close();if(!disposed)Layout();
        }
        internal void Clear()
        {
            foreach(var banner in visible.ToArray())Close(banner,false);
            expiry.Stop();
            if(shell.Island!=null)shell.Island.DismissNotification();
        }
        public void Dispose(){if(disposed)return;disposed=true;GlowCue.Release();++revision;shell.Notifications.SnapshotReady=null;Clear();if(gate!=null)gate.Dispose();}
    }
    internal sealed class NotificationBanner : GlassWindow
    {
        readonly TextBlock sender=Paint.TextBlock("",11,Paint.Brush("#FFC4C7CE")),title=Paint.TextBlock("",14,Paint.Brush("#FFF6F7F9")),body=Paint.TextBlock("",12,Paint.Brush("#FFD3D6DC"));
        readonly Border glow=new Border {IsHitTestVisible=false};
        readonly Border icon=new Border {Width=35,Height=35,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,4,10,0)};
        readonly Image artwork=new Image {Height=118,Stretch=Stretch.Uniform,Margin=new Thickness(0,9,0,0),Visibility=Visibility.Collapsed};
        readonly ColumnDefinition iconColumn=new ColumnDefinition();
        readonly List<NotificationEntry> entries=new List<NotificationEntry>();
        readonly TranslateTransform rise=new TranslateTransform();readonly TransformGroup lift=new TransformGroup();readonly ScaleTransform grow=new ScaleTransform(1,1),iconPop=new ScaleTransform(1,1);Brush glassBrush;bool refracting;StackPanel textPanel;readonly Border bloom=new Border {IsHitTestVisible=false,Opacity=0};System.Windows.Documents.AdornerLayer adorners;
        // The Fluid banner arrives in stages, as in the GalaxyOS clip: the frosted card swells in, the icon pops, then the words fade in.
        const double StageMilliseconds=560;
        bool moving;readonly System.Diagnostics.Stopwatch motionWatch=new System.Diagnostics.Stopwatch();
        System.Drawing.Rectangle monitor;Rect bounds;double progress=1,from,to;TaskCompletionSource<bool> completion;
        internal bool Animating {get{return moving;}}
        // Frames follow the display refresh, like the Dynamic Island, instead of a 16 ms timer that drifts against it.
        void StartMotion(){if(moving)return;moving=true;System.Windows.Media.CompositionTarget.Rendering+=Frame;}
        void StopMotion(){if(!moving)return;moving=false;System.Windows.Media.CompositionTarget.Rendering-=Frame;}
        internal int StackCount {get{return entries.Count;}}
        internal bool HasIcon {get{return icon.Visibility==Visibility.Visible;}}
        internal bool HasArtwork {get{return artwork.Visibility==Visibility.Visible;}}
        internal NotificationEntry Entry;internal DateTime Expires;internal bool Opening,ClosedFlag;
        internal double DesiredHeight {get{return Compact?(String.IsNullOrWhiteSpace(Entry.Body)?54:68):(String.IsNullOrWhiteSpace(Entry.Body)?98:126)+(Entry.Artwork!=null?127:0);}}
        internal readonly bool Compact;
        internal NotificationBanner(NotificationEntry entry,Action<NotificationBanner> open,Action<NotificationBanner,bool> close,bool compact=false)
        {
            Compact=compact;
            Title="One TaskBar · 알림 팝업";ShowActivated=false;Focusable=false;Radius=compact?22:27;StableSurface=true;Dark=true;Surface.CornerRadius=new CornerRadius(Radius);Surface.Background=Paint.Brush("#E6101216");Surface.BorderBrush=Paint.Brush("#36FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var content=new Grid();content.ColumnDefinitions.Add(iconColumn);content.ColumnDefinitions.Add(new ColumnDefinition());content.Children.Add(icon);
            var text=new StackPanel();textPanel=text;icon.RenderTransform=iconPop;icon.RenderTransformOrigin=new Point(.5,.5);sender.TextTrimming=TextTrimming.CharacterEllipsis;text.Children.Add(sender);title.FontWeight=FontWeights.SemiBold;title.TextWrapping=TextWrapping.Wrap;title.TextTrimming=TextTrimming.CharacterEllipsis;title.MaxHeight=39;title.Margin=new Thickness(0,5,12,3);text.Children.Add(title);body.TextWrapping=TextWrapping.Wrap;body.TextTrimming=TextTrimming.CharacterEllipsis;body.MaxHeight=34;body.Margin=new Thickness(0,0,4,0);text.Children.Add(body);Grid.SetColumn(text,1);content.Children.Add(text);
            text.Children.Add(artwork);var row=new Grid();var hit=Paint.Button(content,()=>open(this),"알림 열기");hit.Background=Brushes.Transparent;hit.Padding=compact?new Thickness(14,9,30,9):new Thickness(17,14,33,13);hit.HorizontalContentAlignment=HorizontalAlignment.Stretch;glow.CornerRadius=new CornerRadius(Radius);row.Children.Add(glow);row.Children.Add(hit);bloom.CornerRadius=new CornerRadius(Radius);bloom.Background=BloomBrush();row.Children.Add(bloom);var dismiss=Paint.Button(new Glyph("close",13),()=>close(this,true),"팝업 닫기 · 알림 기록 유지");dismiss.Background=Brushes.Transparent;dismiss.Width=dismiss.Height=28;dismiss.Padding=new Thickness(6);dismiss.HorizontalAlignment=HorizontalAlignment.Right;dismiss.VerticalAlignment=VerticalAlignment.Top;dismiss.Margin=new Thickness(0,7,7,0);row.Children.Add(dismiss);Surface.Child=row;TransitionContent=row;
            SourceInitialized+=(s,e)=> {var h=new WindowInteropHelper(this).Handle;Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)|0x08000000);};
            if(compact)
            {
                sender.Visibility=Visibility.Collapsed;
                hit.HorizontalContentAlignment=HorizontalAlignment.Center;hit.VerticalContentAlignment=VerticalAlignment.Center;content.VerticalAlignment=VerticalAlignment.Center;text.VerticalAlignment=VerticalAlignment.Center;title.TextAlignment=body.TextAlignment=TextAlignment.Center;
                title.FontSize=13;title.TextWrapping=TextWrapping.NoWrap;title.MaxHeight=20;title.Margin=new Thickness(0,1,8,1);
                body.FontSize=11.5;body.TextWrapping=TextWrapping.NoWrap;body.MaxHeight=18;
                icon.Width=icon.Height=28;icon.VerticalAlignment=VerticalAlignment.Center;icon.Margin=new Thickness(0,0,9,0);
            }
            Closed+=(s,e)=>{ClosedFlag=true;if(cueTimer!=null)cueTimer.Stop();StopMotion();motionWatch.Stop();if(completion!=null)completion.TrySetResult(false);};Update(entry);
        }
        internal void PlaceBanner(System.Drawing.Rectangle screen,double x,double y,double width,double height)
        {
            monitor=screen;var next=new Rect(x,y,width,height);if(!IsVisible){bounds=next;Refract(screen,x,y,width,height);ShowAt(screen,x,y,width,height);if(glassBrush!=null)Surface.Background=glassBrush;}else if(bounds!=next){bounds=next;Width=width;Height=height;Position();}
        }
        // With the refraction option on, the surface is drawn from a one-time read of what is behind it.
        void Refract(System.Drawing.Rectangle screen,double x,double y,double width,double height)
        {
            glassBrush=null;refracting=false;
            if(!RefractionGlass.Enabled||Compact)return;
            try
            {
                double scale=Native.PrepareMonitor(this,screen);var rect=Native.PlacementBounds(screen,scale,x,y,width,height);
                var surface=new System.Drawing.Rectangle(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
                System.Drawing.Rectangle area;
                using(var backdrop=RefractionGlass.Capture(surface,(int)Math.Ceiling(RefractionGlass.Margin*scale),out area))
                {
                    var image=RefractionGlass.Render(backdrop,area,surface,Radius*scale,scale,true);
                    if(image==null)return;
                    var brush=new ImageBrush(image){Stretch=Stretch.Fill};brush.Freeze();glassBrush=brush;refracting=true;
                }
            }
            catch(Exception error){AppLog.Once("banner-refraction",error.GetType().Name);}
        }
        internal bool Refracting {get{return refracting;}}
        internal override void RefreshGlassMaterial(){base.RefreshGlassMaterial();if(glassBrush!=null)Surface.Background=glassBrush;}
        // Runs just before the banner rises (the dock glow); set by the banner list.
        internal Func<Native.RectI?> DockCue;
        // Where the card starts when it leaves the bar: its offset from the final place (dip) and its starting size.
        double launchX,launchY,startAlong=.34,startAcross=.3;bool launched;
        System.Windows.Threading.DispatcherTimer cueTimer;
        // The card starts as the bar piece itself (its size, centred on it) and springs up to its place.
        void SetLaunch(Native.RectI? bar)
        {
            launched=false;launchX=launchY=0;startAlong=.34;startAcross=.3;
            if(!bar.HasValue||monitor.Width<1)return;
            double scale=Math.Max(96,Native.GetDpiForWindow(new WindowInteropHelper(this).Handle))/96.0;
            double barW=bar.Value.Right-bar.Value.Left,barH=bar.Value.Bottom-bar.Value.Top,cardW=bounds.Width*scale,cardH=bounds.Height*scale;
            if(barW<8||barH<8||cardW<8||cardH<8)return;
            startAlong=Math.Max(.3,Math.Min(.95,barW/cardW));startAcross=Math.Max(.25,Math.Min(.8,barH/cardH));
            double barCx=(bar.Value.Left+bar.Value.Right)/2.0,barCy=(bar.Value.Top+bar.Value.Bottom)/2.0;
            double cardCx=monitor.Left+(bounds.X+bounds.Width/2)*scale,cardBottom=monitor.Top+(bounds.Y+bounds.Height)*scale;
            launchX=(barCx-cardCx)/scale;launchY=(barCy+startAcross*cardH/2-cardBottom)/scale;launched=true;
        }
        internal void Appear()
        {
            if(DockCue==null||Compact||!Fluid.Enabled||!Motion.Enabled||!IsVisible){Animate(true);return;}
            // Glow first, then the card rises out of it.
            SetLaunch(DockCue());progress=0;Position();
            if(cueTimer==null){cueTimer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(70)};cueTimer.Tick+=(s,e)=>{cueTimer.Stop();if(!ClosedFlag)Animate(true);};}
            cueTimer.Stop();cueTimer.Start();
        }
        internal Task<bool> Disappear(){return Animate(false);}
        Task<bool> Animate(bool opening)
        {
            if(completion!=null)completion.TrySetResult(false);completion=new TaskCompletionSource<bool>();var pending=completion.Task;StopMotion();from=opening?0:progress;to=opening?1:0;if(opening&&Fluid.Enabled&&!Compact&&Motion.Enabled)Stage(0);else ResetStage();
            if(!Motion.Enabled||!IsVisible){ResetStage();progress=to;Position();completion.TrySetResult(true);return pending;}progress=from;Position();motionWatch.Restart();StartMotion();return pending;
        }
        void Frame(object sender,EventArgs args)
        {
            // The Fluid UI arrives on the island's spring, settling a little past its place; closing is quick and does not bounce.
            bool fluid=Fluid.Enabled&&to==1,staged=fluid&&!Compact&&Motion.Enabled;
            double ms=motionWatch.Elapsed.TotalMilliseconds;
            double t=Math.Min(1,ms/(fluid?360:to==1?280:160)),ease=fluid?SpringEase.Position(t,launched?.62:.8):SpringEase.Out(t);
            progress=from+(to-from)*ease;Position();
            if(staged)Stage(ms);
            if(t>=1&&(!staged||ms>=StageMilliseconds)){StopMotion();motionWatch.Stop();progress=to;Position();if(staged)Stage(StageMilliseconds);completion.TrySetResult(true);}
        }
        static Brush BloomBrush()
        {
            var brush=new RadialGradientBrush {Center=new Point(.5,1),GradientOrigin=new Point(.5,1),RadiusX=.75,RadiusY=1.1};
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(235,255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(120,214,232,255),.45));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0,214,232,255),1));
            brush.Freeze();return brush;
        }
        static double Part(double ms,double start,double length){return Math.Max(0,Math.Min(1,(ms-start)/length));}
        // Icon pops in on a spring after the card is up; the title and body fade in last.
        void Stage(double ms)
        {
            double pop=SpringEase.Position(Part(ms,70,380),.72);
            iconPop.ScaleX=iconPop.ScaleY=.3+.7*pop;
            icon.Opacity=Part(ms,50,110);
            // A white glow flares from the pill while the card swells and drains away as it settles.
            double flare=Part(ms,0,90)*(1-Part(ms,90,420));bloom.Opacity=flare;
            if(textPanel!=null)textPanel.Opacity=Part(ms,220,260);
        }
        void ResetStage()
        {
            iconPop.ScaleX=iconPop.ScaleY=1;icon.Opacity=1;bloom.Opacity=0;if(textPanel!=null)textPanel.Opacity=1;
        }        void Position()
        {
            double shown=Math.Max(0,Math.Min(1,progress));
            // The Fluid banner grows out of its bottom-right corner with the spring, then shrinks back the same way.
            bool grows=Fluid.Enabled&&!Compact&&Motion.Enabled;
            Opacity=grows?Math.Min(1,shown*1.7):shown;
            Native.BlurOpacity(this,refracting?0:grows?shown*shown*shown:shown);
            if(grows)
            {
                double along=Math.Max(.05,startAlong+(1-startAlong)*progress),across=Math.Max(.05,startAcross+(1-startAcross)*progress),back=1-Math.Min(1,progress);grow.ScaleX=along;grow.ScaleY=across;
                // The card springs out of a small pill at its lower edge, as in the GalaxyOS clip.
                rise.Y=(1-Math.Min(1,progress))*26;if(lift.Children.Count==0){lift.Children.Add(grow);lift.Children.Add(rise);}
                Surface.RenderTransformOrigin=new Point(.5,1);Surface.RenderTransform=lift;
                // The rim adorner keeps the transform it was laid out with unless it is told the surface changed.
                if(adorners==null)adorners=System.Windows.Documents.AdornerLayer.GetAdornerLayer(Surface);
                if(adorners!=null)adorners.Update(Surface);
                Native.Place(this,monitor,bounds.X+back*launchX,bounds.Y+back*launchY,bounds.Width,bounds.Height);return;
            }
            if(Surface.RenderTransform==lift){Surface.RenderTransform=Transform.Identity;if(adorners!=null)adorners.Update(Surface);}
            PositionSlide();
        }
        void PositionSlide(){if(Compact)Native.Place(this,monitor,bounds.X,bounds.Y+(1-progress)*16,bounds.Width,bounds.Height);else Native.Place(this,monitor,bounds.X+(1-progress)*8,bounds.Y,bounds.Width,bounds.Height);}
        internal void Update(NotificationEntry entry)
        {
            entries.RemoveAll(n=>NotificationBanners.Key(n)==NotificationBanners.Key(entry));entries.Add(entry);entries.Sort((a,b)=>b.Created.CompareTo(a.Created));if(entries.Count>32)entries.RemoveRange(32,entries.Count-32);Present();Expires=DateTime.UtcNow.AddSeconds(7);
        }
        internal bool Retain(HashSet<string> current){if(entries.RemoveAll(n=>!current.Contains(NotificationBanners.Key(n)))>0&&entries.Count>0)Present();return entries.Count>0;}
        void Present(){var entry=entries[0];Entry=entry;sender.Text=entry.App+(entries.Count>1?"  ·  알림 "+entries.Count+"개":"");title.Text=entry.Title;body.Text=entry.Body;body.Visibility=String.IsNullOrWhiteSpace(entry.Body)?Visibility.Collapsed:Visibility.Visible;var source=NotificationStackView.CardIcon(entry);icon.Child=source!=null?AppIcons.Visual(source,Compact?26:32):null;var spill=Fluid.IconGlow(source);glow.Background=spill.HasValue?Fluid.IconGlowBrush(spill.Value):null;icon.Visibility=source!=null?Visibility.Visible:Visibility.Collapsed;iconColumn.Width=new GridLength(source!=null?(Compact?37:45):0);artwork.Source=entry.Artwork;artwork.Visibility=entry.Artwork!=null&&!Compact?Visibility.Visible:Visibility.Collapsed;}
    }
}
