using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // This is a media surface on the user's desktop, not the Windows lock screen.
    internal sealed class FullscreenMedia : GlassWindow
    {
        readonly Canvas canvas=new Canvas();
        readonly Border cover=new Border(),card=new Border();
        readonly TextBlock clock=Paint.TextBlock("",36,Paint.ControlWhite),date=Paint.TextBlock("",16,Paint.ControlWhite);
        readonly MarqueeTitle title=new MarqueeTitle {Centered=true},artist=new MarqueeTitle {Centered=true};
        readonly TextBlock elapsed=Paint.TextBlock("",12,Paint.ControlWhite),remaining=Paint.TextBlock("",12,Paint.ControlWhite);
        readonly Glyph play=new Glyph("play",30){Color=Paint.ControlWhite};
        readonly Button playButton,previous,next,artButton,lockButton;
        readonly Border smallArt=new Border {Width=44,Height=44,CornerRadius=new CornerRadius(10)};
        readonly PillSlider progress=new PillSlider("progress") {Height=24,Slim=true,ShowIcon=false,Light=true,DarkTrack=true,AnimateValue=false};
        readonly Func<double,Task<bool>> seek;
        readonly Action<string> command;
        readonly Stopwatch morph=new Stopwatch();
        MediaState media=new MediaState();BitmapSource artwork;Rect coverBox,from,to;bool large=true,closed,seeking;double? pending;
        internal bool LargeCover {get{return large;}}
        internal bool Morphing {get{return morph.IsRunning;}}
        internal Rect CoverBounds {get{return coverBox;}}
        internal Rect CardBounds {get;private set;}
        internal Button LockButton {get{return lockButton;}}
        internal Button CoverButton {get{return artButton;}}
        internal Button Transport {get{return playButton;}}
        internal PillSlider SeekBar {get{return progress;}}
        internal FullscreenMedia(Action<string> dispatch,Func<double,Task<bool>> seekCommand,Action output,Action lockPC)
        {
            command=dispatch;seek=seekCommand;Title="One TaskBar · 전체화면 미디어";OpaqueSurface=true;Radius=0;Surface.CornerRadius=new CornerRadius(0);Surface.BorderThickness=new Thickness(0);SetMaterial(Paint.Brush("#FF243C50"),Paint.Brush("#FF243C50"));
            Surface.Child=canvas;RenderOptions.SetBitmapScalingMode(cover,BitmapScalingMode.HighQuality);
            clock.TextAlignment=TextAlignment.Center;clock.FontWeight=FontWeights.SemiBold;clock.HorizontalAlignment=HorizontalAlignment.Center;canvas.Children.Add(clock);canvas.Children.Add(date);
            cover.CornerRadius=new CornerRadius(24);artButton=Paint.Button(cover,ToggleArtwork,"커버 크게 · 작게");artButton.Padding=new Thickness(0);artButton.Background=Brushes.Transparent;canvas.Children.Add(artButton);
            var content=new Grid {Margin=new Thickness(22,14,22,14)};content.RowDefinitions.Add(new RowDefinition {Height=new GridLength(62)});content.RowDefinitions.Add(new RowDefinition {Height=new GridLength(38)});content.RowDefinitions.Add(new RowDefinition {Height=new GridLength(64)});
            var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(56)});heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(42)});
            var small=Paint.Button(smallArt,ToggleArtwork,"커버 크게 · 작게");small.Padding=new Thickness(0);small.Background=Brushes.Transparent;heading.Children.Add(small);
            var words=new Grid();words.RowDefinitions.Add(new RowDefinition());words.RowDefinitions.Add(new RowDefinition());words.Children.Add(title);Grid.SetRow(artist,1);words.Children.Add(artist);Grid.SetColumn(words,1);heading.Children.Add(words);title.TextSize=19;artist.TextSize=15;
            var route=Icon("audio-output",()=>{HideImmediately();output();},"출력 기기",38,23);Grid.SetColumn(route,2);heading.Children.Add(route);content.Children.Add(heading);
            var track=new Grid();track.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(48)});track.ColumnDefinitions.Add(new ColumnDefinition());track.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(48)});track.Children.Add(elapsed);Grid.SetColumn(progress,1);track.Children.Add(progress);remaining.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(remaining,2);track.Children.Add(remaining);Grid.SetRow(track,1);content.Children.Add(track);progress.Changed+=Seek;
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};previous=Icon("previous",()=>command("previous"),"이전 곡",52,28);next=Icon("next",()=>command("next"),"다음 곡",52,28);playButton=Paint.Button(play,()=>command("play"),"재생 · 일시 정지");playButton.Width=playButton.Height=60;playButton.Padding=new Thickness(0);playButton.Background=Brushes.Transparent;playButton.Margin=new Thickness(30,0,30,0);buttons.Children.Add(previous);buttons.Children.Add(playButton);buttons.Children.Add(next);Grid.SetRow(buttons,2);content.Children.Add(buttons);
            card.Child=content;card.CornerRadius=new CornerRadius(28);card.Background=Paint.Brush("#48212E40");card.BorderBrush=Paint.Brush("#45FFFFFF");card.BorderThickness=new Thickness(.7);canvas.Children.Add(card);
            var close=Icon("close",Hide,"전체화면 닫기 · Esc",42,20);close.HorizontalAlignment=HorizontalAlignment.Right;canvas.Children.Add(close);close.Tag="close";
            lockButton=Icon("lock",lockPC,"PC 잠금",42,20);canvas.Children.Add(lockButton);lockButton.Tag="lock";
            SizeChanged+=(s,e)=>Layout(false);IsVisibleChanged+=(s,e)=>{if(!IsVisible){StopMorph();title.Running=false;artist.Running=false;}};
            Closed+=(s,e)=>{closed=true;StopMorph();cover.Background=smallArt.Background=null;artwork=null;};
        }
        static new Button Icon(string name,Action action,string hint,double size,double glyphSize)
        {var b=Paint.Button(new Glyph(name,glyphSize){Color=Paint.ControlWhite},action,hint);b.Width=b.Height=size;b.Padding=new Thickness(0);b.Background=Brushes.Transparent;return b;}
        internal void Present(Forms.Screen screen,double scale)
        {
            ShowAt(screen.Bounds,0,0,screen.Bounds.Width/scale,screen.Bounds.Height/scale);Layout(false);Activate();Pop();
        }
        internal static Rect FitCover(double width,double height,bool expanded)
        {
            double cardWidth=Math.Max(240,Math.Min(640,width-64)),edge=Math.Max(40,Math.Min(Math.Min(width-96,height-330),580));
            return expanded?new Rect((width-edge)/2,82,edge,edge):new Rect((width-cardWidth)/2+23,height-202,44,44);
        }
        void Layout(bool animate)
        {
            double w=ActualWidth,h=ActualHeight;if(w<1||h<1)return;double cardWidth=Math.Max(240,Math.Min(640,w-64)),cardHeight=192;
            CardBounds=new Rect((w-cardWidth)/2,h-cardHeight-28,cardWidth,cardHeight);card.Width=cardWidth;card.Height=cardHeight;Canvas.SetLeft(card,CardBounds.X);Canvas.SetTop(card,CardBounds.Y);
            clock.Width=w;clock.Text=DateTime.Now.ToString("H:mm");Canvas.SetTop(clock,large?20:Math.Max(40,h*.12));date.Width=w;date.Text=DateTime.Now.ToString("M월 d일 dddd");date.TextAlignment=TextAlignment.Center;Canvas.SetTop(date,large?62:Math.Max(96,h*.12+68));clock.FontSize=large?32:64;
            foreach(UIElement child in canvas.Children){var b=child as Button;if(b!=null&&b.Tag is string){Canvas.SetLeft(b,(string)b.Tag=="close"?w-54:w-104);Canvas.SetTop(b,16);}}
            var target=FitCover(w,h,large);if(!large)target=new Rect(CardBounds.X+23,CardBounds.Y+23,44,44);
            if(animate&&Motion.Enabled&&IsVisible){from=coverBox;to=target;StopMorph();morph.Restart();CompositionTarget.Rendering+=OnMorph;}else{StopMorph();PlaceCover(target);}
        }
        void PlaceCover(Rect box)
        {
            coverBox=box;artButton.Width=cover.Width=box.Width;artButton.Height=cover.Height=box.Height;Canvas.SetLeft(artButton,box.X);Canvas.SetTop(artButton,box.Y);cover.Clip=new RectangleGeometry(new Rect(0,0,box.Width,box.Height),Math.Min(24,box.Width*.2),Math.Min(24,box.Height*.2));artButton.Visibility=artwork!=null?Visibility.Visible:Visibility.Collapsed;
        }
        internal void ToggleArtwork(){large=!large;Panel.SetZIndex(artButton,2);Layout(true);}
        void OnMorph(object sender,EventArgs args)
        {
            double p=Math.Min(1,morph.Elapsed.TotalMilliseconds/420),t=1-Math.Pow(1-p,3);PlaceCover(new Rect(from.X+(to.X-from.X)*t,from.Y+(to.Y-from.Y)*t,from.Width+(to.Width-from.Width)*t,from.Height+(to.Height-from.Height)*t));if(p>=1)StopMorph();
        }
        void StopMorph(){CompositionTarget.Rendering-=OnMorph;morph.Stop();}
        internal void Update(MediaState state)
        {
            if(closed||!IsVisible)return;media=state??new MediaState();title.Text=media.Active?media.Title:"재생 중인 미디어 없음";artist.Text=media.Active?media.Artist:"";title.Running=artist.Running=media.Active;
            clock.Text=DateTime.Now.ToString("H:mm");date.Text=DateTime.Now.ToString("M월 d일 dddd");
            if(artwork!=media.Artwork){artwork=media.Artwork;Brush image=null;if(artwork!=null){var b=new ImageBrush(artwork){Stretch=Stretch.UniformToFill};b.Freeze();image=b;}cover.Background=smallArt.Background=image;var palette=ArtworkPalette.Read(artwork);SetMaterial(palette.Solid,palette.Solid);PlaceCover(coverBox);}
            play.Kind=media.Playing?"pause":"play";play.InvalidateVisual();playButton.IsEnabled=media.Active&&media.CanPlay;previous.IsEnabled=media.CanPrevious;next.IsEnabled=media.CanNext;progress.IsEnabled=media.CanSeek&&media.Duration>0;
            double position=media.CurrentPosition(DateTime.UtcNow);if(!seeking)progress.Value=media.Duration>0?position/media.Duration*100:0;elapsed.Text=media.Duration>0?NowBar.Clock(position):"실시간";remaining.Text=media.Duration>0?"−"+NowBar.Clock(Math.Max(0,media.Duration-position)):"";
        }
        async void Seek(double value){pending=value;if(seeking)return;seeking=true;try{while(pending.HasValue){double p=pending.Value;pending=null;if(!await seek(p))break;}}finally{seeking=false;pending=null;}}
    }
}
