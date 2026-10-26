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
        readonly Button compact;readonly MediaPlayerView expandedContent;readonly MarqueeTitle title=new MarqueeTitle();
        readonly Grid compactRow;bool circle,retainedHost;
        readonly Border compactArt=new Border {Width=28,Height=28,CornerRadius=new CornerRadius(8),HorizontalAlignment=HorizontalAlignment.Left},glow=new Border {CornerRadius=new CornerRadius(22),Opacity=.2};
        readonly Stopwatch watch=new Stopwatch();bool animating;
        TimeSpan lastRendering=TimeSpan.MinValue;
        IDisposable outsideClick;
        Rect current,from,target,hostBox;NowBarLayout layout;double dockStart,dockTop,monitorScale=1;
        readonly Canvas hostCanvas=new Canvas();readonly RectangleGeometry capsuleClip=new RectangleGeometry();
        internal int FrameCount {get;private set;}internal double MaximumFrameGap {get;private set;}internal double MaximumPlaceMilliseconds {get;private set;}double lastFrame;
        internal ArtworkPalette Palette {get;private set;}MediaState media;ImageSource artwork;DateTime lastActive;bool expanded,wanted,closed;IntPtr previousWindow;
        internal string PlayKind {get{return expandedContent.PlayKind;}}internal bool Expanded {get{return expanded;}}internal Rect Shape {get{return current;}}internal double TitleOffset {get{return title.Offset;}}internal bool MarqueeOverflow {get{return title.NaturalWidth>title.ActualWidth+.5;}}
        internal Button CompactButton {get{return compact;}}internal Button PlayButton {get{return expandedContent.PlayButton;}}internal Button NextButton {get{return expandedContent.NextButton;}}internal Button PreviousButton {get{return expandedContent.PreviousButton;}}internal PillSlider SeekBar {get{return expandedContent.SeekBar;}}
        internal bool OutputBudsIcon {get{return expandedContent.OutputBudsIcon;}}
        internal bool TitleAnimating {get{return title.IsAnimating;}}
        internal bool ReservesSpace {get{return wanted&&media!=null&&(DateTime.UtcNow-lastActive).TotalMilliseconds<900;}}
        internal bool LargeCoverVisible {get{return expanded&&expandedContent.LargeCoverVisible;}}
        internal bool ThumbnailOnly {get{return circle;}}
        internal bool HasRetainedArtwork {get{return artwork!=null;}}
        internal bool Modern {get{return expandedContent.Modern;}}
        internal Action DismissedOutside;
        internal NowBar(ShellController controller,Forms.Screen monitor,Action<string> dispatch=null,Func<double,Task<bool>> seekDispatch=null)
        {
            shell=controller;screen=monitor;command=dispatch??controller.Media;seekCommand=seekDispatch??controller.SeekMedia;Title="One TaskBar · 미디어 플레이어";Width=132;Height=44;Radius=22;Dark=true;ShowActivated=false;Surface.CornerRadius=new CornerRadius(22);Surface.Background=QuickPanel.Glass;Palette=ArtworkPalette.Neutral;Surface.BorderThickness=new Thickness(0);Surface.BorderBrush=Brushes.Transparent;
            title.Centered=Preferences.Current.UIVersion>=90;
            var root=new Grid();root.Children.Add(glow);
            var small=new Grid {Height=44,Margin=new Thickness(10,0,10,0)};compactRow=small;small.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(36)});small.ColumnDefinitions.Add(new ColumnDefinition());small.Children.Add(compactArt);Grid.SetColumn(title,1);small.Children.Add(title);
            compact=Paint.Button(small,()=>SetExpanded(true),"미디어 플레이어 · 재생 컨트롤 열기");compact.Background=Brushes.Transparent;compact.Padding=new Thickness(0);compact.Height=44;compact.HorizontalContentAlignment=HorizontalAlignment.Stretch;compact.VerticalAlignment=VerticalAlignment.Bottom;root.Children.Add(compact);
            expandedContent=new MediaPlayerView(command,seekCommand,()=>{SetExpanded(false);shell.ShowDetail("sound");},()=>SetExpanded(false),openApp:shell.OpenMediaApplication,sourceApp:id=>MediaApplication.Resolve(id,System.Linq.Enumerable.Concat(shell.Running,shell.Apps))) {VerticalAlignment=VerticalAlignment.Bottom,Visibility=Visibility.Collapsed,Opacity=0};
            root.Children.Add(expandedContent);Surface.Child=root;var decorator=(AdornerDecorator)Content;decorator.Child=null;hostCanvas.Children.Add(Surface);decorator.Child=hostCanvas;Surface.Clip=capsuleClip;compact.HorizontalAlignment=HorizontalAlignment.Left;expandedContent.HorizontalAlignment=HorizontalAlignment.Left;
            SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLong(h,-20,(Native.GetWindowLong(h,-20)|0x80|0x08000000)&~0x40000);var source=HwndSource.FromHwnd(h);source.CompositionTarget.RenderMode=RenderMode.SoftwareOnly;source.AddHook((IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled)=>{if(msg==0x21&&!expanded){handled=true;return new IntPtr(3);}return IntPtr.Zero;});};
            Deactivated+=(s,e)=> {if(expanded&&!Program.Inspectable&&!IsMouseOver)CollapseOutside();};Closed+=(s,e)=> {closed=true;StopOutsideClick();StopMotion();title.Running=false;compactArt.Background=glow.Background=null;media=null;};
            IsVisibleChanged+=(s,e)=>{if(!IsVisible&&!wanted){media=null;artwork=null;compactArt.Background=glow.Background=null;compactArt.Child=null;expandedContent.SetPoster(0);expandedContent.Update(new MediaState(),null);}};
            MouseEnter+=(s,e)=>title.Engaged=true;MouseLeave+=(s,e)=>title.Engaged=false;
        }
        protected override void OnPreviewKeyDown(KeyEventArgs e){if(e.Key==Key.Escape&&expanded){SetExpanded(false);e.Handled=true;return;}base.OnPreviewKeyDown(e);}
        static Button Transport(string icon,Action action,string hint,double size){var button=Paint.Button(new Glyph(icon,20),action,hint);button.Width=button.Height=size;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;return button;}
        internal void Update(MediaState state,double scale,double startX,double dockY,bool allowed)
        {
            if(closed)return;monitorScale=scale;dockStart=startX;dockTop=dockY;var now=DateTime.UtcNow;bool active=state!=null&&state.Available&&state.Active;if(active){lastActive=now;media=state;}bool present=media!=null&&(active||(now-lastActive).TotalMilliseconds<900);
            if(!allowed||!present){if(wanted||IsVisible){wanted=false;StopOutsideClick();StopMotion();expanded=false;title.Running=false;SetActivation(false);if(!allowed)HideImmediately();else Hide();}return;}
            if(title.Text!=media.Title){title.Text=media.Title;compact.ToolTip=media.Title+" · 재생 컨트롤 열기";System.Windows.Automation.AutomationProperties.SetName(compact,(string)compact.ToolTip);}
            if(artwork!=media.Artwork){artwork=media.Artwork;ApplyPalette(media.Artwork);if(artwork!=null){var brush=new ImageBrush(artwork){Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center};brush.Freeze();compactArt.Background=brush;glow.Background=Palette.PlayerSurface(media.Artwork);compactArt.Child=null;}else {compactArt.Background=QuickPanel.Card;compactArt.Child=new Glyph("volume",16);glow.Background=null;}}
            if(artwork==null&&compactArt.Child==null){compactArt.Background=QuickPanel.Card;compactArt.Child=new Glyph("volume",16);}
            layout=Fit(expanded);expandedContent.SetPoster(layout.PosterEdge);expandedContent.FitPopup(layout.PopupHeight);if(expanded)expandedContent.Update(media,shell.Services.OutputName);title.ConservePower=shell.Power.Known&&!shell.Power.Connected;title.Running=!expanded&&!animating&&Motion.Enabled;
            if(!wanted||!IsVisible){wanted=true;expanded=false;retainedHost=false;layout=Fit(false);compact.Opacity=1;compact.IsHitTestVisible=true;expandedContent.Visibility=Visibility.Collapsed;SetActivation(false);new WindowInteropHelper(this).EnsureHandle();Native.SetWindowPos(new WindowInteropHelper(this).Handle,IntPtr.Zero,screen.Bounds.Left+12,screen.Bounds.Top+12,0,0,0x1|0x4|0x10);current=new Rect(layout.Bounds.X,layout.Bounds.Y,Motion.Enabled?44:layout.CompactWidth,44);target=Rect.Empty;EnsureHost(true);Place();}
            compact.Width=layout.CompactWidth;bool smallCover=layout.CompactWidth<84;if(circle!=smallCover){circle=smallCover;compactRow.Margin=new Thickness(circle?8:10,0,circle?8:10,0);compactRow.ColumnDefinitions[0].Width=new GridLength(circle?28:36);title.Visibility=circle?Visibility.Collapsed:Visibility.Visible;compactArt.CornerRadius=new CornerRadius(circle?14:8);}EnsureHost(false);Resize(layout.Bounds);
        }
        NowBarLayout Fit(bool open){return NowBarLayout.Fit(screen.Bounds.Width/monitorScale,screen.Bounds.Height/monitorScale,dockStart,dockTop,title.NaturalWidth,open,Preferences.Current.NowBarLargeCover&&media.Artwork!=null,DockLayout.StatusSize(screen.Bounds.Width/monitorScale,PowerDisplay.DockSlot(shell.Power,Preferences.Current.DesktopPowerWatts),Preferences.Current.DuoStatusIcon),Modern,DockGeometry.Edge);}
        internal void SetExpanded(bool value)
        {
            if(closed||!wanted||layout==null||expanded==value)return;expanded=value;if(value)retainedHost=true;layout=Fit(value);expandedContent.SetPoster(layout.PosterEdge);expandedContent.FitPopup(layout.PopupHeight);EnsureHost(false);StopOutsideClick();if(value){expandedContent.Update(media,shell.Services.OutputName);outsideClick=MenuDismiss.Watch(ContainsScreenPoint,CollapseOutside);previousWindow=Native.GetForegroundWindow();shell.NowBarOpening(this);}title.Running=false;compact.IsHitTestVisible=!value;expandedContent.IsHitTestVisible=value;expandedContent.Visibility=Visibility.Visible;
            Motion.Fade(compact,compact.Opacity,value?0:1,value?90:160);
            if(value)Motion.Fade(expandedContent,expandedContent.Opacity,1,210);else{double before=expandedContent.Opacity;expandedContent.BeginAnimation(OpacityProperty,null);expandedContent.Opacity=0;if(Motion.Enabled){var fade=Motion.Tween(before,0,90);fade.Completed+=(s,e)=>{if(!expanded)expandedContent.Visibility=Visibility.Collapsed;};expandedContent.BeginAnimation(OpacityProperty,fade);}else expandedContent.Visibility=Visibility.Collapsed;}glow.Opacity=value?1:.2;SetActivation(value);
            Resize(layout.Bounds);
            if(value){Activate();expandedContent.PlayButton.Focus();}else {if(Native.GetForegroundWindow()==new WindowInteropHelper(this).Handle&&Native.IsWindow(previousWindow))Native.Activate(previousWindow);}
        }
        void StopOutsideClick(){if(outsideClick!=null){outsideClick.Dispose();outsideClick=null;}}
        internal void CollapseOutside(){SetExpanded(false);if(DismissedOutside!=null)DismissedOutside();}
        bool ContainsScreenPoint(Native.PointI point){if(!expanded||!IsVisible)return false;Point first=Surface.PointToScreen(new Point()),last=Surface.PointToScreen(new Point(Surface.ActualWidth,Surface.ActualHeight));return point.X>=first.X&&point.X<last.X&&point.Y>=first.Y&&point.Y<last.Y;}
        void SetActivation(bool value){var h=new WindowInteropHelper(this).Handle;if(h==IntPtr.Zero)return;int style=Native.GetWindowLong(h,-20);Native.SetWindowLong(h,-20,value?style&~0x08000000:style|0x08000000);}
        void Resize(Rect bounds)
        {
            if(Math.Abs(target.X-bounds.X)<.1&&Math.Abs(target.Y-bounds.Y)<.1&&Math.Abs(target.Width-bounds.Width)<.1&&Math.Abs(target.Height-bounds.Height)<.1)return;from=current;target=bounds;StopMotion();FrameCount=0;MaximumFrameGap=MaximumPlaceMilliseconds=0;lastFrame=0;title.Running=false;if(!Motion.Enabled){current=target;Place();if(!expanded)expandedContent.Visibility=Visibility.Collapsed;return;}watch.Restart();animating=true;lastRendering=TimeSpan.MinValue;CompositionTarget.Rendering+=Rendering;
        }
        void StopMotion(){CompositionTarget.Rendering-=Rendering;animating=false;watch.Stop();}
        void Rendering(object sender,EventArgs args){var frame=args as RenderingEventArgs;if(frame!=null){if(frame.RenderingTime==lastRendering)return;lastRendering=frame.RenderingTime;}Frame();}
        void Frame()
        {
            if(closed||!wanted){StopMotion();return;}double milliseconds=watch.Elapsed.TotalMilliseconds;if(lastFrame>0)MaximumFrameGap=Math.Max(MaximumFrameGap,milliseconds-lastFrame);lastFrame=milliseconds;FrameCount++;
            double t=Math.Min(1,milliseconds/300),ease=1-Math.Pow(1-t,3);current=new Rect(from.X+(target.X-from.X)*ease,from.Y+(target.Y-from.Y)*ease,from.Width+(target.Width-from.Width)*ease,from.Height+(target.Height-from.Height)*ease);long stamp=Program.Inspectable?Stopwatch.GetTimestamp():0;Place();if(stamp!=0)MaximumPlaceMilliseconds=Math.Max(MaximumPlaceMilliseconds,(Stopwatch.GetTimestamp()-stamp)*1000.0/Stopwatch.Frequency);
            if(t>=1){StopMotion();if(!expanded)expandedContent.Visibility=Visibility.Collapsed;EnsureHost(false);title.Running=!expanded&&Motion.Enabled;}
        }
        void EnsureHost(bool show)
        {
            // Keep the native origin through the visible collapse. Rebasing a layered
            // HWND at the last frame can present its old pixels before WPF repaints,
            // while the separate blur HWND has already moved. The input clips still
            // cover only the capsule; a hidden/reopened player starts compact again.
            if(layout==null)return;bool smallHost=!retainedHost&&!expanded&&!animating&&current.Height<=44.01;var folded=Fit(false).Bounds;var opened=Fit(true).Bounds;double popupWidth=opened.Width;var next=smallHost?Rect.Union(folded,current):Rect.Union(Rect.Union(folded,opened),current);
            next=new Rect(new Point(Math.Floor(next.Left*monitorScale)/monitorScale,Math.Floor(next.Top*monitorScale)/monitorScale),new Point(Math.Ceiling(next.Right*monitorScale)/monitorScale,Math.Ceiling(next.Bottom*monitorScale)/monitorScale));
            expandedContent.Width=Math.Max(1,popupWidth-28);
            if(show||next!=hostBox){hostBox=next;Width=hostBox.Width;Height=hostBox.Height;Canvas.SetLeft(Surface,current.X-hostBox.X);Canvas.SetTop(Surface,current.Y-hostBox.Y);Surface.Width=current.Width;Surface.Height=current.Height;BackdropClip=new Rect(current.X-hostBox.X,current.Y-hostBox.Y,current.Width,current.Height);
                if(show)ShowAt(screen.Bounds,hostBox.X,hostBox.Y,hostBox.Width,hostBox.Height);else Native.Place(this,screen.Bounds,hostBox.X,hostBox.Y,hostBox.Width,hostBox.Height);hostCanvas.UpdateLayout();Native.Round(this,22);}
        }
        internal void ConstrainDock(double startX,double dockY){if(Math.Abs(dockStart-startX)<.01&&Math.Abs(dockTop-dockY)<.01)return;dockStart=startX;dockTop=dockY;if(wanted&&IsVisible)Place();}
        void Place()
        {
            // Only the finished 44-DIP capsule is bounded by the app dock. The popup
            // follows one uninterrupted curve while its controls fade out.
            if(!DockGeometry.Vertical(DockGeometry.Edge)&&current.Height<=44.01&&current.Bottom>dockTop+.1){double room=Math.Max(1,dockStart-24);current=new Rect(current.X,dockTop,Math.Min(current.Width,room),44);}
            Surface.Width=current.Width;Surface.Height=current.Height;Canvas.SetLeft(Surface,current.X-hostBox.X);Canvas.SetTop(Surface,current.Y-hostBox.Y);capsuleClip.Rect=new Rect(0,0,current.Width,current.Height);capsuleClip.RadiusX=capsuleClip.RadiusY=22;
            // Layered WPF windows can retain stale GPU bitmap caches during logon/device startup.
            // Render this small overlay without those caches, and arrange before the live-blur clip.
            hostCanvas.UpdateLayout();Surface.InvalidateVisual();BackdropClip=new Rect(current.X-hostBox.X,current.Y-hostBox.Y,current.Width,current.Height);Native.Round(this,22);
        }
        void ApplyPalette(System.Windows.Media.Imaging.BitmapSource source)
        {
            Palette=ArtworkPalette.Read(source);var previous=Surface.Background as LinearGradientBrush;var tint=(LinearGradientBrush)Palette.Glass.Clone();
            if(previous!=null&&previous.GradientStops.Count==tint.GradientStops.Count&&Motion.Enabled&&IsVisible)for(int i=0;i<tint.GradientStops.Count;i++)tint.GradientStops[i].BeginAnimation(GradientStop.ColorProperty,new ColorAnimation(previous.GradientStops[i].Color,tint.GradientStops[i].Color,TimeSpan.FromMilliseconds(260)){FillBehavior=FillBehavior.Stop});
            SetMaterial(tint,Palette.Solid);expandedContent.SetPalette(Palette);
        }
        internal static string Clock(double seconds){seconds=Double.IsNaN(seconds)||Double.IsInfinity(seconds)?0:Math.Max(0,seconds);var t=TimeSpan.FromSeconds(seconds);return t.TotalHours>=1?((int)t.TotalHours)+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00"):((int)t.TotalMinutes)+":"+t.Seconds.ToString("00");}
    }
}
