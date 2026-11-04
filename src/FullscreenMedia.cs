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
        readonly Border cover=new Border(),card=new Border();
        readonly Border wallpaper=new Border(),artworkWash=new Border();
        readonly TextBlock clock=Paint.TextBlock("",36,Paint.ControlWhite),date=Paint.TextBlock("",16,Paint.ControlWhite);
        readonly MediaPlayerView player;
        readonly Button artButton,lockButton;
        readonly Stopwatch morph=new Stopwatch();
        readonly RectangleGeometry coverClip=new RectangleGeometry();
        readonly RectangleGeometry cardClip=new RectangleGeometry();
        BitmapSource artwork;
        IntPtr mediaSurface;
        Rect coverBox,from,to;
        Rect cardBox,cardFrom,cardTo;
        double coverRadius,fromCoverRadius,toCoverRadius,cardRadius,fromCardRadius,toCardRadius;
        bool large=true,closed;
        bool layoutValid,layoutLarge;
        double layoutWidth,layoutHeight;
        int wallpaperRequest;
        internal bool LargeCover {get{return large;}}
        internal bool Morphing {get{return morph.IsRunning;}}
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
        internal double ClockBottom {get;private set;}
        internal FullscreenMedia(Action<string> dispatch,Func<double,Task<bool>> seekCommand,Action output,Action lockPC,Func<string,AppEntry> sourceApp=null,ShellController controller=null)
        {
            shell=controller;
            Title="One TaskBar · 전체화면 미디어";OpaqueSurface=true;Radius=0;
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
            card.Child=player;card.CornerRadius=new CornerRadius(28);card.BorderBrush=Paint.Brush("#35FFFFFF");
            card.BorderThickness=new Thickness(.6);card.Clip=cardClip;canvas.Children.Add(card);
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
                else{StopMorph();player.StopFullscreenMedia();wallpaperRequest++;wallpaper.Background=null;WallpaperReady=false;}
            };
            Closed += (s, e) =>
            {
                FullscreenVisibility.TrackMediaSurface(mediaSurface, false);
                Preferences.MediaAppearanceChanged-=ClockChanged;
                if(tiles!=null)tiles.Retire();
                closed = true;
                wallpaperRequest++;
                StopMorph();
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
            Layout(false);Activate();Pop();
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
            Animate(clock,Canvas.TopProperty,clockLine,animate);
            Animate(clock,TextBlock.FontSizeProperty,clockSize,animate);
            Animate(date,Canvas.TopProperty,large?clockLine+clockSize*1.15+6:clockTop,animate);
            ClockBottom=clockLine+clockSize*1.3;
            PlaceCompanions(w,h,animate);
            foreach(UIElement child in canvas.Children){
                var button=child as Button;
                if(button!=null&&button.Tag is string){Canvas.SetLeft(button,(string)button.Tag=="close"?w-54:w-104);Canvas.SetTop(button,16);}
            }
            Rect target=FitCover(w,h,large);
            if(!large){
                var slot=player.FullscreenArtworkSlot;
                target=new Rect(CardBounds.X+slot.X,CardBounds.Y+slot.Y,slot.Width,slot.Height);
            }
            Animate(artworkWash,OpacityProperty,large?1:0,animate);
            toCoverRadius=large?Math.Min(24,target.Width*.2):target.Width/2;
            toCardRadius=large?28:cardHeight/2;
            if(animate&&Motion.Enabled&&IsVisible){
                from=coverBox;to=target;cardFrom=cardBox;cardTo=CardBounds;
                fromCoverRadius=coverRadius;fromCardRadius=cardRadius;
                StopMorph();morph.Restart();CompositionTarget.Rendering+=OnMorph;
            }
            else{
                StopMorph();coverRadius=toCoverRadius;cardRadius=toCardRadius;
                PlaceCard(CardBounds);PlaceCover(target);
            }
        }
        // Collapsed, the wallpaper shows undimmed and the text takes its color from it.
        void ApplyInk(bool animate)
        {
            Animate(shade,OpacityProperty,large?1:0,animate);
            clock.Foreground=date.Foreground=large?Paint.ControlWhite:scheme.Ink;
        }
        void PlaceCompanions(double w,double h,bool animate)
        {
            ApplyInk(animate);
            if(shell==null)return;
            bool showTiles=!large&&Preferences.Current.FullscreenWidgets;
            if(showTiles&&tiles==null)
            {
                tiles=new FullscreenTileStrip(shell,HideImmediately);
                tiles.Recolor(scheme);
                canvas.Children.Add(tiles);
            }
            if(tiles!=null)
            {
                var room=showTiles?tiles.Fit(w-96,CardBounds.Top-ClockBottom-20):new Size(0,0);
                bool fits=room.Width>0;
                tiles.Visibility=fits?Visibility.Visible:Visibility.Collapsed;
                if(fits)
                {
                    TilesBounds=new Rect((w-room.Width)/2,ClockBottom+6,room.Width,room.Height);
                    Canvas.SetLeft(tiles,TilesBounds.X);
                    Canvas.SetTop(tiles,TilesBounds.Y);
                    Animate(tiles,OpacityProperty,1,animate);
                    tiles.Refresh(DateTime.Now);
                }
            }
            if(!large&&corners==null)
            {
                corners=new FullscreenAppCorners(canvas,shell,HideImmediately);
                corners.Recolor(scheme);
            }
            if(corners!=null)corners.Place(w,h,!large&&w>=MediaPlayerView.FullscreenPillWidth+2*(56+FullscreenAppCorners.Size+16));
        }
        static void Animate(FrameworkElement element,DependencyProperty property,double value,bool animate)
        {
            double from=(double)element.GetValue(property);if(Double.IsNaN(from))from=value;
            element.BeginAnimation(property,null);element.SetValue(property,value);
            if(animate&&Motion.Enabled)element.BeginAnimation(property,new DoubleAnimation(from,value,TimeSpan.FromMilliseconds(360)){
                EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
        }
        void PlaceCover(Rect box)
        {
            coverBox=box;artButton.Width=cover.Width=box.Width;artButton.Height=cover.Height=box.Height;
            Canvas.SetLeft(artButton,box.X);Canvas.SetTop(artButton,box.Y);
            coverClip.Rect=new Rect(0,0,box.Width,box.Height);coverClip.RadiusX=coverClip.RadiusY=coverRadius;
            artButton.Visibility=artwork!=null?Visibility.Visible:Visibility.Collapsed;
        }
        void PlaceCard(Rect box)
        {
            cardBox=box;
            card.Width=box.Width;card.Height=box.Height;
            Canvas.SetLeft(card,box.X);Canvas.SetTop(card,box.Y);
            card.CornerRadius=new CornerRadius(cardRadius);
            cardClip.Rect=new Rect(0,0,box.Width,box.Height);
            cardClip.RadiusX=cardClip.RadiusY=cardRadius;
        }
        internal void ToggleArtwork(){large=!large;Layout(true);}
        void OnMorph(object sender,EventArgs args)
        {
            double p=Math.Min(1,morph.Elapsed.TotalMilliseconds/360),t=1-Math.Pow(1-p,3);
            coverRadius=fromCoverRadius+(toCoverRadius-fromCoverRadius)*t;
            cardRadius=fromCardRadius+(toCardRadius-fromCardRadius)*t;
            PlaceCard(new Rect(cardFrom.X+(cardTo.X-cardFrom.X)*t,cardFrom.Y+(cardTo.Y-cardFrom.Y)*t,
                cardFrom.Width+(cardTo.Width-cardFrom.Width)*t,cardFrom.Height+(cardTo.Height-cardFrom.Height)*t));
            PlaceCover(new Rect(from.X+(to.X-from.X)*t,from.Y+(to.Y-from.Y)*t,
                from.Width+(to.Width-from.Width)*t,from.Height+(to.Height-from.Height)*t));
            if(p>=1)StopMorph();
        }
        void StopMorph(){CompositionTarget.Rendering-=OnMorph;morph.Stop();}
        internal static string FormatTime(DateTime value)
        {
            return value.ToString("h:mm", CultureInfo.InvariantCulture);
        }
        internal void Update(MediaState state,string outputName=null)
        {
            if(closed||!IsVisible)return;
            var media=state??new MediaState();player.FitFullscreen(CardBounds.Height,large);player.Update(media,outputName);
            if(!player.Modern&&player.FullscreenPill)player.SeekBar.WavePlaying=media.Available&&media.Active&&media.Playing;
            card.Background=player.MediaBackground;
            var now = DateTime.Now;
            clock.Text = FormatTime(now);
            date.Text = now.ToString("M월 d일 dddd");
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
