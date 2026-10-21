using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace OneTaskBar
{
    // Shared by quick settings and the expanded Now bar.
    internal sealed class MediaPlayerView : Grid
    {
        readonly TextBlock title=Paint.TextBlock("",15,Paint.Text),artist=Paint.TextBlock("",11,Paint.Muted),device=Paint.TextBlock("오디오 출력",11,Paint.Text),elapsed=Paint.TextBlock("0:00",10,Paint.Muted),duration=Paint.TextBlock("0:00",10,Paint.Muted);
        readonly Border art=new Border {Width=42,Height=42,CornerRadius=new CornerRadius(11)};
        readonly Glyph playGlyph=new Glyph("play",20){Color=Paint.Ink},deviceGlyph=new Glyph("volume",14);
        readonly Func<double,Task<bool>> seekCommand;ImageSource artwork;bool seeking;double? pending;DateTime seekUntil;string track;
        internal readonly Button PlayButton,PreviousButton,NextButton,OutputButton;
        internal readonly PillSlider SeekBar=new PillSlider("progress") {Height=22,Light=true,DarkTrack=true,AnimateValue=false,Slim=true,ShowIcon=false};
        internal Action<ArtworkPalette> PaletteChanged;
        readonly Grid route,header;readonly DockPanel timeline;readonly StackPanel controls;readonly Button compactOutput;bool modeSet;
        internal bool IsCompact {get;private set;}
        internal string PlayKind {get{return playGlyph.Kind;}}
        internal string OutputName {get{return device.Text;}}
        internal MediaPlayerView(Action<string> command,Func<double,Task<bool>> seek,Action output,Action collapse=null)
        {
            Height=164;Margin=new Thickness(14,12,14,12);seekCommand=seek;
            foreach(double height in new[]{20.0,44,44,56})RowDefinitions.Add(new RowDefinition {Height=new GridLength(height)});
            route=new Grid();route.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(22)});route.ColumnDefinitions.Add(new ColumnDefinition());route.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(28)});route.Children.Add(deviceGlyph);device.TextTrimming=TextTrimming.CharacterEllipsis;device.FontWeight=FontWeights.SemiBold;Grid.SetColumn(device,1);route.Children.Add(device);OutputButton=Icon("audio-output",output,"출력 기기 변경",28,19);Grid.SetColumn(OutputButton,2);route.Children.Add(OutputButton);Children.Add(route);
            header=new Grid {Margin=new Thickness(0,2,0,0)};header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(50)});header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(28)});header.Children.Add(art);var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,5,0)};title.FontWeight=FontWeights.SemiBold;title.TextTrimming=artist.TextTrimming=TextTrimming.CharacterEllipsis;words.Children.Add(title);words.Children.Add(artist);Grid.SetColumn(words,1);header.Children.Add(words);if(collapse!=null){var fold=Icon("chevron",collapse,"Now bar 접기",28,17);((Glyph)fold.Content).RenderTransform=new RotateTransform(90);((Glyph)fold.Content).RenderTransformOrigin=new Point(.5,.5);Grid.SetColumn(fold,2);header.Children.Add(fold);}compactOutput=Icon("audio-output",output,"출력 기기 변경",28,19);compactOutput.Visibility=Visibility.Collapsed;Grid.SetColumn(compactOutput,2);header.Children.Add(compactOutput);Grid.SetRow(header,1);Children.Add(header);
            timeline=new DockPanel {Margin=new Thickness(2,4,2,0)};var times=new Grid {Height=18};duration.HorizontalAlignment=HorizontalAlignment.Right;times.Children.Add(elapsed);times.Children.Add(duration);DockPanel.SetDock(times,Dock.Bottom);timeline.Children.Add(times);timeline.Children.Add(SeekBar);Grid.SetRow(timeline,2);Children.Add(timeline);System.Windows.Automation.AutomationProperties.SetName(SeekBar,"미디어 재생 위치");SeekBar.Changed+=Seek;
            controls=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};PreviousButton=Icon("previous",()=>command("previous"),"이전 곡",40,20);NextButton=Icon("next",()=>command("next"),"다음 곡",40,20);PlayButton=Paint.Button(playGlyph,()=>command("play"),"재생 · 일시 정지");PlayButton.Width=PlayButton.Height=44;PlayButton.Padding=new Thickness(0);PlayButton.Background=Paint.ControlWhite;PlayButton.Resources["ButtonRadius"]=new CornerRadius(22);PlayButton.Margin=new Thickness(24,0,24,0);controls.Children.Add(PreviousButton);controls.Children.Add(PlayButton);controls.Children.Add(NextButton);Grid.SetRow(controls,3);Children.Add(controls);
        }
        static Button Icon(string glyph,Action action,string hint,double size,double iconSize){var button=Paint.Button(new Glyph(glyph,iconSize),action,hint);button.Width=button.Height=size;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;return button;}
        internal void SetPalette(ArtworkPalette palette){playGlyph.Color=palette.Ink;playGlyph.InvalidateVisual();}
        internal void Update(MediaState media,string outputName)
        {
            media=media??new MediaState();bool empty=!media.Available||!media.Active;bool modeChanged=!modeSet||empty!=IsCompact;
            if(modeChanged){modeSet=true;IsCompact=empty;Height=empty?48:164;Margin=empty?new Thickness(14,7,14,7):new Thickness(14,12,14,12);route.Visibility=timeline.Visibility=controls.Visibility=empty?Visibility.Collapsed:Visibility.Visible;RowDefinitions[0].Height=new GridLength(empty?0:20);RowDefinitions[1].Height=new GridLength(empty?48:44);RowDefinitions[2].Height=new GridLength(empty?0:44);RowDefinitions[3].Height=new GridLength(empty?0:56);art.Width=art.Height=empty?28:42;art.CornerRadius=new CornerRadius(empty?14:11);header.ColumnDefinitions[0].Width=new GridLength(empty?40:50);title.FontSize=empty?13:15;compactOutput.Visibility=empty?Visibility.Visible:Visibility.Collapsed;}
            string key=media.AppId+"|"+media.Title+"|"+media.Artist;if(track!=key){track=key;Motion.Fade(title,.55,1,180);}title.Text=media.Title;artist.Text=media.Artist;
            string output=String.IsNullOrWhiteSpace(outputName)?"오디오 출력":outputName;if(device.Text!=output||device.ToolTip==null){device.Text=output;device.ToolTip="Windows 기본 미디어 출력 · "+device.Text;deviceGlyph.Kind=device.Text.IndexOf("Buds",StringComparison.OrdinalIgnoreCase)>=0||device.Text.IndexOf("헤드",StringComparison.Ordinal)>=0||device.Text.IndexOf("이어",StringComparison.Ordinal)>=0?"headphones":"volume";deviceGlyph.InvalidateVisual();OutputButton.ToolTip=compactOutput.ToolTip="출력 기기 변경 · "+device.Text;}
            if(artwork!=media.Artwork||art.Background==null||modeChanged){artwork=media.Artwork;if(artwork!=null){var brush=new ImageBrush(artwork){Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center};brush.Freeze();art.Background=brush;art.Child=null;}else{art.Background=IsCompact?Brushes.Transparent:QuickPanel.Card;art.Child=new Glyph("volume",23);}var palette=ArtworkPalette.Read(media.Artwork);SetPalette(palette);if(PaletteChanged!=null)PaletteChanged(palette);}
            string stateKind=media.Playing?"pause":"play";if(playGlyph.Kind!=stateKind){playGlyph.Kind=stateKind;playGlyph.InvalidateVisual();}PlayButton.IsEnabled=media.Available&&media.CanPlay;PreviousButton.IsEnabled=media.CanPrevious;NextButton.IsEnabled=media.CanNext;PlayButton.Opacity=PlayButton.IsEnabled?1:.4;PreviousButton.Opacity=PreviousButton.IsEnabled?1:.4;NextButton.Opacity=NextButton.IsEnabled?1:.4;
            var now=DateTime.UtcNow;if(!seeking&&now>seekUntil)SeekBar.Value=media.Duration>0?media.CurrentPosition(now)/media.Duration*100:0;SeekBar.IsEnabled=media.CanSeek&&media.Duration>0;elapsed.Text=media.Duration>0?NowBar.Clock(media.CurrentPosition(now)):"실시간";duration.Text=media.Duration>0?NowBar.Clock(media.Duration):"";
        }
        async void Seek(double value){pending=value;seekUntil=DateTime.UtcNow.AddSeconds(1);if(seeking)return;seeking=true;try{while(pending.HasValue){double next=pending.Value;pending=null;if(!await seekCommand(next))break;}}catch(Exception error){AppLog.Write("media-seek",error.Message);}finally{seeking=false;pending=null;}}
    }
}
