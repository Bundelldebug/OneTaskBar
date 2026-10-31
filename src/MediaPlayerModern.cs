using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace OneTaskBar
{
    internal sealed partial class MediaPlayerView
    {
        internal readonly bool Modern;
        internal Button ShuffleButton {get;private set;}
        internal MarqueeTitle CenterTitle {get{return modernTitle;}}
        internal MarqueeTitle CenterArtist {get{return modernArtist;}}
        MarqueeTitle modernTitle,modernArtist;Image sourceIcon;
        Grid titleLine,modernWords,transport;Button fold;string logoApp;int logoRequest;MediaState modernState;double popupHeight=130;bool showArtist,compactPopup;
        internal bool CompactMedia {get{return dense||compactPopup;}}
        internal void FitPopup(double height){if(!Modern)return;bool compact=height<=150&&posterEdge<120;if(Math.Abs(popupHeight-height)<.1&&compactPopup==compact)return;popupHeight=height;widgetHeight=height;compactPopup=compact;modeSet=false;}
        static readonly Brush controlGlass=Paint.Brush("#24FFFFFF"),selectedControl=Paint.Brush("#54FFFFFF");
        void BuildModern(Action<string> command)
        {
            modernTitle=new MarqueeTitle {Centered=true};modernArtist=new MarqueeTitle {Centered=true,TextWeight=FontWeights.Normal};sourceIcon=new Image {Stretch=Stretch.Uniform,Visibility=Visibility.Collapsed};
            fold=header.Children.OfType<Button>().FirstOrDefault(b=>b!=ArtworkButton&&b!=compactOutput);
            header.Children.Clear();header.ColumnDefinitions.Clear();header.ColumnDefinitions.Add(new ColumnDefinition());
            header.Children.Add(ArtworkButton);header.Children.Add(posterShade);Grid.SetColumnSpan(ArtworkButton,1);Grid.SetColumnSpan(posterShade,1);
            titleLine=new Grid {HorizontalAlignment=HorizontalAlignment.Center};titleLine.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(0)});titleLine.ColumnDefinitions.Add(new ColumnDefinition());titleLine.Children.Add(sourceIcon);Grid.SetColumn(modernTitle,1);titleLine.Children.Add(modernTitle);
            modernWords=new Grid {IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center};modernWords.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});modernWords.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});modernWords.Children.Add(titleLine);Grid.SetRow(modernArtist,1);modernWords.Children.Add(modernArtist);header.Children.Add(modernWords);
            if(fold!=null){Grid.SetColumn(fold,0);fold.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(fold);}Grid.SetColumn(compactOutput,0);compactOutput.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(compactOutput);
            Children.Remove(controls);transport=new Grid();transport.ColumnDefinitions.Add(new ColumnDefinition());transport.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});transport.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(controls,1);transport.Children.Add(controls);
            ShuffleButton=Icon("shuffle",()=>command("shuffle"),"셔플 재생",26,18);ShuffleButton.HorizontalAlignment=HorizontalAlignment.Left;transport.Children.Add(ShuffleButton);Grid.SetRow(transport,3);Children.Add(transport);
            foreach(var button in new[]{PreviousButton,PlayButton,NextButton}){button.Background=controlGlass;button.Resources["ButtonRadius"]=new CornerRadius(40);}
            SeekBar.WaveMode=true;SeekBar.MinWidth=0;SeekBar.WaveProgress=CurrentWavePosition;playGlyph.Color=Paint.ControlWhite;
            if(Glass95.Enabled)Glass95.MediaInk(this);
            header.SizeChanged+=(s,e)=>FitModernControls();IsVisibleChanged+=(s,e)=>{if(IsVisible&&modernState!=null)LoadSourceLogo(modernState.AppId);};Unloaded+=(s,e)=>{logoRequest++;logoApp=null;sourceIcon.Source=null;};
        }
        double CurrentWavePosition()
        {
            if(modernState==null||seeking||DateTime.UtcNow<=seekUntil)return SeekBar.Value;
            var now=DateTime.UtcNow;if(modernState.Duration<=0)return 0;string time=NowBar.Clock(modernState.CurrentPosition(now));if(elapsed.Text!=time)elapsed.Text=time;return modernState.CurrentPosition(now)/modernState.Duration*100;
        }
        async void LoadSourceLogo(string appId)
        {
            if(!IsVisible||logoApp==appId)return;logoApp=appId;int revision=++logoRequest;sourceIcon.Source=null;sourceIcon.Visibility=Visibility.Collapsed;FitModernControls();if(String.IsNullOrEmpty(appId))return;
            try{var entry=sourceApplication==null?null:sourceApplication(appId);ImageSource image=entry==null?null:entry.Icon;if(image==null)image=await SystemServices.AppLogo(appId);if(image==null&&entry!=null)image=await BackgroundWork.Run(()=>AppIcons.Get(entry));if(revision!=logoRequest)return;sourceIcon.Source=image;sourceIcon.Visibility=image==null?Visibility.Collapsed:Visibility.Visible;FitModernControls();}catch(Exception error){AppLog.Once("media-source-logo",error.GetType().Name);}
        }
        void FitModernControls()
        {
            if(titleLine==null)return;double width=header.ActualWidth>1?header.ActualWidth:Math.Max(1,(widgetWidth>0?widgetWidth:ActualWidth)-28);bool narrow=width<210;
            double font=usingPoster?Math.Max(14,Math.Min(20,posterEdge/15)):CompactMedia?(narrow?11:13):15;
            modernTitle.TextSize=font;modernTitle.Height=modernTitle.LineHeight;modernArtist.TextSize=usingPoster?12:CompactMedia?10:11;modernArtist.Height=modernArtist.LineHeight;modernArtist.Margin=new Thickness(0,showArtist?3:0,0,0);modernArtist.Visibility=showArtist?Visibility.Visible:Visibility.Collapsed;modernWords.Height=modernTitle.Height+(showArtist?modernArtist.Height+3:0);
            double symbol=sourceIcon.Source==null||IsCompact?0:font+5;sourceIcon.Width=sourceIcon.Height=Math.Max(1,symbol-4);sourceIcon.Margin=new Thickness(0,0,symbol>0?4:0,0);titleLine.ColumnDefinitions[0].Width=new GridLength(symbol);double room=Math.Max(1,width-(usingPoster?24:fold!=null||IsCompact?56:8));modernTitle.Width=Math.Min(modernTitle.NaturalWidth,Math.Max(1,room-symbol));modernArtist.Width=room;
            bool tight=CompactMedia&&!usingPoster&&widgetHeight>0&&widgetHeight<128;double small=CompactMedia?(tight?24:28):36,big=CompactMedia?(tight?26:32):44,gap=narrow?6:CompactMedia?16:22;if(width<155){small=22;big=26;gap=3;}
            PreviousButton.Width=PreviousButton.Height=NextButton.Width=NextButton.Height=small;PlayButton.Width=PlayButton.Height=big;PlayButton.Margin=new Thickness(gap,0,gap,0);ShuffleButton.Width=ShuffleButton.Height=Math.Max(18,Math.Min(26,(width-(small*2+big+gap*2))/2));
            device.FontSize=CompactMedia?10:11;device.Visibility=narrow?Visibility.Collapsed:Visibility.Visible;route.ColumnDefinitions[1].Width=new GridLength(1,GridUnitType.Star);
        }
        void UpdateModern(MediaState state,string outputName)
        {
            state=state??new MediaState();modernState=state;bool empty=!state.Available||!state.Active,poster=!empty&&posterEdge>=120&&state.Artwork!=null;
            bool changed=!modeSet||empty!=IsCompact||usingPoster!=poster;IsCompact=empty;usingPoster=poster;modeSet=true;applicationId=state.AppId;
            if(changed){
                double contentHeight=CompactMedia?Math.Max(98,Math.Min(196,widgetHeight-16)):Math.Max(100,popupHeight-24);showArtist=empty||poster;
                Height=empty?48:poster?posterEdge+(CompactMedia?102:154):contentHeight;Margin=empty?new Thickness(14,7,14,7):new Thickness(14,CompactMedia?8:12,14,CompactMedia?8:12);
                double extra=Math.Max(0,contentHeight-98);double[] rows=empty?new[]{0.0,48,0,0}:poster?new[]{posterEdge,28.0,CompactMedia?32:56,CompactMedia?42:70}:CompactMedia&&!showArtist?new[]{16.0+extra*.125,24,30+extra*.375,28+extra*.5}:new[]{contentHeight*.133,contentHeight*.225,contentHeight*.285,contentHeight*.357};for(int i=0;i<4;i++)RowDefinitions[i].Height=new GridLength(rows[i]);
                Grid.SetRow(header,poster?0:1);Grid.SetRow(route,poster?1:0);header.Width=poster?posterEdge:Double.NaN;header.HorizontalAlignment=poster?HorizontalAlignment.Center:HorizontalAlignment.Stretch;header.Clip=poster?new RectangleGeometry(new Rect(0,0,posterEdge,posterEdge),20,20):null;
                art.Visibility=Visibility.Collapsed;posterShade.Visibility=poster?Visibility.Visible:Visibility.Collapsed;modernWords.VerticalAlignment=poster?VerticalAlignment.Bottom:VerticalAlignment.Center;modernWords.Margin=poster?new Thickness(12,0,12,16):new Thickness(0);posterShade.Height=poster?100:0;route.Visibility=timeline.Visibility=transport.Visibility=empty?Visibility.Collapsed:Visibility.Visible;controls.Visibility=Visibility.Visible;compactOutput.Visibility=empty?Visibility.Visible:Visibility.Collapsed;if(fold!=null)fold.Visibility=empty?Visibility.Collapsed:Visibility.Visible;
                timeline.Margin=new Thickness(0,2,0,0);double times=CompactMedia?10:16;SeekBar.Height=Math.Max(16,rows[2]-times-2);((Grid)timeline.Children[0]).Height=times;
            }
            modernTitle.Text=!empty&&!poster?InlineCaption(state.Title,state.Artist):state.Title;modernArtist.Text=state.Artist;modernTitle.Running=modernArtist.Running=!empty;ToolTip=InlineCaption(state.Title,state.Artist);
            LoadSourceLogo(empty?null:state.AppId);
            string output=String.IsNullOrWhiteSpace(outputName)?"오디오 출력":outputName;device.Text=output;device.ToolTip="Windows 기본 미디어 출력 · "+output;deviceGlyph.Update(output.IndexOf("Buds",StringComparison.OrdinalIgnoreCase)>=0||output.IndexOf("헤드",StringComparison.Ordinal)>=0||output.IndexOf("이어",StringComparison.Ordinal)>=0?"headphones":"volume",Paint.ControlWhite);OutputButton.ToolTip=compactOutput.ToolTip="출력 기기 변경 · "+output;
            budsOutput=BudsInfo.IsBuds(output);if(budsOutput&&deviceModel!=BudsDeviceData.SymbolFile(output))LoadDeviceSymbol(output);deviceGlyph.SetDeviceIcon(budsOutput?deviceSymbol:null);
            if(artwork!=state.Artwork||changed){artwork=state.Artwork;var picture=artwork==null?null:new ImageBrush(artwork){Stretch=Stretch.UniformToFill};if(picture!=null)picture.Freeze();art.Background=picture;ArtworkButton.Background=poster?(Brush)picture:Brushes.Transparent;ArtworkButton.Resources["ButtonRadius"]=new CornerRadius(poster?20:11);var palette=ArtworkPalette.Read(state.Artwork);SeekBar.WaveColor=palette.ProgressColor;MediaBackground=palette.PlayerSurface(state.Artwork);if(PaletteChanged!=null)PaletteChanged(palette);}
            ArtworkButton.IsEnabled=!opening&&!empty&&artwork!=null&&openApplication!=null&&!String.IsNullOrEmpty(applicationId);
            playGlyph.Update(state.Playing?"pause":"play",Paint.ControlWhite);PlayButton.IsEnabled=state.Available&&state.CanPlay;PreviousButton.IsEnabled=state.CanPrevious;NextButton.IsEnabled=state.CanNext;ShuffleButton.IsEnabled=state.CanShuffle;ShuffleButton.Background=state.Shuffling?selectedControl:Brushes.Transparent;ShuffleButton.Resources["ButtonRadius"]=new CornerRadius(15);ShuffleButton.ToolTip=state.CanShuffle?(state.Shuffling?"셔플 재생 끄기":"셔플 재생 켜기"):"이 앱은 셔플 제어를 지원하지 않습니다";foreach(var button in new[]{PlayButton,PreviousButton,NextButton,ShuffleButton})button.Opacity=button.IsEnabled?1:.35;
            var now=DateTime.UtcNow;if(!seeking&&now>seekUntil)SeekBar.Value=state.Duration>0?state.CurrentPosition(now)/state.Duration*100:0;SeekBar.IsEnabled=state.CanSeek&&state.Duration>0;SeekBar.WavePlaying=!empty&&state.Playing&&state.Duration>0;SeekBar.WheelAdjust=!condensed||Preferences.Current.QuickSliderScroll;elapsed.Text=state.Duration>0?NowBar.Clock(state.CurrentPosition(now)):"실시간";duration.Text=state.Duration>0?NowBar.Clock(state.Duration):"";FitModernControls();
        }
        internal static string InlineCaption(string title,string artist){return (title??"")+(String.IsNullOrWhiteSpace(artist)?"":" · "+artist.Trim());}
    }
}
