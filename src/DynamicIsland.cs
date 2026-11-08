using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed class DynamicIsland : GlassWindow
    {
        static readonly Brush Black=Brushes.Black;
        static readonly Brush Charging=Paint.Brush("#FF79E6A1");
        readonly DynamicIslandState state=new DynamicIslandState();
        readonly Canvas canvas=new Canvas();
        readonly RectangleGeometry clip=new RectangleGeometry();
        readonly Grid compact=new Grid(),feedback=new Grid(),expanded=new Grid(),level=new Grid();
        readonly Grid body=new Grid();
        readonly Border mediaSurface=new Border();
        readonly Border levelSurface=new Border();
        readonly PillSlider levelSlider=new PillSlider("volume")
        {
            IsHitTestVisible=false,MinWidth=0,Light=true,LinearFill=true,
            NoTrackBorder=true,GlassStandalone=false,Height=60
        };
        readonly IslandNotificationView notification;
        readonly Border art=new Border {Width=16,Height=16,CornerRadius=new CornerRadius(5)};
        readonly TextBlock status=Paint.TextBlock("",12,Paint.ControlWhite),number=Paint.TextBlock("",13,Paint.ControlWhite);
        readonly IslandActivity activity=new IslandActivity();
        readonly Glyph feedbackGlyph=new Glyph("volume",20){Color=Paint.ControlWhite};
        readonly Glyph foldGlyph=new Glyph("chevron",15){Color=Paint.ControlWhite,RenderTransform=new RotateTransform(-90),RenderTransformOrigin=new Point(.5,.5)};
        readonly MediaPlayerView player;
        readonly IslandSearchView search;
        readonly Stopwatch motion=new Stopwatch();
        readonly DispatcherTimer expiry=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1.6)};
        readonly Action opening;
        MediaState media;
        string outputName;
        string symbolKey;
        int symbolRequest;
        ImageSource artwork;
        BitmapSource surfaceArtwork;
        Forms.Screen screen;
        double scale=1;
        Rect host,current,from,target;
        double currentRadius=10,fromRadius,targetRadius;
        TimeSpan lastRendering=TimeSpan.MinValue;
        readonly SpringValue slide=new SpringValue(.002) {Omega=38};
        readonly SpringClock slideClock=new SpringClock();
        bool animating,closed,placingHost,placementQueued;
        IDisposable outsideClick;
        IntPtr previousWindow;
        internal bool Expanded {get{return state.Expanded;}}
        internal bool Animating {get{return animating;}}
        internal bool FeedbackVisible {get{return IsVisible&&state.FeedbackVisible(DateTime.UtcNow);}}
        internal bool ExpiryActive {get{return expiry.IsEnabled;}}
        internal Rect Shape {get{return current;}}
        internal MediaPlayerView Player {get{return player;}}
        internal PillSlider LevelSlider {get{return levelSlider;}}
        internal Brush MediaSurface {get{return expanded.Background;}}
        internal IslandNotificationView NotificationView {get{return notification;}}
        internal bool NotificationVisible {get{return IsVisible&&state.NotificationVisible(DateTime.UtcNow);}}
        internal DynamicIsland(Action<string> command,Func<double,Task<bool>> seek,Action output,Action<string> openApp,Action opening=null,Func<string,AppEntry> sourceApp=null,Func<string,List<AppEntry>> findApps=null,Action<AppEntry> launchApp=null,Action<string> searchWeb=null,Action<SearchSetting> openSetting=null)
        {
            this.opening=opening;
            Title="One TaskBar · Dynamic Island";
            ShowActivated=false;
            OpaqueSurface=true;
            Radius=20;
            Width=DynamicIslandLayout.MaximumWidth;
            Height=DynamicIslandLayout.MaximumHeight;
            SetMaterial(Black,Black);
            Surface.BorderBrush=Brushes.Transparent;
            Surface.BorderThickness=new Thickness(0);
            Surface.Clip=clip;
            var root=body;
            root.HorizontalAlignment=HorizontalAlignment.Center;
            root.VerticalAlignment=VerticalAlignment.Top;
            compact.Margin=new Thickness(9,0,9,0);
            compact.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(20)});
            compact.ColumnDefinitions.Add(new ColumnDefinition());
            compact.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(20)});
            compact.Children.Add(art);
            Grid.SetColumn(activity,2);compact.Children.Add(activity);
            feedback.Margin=new Thickness(14,0,14,0);
            feedback.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(30)});
            feedback.ColumnDefinitions.Add(new ColumnDefinition());
            feedback.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});
            feedback.Children.Add(feedbackGlyph);
            status.VerticalAlignment=VerticalAlignment.Center;
            Grid.SetColumn(status,1);feedback.Children.Add(status);
            number.VerticalAlignment=VerticalAlignment.Center;
            number.HorizontalAlignment=HorizontalAlignment.Right;
            Grid.SetColumn(number,2);feedback.Children.Add(number);
            var button=Paint.Button(compact,()=>SetExpanded(!state.Expanded),"Dynamic Island · 미디어 컨트롤 열기");
            button.Background=Brushes.Transparent;
            button.Padding=new Thickness(0);
            button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            button.VerticalContentAlignment=VerticalAlignment.Stretch;
            root.Children.Add(button);
            feedback.IsHitTestVisible=false;
            root.Children.Add(feedback);
            level.RowDefinitions.Add(new RowDefinition {Height=new GridLength(28)});
            level.RowDefinitions.Add(new RowDefinition {Height=new GridLength(60)});
            levelSurface.Background=QuickPanel.Glass;levelSurface.Child=levelSlider;
            Grid.SetRow(levelSurface,1);level.Children.Add(levelSurface);
            root.Children.Add(level);
            notification=new IslandNotificationView(DismissNotification);
            notification.Open=OpenNotification;
            root.Children.Add(notification);
            expanded.RowDefinitions.Add(new RowDefinition {Height=new GridLength(28)});
            expanded.RowDefinitions.Add(new RowDefinition());
            var fold=Paint.Button(foldGlyph,()=>SetExpanded(false),"접기");
            fold.Background=Brushes.Transparent;fold.Width=28;fold.Height=22;
            fold.Padding=new Thickness(0);fold.HorizontalAlignment=HorizontalAlignment.Right;
            fold.Margin=new Thickness(0,3,12,0);expanded.Children.Add(fold);
            player=new MediaPlayerView(command,seek,()=>{SetExpanded(false);output();},openApp:id=>{SetExpanded(false);openApp(id);},sourceApp:sourceApp) {Margin=new Thickness(16,4,16,8)};
            player.FitPopup(164);
            mediaSurface.Child=player;
            Grid.SetRow(mediaSurface,1);expanded.Children.Add(mediaSurface);root.Children.Add(expanded);
            search=new IslandSearchView(findApps??(q=>new List<AppEntry>()),app=>{CloseSearch();if(launchApp!=null)launchApp(app);},text=>{CloseSearch();if(searchWeb!=null)searchWeb(text);},CloseSearch,openSetting==null?(Action<SearchSetting>)null:setting=>{CloseSearch();openSetting(setting);});
            search.Changed=()=>{if(state.Searching&&state.SearchRows!=search.RowCount){state.SearchRows=search.RowCount;RefreshPresentation();}};
            root.Children.Add(search);
            compact.Visibility=feedback.Visibility=expanded.Visibility=level.Visibility=notification.Visibility=search.Visibility=Visibility.Hidden;
            Surface.Child=root;
            var decorator=(AdornerDecorator)Content;
            decorator.Child=null;
            canvas.Children.Add(Surface);decorator.Child=canvas;
            expiry.Tick+=(s,e)=>ExpireFeedback();
            SourceInitialized+=(s,e)=>
            {
                var handle=new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x80|0x08000000)&~0x40000);
                HwndSource.FromHwnd(handle).AddHook((IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled)=>
                {
                    if(msg==0x21&&!state.Expanded&&!state.Searching){handled=true;return new IntPtr(3);}
                    if(msg==0x2E0)QueuePlacement();
                    return IntPtr.Zero;
                });
            };
            Loaded+=(s,e)=>QueuePlacement();
            LocationChanged+=(s,e)=>{if(!placingHost)QueuePlacement();};
            Deactivated+=(s,e)=>{if(state.Expanded&&!IsMouseOver)SetExpanded(false);if(state.Searching&&!IsMouseOver)CloseSearch();};
            Closed+=(s,e)=>{closed=true;activity.SetPlaying(false);StopMotion();StopOutside();expiry.Stop();ClearNotification();player.Update(new MediaState(),null);media=null;artwork=null;surfaceArtwork=null;art.Background=expanded.Background=null;};
        }
        internal override void RefreshGlassMaterial()
        {
            // The header stays black. Only the shared level surface follows the panel theme.
            levelSurface.Background=QuickPanel.Glass;
            levelSlider.InvalidateVisual();
        }
        bool bottom;
        internal void Present(MediaState value,string output,Forms.Screen monitor,double dpi,System.Drawing.Rectangle? work=null,bool atBottom=false)
        {
            if(closed)return;
            outputName=output;
            media=value!=null&&value.Available&&value.Active?value:null;
            screen=monitor;scale=dpi;
            bottom=atBottom;
            ((RotateTransform)foldGlyph.RenderTransform).Angle=bottom?90:-90;
            var next=DynamicIslandLayout.Host(screen.Bounds,work??screen.WorkingArea,scale,bottom);
            if(host!=next||!IsVisible)
            {
                host=next;Width=host.Width;Height=host.Height;
                new WindowInteropHelper(this).EnsureHandle();
                PlaceHost();
                if(!IsVisible)
                {
                    current=DynamicIslandLayout.Capsule(new Size(DynamicIslandLayout.IdleWidth,DynamicIslandLayout.CompactHeight),host.Width,host.Height,bottom);
                    currentRadius=9;
                    PlaceShape();
                    ((Window)this).Show();
                }
                PlaceHost();
            }
            if(media==null&&state.Expanded)SetExpanded(false);
            if(media!=null)
            {
                if(artwork!=media.Artwork)
                {
                    artwork=media.Artwork;
                    if(artwork==null)art.Background=null;
                    else{var brush=new ImageBrush(ArtworkThumbnail.Square(artwork,Math.Max(40,(int)Math.Ceiling(28*scale)))){Stretch=Stretch.UniformToFill};RenderOptions.SetBitmapScalingMode(brush,BitmapScalingMode.HighQuality);brush.Freeze();art.Background=brush;}
                }
                if(state.Expanded)
                {
                    player.Update(media,output);
                    UpdateMediaSurface();
                }
            }
            else if(artwork!=null){artwork=null;art.Background=null;}
            RefreshPresentation();
        }
        void QueuePlacement()
        {
            if(closed||placementQueued||!IsVisible||screen==null)return;
            placementQueued=true;
            Dispatcher.BeginInvoke(new Action(()=>
            {
                placementQueued=false;
                if(!closed&&IsVisible)PlaceHost();
            }),DispatcherPriority.Loaded);
        }
        void PlaceHost()
        {
            // WPF can apply a work-area/DPI suggested position after Show.
            // Reconcile that one layout event against our reserved native band.
            placingHost=true;
            try
            {
                Native.PrepareMonitor(this,screen.Bounds);
                var wanted=Native.PlacementBounds(screen.Bounds,scale,host.X,host.Y,host.Width,host.Height);
                var handle=new WindowInteropHelper(this).Handle;
                Native.RectI actual;
                if(Native.GetWindowRect(handle,out actual)&&actual.Equals(wanted))return;
                Native.SetWindowPos(handle,new IntPtr(-1),wanted.Left,wanted.Top,wanted.Right-wanted.Left,wanted.Bottom-wanted.Top,0x10);
            }
            finally{placingHost=false;}
        }
        internal void ShowFeedback(string kind,double value,bool muted)
        {
            if(!IsVisible||closed)return;
            if(kind!="charge"&&NotificationVisible)ClearNotification();
            state.Feedback(kind,value,muted,DateTime.UtcNow);
            ArmExpiry();
            RefreshPresentation();
        }
        Action<NotificationEntry> openNotification;
        internal bool ShowNotification(NotificationEntry entry,Action<NotificationEntry> open)
        {
            if(closed||!IsVisible||state.Expanded||entry==null)return false;
            notification.Present(entry);
            openNotification=open;
            state.NotificationUntil=DateTime.UtcNow.AddSeconds(7);
            ArmExpiry();
            RefreshPresentation();
            return true;
        }
        internal void RetainNotification(HashSet<string> keys)
        {
            var entry=notification.Entry;
            if(entry!=null&&!keys.Contains(NotificationBanners.Key(entry)))DismissNotification();
        }
        void OpenNotification(NotificationEntry entry)
        {
            var open=openNotification;
            DismissNotification();
            if(open!=null)open(entry);
        }
        internal void DismissNotification()
        {
            ClearNotification();
            ArmExpiry();
            if(IsVisible&&!closed)RefreshPresentation();
        }
        void ClearNotification()
        {
            state.NotificationUntil=DateTime.MinValue;
            notification.Present(null);
            openNotification=null;
        }
        void ArmExpiry()
        {
            expiry.Stop();
            var until=state.FeedbackUntil>state.NotificationUntil?state.FeedbackUntil:state.NotificationUntil;
            var remaining=until-DateTime.UtcNow;
            if(remaining<=TimeSpan.Zero||closed||!IsVisible)return;
            expiry.Interval=TimeSpan.FromMilliseconds(Math.Max(1,remaining.TotalMilliseconds));
            expiry.Start();
        }
        internal void ExpireFeedback()
        {
            expiry.Stop();
            if(closed||!IsVisible)return;
            // Dispatcher timers may fire just before the UTC deadline on a coarse clock.
            // Keep the one-shot wakeup until the feedback really expires.
            if(notification.Entry!=null&&IsMouseOver)state.NotificationUntil=DateTime.UtcNow.AddSeconds(2);
            var until=state.FeedbackUntil>state.NotificationUntil?state.FeedbackUntil:state.NotificationUntil;
            var remaining=until-DateTime.UtcNow;
            if(remaining>TimeSpan.Zero)
            {
                expiry.Interval=TimeSpan.FromMilliseconds(Math.Max(1,remaining.TotalMilliseconds));
                expiry.Start();
                return;
            }
            ClearNotification();
            RefreshPresentation();
        }
        void RefreshPresentation()
        {
            bool searching=state.Searching;
            bool active=media!=null,detail=state.Expanded&&active&&!searching;
            bool notice=state.NotificationVisible(DateTime.UtcNow)&&!detail&&!searching;
            bool show=state.FeedbackVisible(DateTime.UtcNow)&&!detail&&!notice&&!searching;
            bool charge=show&&state.FeedbackKind=="charge";
            bool compactVisible=active&&!show&&!detail&&!notice&&!searching;
            ShowContent(search,searching);
            ShowContent(compact,compactVisible);
            activity.SetPlaying(compactVisible&&media.Playing&&Motion.Enabled);
            ShowContent(feedback,charge);
            ShowContent(level,show&&!charge);
            ShowContent(notification,notice);
            ShowContent(expanded,detail);
            if(show)
            {
                string kind=state.FeedbackKind;
                RefreshDeviceSymbol(kind);
                feedbackGlyph.Kind=kind=="charge"?"battery":kind=="brightness"?"brightness":SoundIcon.For(state.Value,state.Muted,true);
                feedbackGlyph.Color=Charging;
                feedbackGlyph.InvalidateVisual();
                status.Text=kind=="charge"?"충전 중":"";
                status.Visibility=kind=="charge"?Visibility.Visible:Visibility.Collapsed;
                number.Text=Math.Round(state.Muted?0:state.Value)+"%";
                levelSlider.Kind=kind=="brightness"?"brightness":SoundIcon.For(state.Value,state.Muted,true);
                levelSlider.Value=state.Muted?0:state.Value;
            }
            var size=state.Size(active,DateTime.UtcNow);
            size.Height=Math.Min(size.Height,host.Height);
            MoveTo(DynamicIslandLayout.Capsule(size,host.Width,host.Height,bottom));
        }
        void UpdateMediaSurface()
        {
            var source=media==null?null:media.Artwork;
            if(expanded.Background!=null&&Object.ReferenceEquals(surfaceArtwork,source))return;
            surfaceArtwork=source;
            expanded.Background=ArtworkPalette.IslandSurface(source);
        }
        internal void OutputChanged(string name){outputName=name;if(FeedbackVisible)RefreshPresentation();}
        async void RefreshDeviceSymbol(string kind)
        {
            string name=outputName;
            levelSlider.OutputKind=kind=="volume"?OutputIcons.Kind(name):null;
            string key=kind=="volume"&&BudsInfo.IsBuds(name)?BudsDeviceData.SymbolFile(name):null;
            if(key==symbolKey)return;
            symbolKey=key;
            int request=++symbolRequest;
            feedbackGlyph.SetDeviceIcon(null);
            levelSlider.SetDeviceIcon(null);
            if(key==null)return;
            try
            {
                var image=await BackgroundWork.Run(()=>BudsDeviceData.ShortcutIcon(name));
                if(!closed&&request==symbolRequest)levelSlider.SetDeviceIcon(image);
            }
            catch(Exception error){AppLog.Once("island-device-symbol",error.GetType().Name);}
        }
        static void ShowContent(FrameworkElement content,bool visible)
        {
            if(visible==(content.Visibility==Visibility.Visible))return;
            content.Visibility=visible?Visibility.Visible:Visibility.Hidden;
            if(visible)Motion.Fade(content,0,1,180);
        }
        internal void SetExpanded(bool value)
        {
            if(closed||!IsVisible||state.Expanded==value||value&&media==null)return;
            state.Expanded=value;StopOutside();
            var handle=new WindowInteropHelper(this).Handle;
            int style=Native.GetWindowLong(handle,-20);
            Native.SetWindowLong(handle,-20,value?style&~0x08000000:style|0x08000000);
            if(value)
            {
                previousWindow=Native.GetForegroundWindow();
                if(opening!=null)opening();
                player.Update(media,outputName);
                UpdateMediaSurface();
                outsideClick=MenuDismiss.Watch(ContainsScreenPoint,()=>SetExpanded(false));
                Activate();player.PlayButton.Focus();
            }
            else
            {
                player.Update(new MediaState(),null);
                expanded.Background=null;
                surfaceArtwork=null;
                if(Native.GetForegroundWindow()==handle&&Native.IsWindow(previousWindow))Native.Activate(previousWindow);
            }
            RefreshPresentation();
        }
        // Opens the search out of the island, or closes it when it is already open. False when the island cannot take it.
        internal bool ToggleSearch()
        {
            if(closed||!IsVisible)return false;
            if(state.Searching){CloseSearch();return true;}
            if(state.Expanded)SetExpanded(false);
            state.Searching=true;state.SearchRows=0;StopOutside();
            var handle=new WindowInteropHelper(this).Handle;
            Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)&~0x08000000);
            previousWindow=Native.GetForegroundWindow();
            if(opening!=null)opening();
            search.Reset();
            outsideClick=MenuDismiss.Watch(ContainsScreenPoint,CloseSearch);
            RefreshPresentation();
            Activate();search.Focus();
            return true;
        }
        internal bool Searching {get{return state.Searching;}}
        void CloseSearch()
        {
            if(closed||!state.Searching)return;
            state.Searching=false;state.SearchRows=0;StopOutside();
            var handle=new WindowInteropHelper(this).Handle;
            Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x08000000);
            if(Native.GetForegroundWindow()==handle&&Native.IsWindow(previousWindow))Native.Activate(previousWindow);
            RefreshPresentation();
        }
        bool ContainsScreenPoint(Native.PointI point)
        {
            if(!IsVisible)return false;
            Native.RectI rect;
            return Native.GetWindowRect(new WindowInteropHelper(this).Handle,out rect)&&
                point.X>=rect.Left+current.Left*scale&&point.X<rect.Left+current.Right*scale&&
                point.Y>=rect.Top+current.Top*scale&&point.Y<rect.Top+current.Bottom*scale;
        }
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if(e.Key==Key.Escape&&state.Searching){CloseSearch();e.Handled=true;return;}
            if(e.Key==Key.Escape&&state.Expanded){SetExpanded(false);e.Handled=true;return;}
            base.OnPreviewKeyDown(e);
        }
        void MoveTo(Rect next)
        {
            if(target==next)return;
            SpringValue.Handoff(slide,animating,current,from,target,next);
            slideClock.Restart();
            target=next;from=current;
            body.Width=next.Width;body.Height=next.Height;
            fromRadius=currentRadius;
            bool card=state.Expanded||state.Searching||NotificationVisible||next.Height>32;
            targetRadius=Math.Min(Math.Min(next.Width,next.Height)/2,card?29:100);
            if(!Motion.Enabled||current.IsEmpty){StopMotion();current=target;currentRadius=targetRadius;PlaceShape();return;}
            motion.Restart();
            if(!animating){animating=true;lastRendering=TimeSpan.MinValue;CompositionTarget.Rendering+=Frame;}
        }
        void Frame(object sender,EventArgs e)
        {
            var rendering=e as RenderingEventArgs;
            if(rendering!=null){if(rendering.RenderingTime==lastRendering)return;lastRendering=rendering.RenderingTime;}
            slide.Step(slideClock.Next(rendering!=null?rendering.RenderingTime:motion.Elapsed));bool settled=slide.AtRest;double ease=settled?1:slide.Value;
            current=new Rect(from.X+(target.X-from.X)*ease,from.Y+(target.Y-from.Y)*ease,from.Width+(target.Width-from.Width)*ease,from.Height+(target.Height-from.Height)*ease);
            currentRadius=fromRadius+(targetRadius-fromRadius)*ease;
            PlaceShape();
            if(settled)StopMotion();
        }
        void PlaceShape()
        {
            double radius=currentRadius;
            Surface.Width=current.Width;Surface.Height=current.Height;
            Surface.CornerRadius=new CornerRadius(radius);
            clip.Rect=new Rect(0,0,current.Width,current.Height);clip.RadiusX=clip.RadiusY=radius;
            Canvas.SetLeft(Surface,current.X);
            Canvas.SetTop(Surface,current.Y);
            BackdropClip=current;
            Native.Round(this,radius);
        }
        void StopMotion(){if(animating)CompositionTarget.Rendering-=Frame;animating=false;motion.Stop();}
        void StopOutside(){if(outsideClick!=null){outsideClick.Dispose();outsideClick=null;}}
        internal void Suspend()
        {
            if(closed||!IsVisible&&!animating&&!expiry.IsEnabled)return;
            activity.SetPlaying(false);
            StopMotion();StopOutside();expiry.Stop();state.Reset();
            ClearNotification();
            var handle=new WindowInteropHelper(this).Handle;
            if(handle!=IntPtr.Zero)Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x08000000);
            player.Update(new MediaState(),null);
            media=null;artwork=null;surfaceArtwork=null;art.Background=expanded.Background=null;target=Rect.Empty;
            symbolRequest++;symbolKey=null;feedbackGlyph.SetDeviceIcon(null);levelSlider.SetDeviceIcon(null);
            ((Window)this).Hide();
        }
        // Where a flying media window should land, in screen pixels: the compact media shape, whatever the island shows now.
        internal bool MediaCapsule(Forms.Screen monitor,out Rect bounds,out IntPtr owner)
        {
            bounds=Rect.Empty;owner=IntPtr.Zero;
            if(closed||!IsVisible||screen==null||monitor.DeviceName!=screen.DeviceName||host.IsEmpty)return false;
            var shape=DynamicIslandLayout.Capsule(new Size(DynamicIslandLayout.MediaWidth,DynamicIslandLayout.CompactHeight),host.Width,host.Height,bottom);
            var box=Native.PlacementBounds(screen.Bounds,scale,host.X+shape.X,host.Y+shape.Y,shape.Width,shape.Height);
            bounds=new Rect(box.Left,box.Top,box.Right-box.Left,box.Bottom-box.Top);
            owner=new WindowInteropHelper(this).Handle;
            return bounds.Width>0&&bounds.Height>0;
        }
        internal object Inspect()
        {
            return new {visible=IsVisible,expanded=state.Expanded,feedback=FeedbackVisible,activity=activity.Active,animating=animating,expiry=expiry.IsEnabled,width=current.Width,height=current.Height,window=Native.RegionInfo(this),hostX=host.X,hostY=host.Y,bottom=bottom,shapeY=current.Y,scale=scale};
        }
    }
}
