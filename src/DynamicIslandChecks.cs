using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

namespace OneTaskBar
{
    internal static class DynamicIslandChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Dynamic Island: "+message);}
        static BitmapSource CoverFixture()
        {
            var visual=new DrawingVisual();
            using(var context=visual.RenderOpen())
            {
                context.DrawRectangle(Brushes.Coral,null,new Rect(0,0,192,216));
                context.DrawRectangle(Brushes.Turquoise,null,new Rect(192,0,192,216));
                context.DrawEllipse(Brushes.Gold,null,new Point(260,90),58,58);
            }
            var image=new RenderTargetBitmap(384,216,96,96,PixelFormats.Pbgra32);
            image.Render(visual);
            image.Freeze();
            return image;
        }
        static void CheckSurface(BitmapSource artwork)
        {
            var brush=ArtworkPalette.IslandSurface(artwork);
            Require(brush.IsFrozen&&Object.ReferenceEquals(brush,ArtworkPalette.IslandSurface(artwork)),
                "unchanged artwork reuses a frozen island cover");
            var visual=new DrawingVisual();
            using(var context=visual.RenderOpen())context.DrawRectangle(brush,null,new Rect(0,0,360,204));
            var image=new RenderTargetBitmap(360,204,96,96,PixelFormats.Pbgra32);
            image.Render(visual);
            var pixels=new byte[360*204*4];
            image.CopyPixels(pixels,360*4,0);
            int top=(5*360+180)*4,edge=(27*360+180)*4,next=(28*360+180)*4;
            Require(pixels[top]+pixels[top+1]+pixels[top+2]==0,"the original top cap remains black");
            for(int channel=0;channel<3;channel++)
                Require(Math.Abs(pixels[edge+channel]-pixels[next+channel])<8,
                    "the cover fades through the header boundary without a hard seam");
            int left=(145*360+60)*4,right=(145*360+300)*4;
            Require(Math.Abs(pixels[left]-pixels[right])+Math.Abs(pixels[left+2]-pixels[right+2])>25,
                "the expanded surface retains recognizable thumbnail colors");
        }
        internal static void Validate()
        {
            Require(!new PreferenceData().DynamicIsland,"existing installations do not enable an overlay on upgrade");
            var state=new DynamicIslandState();
            var now=DateTime.UtcNow;
            Require(state.Size(false,now)==new Size(DynamicIslandLayout.IdleWidth,DynamicIslandLayout.CompactHeight)&&state.Size(true,now)==new Size(DynamicIslandLayout.MediaWidth,DynamicIslandLayout.CompactHeight),"idle and media shapes stay compact");
            state.Feedback("volume",135,false,now);
            Require(state.Value==100&&state.FeedbackVisible(now)&&!state.FeedbackVisible(now.AddSeconds(2)),"level values and transient lifetime are bounded");
            state.Expanded=true;
            Require(state.Size(true,now).Height==204,"feedback cannot interrupt expanded media controls");
            state.Expanded=false;
            state.NotificationUntil=now.AddSeconds(7);
            Require(state.Size(true,now)==new Size(360,156)&&!state.NotificationVisible(now.AddSeconds(8)),
                "notifications have priority over feedback and a bounded lifetime");
            state.Reset();
            Require(!state.Expanded&&!state.FeedbackVisible(now),"suspend clears transient state");
            foreach(double dpi in new[]{1.0,1.25,1.5,2,2.5})foreach(int edge in new[]{0,1,2,3})
            {
                var screen=new Drawing.Rectangle(-2560,-240,1920,1080);
                var work=Drawing.Rectangle.FromLTRB(screen.Left+(edge==0?88:0),screen.Top+(edge==1?88:0),screen.Right-(edge==2?88:0),screen.Bottom-(edge==3?88:0));
                bool bottom=edge==1;
                var host=DynamicIslandLayout.Host(screen,work,dpi,bottom);
                var physical=Native.PlacementBounds(screen,dpi,host.X,host.Y,host.Width,host.Height);
                Require(physical.Left>=work.Left&&physical.Top>=work.Top&&physical.Right<=work.Right&&physical.Bottom<=work.Bottom,"host respects negative origins, dock edges and mixed DPI");
                var compact=DynamicIslandLayout.Capsule(new Size(DynamicIslandLayout.MediaWidth,DynamicIslandLayout.CompactHeight),host.Width,host.Height,bottom);
                var expanded=DynamicIslandLayout.Capsule(new Size(360,host.Height),host.Width,host.Height,bottom);
                Require(bottom?compact.Bottom==expanded.Bottom&&compact.Top>expanded.Top:compact.Top==0&&expanded.Top==0,"the top dock places the island below, with expansion anchored to the same bottom edge");
                Require(DynamicIslandLayout.Clear(screen,work,dpi,new FullscreenCandidate[0]),"empty desktop permits the capsule");
                var blocking=new FullscreenCandidate {Bounds=new Native.RectI {Left=work.Left,Top=work.Top,Right=work.Right,Bottom=work.Top+100},Monitor=screen};
                Require(DynamicIslandLayout.Clear(screen,work,dpi,new[]{blocking}),"ordinary windows retain a persistent island above their reserved work area");
                var full=new FullscreenCandidate {Bounds=new Native.RectI {Left=screen.Left,Top=screen.Top,Right=screen.Right,Bottom=screen.Bottom},Client=new Native.RectI {Left=screen.Left,Top=screen.Top,Right=screen.Right,Bottom=screen.Bottom},Monitor=screen};
                Require(!DynamicIslandLayout.Clear(screen,work,dpi,new[]{full}),"fullscreen content hides the island");
                full.Bounds.Left=full.Client.Left=screen.Right+100;full.Bounds.Right=full.Client.Right=screen.Right+2020;
                full.Monitor=new Drawing.Rectangle(screen.Right+100,screen.Top,1920,1080);
                Require(DynamicIslandLayout.Clear(screen,work,dpi,new[]{full}),"a secondary fullscreen display leaves the primary desktop alone");
            }
        }
        internal static int Run(bool inspect=false)
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;DynamicIsland view=null;Window source=null;IDisposable motion=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>
            {
                try
                {
                    Program.Inspectable=true;
                    Validate();
                    if(!ReleaseFeatures.GlassUI)
                    {
                        AppLog.Json("dynamic-island-probe.json",new {passed=true,betaOnly=true,stableFeatureDisabled=true});
                        result=0;return;
                    }
                    Preferences.Set(p=>p.UIVersion=95);
                    motion=Motion.WithAnimationsForFixture();
                    source=new Window {Title="Island fixture",Width=300,Height=200,Left=40,Top=300};source.Show();source.Activate();
                    var commands=new List<string>();
                    view=new DynamicIsland(command=>commands.Add(command),value=>Task.FromResult(true),()=>commands.Add("output"),id=>commands.Add("app"));
                    var screen=Forms.Screen.PrimaryScreen;
                    double dpi=Native.PrepareMonitor(view,screen.Bounds);
                    bool safeAreaVerified=await IslandSafetyChecks.Validate(source,screen,dpi);
                    source.Activate();Native.Activate(new WindowInteropHelper(source).Handle);await Task.Delay(120);
                    Require(Native.GetForegroundWindow()==new WindowInteropHelper(source).Handle,"the source fixture owns focus before showing a compact capsule");
                    var media=new MediaState {Available=true,Active=true,Playing=true,Title="Long media title · 아티스트 이름",Artist="테스트 아티스트",CanPlay=true,CanPrevious=true,CanNext=true,CanSeek=true,Duration=200,Position=45,Artwork=CoverFixture()};
                    CheckSurface(media.Artwork);
                    view.Present(media,"헤드폰",screen,dpi);
                    await Task.Delay(330);
                    Require(view.IsVisible&&!view.Animating&&view.Shape.Width==DynamicIslandLayout.MediaWidth&&view.Shape.Height==DynamicIslandLayout.CompactHeight,"media geometry settles without retaining resize callbacks");
                    var handle=new WindowInteropHelper(view).Handle;
                    Require((Native.GetWindowLong(handle,-20)&0x08000000)!=0,"a compact capsule keeps its no-activate window style");
                    Require(Native.GetForegroundWindow()==new WindowInteropHelper(source).Handle,"showing a compact capsule preserves foreground focus");
                    Require(!view.HasBlur,"a solid black island creates no live backdrop renderer");
                    Program.Save(view.Surface,(int)DynamicIslandLayout.MediaWidth,(int)DynamicIslandLayout.CompactHeight,Path.Combine(AppLog.Data,"island-media.png"));
                    view.SetExpanded(true);await Task.Delay(330);
                    Require(view.Expanded&&!view.Animating&&view.Shape.Height==204,"media expands and settles");
                    Require(view.Player.ActualHeight>0&&view.Player.ActualHeight+28<=view.Shape.Height+.5,"transport and timeline fit below the black header");
                    Require(view.Surface.Background==Brushes.Black&&
                        Object.ReferenceEquals(view.MediaSurface,ArtworkPalette.IslandSurface(media.Artwork)),
                        "expanded media shares the cached thumbnail blur under a fading black cap");
                    view.Player.PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(commands.Count==1&&commands[0]=="play","transport uses the shared media command");
                    Program.Save(view.Surface,360,204,Path.Combine(AppLog.Data,"island-expanded.png"));
                    view.SetExpanded(false);await Task.Delay(280);
                    Require(!view.Expanded&&Native.GetForegroundWindow()==new WindowInteropHelper(source).Handle,"collapse returns focus to the previous fixture window");
                    view.ShowFeedback("volume",43,false);await Task.Delay(280);
                    Require(view.FeedbackVisible&&view.ExpiryActive&&view.Shape.Width==286&&view.Shape.Height==88,
                        "volume feedback uses a full-size capsule below the black header");
                    Require(view.LevelSlider.ActualHeight==60&&view.LevelSlider.LinearFill&&view.LevelSlider.NoTrackBorder&&
                        !view.LevelSlider.DarkTrack&&view.LevelSlider.Value==43,
                        "island level feedback shares the quick-settings capsule geometry and value");
                    view.ExpireFeedback();
                    Require(view.FeedbackVisible&&view.ExpiryActive,"an early timer tick retains the expiry wakeup");
                    Program.Save(view.Surface,286,88,Path.Combine(AppLog.Data,"island-volume.png"));
                    var expiryDeadline=DateTime.UtcNow.AddSeconds(3);
                    while((view.FeedbackVisible||view.ExpiryActive||view.Animating)&&DateTime.UtcNow<expiryDeadline)await Task.Delay(25);
                    Require(!view.FeedbackVisible&&!view.ExpiryActive&&!view.Animating&&view.Shape.Width==DynamicIslandLayout.MediaWidth,"feedback expires once and restores media without polling");
                    view.ShowFeedback("brightness",62,false);await Task.Delay(280);
                    Require(view.LevelSlider.Kind=="brightness"&&view.LevelSlider.Value==62,
                        "brightness feedback resets the sound icon and retains the shared capsule style");
                    Program.Save(view.Surface,286,88,Path.Combine(AppLog.Data,"island-brightness.png"));
                    var notice=new NotificationEntry {Id=1,App="테스트 앱",AppId="fixture",Title="새 알림",Body="알림 내용과 원래 알림 열기 동작을 표시합니다.",Icon=media.Artwork};
                    var noticeForeground=Native.GetForegroundWindow();
                    Require(view.ShowNotification(notice,entry=>commands.Add("notification:"+entry.Id)),"accepted notification can use the island");
                    await Task.Delay(280);
                    Require(view.NotificationVisible&&view.NotificationView.Entry==notice&&view.ExpiryActive&&
                        Native.GetForegroundWindow()==noticeForeground,
                        "notification presentation does not steal foreground focus; before="+noticeForeground+"; after="+Native.GetForegroundWindow());
                    Program.Save(view.Surface,360,156,Path.Combine(AppLog.Data,"island-notification.png"));
                    view.NotificationView.OpenButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(!view.NotificationVisible&&view.NotificationView.Entry==null&&commands.Contains("notification:1"),
                        "opening clears retained content and invokes the original entry once");
                    view.ShowNotification(notice,entry=>{});
                    view.RetainNotification(new HashSet<string>());
                    Require(!view.NotificationVisible&&view.NotificationView.Entry==null,
                        "removal from native history releases a visible island notification");
                    for(int i=0;i<6;i++){view.SetExpanded(true);await Task.Delay(25);view.SetExpanded(false);await Task.Delay(25);}
                    await Task.Delay(300);
                    Require(!view.Expanded&&!view.Animating,"rapid reversals settle without a second motion subscription");
                    view.SetExpanded(true);view.ShowFeedback("brightness",62,false);view.Suspend();
                    Require(!view.IsVisible&&!view.Expanded&&!view.Animating&&!view.ExpiryActive&&!MenuDismiss.Active,"hiding releases input observers and motion timers");
                    view.Present(new MediaState(),null,screen,dpi);await Task.Delay(280);
                    Require(view.Shape.Width==DynamicIslandLayout.IdleWidth&&view.Shape.Height==DynamicIslandLayout.CompactHeight,"inactive media returns to the small pill");
                    view.SetExpanded(true);Require(!view.Expanded,"an empty capsule cannot open inactive controls");
                    view.ShowFeedback("charge",85,false);await Task.Delay(280);
                    Program.Save(view.Surface,(int)DynamicIslandLayout.ChargeWidth,(int)DynamicIslandLayout.ChargeHeight,Path.Combine(AppLog.Data,"island-charging.png"));
                    view.Suspend();
                    view.Present(media,"헤드폰",screen,dpi,null,true);
                    await Task.Delay(280);
                    double bottomEdge=view.Shape.Bottom;
                    Require(view.Shape.Top>0,"the lower capsule sits at the bottom of its host");
                    Require(!Native.RegionContains(view,(int)(180*dpi),2)&&Native.RegionContains(view,(int)(180*dpi),(int)((bottomEdge-14)*dpi)),"transparent space above the lower capsule does not intercept application clicks");
                    view.SetExpanded(true);
                    await Task.Delay(70);
                    Require(Math.Abs(view.Shape.Bottom-bottomEdge)<.01,"expansion keeps the lower edge fixed on every frame");
                    await Task.Delay(220);
                    Require(view.Shape.Top==DynamicIslandLayout.MaximumHeight-204&&view.Shape.Height==204,"the lower capsule expands upward into the media card");
                    Program.Save(view.Surface,360,204,Path.Combine(AppLog.Data,"island-bottom-expanded.png"));
                    view.SetExpanded(false);
                    await Task.Delay(70);
                    Require(Math.Abs(view.Shape.Bottom-bottomEdge)<.01,"collapse also keeps the lower edge fixed");
                    await Task.Delay(220);
                    Require(!view.Animating&&view.Shape.Height==DynamicIslandLayout.CompactHeight&&Math.Abs(view.Shape.Bottom-bottomEdge)<.01,"bottom motion settles and releases its rendering callback");
                    if(inspect){AppLog.Json("island-preview.json",new {ready=true});for(int i=0;i<120&&!File.Exists(Path.Combine(AppLog.Data,"preview-done"));i++)await Task.Delay(250);}
                    view.Suspend();
                    AppLog.Json("dynamic-island-probe.json",new {passed=true,disabledByDefault=true,sharedMediaCommands=true,blackHeaderWithArtworkBlur=true,sharedCapsuleLevels=true,acceptedNotificationRouting=true,topDockMovesIslandBelow=true,bottomExpansionAnchored=true,nativeSafeAreaVerified=safeAreaVerified,fullscreenReleasesWorkspace=safeAreaVerified,crashRestoresWorkspace=safeAreaVerified,mixedDpiAndFourEdges=true,compactDoesNotStealFocus=true,rapidReversalsSettle=true,expiryTimerStops=true,hiddenInputAndMotionReleased=true,noLiveBackdrop=true,deviceValuesChanged=false});
                    result=0;
                }
                catch(Exception error){AppLog.Json("dynamic-island-probe.json",new {passed=false,error=error.ToString(),island=view==null?null:view.Inspect()});}
                finally{if(view!=null)view.Close();if(source!=null)source.Close();if(motion!=null)motion.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);
            app.Run();return result;
        }
    }
}
