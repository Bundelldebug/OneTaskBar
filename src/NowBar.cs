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
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    // A single native window morphs from the dock-height capsule into its media controls.
    internal sealed class NowBar : GlassWindow
    {
        readonly ShellController shell;readonly Forms.Screen screen;readonly Action<string> command;readonly Func<double,Task<bool>> seekCommand;
        readonly Button compact,play,previous,next;readonly Grid expandedContent;readonly MarqueeTitle title=new MarqueeTitle();
        readonly Border compactArt=new Border {Width=28,Height=28,CornerRadius=new CornerRadius(8)},largeArt=new Border {Width=42,Height=42,CornerRadius=new CornerRadius(11)},glow=new Border {CornerRadius=new CornerRadius(22)};
        readonly Glyph smallState=new Glyph("pause",14),playGlyph=new Glyph("pause",20){Color=Paint.Ink};
        readonly TextBlock fullTitle=Paint.TextBlock("",15,Paint.Text),artist=Paint.TextBlock("",11,Paint.Muted),elapsed=Paint.TextBlock("0:00",10,Paint.Muted),duration=Paint.TextBlock("0:00",10,Paint.Muted);
        readonly PillSlider seek=new PillSlider("progress") {Height=22,Light=true,DarkTrack=true,AnimateValue=false,Slim=true,ShowIcon=false};
        readonly DispatcherTimer motionTimer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(16)};readonly Stopwatch watch=new Stopwatch();
        Rect current,from,target,hostBox;NowBarLayout layout;double dockStart,dockTop,monitorScale=1;
        readonly Canvas hostCanvas=new Canvas();readonly RectangleGeometry capsuleClip=new RectangleGeometry();
        internal int FrameCount {get;private set;}internal double MaximumFrameGap {get;private set;}double lastFrame;
        internal ArtworkPalette Palette {get;private set;}MediaState media;ImageSource artwork;DateTime lastActive,seekUntil;double? seekPending;bool seeking,expanded,wanted,closed;IntPtr previousWindow;
        internal string PlayKind {get{return playGlyph.Kind;}}internal bool Expanded {get{return expanded;}}internal Rect Shape {get{return current;}}internal double TitleOffset {get{return title.Offset;}}internal bool MarqueeOverflow {get{return title.NaturalWidth>title.ActualWidth+.5;}}
        internal Button CompactButton {get{return compact;}}internal Button PlayButton {get{return play;}}internal Button NextButton {get{return next;}}internal Button PreviousButton {get{return previous;}}internal PillSlider SeekBar {get{return seek;}}
        internal NowBar(ShellController controller,Forms.Screen monitor,Action<string> dispatch=null,Func<double,Task<bool>> seekDispatch=null)
        {
            shell=controller;screen=monitor;command=dispatch??controller.Media;seekCommand=seekDispatch??controller.SeekMedia;Title="One TaskBar · Now bar";Width=132;Height=44;Radius=22;Dark=true;ShowActivated=false;Surface.CornerRadius=new CornerRadius(22);Surface.Background=QuickPanel.Glass;Palette=ArtworkPalette.Neutral;Surface.BorderThickness=new Thickness(0);Surface.BorderBrush=Brushes.Transparent;
            var root=new Grid();root.Children.Add(glow);
            var small=new Grid {Height=44,Margin=new Thickness(10,0,10,0)};small.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(36)});small.ColumnDefinitions.Add(new ColumnDefinition());small.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(24)});small.Children.Add(compactArt);Grid.SetColumn(title,1);small.Children.Add(title);Grid.SetColumn(smallState,2);small.Children.Add(smallState);
            compact=Paint.Button(small,()=>SetExpanded(true),"Now bar · 재생 컨트롤 열기");compact.Background=Brushes.Transparent;compact.Padding=new Thickness(0);compact.Height=44;compact.VerticalAlignment=VerticalAlignment.Bottom;root.Children.Add(compact);
            expandedContent=new Grid {Height=164,Margin=new Thickness(14,12,14,12),VerticalAlignment=VerticalAlignment.Bottom,Visibility=Visibility.Collapsed,Opacity=0};expandedContent.RowDefinitions.Add(new RowDefinition {Height=new GridLength(50)});expandedContent.RowDefinitions.Add(new RowDefinition {Height=new GridLength(52)});expandedContent.RowDefinitions.Add(new RowDefinition());
            var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(54)});header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(28)});header.Children.Add(largeArt);var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};fullTitle.FontWeight=FontWeights.SemiBold;fullTitle.TextTrimming=TextTrimming.CharacterEllipsis;artist.TextTrimming=TextTrimming.CharacterEllipsis;words.Children.Add(fullTitle);words.Children.Add(artist);Grid.SetColumn(words,1);header.Children.Add(words);
            var fold=Paint.Button(new Glyph("chevron",17){RenderTransform=new RotateTransform(90),RenderTransformOrigin=new Point(.5,.5)},()=>SetExpanded(false),"Now bar 접기");fold.Padding=new Thickness(4);fold.Width=fold.Height=28;fold.Background=Brushes.Transparent;Grid.SetColumn(fold,2);header.Children.Add(fold);expandedContent.Children.Add(header);
            var timeline=new DockPanel {Margin=new Thickness(2,7,2,0)};var times=new Grid {Height=18};duration.HorizontalAlignment=HorizontalAlignment.Right;times.Children.Add(elapsed);times.Children.Add(duration);DockPanel.SetDock(times,Dock.Bottom);timeline.Children.Add(times);timeline.Children.Add(seek);Grid.SetRow(timeline,1);expandedContent.Children.Add(timeline);System.Windows.Automation.AutomationProperties.SetName(seek,"미디어 재생 위치");seek.Changed+=Seek;
            var transport=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};previous=Transport("previous",()=>command("previous"),"이전 곡",40);play=Paint.Button(playGlyph,()=>command("play"),"재생 · 일시 정지");play.Width=play.Height=44;play.Padding=new Thickness(0);play.Background=Paint.ControlWhite;play.Margin=new Thickness(24,0,24,0);play.Resources["ButtonRadius"]=new CornerRadius(22);next=Transport("next",()=>command("next"),"다음 곡",40);transport.Children.Add(previous);transport.Children.Add(play);transport.Children.Add(next);Grid.SetRow(transport,2);expandedContent.Children.Add(transport);root.Children.Add(expandedContent);Surface.Child=root;var decorator=(AdornerDecorator)Content;decorator.Child=null;hostCanvas.Children.Add(Surface);decorator.Child=hostCanvas;Surface.Clip=capsuleClip;compact.HorizontalAlignment=HorizontalAlignment.Left;expandedContent.HorizontalAlignment=HorizontalAlignment.Left;
            SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLong(h,-20,(Native.GetWindowLong(h,-20)|0x80|0x08000000)&~0x40000);HwndSource.FromHwnd(h).AddHook((IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled)=>{if(msg==0x21&&!expanded){handled=true;return new IntPtr(3);}return IntPtr.Zero;});};
            Deactivated+=(s,e)=> {if(expanded&&!Program.Inspectable&&!IsMouseOver)SetExpanded(false);};motionTimer.Tick+=(s,e)=>Frame();Closed+=(s,e)=> {closed=true;motionTimer.Stop();title.Running=false;compactArt.Background=largeArt.Background=glow.Background=null;media=null;};
        }
        protected override void OnPreviewKeyDown(KeyEventArgs e){if(e.Key==Key.Escape&&expanded){SetExpanded(false);e.Handled=true;return;}base.OnPreviewKeyDown(e);}
        static Button Transport(string icon,Action action,string hint,double size){var button=Paint.Button(new Glyph(icon,20),action,hint);button.Width=button.Height=size;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;return button;}
        internal void Update(MediaState state,double scale,double startX,double dockY,bool allowed)
        {
            if(closed)return;monitorScale=scale;dockStart=startX;dockTop=dockY;var now=DateTime.UtcNow;bool active=state!=null&&state.Available&&state.Active;if(active){lastActive=now;media=state;}bool present=media!=null&&(active||(now-lastActive).TotalMilliseconds<900);
            if(!allowed||!present){if(wanted||IsVisible){wanted=false;motionTimer.Stop();expanded=false;title.Running=false;SetActivation(false);if(!allowed)HideImmediately();else Hide();}return;}
            if(title.Text!=media.Title){title.Text=media.Title;fullTitle.Text=media.Title;compact.ToolTip=media.Title+" · 재생 컨트롤 열기";System.Windows.Automation.AutomationProperties.SetName(compact,(string)compact.ToolTip);}
            artist.Text=media.Artist;smallState.Kind=playGlyph.Kind=media.Playing?"pause":"play";smallState.InvalidateVisual();playGlyph.InvalidateVisual();play.IsEnabled=media.CanPlay;next.IsEnabled=media.CanNext;previous.IsEnabled=media.CanPrevious;play.Opacity=media.CanPlay?1:.4;next.Opacity=media.CanNext?1:.4;previous.Opacity=media.CanPrevious?1:.4;
            if(artwork!=media.Artwork){artwork=media.Artwork;ApplyPalette(media.Artwork);if(artwork!=null){var brush=new ImageBrush(artwork){Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center};brush.Freeze();compactArt.Background=largeArt.Background=brush;var background=new ImageBrush(artwork){Stretch=Stretch.UniformToFill,Opacity=.045};background.Freeze();glow.Background=background;compactArt.Child=largeArt.Child=null;}else {compactArt.Background=largeArt.Background=QuickPanel.Card;compactArt.Child=new Glyph("volume",16);largeArt.Child=new Glyph("volume",23);glow.Background=null;}}
            if(artwork==null&&compactArt.Child==null){compactArt.Background=largeArt.Background=QuickPanel.Card;compactArt.Child=new Glyph("volume",16);largeArt.Child=new Glyph("volume",23);}
            if(!seeking&&now>seekUntil)seek.Value=media.Duration>0?media.CurrentPosition(now)/media.Duration*100:0;seek.IsEnabled=media.CanSeek&&media.Duration>0;elapsed.Text=media.Duration>0?Clock(media.CurrentPosition(now)):"실시간";duration.Text=media.Duration>0?Clock(media.Duration):"";
            layout=NowBarLayout.Fit(screen.Bounds.Width/scale,screen.Bounds.Height/scale,startX,dockY,title.NaturalWidth,expanded);title.Running=!expanded&&!motionTimer.IsEnabled&&Motion.Enabled;
            if(!wanted||!IsVisible){wanted=true;expanded=false;layout=NowBarLayout.Fit(screen.Bounds.Width/scale,screen.Bounds.Height/scale,startX,dockY,title.NaturalWidth,false);compact.Opacity=1;compact.IsHitTestVisible=true;expandedContent.Visibility=Visibility.Collapsed;SetActivation(false);new WindowInteropHelper(this).EnsureHandle();Native.SetWindowPos(new WindowInteropHelper(this).Handle,IntPtr.Zero,screen.Bounds.Left+12,screen.Bounds.Top+12,0,0,0x1|0x4|0x10);current=new Rect(12,layout.CompactY,Motion.Enabled?44:layout.CompactWidth,44);target=Rect.Empty;EnsureHost(true);Place();}
            compact.Width=layout.CompactWidth;EnsureHost(false);Resize(layout.Bounds);
        }
        internal void SetExpanded(bool value)
        {
            if(closed||!wanted||layout==null||expanded==value)return;expanded=value;if(value){previousWindow=Native.GetForegroundWindow();shell.NowBarOpening(this);}title.Running=false;compact.IsHitTestVisible=!value;expandedContent.IsHitTestVisible=value;expandedContent.Visibility=Visibility.Visible;
            Motion.Fade(compact,compact.Opacity,value?0:1,value?90:160);
            if(value)Motion.Fade(expandedContent,expandedContent.Opacity,1,210);else{double before=expandedContent.Opacity;expandedContent.BeginAnimation(OpacityProperty,null);expandedContent.Opacity=0;if(Motion.Enabled){var fade=Motion.Tween(before,0,90);fade.Completed+=(s,e)=>{if(!expanded)expandedContent.Visibility=Visibility.Collapsed;};expandedContent.BeginAnimation(OpacityProperty,fade);}else expandedContent.Visibility=Visibility.Collapsed;}glow.Opacity=value?1:.2;SetActivation(value);
            var bounds=value?new Rect(12,Math.Max(12,layout.CompactY-8-Math.Min(188,layout.CompactY-20)),Math.Min(400,screen.Bounds.Width/(double)(Native.GetDpiForWindow(new WindowInteropHelper(this).Handle)/96.0)-24),Math.Min(188,layout.CompactY-20)):new Rect(12,layout.CompactY,layout.CompactWidth,44);Resize(bounds);
            if(value){Activate();play.Focus();}else {if(Native.GetForegroundWindow()==new WindowInteropHelper(this).Handle&&Native.IsWindow(previousWindow))Native.Activate(previousWindow);}
        }
        void SetActivation(bool value){var h=new WindowInteropHelper(this).Handle;if(h==IntPtr.Zero)return;int style=Native.GetWindowLong(h,-20);Native.SetWindowLong(h,-20,value?style&~0x08000000:style|0x08000000);}
        void Resize(Rect bounds)
        {
            if(Math.Abs(target.X-bounds.X)<.1&&Math.Abs(target.Y-bounds.Y)<.1&&Math.Abs(target.Width-bounds.Width)<.1&&Math.Abs(target.Height-bounds.Height)<.1)return;from=current;target=bounds;motionTimer.Stop();FrameCount=0;MaximumFrameGap=0;lastFrame=0;title.Running=false;var cache=new BitmapCache(Math.Max(1,Math.Min(2,monitorScale))){EnableClearType=false,SnapsToDevicePixels=true};compact.CacheMode=cache;expandedContent.CacheMode=cache;if(!Motion.Enabled){current=target;Place();compact.CacheMode=expandedContent.CacheMode=null;if(!expanded)expandedContent.Visibility=Visibility.Collapsed;return;}watch.Restart();motionTimer.Start();
        }
        void Frame()
        {
            if(closed||!wanted){motionTimer.Stop();return;}double milliseconds=watch.Elapsed.TotalMilliseconds;if(lastFrame>0)MaximumFrameGap=Math.Max(MaximumFrameGap,milliseconds-lastFrame);lastFrame=milliseconds;FrameCount++;
            double t=Math.Min(1,milliseconds/300),ease=1-Math.Pow(1-t,3);current=new Rect(from.X+(target.X-from.X)*ease,from.Y+(target.Y-from.Y)*ease,from.Width+(target.Width-from.Width)*ease,from.Height+(target.Height-from.Height)*ease);Place();
            if(t>=1){motionTimer.Stop();compact.CacheMode=expandedContent.CacheMode=null;if(!expanded)expandedContent.Visibility=Visibility.Collapsed;title.Running=!expanded&&Motion.Enabled;}
        }
        void EnsureHost(bool show)
        {
            if(layout==null)return;double width=Math.Max(1,Math.Min(400,screen.Bounds.Width/monitorScale-24)),top=Math.Max(12,layout.CompactY-196),height=layout.CompactY+44-top;var next=new Rect(12,top,width,height);
            expandedContent.Width=Math.Max(1,width-28);
            if(show||next!=hostBox){hostBox=next;Width=hostBox.Width;Height=hostBox.Height;Canvas.SetLeft(Surface,current.X-hostBox.X);Canvas.SetTop(Surface,current.Y-hostBox.Y);Surface.Width=current.Width;Surface.Height=current.Height;BackdropClip=new Rect(current.X-hostBox.X,current.Y-hostBox.Y,current.Width,current.Height);
                if(show)ShowAt(screen.Bounds,hostBox.X,hostBox.Y,hostBox.Width,hostBox.Height);else Native.Place(this,screen.Bounds,hostBox.X,hostBox.Y,hostBox.Width,hostBox.Height);}
        }
        internal void ConstrainDock(double startX,double dockY){dockStart=startX;dockTop=dockY;if(wanted&&IsVisible)Place();}
        void Place()
        {
            // Only the finished 44-DIP capsule is bounded by the app dock. The popup
            // follows one uninterrupted curve while its controls fade out.
            if(current.Height<=44.01&&current.Bottom>dockTop+.1){double room=dockStart-24;if(room<132)current=new Rect(current.X,Math.Min(current.Y,dockTop-52),current.Width,current.Height);else if(current.Width>room)current=new Rect(current.X,current.Y,Math.Max(1,room),current.Height);}
            Surface.Width=current.Width;Surface.Height=current.Height;Canvas.SetLeft(Surface,current.X-hostBox.X);Canvas.SetTop(Surface,current.Y-hostBox.Y);capsuleClip.Rect=new Rect(0,0,current.Width,current.Height);capsuleClip.RadiusX=capsuleClip.RadiusY=22;BackdropClip=new Rect(current.X-hostBox.X,current.Y-hostBox.Y,current.Width,current.Height);Native.Round(this,22);
        }
        void ApplyPalette(System.Windows.Media.Imaging.BitmapSource source)
        {
            Palette=ArtworkPalette.Read(source);var previous=Surface.Background as LinearGradientBrush;var tint=(LinearGradientBrush)Palette.Glass.Clone();
            if(previous!=null&&previous.GradientStops.Count==tint.GradientStops.Count&&Motion.Enabled&&IsVisible)for(int i=0;i<tint.GradientStops.Count;i++)tint.GradientStops[i].BeginAnimation(GradientStop.ColorProperty,new ColorAnimation(previous.GradientStops[i].Color,tint.GradientStops[i].Color,TimeSpan.FromMilliseconds(260)){FillBehavior=FillBehavior.Stop});
            SetMaterial(tint,Palette.Solid);playGlyph.Color=Palette.Ink;playGlyph.InvalidateVisual();artist.Foreground=elapsed.Foreground=duration.Foreground=Paint.Brush("#FFE9EEEB");
        }
        async void Seek(double value){seekPending=value;seekUntil=DateTime.UtcNow.AddSeconds(1);if(seeking)return;seeking=true;try{while(seekPending.HasValue&&!closed){double next=seekPending.Value;seekPending=null;if(!await seekCommand(next))break;}}catch(Exception error){AppLog.Write("nowbar-seek",error.Message);}finally{seeking=false;seekPending=null;}}
        internal static string Clock(double seconds){seconds=Double.IsNaN(seconds)||Double.IsInfinity(seconds)?0:Math.Max(0,seconds);var t=TimeSpan.FromSeconds(seconds);return t.TotalHours>=1?((int)t.TotalHours)+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00"):((int)t.TotalMinutes)+":"+t.Seconds.ToString("00");}
    }
}
