using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class MediaModernChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Media UI 9: "+message);}
        static bool Inside(FrameworkElement element,FrameworkElement parent){var point=element.TranslatePoint(new Point(),parent);return point.X>=-.5&&point.Y>=-.5&&point.X+element.ActualWidth<=parent.ActualWidth+.5&&point.Y+element.ActualHeight<=parent.ActualHeight+.5;}
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;Window window=null;int oldUI=Preferences.Current.UIVersion;bool oldAnimations=Preferences.Current.Animations;IDisposable scope=null;
            application.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;scope=Motion.WithAnimationsForFixture();Preferences.Current.UIVersion=90;Preferences.Current.Animations=true;var commands=new List<string>();double sought=-1;string opened=null;
                var media=new MediaState {Available=true,Active=true,Playing=true,CanPlay=true,CanPrevious=true,CanNext=true,CanSeek=true,CanShuffle=true,Title="中央に表示する曲名",Artist="아티스트",Duration=214,Position=103,Artwork=ArtworkPalette.Sample(Color.FromRgb(210,38,144))};
                var view=new MediaPlayerView(commands.Add,p=>{sought=p;return Task.FromResult(true);},()=>{},condensed:true,openApp:id=>opened=id,dense:true);var card=new Border {CornerRadius=new CornerRadius(24),Child=view};view.PaletteChanged=p=>card.Background=view.MediaBackground;window=new Window {WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,SizeToContent=SizeToContent.WidthAndHeight,Content=card,Left=20,Top=20};window.Show();
                var sizes=new List<object>();foreach(double width in new[]{168.0,236,342,472})foreach(double height in new[]{114.0,130,200,246,378}){
                    card.Width=width;card.Height=height;view.FitWidget(width,height);view.Update(media,"헤드폰 (테스트 출력)");await Task.Delay(65);window.UpdateLayout();
                    Require(view.Modern&&new[]{view.PlayButton,view.PreviousButton,view.NextButton,view.ShuffleButton}.All(b=>Inside(b,card)),"transport bounds "+width+" x "+height);
                    Require(Inside(view.SeekBar,card)&&Inside(view.CenterTitle,card)&&(!view.CenterArtist.IsVisible||Inside(view.CenterArtist,card)),"progress and labels fit "+width+" x "+height);
                    double textBottom=view.CenterTitle.TranslatePoint(new Point(0,view.CenterTitle.ActualHeight),card).Y;if(view.CenterArtist.IsVisible)textBottom=view.CenterArtist.TranslatePoint(new Point(0,view.CenterArtist.ActualHeight),card).Y;double waveTop=view.SeekBar.TranslatePoint(new Point(),card).Y;
                    if(waveTop<textBottom+1)Program.Save(card,(int)width,(int)height,Path.Combine(AppLog.Data,"media9-spacing-failure.png"));
                    Require(waveTop>=textBottom+1,"readable spacing between text and wave "+width+" x "+height+"; text="+textBottom+", wave="+waveTop+", artist="+view.CenterArtist.Visibility);
                    sizes.Add(new {width=width,height=height,poster=view.LargeCoverVisible});if((width==168&&height==114)||(width==342&&height==130)||(width==342&&height==378))Program.Save(card,(int)width,(int)height,Path.Combine(AppLog.Data,"media9-"+width+"x"+height+".png"));
                }
                card.Width=342;card.Height=114;view.FitWidget(342,114);view.Update(media,"스피커");await Task.Delay(100);Require(view.CenterTitle.Centered&&view.CenterTitle.ActualWidth>=view.CenterTitle.NaturalWidth-.5,"short title remains centered");Require(view.SeekBar.WaveColor==ArtworkPalette.Read(media.Artwork).ProgressColor,"progress color follows the artwork");int frames=view.SeekBar.WaveFrames;await Task.Delay(200);Require(view.SeekBar.WaveAnimating&&view.SeekBar.WaveFrames>frames&&view.SeekBar.Value>103.0/214*100,"wave and smooth timeline advance while playing");
                media.Playing=false;view.Update(media,"스피커");frames=view.SeekBar.WaveFrames;await Task.Delay(130);Require(!view.SeekBar.WaveAnimating&&view.SeekBar.WaveFrames==frames,"paused playback must stop all wave frames");
                card.Height=200;view.FitWidget(342,200);media.Title="긴 제목 자동 스크롤 검증 "+new string('가',90);media.Artist=new string('나',80);view.Update(media,"스피커");await Task.Delay(1500);Require(view.CenterTitle.Text==media.Title+" · "+media.Artist&&view.CenterTitle.IsAnimating&&view.CenterTitle.Offset>0&&!view.CenterArtist.IsVisible,"landscape title and artist scroll together with a middle dot");Program.Save(card,342,200,Path.Combine(AppLog.Data,"media9-marquee.png"));
                card.Height=378;view.FitWidget(342,378);view.Update(media,"스피커");await Task.Delay(1500);Require(view.LargeCoverVisible&&view.CenterTitle.Text==media.Title&&view.CenterTitle.IsAnimating&&view.CenterArtist.IsAnimating,"cover layout centers and scrolls its title and artist separately");Require(MediaPlayerView.InlineCaption("곡",null)=="곡"&&MediaPlayerView.InlineCaption("곡","  ")=="곡","missing artist has no dangling separator");
                view.ShuffleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));view.PreviousButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));view.PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));view.NextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(commands.SequenceEqual(new[]{"shuffle","previous","play","next"}),"transport dispatches real commands");
                var peer=UIElementAutomationPeer.CreatePeerForElement(view.SeekBar);var range=(IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue);range.SetValue(63);await Task.Delay(30);Require(sought==63,"accessible slider preserves seeking");media.CanSeek=media.CanShuffle=false;view.Update(media,"스피커");Require(!view.SeekBar.IsEnabled&&!view.ShuffleButton.IsEnabled,"provider capabilities disable unsupported controls");
                media.CanSeek=true;media.AppId="fixture.media";view.Update(media,"스피커");view.ArtworkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(150);Require(opened==media.AppId,"artwork opens the currently playing application");
                media.Playing=true;view.Update(media,"스피커");await Task.Delay(100);window.Hide();frames=view.SeekBar.WaveFrames;await Task.Delay(120);Require(!view.SeekBar.WaveAnimating&&frames==view.SeekBar.WaveFrames&&!view.CenterTitle.IsAnimating&&!view.CenterArtist.IsAnimating,"hidden players release all animation clocks");window.Show();await Task.Delay(80);Require(view.SeekBar.WaveAnimating,"reopening resumes the wave");Preferences.Current.Animations=false;view.Update(media,"스피커");Require(!view.SeekBar.WaveAnimating&&!view.CenterTitle.IsAnimating,"animations-off preference is respected");Preferences.Current.Animations=true;
                view.Update(new MediaState(),"스피커");await Task.Delay(50);Require(view.IsCompact&&!view.LargeCoverVisible&&!view.SeekBar.WaveAnimating,"idle media returns to one row");Preferences.Current.UIVersion=85;var legacy=new MediaPlayerView(commands.Add,p=>Task.FromResult(true),()=>{});legacy.Update(media,"스피커");Require(!legacy.Modern&&!legacy.SeekBar.WaveMode&&legacy.ShuffleButton==null,"UI 8.5 retains its original transport and track");
                AppLog.Json("media9-probe.json",new {passed=true,sizes=sizes,centered=true,longTitleAndArtistScroll=true,playingWave=true,pausedAndHiddenClocksStop=true,reopens=true,animationsOff=true,seekAndTransport=true,artworkOpensApplication=true,capabilities=true,idleOneRow=true,legacyPreserved=true,osMediaCommandsSent=false});result=0;
            }catch(Exception error){AppLog.Json("media9-probe.json",new {passed=false,error=error.ToString()});}finally{if(window!=null)window.Close();Preferences.Current.UIVersion=oldUI;Preferences.Current.Animations=oldAnimations;if(scope!=null)scope.Dispose();application.Shutdown(result);}}),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
