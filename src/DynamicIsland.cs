using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed class DynamicIsland : GlassWindow
    {
        static readonly Brush Black=Brushes.Black;
        readonly DynamicIslandState state=new DynamicIslandState();
        readonly Canvas canvas=new Canvas();
        readonly RectangleGeometry clip=new RectangleGeometry();
        readonly Grid compact=new Grid(),feedback=new Grid(),expanded=new Grid();
        readonly Grid body=new Grid();
        readonly Border art=new Border {Width=20,Height=20,CornerRadius=new CornerRadius(6)};
        readonly TextBlock status=Paint.TextBlock("",12,Paint.ControlWhite),number=Paint.TextBlock("",13,Paint.ControlWhite);
        readonly IslandActivity activity=new IslandActivity();
        readonly Glyph feedbackGlyph=new Glyph("volume",20){Color=Paint.ControlWhite};
        readonly Border feedbackTrack=new Border {Height=5,Background=Paint.Brush("#FF3D3D3D"),CornerRadius=new CornerRadius(2.5)};
        readonly Border feedbackFill=new Border {Height=5,Background=Paint.ControlWhite,CornerRadius=new CornerRadius(2.5),HorizontalAlignment=HorizontalAlignment.Left};
        readonly MediaPlayerView player;
        readonly Stopwatch motion=new Stopwatch();
        readonly DispatcherTimer expiry=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1.6)};
        readonly Action opening;
        MediaState media;
        string outputName;
        string symbolKey;
        int symbolRequest;
        ImageSource artwork;
        Forms.Screen screen;
        double scale=1;
        Rect host,current,from,target;
        double currentRadius=10,fromRadius,targetRadius;
        TimeSpan lastRendering=TimeSpan.MinValue;
        bool animating,closed,placingHost,placementQueued;
        IDisposable outsideClick;
        IntPtr previousWindow;
        internal bool Expanded {get{return state.Expanded;}}
        internal bool Animating {get{return animating;}}
        internal bool FeedbackVisible {get{return IsVisible&&state.FeedbackVisible(DateTime.UtcNow);}}
        internal bool ExpiryActive {get{return expiry.IsEnabled;}}
        internal Rect Shape {get{return current;}}
        internal MediaPlayerView Player {get{return player;}}
        internal DynamicIsland(Action<string> command,Func<double,Task<bool>> seek,Action output,Action<string> openApp,Action opening=null,Func<string,AppEntry> sourceApp=null)
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
            compact.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(24)});
            compact.ColumnDefinitions.Add(new ColumnDefinition());
            compact.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(24)});
            compact.Children.Add(art);
            Grid.SetColumn(activity,2);compact.Children.Add(activity);
            feedback.Margin=new Thickness(14,0,14,0);
            feedback.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(30)});
            feedback.ColumnDefinitions.Add(new ColumnDefinition());
            feedback.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});
            feedback.Children.Add(feedbackGlyph);
            feedbackTrack.Child=feedbackFill;
            feedbackTrack.VerticalAlignment=VerticalAlignment.Center;
            feedbackTrack.SizeChanged+=(s,e)=>UpdateFeedbackFill();
            Grid.SetColumn(feedbackTrack,1);feedback.Children.Add(feedbackTrack);
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
            expanded.RowDefinitions.Add(new RowDefinition {Height=new GridLength(20)});
            expanded.RowDefinitions.Add(new RowDefinition());
            var fold=Paint.Button(new Glyph("chevron",15){Color=Paint.ControlWhite,RenderTransform=new RotateTransform(-90),RenderTransformOrigin=new Point(.5,.5)},()=>SetExpanded(false),"접기");
            fold.Background=Brushes.Transparent;fold.Width=28;fold.Height=22;
            fold.Padding=new Thickness(0);fold.HorizontalAlignment=HorizontalAlignment.Right;
            fold.Margin=new Thickness(0,3,12,0);expanded.Children.Add(fold);
            player=new MediaPlayerView(command,seek,()=>{SetExpanded(false);output();},openApp:id=>{SetExpanded(false);openApp(id);},sourceApp:sourceApp) {Margin=new Thickness(16,0,16,0)};
            player.FitPopup(164);
            Grid.SetRow(player,1);expanded.Children.Add(player);root.Children.Add(expanded);
            compact.Visibility=feedback.Visibility=expanded.Visibility=Visibility.Hidden;
            Surface.Child=root;
            var decorator=(AdornerDecorator)Content;
            decorator.Child=null;
            canvas.Children.Add(Surface);decorator.Child=canvas;
            expiry.Tick+=(s,e)=>{expiry.Stop();if(!closed&&IsVisible)RefreshPresentation();};
            SourceInitialized+=(s,e)=>
            {
                var handle=new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x80|0x08000000)&~0x40000);
                HwndSource.FromHwnd(handle).AddHook((IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled)=>
                {
                    if(msg==0x21&&!state.Expanded){handled=true;return new IntPtr(3);}
                    if(msg==0x2E0)QueuePlacement();
                    return IntPtr.Zero;
                });
            };
            Loaded+=(s,e)=>QueuePlacement();
            LocationChanged+=(s,e)=>{if(!placingHost)QueuePlacement();};
            Deactivated+=(s,e)=>{if(state.Expanded&&!IsMouseOver)SetExpanded(false);};
            Closed+=(s,e)=>{closed=true;activity.SetPlaying(false);StopMotion();StopOutside();expiry.Stop();player.Update(new MediaState(),null);media=null;artwork=null;art.Background=null;};
        }
        internal override void RefreshGlassMaterial()
        {
            // This black activity surface intentionally keeps its own contrast palette.
        }
        internal void Present(MediaState value,string output,Forms.Screen monitor,double dpi,System.Drawing.Rectangle? work=null)
        {
            if(closed)return;
            outputName=output;
            media=value!=null&&value.Available&&value.Active?value:null;
            screen=monitor;scale=dpi;
            var next=DynamicIslandLayout.Host(screen.Bounds,work??screen.WorkingArea,scale);
            if(host!=next||!IsVisible)
            {
                host=next;Width=host.Width;Height=host.Height;
                new WindowInteropHelper(this).EnsureHandle();
                PlaceHost();
                if(!IsVisible){current=DynamicIslandLayout.Capsule(new Size(56,18),host.Width);currentRadius=9;PlaceShape();((Window)this).Show();}
                PlaceHost();
            }
            if(media==null&&state.Expanded)SetExpanded(false);
            if(media!=null)
            {
                if(artwork!=media.Artwork)
                {
                    artwork=media.Artwork;
                    if(artwork==null)art.Background=null;
                    else{var brush=new ImageBrush(artwork){Stretch=Stretch.UniformToFill};brush.Freeze();art.Background=brush;}
                }
                if(state.Expanded)player.Update(media,output);
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
            state.Feedback(kind,value,muted,DateTime.UtcNow);
            expiry.Stop();expiry.Interval=TimeSpan.FromSeconds(kind=="charge"?3:1.6);expiry.Start();
            RefreshPresentation();
        }
        void RefreshPresentation()
        {
            bool active=media!=null,detail=state.Expanded&&active,show=state.FeedbackVisible(DateTime.UtcNow)&&!detail;
            bool compactVisible=active&&!show&&!detail;
            ShowContent(compact,compactVisible);
            activity.SetPlaying(compactVisible&&media.Playing&&Motion.Enabled);
            ShowContent(feedback,show);
            ShowContent(expanded,detail);
            if(show)
            {
                string kind=state.FeedbackKind;
                RefreshDeviceSymbol(kind);
                feedbackGlyph.Kind=kind=="charge"?"battery":kind=="brightness"?"brightness":SoundIcon.For(state.Value,state.Muted,true);
                feedbackGlyph.Color=kind=="charge"?Paint.Brush("#FF79E6A1"):Paint.ControlWhite;
                feedbackGlyph.InvalidateVisual();
                status.Text=kind=="charge"?"충전 중":"";
                status.Visibility=kind=="charge"?Visibility.Visible:Visibility.Collapsed;
                feedbackTrack.Visibility=kind=="charge"?Visibility.Collapsed:Visibility.Visible;
                number.Text=Math.Round(state.Muted?0:state.Value)+"%";
                UpdateFeedbackFill();
            }
            var size=state.Size(active,DateTime.UtcNow);
            size.Height=Math.Min(size.Height,host.Height);
            MoveTo(DynamicIslandLayout.Capsule(size,host.Width));
        }
        void UpdateFeedbackFill(){feedbackFill.Width=Math.Max(0,feedbackTrack.ActualWidth)*(state.Muted?0:state.Value)/100;}
        internal void OutputChanged(string name){outputName=name;if(FeedbackVisible)RefreshPresentation();}
        async void RefreshDeviceSymbol(string kind)
        {
            string name=outputName;
            string key=kind=="volume"&&BudsInfo.IsBuds(name)?BudsDeviceData.SymbolFile(name):null;
            if(key==symbolKey)return;
            symbolKey=key;
            int request=++symbolRequest;
            feedbackGlyph.SetDeviceIcon(null);
            if(key==null)return;
            try
            {
                var image=await BackgroundWork.Run(()=>BudsDeviceData.ShortcutIcon(name));
                if(!closed&&request==symbolRequest)feedbackGlyph.SetDeviceIcon(image);
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
                outsideClick=MenuDismiss.Watch(ContainsScreenPoint,()=>SetExpanded(false));
                Activate();player.PlayButton.Focus();
            }
            else
            {
                player.Update(new MediaState(),null);
                if(Native.GetForegroundWindow()==handle&&Native.IsWindow(previousWindow))Native.Activate(previousWindow);
            }
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
            if(e.Key==Key.Escape&&state.Expanded){SetExpanded(false);e.Handled=true;return;}
            base.OnPreviewKeyDown(e);
        }
        void MoveTo(Rect next)
        {
            if(target==next)return;
            target=next;from=current;
            body.Width=next.Width;body.Height=next.Height;
            fromRadius=currentRadius;
            targetRadius=Math.Min(Math.Min(next.Width,next.Height)/2,state.Expanded?29:100);
            if(!Motion.Enabled||current.IsEmpty){StopMotion();current=target;currentRadius=targetRadius;PlaceShape();return;}
            motion.Restart();
            if(!animating){animating=true;lastRendering=TimeSpan.MinValue;CompositionTarget.Rendering+=Frame;}
        }
        void Frame(object sender,EventArgs e)
        {
            var rendering=e as RenderingEventArgs;
            if(rendering!=null){if(rendering.RenderingTime==lastRendering)return;lastRendering=rendering.RenderingTime;}
            double t=Math.Min(1,motion.Elapsed.TotalMilliseconds/220),ease=1-Math.Pow(1-t,3);
            current=new Rect(from.X+(target.X-from.X)*ease,0,from.Width+(target.Width-from.Width)*ease,from.Height+(target.Height-from.Height)*ease);
            currentRadius=fromRadius+(targetRadius-fromRadius)*ease;
            PlaceShape();
            if(t>=1)StopMotion();
        }
        void PlaceShape()
        {
            double radius=currentRadius;
            Surface.Width=current.Width;Surface.Height=current.Height;
            Surface.CornerRadius=new CornerRadius(radius);
            clip.Rect=new Rect(0,0,current.Width,current.Height);clip.RadiusX=clip.RadiusY=radius;
            Canvas.SetLeft(Surface,current.X);Canvas.SetTop(Surface,0);
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
            var handle=new WindowInteropHelper(this).Handle;
            if(handle!=IntPtr.Zero)Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x08000000);
            player.Update(new MediaState(),null);
            media=null;artwork=null;art.Background=null;target=Rect.Empty;
            symbolRequest++;symbolKey=null;feedbackGlyph.SetDeviceIcon(null);
            ((Window)this).Hide();
        }
        internal object Inspect()
        {
            return new {visible=IsVisible,expanded=state.Expanded,feedback=FeedbackVisible,activity=activity.Active,animating=animating,expiry=expiry.IsEnabled,width=current.Width,height=current.Height,window=Native.RegionInfo(this),hostX=host.X,hostY=host.Y,scale=scale};
        }
    }
}
