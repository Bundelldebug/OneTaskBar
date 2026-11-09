using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // A fullscreen desktop media surface with a separate, explicit PC lock action.
    internal sealed class FullscreenMedia : GlassWindow
    {
        readonly Canvas canvas=new Canvas();
        readonly Border cover=new Border(),card=new Border(),fill=new Border();
        readonly FullscreenCaption caption=new FullscreenCaption();
        readonly Border wallpaper=new Border(),artworkWash=new Border();
        readonly TextBlock clock=Paint.TextBlock("",36,Paint.ControlWhite),date=Paint.TextBlock("",16,Paint.ControlWhite);
        readonly MediaPlayerView player;
        readonly Button artButton,lockButton;
        readonly RectangleGeometry coverClip=new RectangleGeometry();
        readonly RectangleGeometry cardClip=new RectangleGeometry();
        BitmapSource artwork;
        IntPtr mediaSurface;
        Rect coverBox,cardBox;
        double coverRadius,cardRadius;
        // Every moving part follows its own spring toward the layout it should have. They share one
        // clock, so the cover, card, clock and tiles move as one continuous gesture, and turning
        // around mid-way keeps their speed.
        readonly SpringValue coverX=new SpringValue(),coverY=new SpringValue(),coverW=new SpringValue(),coverH=new SpringValue();
        readonly SpringValue cardX=new SpringValue(),cardY=new SpringValue(),cardW=new SpringValue(),cardH=new SpringValue();
        readonly SpringValue coverRound=new SpringValue(),cardRound=new SpringValue();
        readonly SpringValue clockTopSpring=new SpringValue(),clockSizeSpring=new SpringValue(),dateTopSpring=new SpringValue();
        readonly SpringValue washSpring=new SpringValue(.003),shadeSpring=new SpringValue(.003),tilesSpring=new SpringValue(.003);
        readonly TranslateTransform tilesShift=new TranslateTransform();
        SpringValue[] springs;
        bool springsReady,motionHooked;
        readonly SpringClock motionClock=new SpringClock();
        TimeSpan lastMotion=TimeSpan.MinValue;
        bool large=true,closed;
        bool layoutValid,layoutLarge;
        double layoutWidth,layoutHeight;
        int wallpaperRequest;
        internal bool LargeCover {get{return large;}}
        internal bool Morphing {get{return motionHooked;}}
        internal int LayoutPasses {get;private set;}
        internal Rect CoverBounds {get{return coverBox;}}
        internal Rect CardBounds {get;private set;}
        internal Button LockButton {get{return lockButton;}}
        internal Button CoverButton {get{return artButton;}}
        internal Button Transport {get{return player.PlayButton;}}
        internal PillSlider SeekBar {get{return player.SeekBar;}}
        internal MediaPlayerView Player {get{return player;}}
        internal bool WallpaperReady {get;private set;}
        internal Brush WallpaperBrush {get{return wallpaper.Background;}}
        internal double ClockSize {get{return clock.FontSize;}}
        readonly ShellController shell;
        readonly Border shade;
        TileScheme scheme=TileScheme.Unset;
        internal TileScheme Scheme {get{return scheme;}}
        FullscreenTileStrip tiles;
        FullscreenAppCorners corners;
        internal FullscreenTileStrip Tiles {get{return tiles;}}
        internal FullscreenAppCorners Corners {get{return corners;}}
        internal Rect TilesBounds {get;private set;}
        internal override bool EscapeConsumed(){return tiles!=null&&tiles.ConsumeEscape();}
        internal double ClockBottom {get;private set;}
        internal FullscreenMedia(Action<string> dispatch,Func<double,Task<bool>> seekCommand,Action output,Action lockPC,Func<string,AppEntry> sourceApp=null,ShellController controller=null)
        {
            shell=controller;
            Title="One TaskBar · 전체화면 미디어";OpaqueSurface=true;FluidLightOnOpaque=true;Radius=0;
            Surface.CornerRadius=new CornerRadius(0);Surface.BorderThickness=new Thickness(0);
            SetMaterial(Brushes.Black,Brushes.Black);
            var root=new Grid();root.Children.Add(wallpaper);root.Children.Add(artworkWash);
            shade=new Border {Background=Paint.Brush("#48000000"),IsHitTestVisible=false};root.Children.Add(shade);
            root.Children.Add(canvas);Surface.Child=root;artworkWash.Opacity=1;
            clock.TextAlignment=date.TextAlignment=TextAlignment.Center;clock.FontWeight=FontWeights.SemiBold;
            canvas.Children.Add(clock);canvas.Children.Add(date);
            cover.Clip=coverClip;RenderOptions.SetBitmapScalingMode(cover,BitmapScalingMode.HighQuality);
            artButton=Paint.Button(cover,ToggleArtwork,"커버 크게 · 작게");artButton.Padding=new Thickness(0);
            artButton.Background=Brushes.Transparent;canvas.Children.Add(artButton);Panel.SetZIndex(artButton,2);
            player=new MediaPlayerView(dispatch,seekCommand,()=>{HideImmediately();output();},sourceApp:sourceApp);
            // The card's own fill and edge fade out as the cover opens, so the expanded player has no panel.
            fill.BorderBrush=Paint.Brush("#35FFFFFF");fill.BorderThickness=new Thickness(.6);fill.IsHitTestVisible=false;
            var cardContent=new Grid();cardContent.Children.Add(fill);cardContent.Children.Add(player);
            card.Child=cardContent;card.CornerRadius=new CornerRadius(28);card.Clip=cardClip;canvas.Children.Add(card);
            canvas.Children.Add(caption);Panel.SetZIndex(caption,3);
            var close=Icon("close",Hide,"전체화면 닫기 · Esc");close.Tag="close";canvas.Children.Add(close);
            lockButton=Icon("lock",lockPC,"PC 잠금");lockButton.Tag="lock";canvas.Children.Add(lockButton);
            SizeChanged+=(s,e)=>Layout(false);
            Preferences.MediaAppearanceChanged+=ClockChanged;
            SourceInitialized += (s, e) =>
            {
                mediaSurface = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                FullscreenVisibility.TrackMediaSurface(mediaSurface, true);
            };
            IsVisibleChanged+=(s,e)=>{
                if(IsVisible)LoadWallpaper();
                else{FinishMotion();player.StopFullscreenMedia();wallpaperRequest++;wallpaper.Background=null;WallpaperReady=false;}
            };
            Closed += (s, e) =>
            {
                FullscreenVisibility.TrackMediaSurface(mediaSurface, false);
                Preferences.MediaAppearanceChanged-=ClockChanged;
                if(tiles!=null)tiles.Retire();
                closed = true;
                wallpaperRequest++;
                StopMotion();
                cover.Background = artworkWash.Background = wallpaper.Background = null;
                artwork = null;
                player.StopFullscreenMedia();
            };
        }
        static new Button Icon(string name,Action action,string hint)
        {
            var button=Paint.Button(new Glyph(name,20){Color=Paint.ControlWhite},action,hint);
            button.Width=button.Height=42;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;return button;
        }
        internal void Present(Forms.Screen screen,double scale)
        {
            ShowAt(screen.Bounds,0,0,screen.Bounds.Width/scale,screen.Bounds.Height/scale);
            Layout(false);Activate();
            fill.BorderBrush=Fluid.Enabled?(Brush)Glass95.Edge(true,true,true):Paint.Brush("#35FFFFFF");
            if(Fluid.Enabled&&Motion.Enabled&&springsReady)DropIn();
            else Pop();
        }
        // The Fluid UI's arrival: the cover and the player start as small drops below their place and swell into it on springs.
        void DropIn()
        {
            ShrinkToward(cardX,cardY,cardW,cardH,.5,.45,70);
            ShrinkToward(coverX,coverY,coverW,coverH,.3,.3,-34);
            coverRound.Value=Math.Max(coverRound.Value,coverW.Value/2);
            Surface.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(170)) {FillBehavior=FillBehavior.Stop});
            Surface.Opacity=1;ApplyMotion();StartMotion();
        }
        static void ShrinkToward(SpringValue x,SpringValue y,SpringValue width,SpringValue height,double widthPart,double heightPart,double drop)
        {
            double cx=x.Target+width.Target/2,cy=y.Target+height.Target/2;
            width.Value=Math.Max(1,width.Target*widthPart);height.Value=Math.Max(1,height.Target*heightPart);
            x.Value=cx-width.Value/2;y.Value=cy-height.Value/2+drop;
            x.Velocity=y.Velocity=width.Velocity=height.Velocity=0;
        }
        async void LoadWallpaper()
        {
            int request=++wallpaperRequest;
            var bounds=Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).Bounds;
            try{
                var snapshot=await BackgroundWork.Run(()=>DesktopWallpaper.Read(bounds));
                if(closed||!IsVisible||request!=wallpaperRequest)return;
                wallpaper.Background=snapshot.Brush;WallpaperReady=snapshot.HasImage;
                scheme=TileScheme.FromWallpaper(snapshot.Brush);
                if(tiles!=null)tiles.Recolor(scheme);
                if(corners!=null)corners.Recolor(scheme);
                ApplyInk(false);
            }catch(Exception error){AppLog.Once("fullscreen-wallpaper",error.GetType().Name);}
        }
        internal static Rect FitCover(double width,double height,bool expanded)
        {
            double top=Math.Max(82,18+32*Preferences.Current.FullscreenClockPercent/100.0*1.15+32);
            double cardHeight=Math.Min(216,Math.Max(180,height*.27));
            double edge=Math.Max(40,Math.Min(Math.Min(width-96,height-cardHeight-44-top),580));
            return expanded?new Rect((width-edge)/2,top,edge,edge):new Rect((width-Math.Min(MediaPlayerView.FullscreenPillWidth,width-48))/2+18,height-MediaPlayerView.FullscreenPillHeight-24+20,44,44);
        }
        void ClockChanged()
        {
            layoutValid=false;
            if(tiles!=null)tiles.Sync();
            if(IsVisible)Layout(false);
        }
        void Layout(bool animate)
        {
            double w=ActualWidth,h=ActualHeight;if(w<1||h<1)return;
            if(!animate&&layoutValid&&layoutWidth==w&&layoutHeight==h&&layoutLarge==large)return;
            LayoutPasses++;
            layoutValid=true;layoutWidth=w;layoutHeight=h;layoutLarge=large;
            bool run=animate&&Motion.Enabled&&IsVisible&&springsReady;
            double cardWidth=Math.Max(240,Math.Min(large?640:MediaPlayerView.FullscreenPillWidth,w-48));
            double cardHeight=large?Math.Min(216,Math.Max(180,h*.27)):MediaPlayerView.FullscreenPillHeight;
            CardBounds=new Rect((w-cardWidth)/2,h-cardHeight-24,cardWidth,cardHeight);
            player.Width=Math.Max(1,cardWidth-1.2);
            player.FitFullscreen(cardHeight,large);player.UpdateLayout();
            clock.Width=date.Width=w;
            double clockTop=large?18:Math.Max(42,h*.12);
            double clockSize=(large?32:64)*Preferences.Current.FullscreenClockPercent/100.0;
            // Collapsed, the date sits above the clock so tiles can follow the time directly.
            double clockLine=large?clockTop:clockTop+28;
            Aim(clockTopSpring,clockLine,run);
            Aim(clockSizeSpring,clockSize,run);
            Aim(dateTopSpring,large?clockLine+clockSize*1.15+6:clockTop,run);
            ClockBottom=clockLine+clockSize*1.3;
            PlaceCompanions(w,h,run);
            foreach(UIElement child in canvas.Children){
                var button=child as Button;
                if(button!=null&&button.Tag is string){Canvas.SetLeft(button,(string)button.Tag=="close"?w-54:w-104);Canvas.SetTop(button,16);}
            }
            Rect target=FitCover(w,h,large);
            if(!large){
                var slot=player.FullscreenArtworkSlot;
                target=new Rect(CardBounds.X+slot.X,CardBounds.Y+slot.Y,slot.Width,slot.Height);
            }
            Aim(washSpring,large?1:0,run);
            Aim(coverRound,large?Math.Min(24,target.Width*.2):target.Width/2,run);
            Aim(cardRound,large?28:cardHeight/2,run);
            Aim(coverX,target.X,run);Aim(coverY,target.Y,run);Aim(coverW,target.Width,run);Aim(coverH,target.Height,run);
            Aim(cardX,CardBounds.X,run);Aim(cardY,CardBounds.Y,run);Aim(cardW,CardBounds.Width,run);Aim(cardH,CardBounds.Height,run);
            springsReady=true;
            if(run)StartMotion();
            else FinishMotion();
        }
        // Collapsed, the wallpaper shows undimmed and the text takes its color from it.
        void ApplyInk(bool animate)
        {
            Aim(shadeSpring,large?1:0,animate);
            clock.Foreground=date.Foreground=large?Paint.ControlWhite:scheme.Ink;
            if(!animate)ApplyMotion();
        }
        void PlaceCompanions(double w,double h,bool animate)
        {
            ApplyInk(animate);
            if(shell==null)return;
            bool showTiles=!large&&Preferences.Current.FullscreenWidgets;
            if(showTiles&&tiles==null)
            {
                tiles=new FullscreenTileStrip(shell,HideImmediately);
                tiles.Changed=()=>{layoutValid=false;Layout(false);};
                tiles.Recolor(scheme);
                canvas.Children.Add(tiles);
            }
            if(tiles!=null)
            {
                if(!showTiles)tiles.EndEdit(false);
                var room=showTiles?tiles.Fit(w-96,CardBounds.Top-ClockBottom-20):new Size(0,0);
                bool fits=room.Width>0;
                if(!fits)tiles.EndEdit(false);
                if(fits)
                {
                    tiles.Visibility=Visibility.Visible;
                    tiles.RenderTransform=tilesShift;
                    TilesBounds=new Rect((w-room.Width)/2,ClockBottom+6,room.Width,room.Height);
                    Canvas.SetLeft(tiles,TilesBounds.X);
                    Canvas.SetTop(tiles,TilesBounds.Y);
                    Aim(tilesSpring,1,animate);
                    tiles.Refresh(DateTime.Now);
                    tiles.PlaceEditing();
                }
                else
                {
                    // Fading out, the tiles stay where they were until they are gone.
                    Aim(tilesSpring,0,animate);
                    if(!animate||tilesSpring.Value<=.01)tiles.Visibility=Visibility.Collapsed;
                }
            }
            if(!large&&corners==null)
            {
                corners=new FullscreenAppCorners(canvas,shell,HideImmediately);
                corners.Recolor(scheme);
            }
            if(corners!=null)corners.Place(w,h,!large&&w>=MediaPlayerView.FullscreenPillWidth+2*(56+FullscreenAppCorners.Size+16));
        }
        void Aim(SpringValue spring,double value,bool run)
        {
            spring.Target=value;
            if(run)return;
            spring.Value=value;
            spring.Velocity=0;
        }
        void PlaceCover(Rect box)
        {
            coverBox=box;artButton.Width=cover.Width=box.Width;artButton.Height=cover.Height=box.Height;
            Canvas.SetLeft(artButton,box.X);Canvas.SetTop(artButton,box.Y);
            coverClip.Rect=new Rect(0,0,box.Width,box.Height);coverClip.RadiusX=coverClip.RadiusY=coverRadius;
            artButton.Visibility=artwork!=null?Visibility.Visible:Visibility.Collapsed;
            caption.Place(box,coverRadius,artwork!=null?Unit((washSpring.Value-.55)/.45):0);
        }
        void PlaceCard(Rect box)
        {
            cardBox=box;
            card.Width=box.Width;card.Height=box.Height;
            Canvas.SetLeft(card,box.X);Canvas.SetTop(card,box.Y);
            card.CornerRadius=fill.CornerRadius=new CornerRadius(cardRadius);
            cardClip.Rect=new Rect(0,0,box.Width,box.Height);
            cardClip.RadiusX=cardClip.RadiusY=cardRadius;
        }
        internal void ToggleArtwork(){large=!large;Layout(true);}
        void StartMotion()
        {
            if(motionHooked)return;
            motionHooked=true;
            lastMotion=TimeSpan.MinValue;
            motionClock.Restart();
            CompositionTarget.Rendering+=OnMotion;
        }
        void StopMotion()
        {
            CompositionTarget.Rendering-=OnMotion;
            motionHooked=false;
        }
        SpringValue[] Springs()
        {
            if(springs==null)springs=new[]{coverX,coverY,coverW,coverH,cardX,cardY,cardW,cardH,coverRound,cardRound,clockTopSpring,clockSizeSpring,dateTopSpring,washSpring,shadeSpring,tilesSpring};
            foreach(var spring in springs)spring.Omega=32;
            // In the Fluid UI the shapes overshoot a little and settle like liquid; fades and tiles stay smooth.
            double shape=Fluid.Enabled?.62:1;
            foreach(var spring in new[]{coverX,coverY,coverW,coverH,cardX,cardY,cardW,cardH,coverRound,cardRound})spring.Damping=shape;
            return springs;
        }
        // Settles every part on its target at once, for a hidden window or a plain resize.
        void FinishMotion()
        {
            foreach(var spring in Springs())spring.Snap();
            StopMotion();
            ApplyMotion();
        }
        void OnMotion(object sender,EventArgs args)
        {
            var frame=args as RenderingEventArgs;
            if(frame!=null&&frame.RenderingTime==lastMotion)return;
            lastMotion=frame!=null?frame.RenderingTime:lastMotion;
            double seconds=motionClock.Next(frame!=null?frame.RenderingTime:TimeSpan.FromMilliseconds(Environment.TickCount));
            bool rest=true;
            foreach(var spring in Springs())
            {
                spring.Step(seconds);
                rest&=spring.AtRest;
            }
            if(rest)
            {
                FinishMotion();
                return;
            }
            ApplyMotion();
        }
        static double Unit(double value){return Math.Max(0,Math.Min(1,value));}
        void ApplyMotion()
        {
            if(!springsReady)return;
            coverRadius=Math.Max(0,coverRound.Value);
            cardRadius=Math.Max(0,cardRound.Value);
            PlaceCard(new Rect(cardX.Value,cardY.Value,Math.Max(1,cardW.Value),Math.Max(1,cardH.Value)));
            PlaceCover(new Rect(coverX.Value,coverY.Value,Math.Max(1,coverW.Value),Math.Max(1,coverH.Value)));
            Canvas.SetTop(clock,clockTopSpring.Value);
            clock.FontSize=Math.Max(1,clockSizeSpring.Value);
            Canvas.SetTop(date,dateTopSpring.Value);
            artworkWash.Opacity=Unit(washSpring.Value);
            fill.Opacity=1-Unit(washSpring.Value);
            shade.Opacity=Unit(shadeSpring.Value);
            if(tiles!=null)
            {
                double shown=Unit(tilesSpring.Value);
                tiles.Opacity=shown;
                tilesShift.Y=(1-shown)*18;
                if(tilesSpring.Target<=0&&shown<=.01&&tiles.Visibility==Visibility.Visible)tiles.Visibility=Visibility.Collapsed;
            }
        }
        internal static string FormatTime(DateTime value)
        {
            return value.ToString("h:mm", CultureInfo.InvariantCulture);
        }
        internal void Update(MediaState state,string outputName=null)
        {
            if(closed||!IsVisible)return;
            var media=state??new MediaState();player.FitFullscreen(CardBounds.Height,large);player.Update(media,outputName);
            if(!player.Modern&&player.FullscreenPill)player.SeekBar.WavePlaying=media.Available&&media.Active&&media.Playing;
            fill.Background=player.MediaBackground;
            caption.Set(media.Title,media.Artist,player.SourceLogo);
            var now = DateTime.Now;
            clock.Text = FormatTime(now);
            date.Text = now.ToString("M월 d일 dddd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));
            if(!large&&tiles!=null&&tiles.Visibility==Visibility.Visible)tiles.Refresh(now);
            if(!large&&corners!=null)corners.Refresh();
            if(artwork!=media.Artwork){
                artwork=media.Artwork;Brush picture=null;
                if(artwork!=null){var image=new ImageBrush(artwork) {Stretch=Stretch.UniformToFill};image.Freeze();picture=image;}
                cover.Background=picture;artworkWash.Background=player.MediaBackground;PlaceCover(coverBox);
            }
            // Timeline updates never restart the cover morph or force layout.
            // SizeChanged and the explicit artwork toggle own geometry changes.
            if(!layoutValid)Layout(false);
        }
    }
}
