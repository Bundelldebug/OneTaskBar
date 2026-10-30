using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

namespace OneTaskBar
{
    internal static class DynamicIslandChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Dynamic Island: "+message);}
        internal static void Validate()
        {
            Require(!new PreferenceData().DynamicIsland,"existing installations do not enable an overlay on upgrade");
            var state=new DynamicIslandState();
            var now=DateTime.UtcNow;
            Require(state.Size(false,now)==new Size(56,18)&&state.Size(true,now)==new Size(112,28),"idle and media shapes stay compact");
            state.Feedback("volume",135,false,now);
            Require(state.Value==100&&state.FeedbackVisible(now)&&!state.FeedbackVisible(now.AddSeconds(2)),"level values and transient lifetime are bounded");
            state.Expanded=true;
            Require(state.Size(true,now).Height==184,"feedback cannot interrupt expanded media controls");
            state.Reset();
            Require(!state.Expanded&&!state.FeedbackVisible(now),"suspend clears transient state");
            foreach(double dpi in new[]{1.0,1.25,1.5,2,2.5})foreach(int edge in new[]{0,1,2,3})
            {
                var screen=new Drawing.Rectangle(-2560,-240,1920,1080);
                var work=Drawing.Rectangle.FromLTRB(screen.Left+(edge==0?88:0),screen.Top+(edge==1?88:0),screen.Right-(edge==2?88:0),screen.Bottom-(edge==3?88:0));
                var host=DynamicIslandLayout.Host(screen,work,dpi);
                var physical=Native.PlacementBounds(screen,dpi,host.X,host.Y,host.Width,host.Height);
                Require(physical.Left>=work.Left&&physical.Top>=work.Top&&physical.Right<=work.Right&&physical.Bottom<=work.Bottom,"host respects negative origins, dock edges and mixed DPI");
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
                    var media=new MediaState {Available=true,Active=true,Playing=true,Title="Long media title · 아티스트 이름",Artist="테스트 아티스트",CanPlay=true,CanPrevious=true,CanNext=true,CanSeek=true,Duration=200,Position=45,Artwork=ArtworkPalette.Sample(Colors.Coral)};
                    view.Present(media,"헤드폰",screen,dpi);
                    await Task.Delay(330);
                    Require(view.IsVisible&&!view.Animating&&view.Shape.Width==112&&view.Shape.Height==28,"media geometry settles without retaining resize callbacks");
                    var handle=new WindowInteropHelper(view).Handle;
                    Require((Native.GetWindowLong(handle,-20)&0x08000000)!=0,"a compact capsule keeps its no-activate window style");
                    Require(Native.GetForegroundWindow()==new WindowInteropHelper(source).Handle,"showing a compact capsule preserves foreground focus");
                    Require(!view.HasBlur,"a solid black island creates no live backdrop renderer");
                    Program.Save(view.Surface,112,28,Path.Combine(AppLog.Data,"island-media.png"));
                    view.SetExpanded(true);await Task.Delay(330);
                    Require(view.Expanded&&!view.Animating&&view.Shape.Height==184,"media expands and settles");
                    Require(view.Player.ActualHeight>0&&view.Player.ActualHeight+20<=view.Shape.Height+.5,"transport and timeline fit inside the capsule");
                    view.Player.PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(commands.Count==1&&commands[0]=="play","transport uses the shared media command");
                    Program.Save(view.Surface,360,184,Path.Combine(AppLog.Data,"island-expanded.png"));
                    view.SetExpanded(false);await Task.Delay(280);
                    Require(!view.Expanded&&Native.GetForegroundWindow()==new WindowInteropHelper(source).Handle,"collapse returns focus to the previous fixture window");
                    view.ShowFeedback("volume",43,false);await Task.Delay(280);
                    Require(view.FeedbackVisible&&view.ExpiryActive&&view.Shape.Width==240,"volume feedback replaces the compact media row");
                    Program.Save(view.Surface,240,28,Path.Combine(AppLog.Data,"island-volume.png"));
                    await Task.Delay(1700);
                    Require(!view.FeedbackVisible&&!view.ExpiryActive&&!view.Animating&&view.Shape.Width==112,"feedback expires once and restores media without polling");
                    for(int i=0;i<6;i++){view.SetExpanded(true);await Task.Delay(25);view.SetExpanded(false);await Task.Delay(25);}
                    await Task.Delay(300);
                    Require(!view.Expanded&&!view.Animating,"rapid reversals settle without a second motion subscription");
                    view.SetExpanded(true);view.ShowFeedback("brightness",62,false);view.Suspend();
                    Require(!view.IsVisible&&!view.Expanded&&!view.Animating&&!view.ExpiryActive&&!MenuDismiss.Active,"hiding releases input observers and motion timers");
                    view.Present(new MediaState(),null,screen,dpi);await Task.Delay(280);
                    Require(view.Shape.Width==56&&view.Shape.Height==18,"inactive media returns to the small pill");
                    view.SetExpanded(true);Require(!view.Expanded,"an empty capsule cannot open inactive controls");
                    view.ShowFeedback("charge",85,false);await Task.Delay(280);
                    Program.Save(view.Surface,240,28,Path.Combine(AppLog.Data,"island-charging.png"));
                    if(inspect){AppLog.Json("island-preview.json",new {ready=true});for(int i=0;i<120&&!File.Exists(Path.Combine(AppLog.Data,"preview-done"));i++)await Task.Delay(250);}
                    view.Suspend();
                    AppLog.Json("dynamic-island-probe.json",new {passed=true,betaOnly=true,disabledByDefault=true,sharedMediaCommands=true,persistentTopSafeArea=true,nativeSafeAreaVerified=safeAreaVerified,fullscreenReleasesWorkspace=safeAreaVerified,crashRestoresWorkspace=safeAreaVerified,mixedDpiAndFourEdges=true,compactDoesNotStealFocus=true,rapidReversalsSettle=true,expiryTimerStops=true,hiddenInputAndMotionReleased=true,noLiveBackdrop=true,deviceValuesChanged=false});
                    result=0;
                }
                catch(Exception error){AppLog.Json("dynamic-island-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(view!=null)view.Close();if(source!=null)source.Close();if(motion!=null)motion.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);
            app.Run();return result;
        }
    }
}
