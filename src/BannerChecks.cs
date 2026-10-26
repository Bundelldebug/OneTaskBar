using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class BannerChecks
    {
        static NotificationEntry Entry(uint id){return new NotificationEntry {Id=id,AppId="OneTaskBar.QA",App="One TaskBar",Title="새로운 알림 "+id,Body="작은 One UI 블러 카드에서 알림 내용을 확인합니다.",Created=DateTimeOffset.Now};}
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;Window target=null;IDisposable motion=null;bool oldAnimations=Preferences.Current.Animations;
            app.Dispatcher.BeginInvoke(new Action(async()=> {
                try {
                    Program.Inspectable=true;motion=Motion.WithAnimationsForFixture();Preferences.Current.Animations=true;shell=new ShellController(true);target=new Window {Title="Banner focus fixture",Width=300,Height=180};target.Show();target.Activate();await Task.Delay(100);var foreground=Native.GetForegroundWindow();
                    var item=Entry(1);shell.Banners.Show(item);var banner=shell.Banners.Last;await Task.Delay(65);bool animated=banner.Animating&&banner.Opacity>0&&banner.Opacity<1&&Math.Abs(banner.Opacity-Native.BlurOpacity(banner))<.002;await Task.Delay(350);bool noActivation=Native.GetForegroundWindow()==foreground;bool glass=banner.HasBlur&&banner.Surface.RenderTransform.Value.IsIdentity&&LiveBackdrop.OrphanCount==0;bool noPlaceholder=!banner.HasIcon&&!banner.HasArtwork;Program.Save(banner.Surface,(int)banner.Width,(int)banner.Height,Path.Combine(AppLog.Data,"one-ui-notification-banner.png"));
                    var picture=new System.Windows.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppLog.Root,"assets","OneTaskBar.png")));picture.Freeze();var rich=Entry(8);rich.AppId="rich.fixture";rich.Icon=picture;rich.Artwork=picture;shell.Banners.Show(rich);await Task.Delay(100);bool richArtwork=shell.Banners.Last.Entry.Artwork!=null&&shell.Banners.Last.DesiredHeight==253;Program.Save(shell.Banners.Last.Surface,(int)shell.Banners.Last.Width,(int)shell.Banners.Last.Height,Path.Combine(AppLog.Data,"one-ui-rich-notification.png"));shell.Banners.Close(shell.Banners.Last,false);
                    int shown=shell.Banners.Shown;shell.Banners.Show(item);bool duplicate=shell.Banners.VisibleCount==1&&shell.Banners.Shown==shown;
                    shell.Banners.Show(Entry(2));bool grouped=shell.Banners.VisibleCount==1&&shell.Banners.Last.StackCount==2;
                    for(uint i=3;i<=6;i++){var other=Entry(i);other.AppId="fixture:"+i;shell.Banners.Show(other);}await Task.Delay(200);bool bounded=shell.Banners.VisibleCount==3&&shell.Banners.TimerActive&&grouped;
                    var view=new NativeToastView {Title=item.Title,Body=item.Body,App=item.App};bool match=view.Matches(item)&&!view.Matches(Entry(99));
                    bool classifier=NativeToastGate.Candidate("Windows.UI.Core.CoreWindow","ShellExperienceHost","새 알림",4)&&NativeToastGate.Candidate("Windows.UI.Core.CoreWindow","ShellExperienceHost","New notification",18)&&!NativeToastGate.Candidate("Windows.UI.Core.CoreWindow","ShellExperienceHost","알림 센터",4)&&!NativeToastGate.Candidate("XamlExplorerHostIslandWindow","explorer","",18)&&!NativeToastGate.Candidate("Windows.UI.Core.CoreWindow","ShellHost","DesktopWindowXamlSource",1);
                    var stale=Entry(20);stale.Created=DateTimeOffset.Now.AddMinutes(-1);var silent=Entry(21);silent.Payload=new NotificationPayload {SuppressPopup=true};bool history=!NotificationBanners.Fresh(stale,DateTimeOffset.Now)&&!NotificationBanners.Fresh(silent,DateTimeOffset.Now);
                    shell.Banners.Clear();await Task.Delay(100);bool cleaned=shell.Banners.VisibleCount==0&&!shell.Banners.TimerActive&&LiveBackdrop.OrphanCount==0&&LiveBackdrop.VisibleCount==0;
                    bool passed=animated&&noActivation&&glass&&duplicate&&bounded&&match&&classifier&&history&&cleaned&&richArtwork&&noPlaceholder;AppLog.Json("notification-ui-probe.json",new {passed=passed,popupAndBlurAnimateTogether=animated,neverStealsFocus=noActivation,matchingGlassAndContent=glass,nativeImageCard=richArtwork,missingImagesHaveNoPlaceholder=noPlaceholder,deduplicated=duplicate,maximumThreeCards=bounded,exactNativeContentMatch=match,shellToastOnly=classifier,noStaleOrSuppressedPopup=history,noIdleTimerOrOrphanBackdrop=cleaned});result=passed?0:1;
                }catch(Exception error){AppLog.Json("notification-ui-probe.json",new {passed=false,error=error.ToString()});}
                finally {if(shell!=null)shell.Dispose();if(target!=null)target.Close();if(motion!=null)motion.Dispose();Preferences.Current.Animations=oldAnimations;app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
