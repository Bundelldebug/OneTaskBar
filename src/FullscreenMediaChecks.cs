using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class FullscreenMediaChecks
    {
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        internal static int Run()
        {
            var application=new Application();int result=1;FullscreenMedia view=null;
            IDisposable animations=null;
            bool previousAnimations=Preferences.Current.Animations;
            application.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    Program.Inspectable=true;
                    animations=Motion.WithAnimationsForFixture();
                    Preferences.Current.Animations=true;
                    Preferences.Current.UIVersion=95;
                    bool locked=false;string command=null;
                    view=new FullscreenMedia(c=>command=c,p=>Task.FromResult(true),()=>{},()=>locked=true);
                    view.Width=760;view.Height=900;view.Show();
                    var art=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[]{160,80,30,255,160,80,30,255,160,80,30,255,160,80,30,255},8);art.Freeze();
                    var media=new MediaState {Available=true,Active=true,Playing=true,CanPlay=true,CanNext=true,CanSeek=true,Duration=210,Position=50,Title="테스트 미디어",Artist="테스트 아티스트",Artwork=art};
                    string opened=null;
                    media.AppId="OneTaskBar.MediaFixture";
                    var ordinary=new MediaPlayerView(c=>{},p=>Task.FromResult(true),()=>{},openApp:id=>opened=id);
                    ordinary.Update(media,"Fixture output");
                    ordinary.ArtworkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(opened==media.AppId,"ordinary artwork opens its app instead of resizing the player");
                    view.Update(media,"Fixture output");await Task.Delay(60);
                    Require(view.CoverBounds.Width>300&&view.CoverBounds.Bottom<view.CardBounds.Top,"cover and controls do not overlap");
                    Require(view.Player.ArtworkButton.Visibility==Visibility.Collapsed,"fullscreen renders one artwork visual, not a duplicate shared thumbnail");
                    Require(view.SeekBar.WaveMode&&view.SeekBar.WavePlaying&&view.Player.OutputName=="Fixture output","fullscreen uses the shared wave and output controls");
                    view.Transport.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(command=="play","transport dispatches media commands");
                    view.LockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(locked,"PC lock is a separate explicit action");
                    Program.Save(view.Surface,760,900,Path.Combine(AppLog.Data,"fullscreen-media.png"));
                    int geometryPasses=view.LayoutPasses;
                    for(int n=0;n<12;n++)view.Update(media,"Fixture output");
                    Require(view.LayoutPasses==geometryPasses,"timeline refreshes do not force full-screen layout");
                    view.CoverButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(90);Require(view.Morphing&&!view.LargeCover,"cover animates into the compact card");
                    view.Update(media,"Fixture output");Require(view.Morphing,"an in-flight media refresh preserves the cover animation");
                    view.ToggleArtwork();await Task.Delay(500);Require(!view.Morphing&&view.LargeCover,"rapid reversals settle without stale callbacks");
                    view.ToggleArtwork();await Task.Delay(420);view.Update(media,"Fixture output");
                    Require(view.CoverBounds.Width==54&&view.CoverBounds.Top>=view.CardBounds.Top&&view.CoverBounds.Bottom<=view.CardBounds.Bottom,"compact cover stays inside its reserved pill slot");
                    Require(view.CardBounds.Height==104&&view.CardBounds.Width<=440&&view.Player.FullscreenPill,"collapsed media uses a short pill instead of the full controller");
                    Require(view.Player.PreviousButton.Visibility==Visibility.Collapsed&&view.SeekBar.WaveDecoration,"pill keeps play/next and a decorative bottom wave");
                    view.Player.NextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(command=="next","pill next button keeps the shared media command");
                    int pillPasses=view.LayoutPasses;
                    for(int n=0;n<12;n++)view.Update(media,"Fixture output");
                    Require(view.LayoutPasses==pillPasses&&!view.Morphing,"unchanged pill refreshes do not start layout or animations");
                    Require(view.WallpaperBrush!=null&&view.WallpaperBrush.IsFrozen,"collapsed mode loads a frozen desktop wallpaper surface");
                    Program.Save(view.Surface,760,900,Path.Combine(AppLog.Data,"fullscreen-media-collapsed.png"));
                    media.Playing=false;view.Update(media);Require(!view.SeekBar.WavePlaying,"pausing uses the shared wave decay");media.Playing=true;
                    view.ToggleArtwork();await Task.Delay(420);
                    Require(!view.Player.FullscreenPill&&!view.SeekBar.WaveDecoration&&view.Player.PreviousButton.Visibility==Visibility.Visible,"expanding restores the full shared controller");
                    foreach(var size in new[]{new Size(640,480),new Size(1280,720),new Size(2160,1440)}){view.Width=size.Width;view.Height=size.Height;await Task.Delay(30);view.Update(media);Require(view.CoverBounds.Left>=0&&view.CoverBounds.Right<=size.Width&&view.CoverBounds.Bottom<view.CardBounds.Top,"responsive cover remains clear of transport");}
                    foreach(int version in new[]{85,90}){
                        Preferences.Current.UIVersion=version;
                        var alternate=new FullscreenMedia(c=>command=c,p=>Task.FromResult(true),()=>{},()=>{});
                        try{
                            alternate.Width=640;alternate.Height=480;alternate.Show();alternate.Update(media,"Fixture output");
                            alternate.ToggleArtwork();await Task.Delay(420);alternate.Update(media,"Fixture output");
                            Require(alternate.Player.FullscreenPill&&alternate.CardBounds.Height==104&&alternate.CoverBounds.Width==54,"older UI versions use the same compact pill geometry");
                            alternate.ToggleArtwork();await Task.Delay(420);alternate.Update(media,"Fixture output");
                            Require(!alternate.Player.FullscreenPill&&alternate.Player.PreviousButton.Visibility==Visibility.Visible,"older UI versions restore full controls after a pill transition");
                        }
                        finally{alternate.Close();}
                    }
                    Preferences.Current.UIVersion=95;
                    view.ToggleArtwork();view.HideImmediately();await Task.Delay(40);Require(!view.Morphing,"hidden fullscreen surface stops rendering");
                    view.Show();await Task.Delay(30);
                    var sourceWindow=new System.Windows.Interop.WindowInteropHelper(view).Handle;
                    using(var lease=new MediaWindowLease())
                    {
                        Require(lease.Arm(sourceWindow),"per-window transition lease acquired");
                        Require(lease.HideRepresentation(sourceWindow),"real window hidden while its representation animates");
                        int cloaked;Native.DwmGetWindowAttribute(sourceWindow,14,out cloaked,4);
                        Require(cloaked!=0,"duplicate source is absent from composition");
                        lease.RevealAll();Native.DwmGetWindowAttribute(sourceWindow,14,out cloaked,4);
                        Require(cloaked==0,"source is restored after cancellation");
                        Require(lease.HideRepresentation(sourceWindow),"recovery fixture cloaks source");
                        var guard=System.Reflection.Assembly.LoadFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"OneTaskBar.Guard.exe"));
                        guard.GetType("Guard").GetMethod("RestoreMedia",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{AppLog.Data});
                        Native.DwmGetWindowAttribute(sourceWindow,14,out cloaked,4);
                        Require(cloaked==0,"independent watchdog restores source after an interrupted transition");
                    }
                    await MediaFlightChecks.CaptureRace(view);
                    using(var motion=new MediaWindowMotion(null)){
                        var source=new System.Windows.Interop.WindowInteropHelper(view).Handle;var foreground=Native.GetForegroundWindow();
                        view.Width=800;view.Height=600;view.Update(media);view.UpdateLayout();
                        var snapshot=new RenderTargetBitmap(800,600,96,96,PixelFormats.Pbgra32);snapshot.Render(view.Surface);snapshot.Freeze();
                        var initial=new Rect(40,40,800,600);var capsule=new Rect(900,30,44,88);
                        var pose=MediaWindowPose.At(initial,capsule,.18);
                        MediaFlightChecks.Check();
                        Require(Math.Abs(pose.Width/pose.Height-initial.Width/initial.Height)<.001&&Math.Abs(pose.Turn)>1&&Math.Abs(pose.Tilt)>1,"reference flight begins with a restrained perspective tilt");
                        Require(MediaWindowPose.At(initial,capsule,0).Width==800&&MediaWindowPose.At(initial,capsule,1).Opacity==0,"flight hands off at its two endpoints");
                        foreach(bool reverse in new[]{false,true})
                        {
                            double last=MediaFlightTiming.Position(0,reverse);
                            for(int step=1;step<=100;step++)
                            {
                                double next=MediaFlightTiming.Position(step/100.0,reverse);
                                Require(reverse?next<=last:next>=last,"directional timeline never reverses or bounces");
                                last=next;
                            }
                            var firstPose=MediaWindowPose.At(initial,capsule,MediaFlightTiming.Position(0,reverse));
                            var nearFirst=MediaWindowPose.At(initial,capsule,MediaFlightTiming.Position(.001,reverse));
                            var lastPose=MediaWindowPose.At(initial,capsule,MediaFlightTiming.Position(1,reverse));
                            var nearLast=MediaWindowPose.At(initial,capsule,MediaFlightTiming.Position(.999,reverse));
                            Require(Math.Abs(firstPose.Width-nearFirst.Width)<.05&&Math.Abs(lastPose.Width-nearLast.Width)<.05,"flight starts and hands back at rest");
                        }
                        var halfway=MediaWindowPose.At(initial,capsule,.5);
                        Require(halfway.Width>initial.Width*.45&&halfway.Width<initial.Width*.65,"the app remains readable through the middle of its flight");
                        int backdropsBefore=LiveBackdrop.InstanceCount;
                        var plane=new MediaWindowPlane(snapshot,initial,capsule);
                        Require(LiveBackdrop.InstanceCount==backdropsBefore,"warming a flight allocates no full-display blur host");
                        plane.Frame(initial,capsule,.18);plane.Present();await Task.Delay(70);
                        Program.Save(plane.Scene,(int)plane.ActualWidth,(int)plane.ActualHeight,Path.Combine(AppLog.Data,"media-flight-pose.png"));plane.Close();
                        Native.ShowWindow(source,6);
                        var cover=new MediaWindowPlane(snapshot,initial,capsule);
                        try
                        {
                            Require(cover.CoverRestoringWindow(source)&&cover.HasOcclusion,"a minimized app can restore behind one visual cover without foreign-window cloaking");
                            Require(Native.IsIconic(source),"preparing the cover never restores or changes the source window");
                        }
                        finally {cover.Close();Native.ShowWindow(source,4);}
                        motion.Begin(source,new AppEntry {Name="tilted fixture"},source,initial,capsule,true,false,snapshot);
                        await Task.Delay(70);Require(motion.Active&&Native.GetForegroundWindow()!=motion.PreviewHandle,"tilted flight does not steal focus");
                        Require(motion.FrameCount>0&&motion.FirstProgress==0,"first presented frame starts at zero after setup");
                        motion.Observe(IntPtr.Zero);
                        Require(motion.Active,"an inactive media refresh cannot interrupt a committed flight");
                        await Task.Delay(600);int released;Native.DwmGetWindowAttribute(source,14,out released,4);
                        Require(!motion.Active&&released==0,"tilted flight releases its texture host and restores source visibility");
                        Require(LiveBackdrop.InstanceCount==backdropsBefore,"temporary defocus host is released with the flight");
                        AppLog.Json("media-flight-baseline.json",new {frames=motion.FrameCount,firstProgress=motion.FirstProgress,maximumFrameGap=motion.MaximumFrameGap});
                        motion.Begin(source,new AppEntry {Name="tilted restore"},source,capsule,initial,true,true,snapshot);
                        await Task.Delay(70);Require(motion.Active,"restore uses the same textured plane");motion.Stop();Native.DwmGetWindowAttribute(source,14,out released,4);
                        Require(released==0&&!motion.Active,"cancelled reverse flight restores its owner");
                        var focus=new Window {Width=180,Height=100,ShowInTaskbar=false};
                        bool beforeRestore=false;
                        Action<IntPtr> prepare=window=> {
                            if(window!=source)return;
                            beforeRestore=Native.IsIconic(source);
                            motion.Begin(source,new AppEntry {Name="native restore"},source,capsule,initial,true,true,snapshot);
                        };
                        try
                        {
                            Native.ShowWindow(source,6);
                            focus.Show();
                            focus.Activate();
                            WindowActivation.Restoring+=prepare;
                            WindowActivation.TryActivate(source);
                            await Task.Delay(80);
                            Native.DwmGetWindowAttribute(source,14,out released,4);
                            Require(beforeRestore&&motion.Active&&released!=0,"native restoration is cloaked before its first visible frame");
                            Require(motion.FirstProgress==0,"restore preparation cannot skip ahead in the animation");
                            await Task.Delay(650);
                            Native.DwmGetWindowAttribute(source,14,out released,4);
                            Require(!motion.Active&&!Native.IsIconic(source)&&released==0,"native restoration finishes with the real app visible");
                        }
                        finally
                        {
                            WindowActivation.Restoring-=prepare;
                            motion.Stop();
                            focus.Close();
                        }
                        motion.Begin(source,new AppEntry {Name="media fixture"},source,new Rect(40,40,400,300),new Rect(30,400,80,44));
                        await Task.Delay(70);Require(motion.Active&&Native.GetForegroundWindow()!=motion.PreviewHandle,"media thumbnail animation does not take focus");
                        await Task.Delay(400);Require(!motion.Active,"media thumbnail HWND is released after the transition");
                        motion.Begin(source,new AppEntry {Name="restore fixture"},IntPtr.Zero,new Rect(30,400,80,44),new Rect(40,40,400,300),true,true);
                        await Task.Delay(70);int movingSource;Native.DwmGetWindowAttribute(source,14,out movingSource,4);
                        Require(motion.Active&&movingSource!=0,"only one window representation is visible during restore");
                        await Task.Delay(400);Native.DwmGetWindowAttribute(source,14,out movingSource,4);
                        Require(!motion.Active&&movingSource==0,"restore hands visibility back to the original window");
                        for(int i=0;i<6;i++){motion.Begin(source,new AppEntry {Name="media fixture"},source,new Rect(30,400,80,44),new Rect(40,40,400,300));await Task.Delay(24);}
                        motion.Stop();Require(!motion.Active,"repeated cancellation releases the preview without nested rendering");
                        Require(MediaWindowMotion.Interpolate(new Rect(0,0,400,300),new Rect(10,20,44,44),1)==new Rect(10,20,44,44),"motion reaches the capsule exactly");
                    }
                    AppLog.Json("fullscreen-media-probe.json",new {passed=true,responsive=true,separateLock=true,commands=true,morphReversal=true,hiddenClockReleased=true});result=0;
                }catch(Exception error){AppLog.Json("fullscreen-media-probe.json",new {passed=false,error=error.ToString()});}
                finally
                {
                    if(view!=null)view.Close();
                    Preferences.Current.Animations=previousAnimations;
                    if(animations!=null)animations.Dispose();
                    application.Shutdown(result);
                }
            }),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
