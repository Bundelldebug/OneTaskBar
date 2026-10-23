using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class MediaActivationChecks
    {
        internal static void Validate(List<string> checks)
        {
            var browser=new AppEntry {AppID="Internet",Executable=@"C:\Apps\browser.exe"};var web=new AppEntry {AppID="Internet._crx_music",Executable=browser.Executable};var player=new AppEntry {AppID="Player.Desktop",Executable=@"C:\Apps\player.exe"};var all=new[]{browser,web,player};
            if(MediaApplication.Resolve(web.AppID,all)!=web||MediaApplication.Resolve("player.exe",all)!=player||MediaApplication.Resolve("music",all)!=null||MediaApplication.Resolve(null,all)!=null)throw new Exception("Media activation must match the complete session identity without confusing browser web apps");
            checks.Add("Artwork activation resolves exact media app IDs or complete legacy executable names, retaining distinct web app identities");
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;GlassWindow window=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{Directory.CreateDirectory(AppLog.Data);Program.Inspectable=true;Validate(new List<string>());var opened=new List<string>();var media=new MediaState {Available=true,Active=true,AppId="Fixture!Player",Title="앨범 커버를 눌러 앱 열기",Artist="One TaskBar",Artwork=ArtworkPalette.Sample(Colors.CornflowerBlue)};bool passed=true;var styles=new List<string>();
                    foreach(string style in new[]{"quick","expanded","large-cover"}){var view=new MediaPlayerView(_=>{},_=>Task.FromResult(true),()=>{},condensed:style=="quick",openApp:id=>opened.Add(id));if(style=="large-cover")view.SetPoster(240);view.Update(media,"스피커");window=new GlassWindow();window.Surface.Child=view;window.ShowAt(System.Windows.Forms.Screen.PrimaryScreen.Bounds,30,30,380,style=="large-cover"?400:210);await Task.Delay(80);int before=opened.Count;passed&=view.ArtworkButton.IsEnabled;view.ArtworkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(210);passed&=opened.Count==before+1&&opened.Last()==media.AppId;media.Artwork=null;view.Update(media,"스피커");passed&=!view.ArtworkButton.IsEnabled;media.Artwork=ArtworkPalette.Sample(Colors.CornflowerBlue);window.Close();window=null;styles.Add(style);}
                    AppLog.Json("media-activation-probe.json",new {passed=passed,styles=styles.ToArray(),sourceSessionIds=opened.ToArray(),missingArtworkNotClickable=true,realAppsOpened=false});result=passed?0:1;
                }catch(Exception error){AppLog.Json("media-activation-probe.json",new {passed=false,error=error.ToString()});}finally{if(window!=null)window.Close();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
