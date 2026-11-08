using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // An Android-style status row along the top edge, in two transparent pieces so the middle stays free for the
    // Dynamic Island. The left piece is the clock and date (the notification center), the playing media and the apps
    // that have notifications; the right piece is the network, sound and battery (quick settings). It sits in the same
    // reserved band as the island, which is as thick as the idle island.
    internal sealed class TopStatusBar : GlassWindow
    {
        internal enum Side {Left,Right}
        readonly ShellController shell;
        readonly Side side;
        readonly StackPanel row=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        readonly TextBlock time=Paint.TextBlock("",13,Paint.Text),date=Paint.TextBlock("",12,Paint.Text);
        readonly StackPanel icons=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        readonly Border chip=new Border {Height=18,CornerRadius=new CornerRadius(9),Padding=new Thickness(3,0,9,0),Visibility=Visibility.Collapsed,VerticalAlignment=VerticalAlignment.Center};
        readonly Border chipArt=new Border {Width=13,Height=13,CornerRadius=new CornerRadius(4),Margin=new Thickness(0,0,6,0)};
        readonly TextBlock chipTitle=Paint.TextBlock("",11.5,Paint.ControlWhite);
        readonly Glyph wifi=new Glyph("wifi",19),volume=new Glyph("volume",17);
        readonly TranslateTransform iconShift=new TranslateTransform();
        bool shifted;double shiftBy;Button chipButton;bool chipShown;
        // The dock's own battery pill, so the icon and the charging animation are the same ones.
        internal readonly BatteryPill Battery=new BatteryPill {Width=46,Height=18,Compact=true,Margin=new Thickness(7,0,0,0),VerticalAlignment=VerticalAlignment.Center};
        bool charging;
        // Over the black band beside a Dynamic Island the three status icons collapse into the dock's Duo glyph.
        readonly DuoStatusGlyph duo=new DuoStatusGlyph {Width=22.6,Height=24,Margin=new Thickness(2,0,2,0),VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed};
        bool duoMode;
        Popup card;MediaPlayerView player;Border cardSurface;
        Forms.Screen screen;double scale=1;bool bottom,closed;
        Rect host;string iconKey="";ImageSource chipArtwork;
        internal Side Placement {get{return side;}}
        bool concealed;
        readonly TranslateTransform lift=new TranslateTransform();
        internal bool OnBlack {get;private set;}
        // Over the black safe-area band the bar is light-on-dark, and it can be tucked away until the pointer reaches the edge.
        internal void Conceal(bool hidden,bool onBlack)
        {
            if(closed)return;
            if(OnBlack!=onBlack){OnBlack=onBlack;layoutKey="";if(IsVisible)Refresh();}
            if(concealed==hidden)return;
            concealed=hidden;
            IsHitTestVisible=!hidden;
            var fade=new System.Windows.Media.Animation.DoubleAnimation(hidden?0:1,TimeSpan.FromMilliseconds(hidden?140:180)){FillBehavior=System.Windows.Media.Animation.FillBehavior.HoldEnd};
            row.BeginAnimation(UIElement.OpacityProperty,fade);
            row.RenderTransform=lift;
            var slide=new System.Windows.Media.Animation.DoubleAnimation(hidden?-6:0,TimeSpan.FromMilliseconds(hidden?140:220)){EasingFunction=new System.Windows.Media.Animation.CubicEase {EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut},FillBehavior=System.Windows.Media.Animation.FillBehavior.HoldEnd};
            lift.BeginAnimation(TranslateTransform.YProperty,slide);
        }
        internal bool CardOpen {get{return card!=null&&card.IsOpen;}}
        internal TopStatusBar(ShellController controller,Side placement)
        {
            shell=controller;side=placement;
            Title="One TaskBar · 상단 상태 "+(placement==Side.Left?"왼쪽":"오른쪽");
            ShowActivated=false;OpaqueSurface=true;Radius=0;
            Height=IslandSafeArea.Height;Width=80;
            SetMaterial(Brushes.Transparent,Brushes.Transparent);
            Surface.BorderBrush=Brushes.Transparent;Surface.BorderThickness=new Thickness(0);Surface.CornerRadius=new CornerRadius(0);
            if(side==Side.Left)BuildLeft();else BuildRight();
            row.Margin=new Thickness(0);
            row.Height=IslandSafeArea.Height;
            row.VerticalAlignment=VerticalAlignment.Top;
            // A nearly clear fill makes every pixel of the piece take the click, including gaps between its parts.
            Surface.Background=Hit;
            Surface.MouseLeftButtonUp+=(s,e)=>{if(e.Handled)return;e.Handled=true;if(side==Side.Left)shell.ToggleNotifications(Dock(),true);else shell.ToggleQuick(Dock(),false,true);};
            Surface.Child=row;
            SourceInitialized+=(s,e)=>
            {
                var handle=new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x80|0x08000000)&~0x40000);
                HwndSource.FromHwnd(handle).AddHook((IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled)=>{if(msg==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});
            };
            Closed+=(s,e)=>{closed=true;StopOutside();if(card!=null)card.IsOpen=false;chipArt.Background=null;icons.Children.Clear();};
        }
        static readonly Brush Hit=HitBrush();
        static Brush HitBrush(){var brush=new SolidColorBrush(Color.FromArgb(3,0,0,0));brush.Freeze();return brush;}
        static readonly Brush darkInk=Paint.Brush("#FF10151C"),lightInk=Paint.Brush("#FFF7FAFC");
        static readonly System.Windows.Media.Effects.Effect darkHalo=Halo(Colors.Black),lightHalo=Halo(Colors.White);
        static System.Windows.Media.Effects.Effect Halo(Color color){var effect=new System.Windows.Media.Effects.DropShadowEffect {Color=color,BlurRadius=4,ShadowDepth=0,Opacity=.5};effect.Freeze();return effect;}
        // White text over dark wallpaper, dark over light; read from the strip just under the bar a few times a second.
        DateTime inkAt=DateTime.MinValue;bool? lightText;bool sampling;
        Brush Ink
        {
            get
            {
                if(OnBlack)return lightInk;
                if(DateTime.UtcNow>=inkAt&&screen!=null&&host.Width>0&&!sampling)
                {
                    inkAt=DateTime.UtcNow.AddSeconds(2.5);
                    int thick=(int)Math.Ceiling(IslandSafeArea.Height*scale),gap=(int)Math.Ceiling(3*scale);
                    var box=Native.PlacementBounds(screen.Bounds,scale,host.X,host.Y,host.Width,host.Height);
                    // The bar itself is read, since that is what its text sits on. Its own text is part of what the screen
                    // shows there, so it is taken back out; reading the strip beside it instead followed whatever window sat below.
                    var area=new System.Drawing.Rectangle(box.Left,box.Top,box.Right-box.Left,Math.Min(box.Bottom-box.Top,thick));
                    // The bar itself is read, since that is what its text sits on; its own text is part of what the screen
                    // shows there and is taken back out. The read can take tens of milliseconds, so it runs off the
                    // interface thread and the result is applied when it comes back.
                    bool wasLight=lightText==true;sampling=true;
                    BackgroundWork.Run(()=>LiquidReadability.Sample(area)).ContinueWith(task=>
                    {
                        Dispatcher.BeginInvoke(new Action(()=>
                        {
                            sampling=false;
                            if(closed||task.Status!=System.Threading.Tasks.TaskStatus.RanToCompletion||task.Result<0)return;
                            const double coverage=.12;
                            double own=lightText==false?.06:.97;
                            double behind=Math.Max(0,Math.Min(1,(task.Result-coverage*own)/(1-coverage)));
                            // Two thresholds, so a value near the middle cannot make the text flip back and forth.
                            bool light=lightText.HasValue?(lightText.Value?behind<.60:behind<.50):behind<.55;
                            if(lightText!=light){lightText=light;layoutKey="";Refresh();}
                        }));
                    });
                }                return lightText.HasValue?(lightText.Value?lightInk:darkInk):Glass95.DockForeground;
            }
        }
        void BuildLeft()
        {
            var clock=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            time.FontWeight=FontWeights.SemiBold;
            date.Margin=new Thickness(9,0,0,0);
            clock.Children.Add(time);clock.Children.Add(date);
            var clockButton=Paint.Button(clock,()=>shell.ToggleNotifications(Dock(),true),"알림센터");
            clockButton.Background=Hit;clock.Background=Hit;clockButton.Padding=new Thickness(8,0,8,0);clockButton.VerticalAlignment=VerticalAlignment.Stretch;
            row.Children.Add(clockButton);
            var content=new StackPanel {Orientation=Orientation.Horizontal};
            content.Children.Add(chipArt);content.Children.Add(chipTitle);
            chipTitle.TextTrimming=TextTrimming.CharacterEllipsis;chipTitle.MaxWidth=170;
            chip.Child=content;
            chipButton=Paint.Button(chip,ToggleCard,"미디어 컨트롤");
            chipButton.Background=Hit;chipButton.Padding=new Thickness(10,0,0,0);chipButton.VerticalAlignment=VerticalAlignment.Stretch;
            row.Children.Add(chipButton);
            var iconButton=Paint.Button(icons,()=>shell.ToggleNotifications(Dock(),true),"알림센터");
            iconButton.Background=Hit;icons.Background=Hit;iconButton.RenderTransform=iconShift;iconButton.Padding=new Thickness(8,0,8,0);iconButton.VerticalAlignment=VerticalAlignment.Stretch;
            row.Children.Add(iconButton);
        }
        void BuildRight()
        {
            var cluster=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            wifi.Margin=new Thickness(0,0,7,0);
            cluster.Children.Add(wifi);cluster.Children.Add(volume);
            cluster.Children.Add(Battery);cluster.Children.Add(duo);
            Battery.MorphChanged=morph=>{bool next=morph>.001;if(next==charging)return;charging=next;if(!closed)Refresh();};
            row.HorizontalAlignment=HorizontalAlignment.Right;row.VerticalAlignment=VerticalAlignment.Top;
            var button=Paint.Button(cluster,()=>shell.ToggleQuick(Dock(),false,true),"빠른 설정");
            button.Background=Hit;cluster.Background=Hit;button.Padding=new Thickness(8,0,8,0);button.VerticalAlignment=VerticalAlignment.Stretch;
            row.Children.Add(button);
        }
        DockHost Dock(){return shell.DockFor(screen);}
        // Called by the shell on its regular pulse. Cheap when nothing changed.
        internal void Present(Forms.Screen monitor,double dpi,bool atBottom)
        {
            if(closed)return;
            screen=monitor;scale=dpi;bottom=atBottom;
            Refresh();
        }
        void Refresh()
        {
            var ink=Ink;
            // A faint halo in the opposite tone keeps the text readable where the picture behind it is busy.
            var effect=!OnBlack&&lightText==false?lightHalo:null;
            if(side==Side.Left)RefreshLeft(ink,effect);else RefreshRight(ink,effect);
            // Measuring and moving the window cost real time on every shell pass; they happen only when what is shown changed.
            string key=side==Side.Left?time.Text+"|"+date.Text+"|"+chip.Visibility+chipTitle.Text+"|"+iconKey+"|"+shifted+"|"+(lightText==true):wifi.Kind+"|"+volume.Kind+"|"+duoMode+"|"+Battery.Visibility+"|"+Battery.State.Level+"|"+Battery.State.Connected+"|"+charging+"|"+(lightText==true);
            if(key==layoutKey&&IsVisible&&screen!=null&&host.Width>0)return;
            layoutKey=key;
            if(key!=checkedKey){checkedKey=key;Dispatcher.BeginInvoke(new Action(()=>
            {
                if(closed||!IsVisible||shifted)return;
                row.Measure(new Size(Double.PositiveInfinity,IslandSafeArea.Height));
                if(Math.Ceiling(row.DesiredSize.Width+4)>host.Width+1){layoutKey="";Refresh();}
            }),System.Windows.Threading.DispatcherPriority.Loaded);}
            row.Measure(new Size(Double.PositiveInfinity,IslandSafeArea.Height));
            double maximum=Math.Max(80,screen.Bounds.Width/scale*(side==Side.Left?.34:.28));
            // While the battery pill celebrates it grows to the left and a little taller: give it room, then take it back.
            double extra=side==Side.Right&&charging?Math.Max(0,Battery.ExpandedWidth-Battery.Width)+8:0;
            row.Margin=new Thickness(0,side==Side.Right&&charging?1:0,0,0);
            double width=Math.Ceiling(Math.Min(maximum+extra,row.DesiredSize.Width+4+extra));
            var next=Place(width);
            if(host!=next||!IsVisible)
            {
                host=next;Width=host.Width;Height=host.Height;
                new WindowInteropHelper(this).EnsureHandle();
                var box=Native.PlacementBounds(screen.Bounds,scale,host.X,host.Y,host.Width,host.Height);
                Native.PrepareMonitor(this,screen.Bounds);
                if(!IsVisible)((Window)this).Show();
                Native.SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),box.Left,box.Top,box.Right-box.Left,box.Bottom-box.Top,0x10);
            }
        }
        Rect Place(double width)
        {
            double wide=screen.Bounds.Width/scale,top=bottom?screen.Bounds.Height/scale-IslandSafeArea.Height:0;
            double x=side==Side.Left?6:wide-width-6;
            return new Rect(x,top,width,IslandSafeArea.Height+(side==Side.Right&&charging?4:0));
        }
        void RefreshLeft(Brush ink,System.Windows.Media.Effects.Effect effect)
        {
            var now=DateTime.Now;
            string clock=now.ToString("h:mm",CultureInfo.InvariantCulture),day=now.ToString("M월 d일 dddd",CultureInfo.GetCultureInfo("ko-KR"));
            if(time.Text!=clock)time.Text=clock;
            if(date.Text!=day)date.Text=day;
            time.Foreground=date.Foreground=ink;time.Effect=date.Effect=effect;
            var media=shell.Services.Media;
            bool playing=Preferences.Current.ShowNowBar&&media!=null&&media.Available&&media.Active;
            chip.Visibility=playing?Visibility.Visible:Visibility.Collapsed;
            if(chipButton.Visibility!=chip.Visibility)chipButton.Visibility=chip.Visibility;
            // The chip arrives the way the NowBar does at the dock: a short rise with a soft settle and fade.
            if(playing&&!chipShown&&Motion.Enabled&&IsVisible&&!closing)Motion.Launcher(chip,true,true);
            chipShown=playing;
            if(playing)
            {
                chipTitle.Text=String.IsNullOrWhiteSpace(media.Artist)?media.Title:media.Title+" · "+media.Artist;
                if(!Object.ReferenceEquals(chipArtwork,media.Artwork))
                {
                    chipArtwork=media.Artwork;
                    if(chipArtwork==null)chipArt.Background=null;
                    else
                    {
                        var brush=new ImageBrush(ArtworkThumbnail.Square(chipArtwork,Math.Max(32,(int)Math.Ceiling(20*scale)))){Stretch=Stretch.UniformToFill};
                        RenderOptions.SetBitmapScalingMode(brush,BitmapScalingMode.HighQuality);brush.Freeze();chipArt.Background=brush;
                    }
                    var palette=chipArtwork==null?null:ArtworkPalette.Read(chipArtwork as BitmapSource);
                    var tone=palette==null?Color.FromRgb(48,52,60):palette.Background;
                    var fill=new SolidColorBrush(Color.FromArgb(235,tone.R,tone.G,tone.B));fill.Freeze();chip.Background=fill;
                }
                if(card!=null&&card.IsOpen&&player!=null&&!closing){player.Update(media,shell.Services.OutputName);cardSurface.Background=player.MediaBackground??cardSurface.Background;}
            }
            else if(card!=null&&card.IsOpen)FinishClose();
            // The apps that have notifications, once each, most recent first.
            var apps=shell.Notifications.Items.Where(n=>n!=null&&n.Icon!=null).OrderByDescending(n=>n.Created).GroupBy(n=>n.AppId??n.App).Select(g=>g.First()).Take(5).ToList();
            string key=String.Join("|",apps.Select(n=>(n.AppId??n.App)+":"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(n.Icon)));
            if(key!=iconKey)
            {
                iconKey=key;icons.Children.Clear();
                foreach(var entry in apps)
                {
                    ImageSource source;
                    try
                    {
                        source=ArtworkThumbnail.Square(entry.Icon,Math.Max(32,(int)Math.Ceiling(24*scale)));
                        if(source!=null&&!source.IsFrozen&&source.CanFreeze)source=source.Clone();
                        if(source!=null&&!source.IsFrozen&&source.CanFreeze)source.Freeze();
                    }
                    catch(Exception){continue;}
                    if(source==null)continue;
                    var image=new Image {Source=source,Width=14,Height=14,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,5,0),ToolTip=entry.App,SnapsToDevicePixels=true,UseLayoutRounding=true};
                    RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
                    icons.Children.Add(image);
                }
            }
        }
        void RefreshRight(Brush ink,System.Windows.Media.Effects.Effect effect)
        {
            bool radioOff=shell.Services.WifiOn==false;
            string wifiKind=NetworkRouteInfo.Glyph(shell.Services.NetworkRoute,shell.Services.WifiPresent,radioOff,shell.Wifi!=null,shell.Services.NetworkConnected);
            string soundKind=SoundIcon.For(shell.Audio.Value,shell.Audio.Muted,shell.Audio.Available);
            if(wifi.Kind!=wifiKind){wifi.Kind=wifiKind;wifi.InvalidateVisual();}
            wifi.Opacity=wifiKind=="wifi"||wifiKind=="network"||wifiKind=="ethernet"?1:.55;
            int generation=wifiKind=="wifi"?shell.WifiGeneration:0;
            if(wifi.Generation!=generation){wifi.Generation=generation;wifi.InvalidateVisual();}
            int signal=wifiKind=="wifi"?shell.WifiSignal:0,traffic=wifiKind=="wifi"?shell.WifiTraffic:-1;
            if(wifi.Signal!=signal||wifi.Traffic!=traffic){wifi.Signal=signal;wifi.Traffic=traffic;wifi.InvalidateVisual();}
            volume.Opacity=shell.Audio.Available?wifi.Opacity:.4;
            if(volume.Kind!=soundKind){volume.Kind=soundKind;volume.InvalidateVisual();}
            if(wifi.Color!=ink||volume.Color!=ink){wifi.Color=volume.Color=ink;wifi.InvalidateVisual();volume.InvalidateVisual();}
            wifi.Effect=volume.Effect=effect;
            var power=shell.Power;
            bool known=power!=null&&power.Known;
            // The top bar keeps the regular Wi-Fi, volume and battery icons, also over the black band.
            bool together=false;
            if(together!=duoMode)
            {
                duoMode=together;layoutKey="";
                var separate=together?Visibility.Collapsed:Visibility.Visible;
                wifi.Visibility=volume.Visibility=separate;
                duo.Visibility=together?Visibility.Visible:Visibility.Collapsed;
            }
            if(together)
            {
                duo.Update(power,wifiKind,signal,shell.Audio.Value,shell.Audio.Muted,shell.Audio.Available,ink);
                duo.Effect=effect;
                Battery.Visibility=Visibility.Collapsed;
                return;
            }
            Battery.Visibility=known?Visibility.Visible:Visibility.Collapsed;
            if(known){Battery.ExpandedWidth=96;Battery.Update(power);}
        }
        // The expanded media card is the pill's own look. It opens the way the Android status bar's does: the chip
        // swells into the card (a rounded clip growing from the chip's size to the card's, on the spring the rest of the
        // app uses) and the controls fade in; it folds back into the chip, and a click anywhere else folds it too.
        void ToggleCard()
        {
            var media=shell.Services.Media;
            if(card!=null&&card.IsOpen){CloseCard();return;}
            if(media==null||!media.Available||!media.Active)return;
            if(card==null)
            {
                player=new MediaPlayerView(shell.Media,shell.SeekMedia,()=>{CloseCard();shell.ShowDetail("sound");},collapse:CloseCard,openApp:id=>{CloseCard();shell.OpenMediaApplication(id);},sourceApp:id=>MediaApplication.Resolve(id,shell.Running.Concat(shell.Apps))) {Margin=new Thickness(16,8,16,10)};
                content=new Grid();
                content.Children.Add(player);
                miniArt=new Border {Width=13,Height=13,CornerRadius=new CornerRadius(4),Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center};
                miniTitle=Paint.TextBlock("",11.5,Paint.ControlWhite);miniTitle.TextTrimming=TextTrimming.CharacterEllipsis;miniTitle.MaxWidth=170;
                var mini=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(3,0,0,0),Height=18,VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Left,IsHitTestVisible=false};
                mini.Children.Add(miniArt);mini.Children.Add(miniTitle);
                miniHost=mini;
                var layers=new Grid();layers.Children.Add(content);layers.Children.Add(mini);
                cardSurface=new Border {Width=CardWidth,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Child=layers,SnapsToDevicePixels=true,Clip=cardClip};
                holder=new Grid {Margin=new Thickness(CardPad,0,CardPad,22)};holder.Children.Add(cardSurface);
                // Placed from the chip's own top-left corner. The card ends up just under the bar and starts as the chip.
                card=new Popup {AllowsTransparency=true,StaysOpen=true,Placement=PlacementMode.Relative,PlacementTarget=chip,HorizontalOffset=-CardPad,VerticalOffset=0,PopupAnimation=PopupAnimation.None,Child=holder};
                card.Closed+=(s,e)=>{closing=false;closeTimer.Stop();StopOutside();};
            }
            // The large cover, when it is switched on, makes the card taller the way it does for the NowBar.
            double room=screen==null?600:screen.Bounds.Height/scale-IslandSafeArea.Height-40;
            double edge=Preferences.Current.NowBarLargeCover&&media.Artwork!=null?Math.Min(288,Math.Min(CardWidth-56,room-178)):0;
            if(edge<120)edge=0;
            cardHeight=edge>0?edge+178:164;
            player.SetPoster(edge);player.FitPopup(cardHeight);
            // The card's rectangle starts at the chip and ends under the bar; this is how far down that is.
            double chipTop=chip.TranslatePoint(new Point(0,0),Surface).Y;
            drop=Math.Max(0,IslandSafeArea.Height-chipTop)+4;
            holder.Margin=new Thickness(CardPad,0,CardPad,22);
            cardSurface.Height=drop+cardHeight;
            content.Margin=new Thickness(0,drop,0,0);
            player.Update(media,shell.Services.OutputName);
            cardSurface.Background=player.MediaBackground??chip.Background??Paint.Brush("#F0242A33");
            miniArt.Background=chipArt.Background;miniTitle.Text=chipTitle.Text;
            card.PlacementTarget=chip;
            closing=false;closeTimer.Stop();
            // Start as the chip, so the first frame is not the full card.
            var small=ChipRect();
            cardClip.BeginAnimation(RectangleGeometry.RectProperty,null);cardClip.Rect=small;
            cardClip.BeginAnimation(RectangleGeometry.RadiusXProperty,null);cardClip.BeginAnimation(RectangleGeometry.RadiusYProperty,null);cardClip.RadiusX=cardClip.RadiusY=small.Height/2;
            player.BeginAnimation(UIElement.OpacityProperty,null);player.Opacity=0;
            miniHost.BeginAnimation(UIElement.OpacityProperty,null);miniHost.Opacity=1;
            // The notification icons move left into the place the chip leaves.
            shiftBy=-(chipButton.ActualWidth);shifted=true;
            card.IsOpen=true;
            // The card's own corner has to sit exactly on the chip's, or it opens and folds at a different place.
            AlignCard();
            Dispatcher.BeginInvoke(new Action(AlignCard),System.Windows.Threading.DispatcherPriority.Loaded);
            Morph(true);
            StopOutside();
            outside=MenuDismiss.Watch(ContainsPoint,CloseCard);
        }
        // Measures where the card and the chip really are on screen and moves the popup by the difference. Popup
        // placement alone drifts with the window's DPI and the way the bar is positioned.
        void AlignCard()
        {
            if(card==null||!card.IsOpen||closing||cardSurface==null||!cardSurface.IsVisible||!chip.IsVisible)return;
            try
            {
                var dpi=VisualTreeHelper.GetDpi(chip);
                var chipAt=chip.PointToScreen(new Point(0,0));
                var cardAt=cardSurface.PointToScreen(new Point(0,0));
                double dx=(chipAt.X-cardAt.X)/dpi.DpiScaleX,dy=(chipAt.Y-cardAt.Y)/dpi.DpiScaleY;
                if(Math.Abs(dx)<.5&&Math.Abs(dy)<.5)return;
                card.HorizontalOffset+=dx;card.VerticalOffset+=dy;
            }
            catch(InvalidOperationException){}
        }
        const double CardWidth=352,CardPad=14;
        double cardHeight=164,drop=30;Grid content,holder;
        UIElement miniHost;Border miniArt;TextBlock miniTitle;
        readonly RectangleGeometry cardClip=new RectangleGeometry(new Rect(0,0,CardWidth,194),9,9);
        IDisposable outside;
        void StopOutside(){if(outside!=null){outside.Dispose();outside=null;}}
        bool ContainsPoint(Native.PointI point)
        {
            if(card==null||!card.IsOpen)return false;
            var source=PresentationSource.FromVisual(cardSurface) as HwndSource;
            Native.RectI box;
            if(source!=null&&Native.GetWindowRect(source.Handle,out box)&&point.X>=box.Left&&point.X<box.Right&&point.Y>=box.Top&&point.Y<box.Bottom)return true;
            return false;
        }
        Rect ChipRect(){return new Rect(0,0,Math.Max(60,chip.ActualWidth>0?chip.ActualWidth:120),chip.ActualHeight>0?chip.ActualHeight:18);}
        // The clip runs from the chip's rectangle to the card's, which sits under the bar. The chip fades out as it
        // leaves, the notification icons slide left into its place, and all of it runs back on close.
        void Morph(bool opening)
        {
            var small=ChipRect();
            var full=new Rect(0,drop,CardWidth,cardHeight);
            double radius0=small.Height/2;
            if(!Motion.Enabled)
            {
                cardClip.BeginAnimation(RectangleGeometry.RectProperty,null);cardClip.Rect=opening?full:small;
                player.BeginAnimation(UIElement.OpacityProperty,null);player.Opacity=opening?1:0;
                miniHost.BeginAnimation(UIElement.OpacityProperty,null);miniHost.Opacity=opening?0:1;
                iconShift.BeginAnimation(TranslateTransform.XProperty,null);iconShift.X=opening?shiftBy:0;
                chip.BeginAnimation(UIElement.OpacityProperty,null);chip.Opacity=opening?0:1;
                return;
            }
            var ease=opening?SpringEase.Snappy:SpringEase.Smooth;
            int length=opening?380:240;
            var span=TimeSpan.FromMilliseconds(length);
            var hold=System.Windows.Media.Animation.FillBehavior.HoldEnd;
            var rect=new System.Windows.Media.Animation.RectAnimation(opening?small:cardClip.Rect,opening?full:small,span){EasingFunction=ease,FillBehavior=hold};
            cardClip.BeginAnimation(RectangleGeometry.RectProperty,rect);
            var radius=new System.Windows.Media.Animation.DoubleAnimation(opening?radius0:cardClip.RadiusX,opening?26:radius0,span){EasingFunction=SpringEase.Smooth,FillBehavior=hold};
            cardClip.BeginAnimation(RectangleGeometry.RadiusXProperty,radius);cardClip.BeginAnimation(RectangleGeometry.RadiusYProperty,radius.Clone());
            var fade=new System.Windows.Media.Animation.DoubleAnimation(opening?0:player.Opacity,opening?1:0,TimeSpan.FromMilliseconds(opening?200:110)){BeginTime=TimeSpan.FromMilliseconds(opening?130:0),FillBehavior=hold};
            player.BeginAnimation(UIElement.OpacityProperty,fade);
            var title=new System.Windows.Media.Animation.DoubleAnimation(opening?1:miniHost.Opacity,opening?0:1,TimeSpan.FromMilliseconds(120)){BeginTime=TimeSpan.FromMilliseconds(opening?0:120),FillBehavior=hold};
            miniHost.BeginAnimation(UIElement.OpacityProperty,title);
            var slide=new System.Windows.Media.Animation.DoubleAnimation(iconShift.X,opening?shiftBy:0,span){EasingFunction=ease,FillBehavior=hold};
            iconShift.BeginAnimation(TranslateTransform.XProperty,slide);
            // The bar's own chip is hidden while the card stands in for it.
            var chipFade=new System.Windows.Media.Animation.DoubleAnimation(opening?1:0,opening?0:1,TimeSpan.FromMilliseconds(100)){BeginTime=TimeSpan.FromMilliseconds(opening?0:length-100),FillBehavior=hold};
            chip.BeginAnimation(UIElement.OpacityProperty,chipFade);
        }
        bool closing;
        string layoutKey="",checkedKey="";
        readonly System.Windows.Threading.DispatcherTimer closeTimer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
        void CloseCard()
        {
            if(card==null||!card.IsOpen||closing)return;
            closing=true;StopOutside();
            Morph(false);
            if(!Motion.Enabled){FinishClose();return;}
            closeTimer.Tick-=OnCloseTick;closeTimer.Tick+=OnCloseTick;closeTimer.Start();
        }
        void OnCloseTick(object sender,EventArgs e){closeTimer.Stop();FinishClose();}
        void FinishClose()
        {
            if(card!=null)card.IsOpen=false;
            closing=false;
            chip.BeginAnimation(UIElement.OpacityProperty,null);chip.Opacity=1;
            if(!shifted)return;
            shifted=false;iconShift.BeginAnimation(TranslateTransform.XProperty,null);iconShift.X=0;
        }        internal void ToggleMediaCard(){ToggleCard();}
        internal object Inspect()
        {
            return new {side=side.ToString(),visible=IsVisible,left=host.X,top=host.Y,width=host.Width,height=host.Height,chip=chip.Visibility==Visibility.Visible,notificationIcons=icons.Children.Count,cardOpen=CardOpen};
        }
    }
}
