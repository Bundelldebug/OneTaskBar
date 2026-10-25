using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class Glass95Checks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("UI 9.5: "+message);}
        static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
        {
            var own=root as T;if(own!=null)yield return own;foreach(object value in LogicalTreeHelper.GetChildren(root)){var child=value as DependencyObject;if(child!=null)foreach(var result in Children<T>(child))yield return result;}
        }
        internal static void Validate(List<string> checks)
        {
            foreach(bool vertical in new[]{false,true})foreach(double cross in new[]{36.0,60,80})foreach(int span in new[]{1,2,3,4,5}){
                double width=vertical?cross:cross*span,height=vertical?cross*span:cross;
                double low=PillSlider.PositionValue(width,height,new Point(-100,-100),vertical,false),high=PillSlider.PositionValue(width,height,new Point(width+100,height+100),vertical,false);
                Require(vertical?low==100&&high==0:low==0&&high==100,"slider input reaches both bounded endpoints");
            }
            var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){dc.DrawEllipse(Brushes.White,null,new Point(24,24),16,16);}var bitmap=new RenderTargetBitmap(48,48,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);bitmap.Freeze();
            var native=AppIcons.Visual(bitmap,30) as Image;Require(native!=null&&Object.ReferenceEquals(native.Source,bitmap)&&native.Width==30&&native.Height==30,"white transparent icons retain their exact source with no plate or inset");
            checks.Add("9.5 retains capsule slider endpoints in either orientation and native white/transparent app icons receive no added plate");
        }
        internal static int Run(bool inspect=false)
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1,oldUI=Preferences.Current.UIVersion;bool oldBlur=Preferences.Current.Blur,oldAnimations=Preferences.Current.Animations;ShellController shell=null;DockHost dock=null;QuickWindow quick=null;Window scene=null;WelcomeWindow settings=null;HudWindow hud=null;StartWindow start=null;SearchWindow search=null;IDisposable palette=null,motion=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;Directory.CreateDirectory(AppLog.Data);Validate(new List<string>());motion=Motion.WithAnimationsForFixture();Preferences.Current.Animations=true;Preferences.Current.Blur=true;Preferences.Current.UIVersion=95;shell=new ShellController(true);
                var icon=ArtworkPalette.Sample(Colors.CornflowerBlue);shell.Pins=Enumerable.Range(1,3).Select(i=>new AppEntry {Name="고정 앱 "+i,Target="fixture:pin:"+i,Pinned=true,Icon=icon}).ToList();shell.Running=new List<AppEntry>();
                dock=new DockHost(shell,System.Windows.Forms.Screen.PrimaryScreen);dock.Show();await Task.Delay(100);Require(!dock.PinDividerVisible,"a pinned-only dock has no trailing divider");
                shell.Running.Add(new AppEntry {Name="실행 앱",Target="fixture:running",Icon=icon});dock.Rebuild();await Task.Delay(100);Require(dock.PinDividerVisible,"an unpinned running group shows its boundary");shell.Running.Clear();dock.Rebuild();await Task.Delay(100);Require(!dock.PinDividerVisible,"the last unpinned app removes the divider");dock.Close();dock=null;
                var sceneBody=new Grid {Background=new LinearGradientBrush(Colors.SteelBlue,Colors.Plum,35)};var color=new Border {Background=Brushes.Teal,Width=160,Height=420,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(40,0,0,0),RenderTransform=new TranslateTransform()};sceneBody.Children.Add(color);scene=new Window {Title="One TaskBar 9.5 · 재질 검사 배경",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,ShowActivated=false,Width=850,Height=820,Left=20,Top=20,Content=sceneBody};scene.Show();
                var screen=System.Windows.Forms.Screen.PrimaryScreen;var reports=new List<object>();
                foreach(bool dark in new[]{false,true}){
                    palette=Glass95.WithDarkForFixture(dark);quick=new QuickWindow(shell);shell.Quick=quick;quick.ShowAt(screen.Bounds,30,30,380,780);quick.Refresh(shell.Power,64,35,false,true,true);await Task.Delay(120);
                    Require(quick.InterfaceVersion==95&&quick.Modern&&quick.GlassMaterial&&Native.RealtimeBackdrop(quick),"9.5 uses a real host backdrop and the modern layout");Require(quick.Surface.BorderBrush is LinearGradientBrush&&quick.Surface.BorderThickness.Left<1.1,"edge lighting remains thin");
                    var before=quick.GlassHighlight;quick.MoveGlassHighlight(new Point(quick.Width*.9,quick.Height*.1));Require(quick.GlassHighlight!=before,"pointer movement updates the specular rim");before=quick.GlassHighlight;Preferences.Current.Animations=false;quick.MoveGlassHighlight(new Point(0,quick.Height));Require(quick.GlassHighlight==before,"reduced motion stops decorative movement");Preferences.Current.Animations=true;
                    var toggle=quick.FindToggle("dnd");toggle.RefreshState(true,true);await Task.Delay(225);for(int wait=0;wait<40&&((SolidColorBrush)toggle.Background).Color!=((SolidColorBrush)Glass95.Selected).Color;wait++)await Task.Delay(25);Require(((SolidColorBrush)toggle.Background).Color==((SolidColorBrush)Glass95.Selected).Color&&((Glyph)toggle.Content).Color==Paint.OnLight,"selected circle has a white fill and dark readable icon");toggle.RefreshState(false,true);Require(((Glyph)toggle.Content).Color==Glass95.Text,"inactive circle follows the foreground palette");
                    var board=quick.GridUI.Board;foreach(int columns in new[]{1,2,3,4})foreach(int rows in new[]{1,2,3,4,5}){board.Layout.Resize("wifi",columns,rows,false);board.Reflow(false);quick.UpdateLayout();var frame=board.Frames["wifi"];Require(board.DisplayLayout.Fits()&&frame.Body.ActualWidth>0&&frame.Body.ActualHeight>0,"all 20 glass tile sizes fit");if(columns==1&&rows==1)Require(Math.Abs(frame.Body.ActualWidth-frame.Body.ActualHeight)<1&&frame.Body.CornerRadius.TopLeft>=frame.Body.ActualWidth/2-.5,"one cell is a circle");}
                    board.Layout.Reset(false);board.Reflow(false);quick.UpdateLayout();Program.Save(quick.Surface,380,780,Path.Combine(AppLog.Data,dark?"glass95-dark.png":"glass95-light.png"));
                    var initialSource=Native.RealtimeBackdrop(quick);color.Background=Brushes.OrangeRed;((TranslateTransform)color.RenderTransform).X=140;await Task.Delay(80);Require(initialSource&&Native.RealtimeBackdrop(quick),"changing content behind the panel retains a live compositor source");
                    Preferences.Set(p=>p.Blur=false);Require(Native.BlurOpacity(quick)==0&&quick.Surface.Background==Glass95.Solid,"blur off switches to the opaque fallback");Preferences.Set(p=>p.Blur=true);Require(Native.BlurOpacity(quick)>.99,"blur can be restored without recreating the window");
                    hud=new HudWindow();hud.Set("volume",35,false);hud.ShowAt(screen.Bounds,440,30,286,hud.DisplayHeight);await Task.Delay(60);Require(hud.GlassMaterial&&hud.Slider.GlassStandalone&&Native.RealtimeBackdrop(hud),"keyboard HUD shares the material without doubled outer edges");Program.Save(hud.Surface,286,60,Path.Combine(AppLog.Data,dark?"glass95-hud-dark.png":"glass95-hud-light.png"));hud.Hide();await Task.Delay(220);Require(!hud.IsVisible&&Native.BlurOpacity(hud)>.99,"HUD fade completes and resets its next-show state");hud.Close();hud=null;
                    settings=new WelcomeWindow(shell);settings.ShowAt(screen.Bounds,440,110,700,650);await Task.Delay(60);Require(!settings.HasBlur&&!settings.GlassMaterial&&settings.VersionPicker.Items.Count==3,"settings expose 8.5, 9.0 and 9.5");settings.ReleaseIdle();settings=null;
                    reports.Add(new {dark=dark,liveBackdrop=true,twentyTileSizes=true,originalCapsuleSliders=true,pointerRim=true,opaqueFallback=true});
                    if(inspect&&dark){quick.Title="One TaskBar 9.5 · 글래스 미리보기";quick.ShowInTaskbar=true;var previewHandle=new WindowInteropHelper(quick).Handle;Native.SetWindowLong(previewHandle,-20,(Native.GetWindowLong(previewHandle,-20)&~0x80)|0x40000);AppLog.Json("glass95-preview.json",new {ready=true,pid=System.Diagnostics.Process.GetCurrentProcess().Id,data=AppLog.Data});var done=Path.Combine(AppLog.Data,"glass95-preview-done");for(int i=0;i<480&&!File.Exists(done);i++)await Task.Delay(250);}
                    quick.Close();quick=null;shell.Quick=null;palette.Dispose();palette=null;
                }
                Preferences.Current.UIVersion=95;start=new StartWindow(shell);start.ShowAt(screen.Bounds,30,30,940,700);await Task.Delay(60);Require(start.GlassMaterial&&Native.RealtimeBackdrop(start),"Start inherits the 9.5 live material");start.Close();start=null;search=new SearchWindow(shell);search.ShowAt(screen.Bounds,30,30,700,620);await Task.Delay(60);Require(search.GlassMaterial&&Native.RealtimeBackdrop(search),"Search inherits the 9.5 live material");search.Close();search=null;
                foreach(int ui in new[]{90,85}){Preferences.Current.UIVersion=ui;quick=new QuickWindow(shell);shell.Quick=quick;quick.ShowAt(screen.Bounds,30,30,380,780);await Task.Delay(60);Require(!quick.GlassMaterial&&quick.Modern==(ui==90),"previous UI versions retain their material and layout");quick.Close();quick=null;shell.Quick=null;}
                Require(LiveBackdrop.OrphanCount==0,"no orphan glass hosts remain");AppLog.Json("glass95-probe.json",new {passed=true,palettes=reports,singleCellCircle=true,pinnedOnlyDividerHidden=true,unpinnedGroupDivider=true,nativeIconsUnplated=true,legacyVersionsPreserved=true,startAndSearchMaterial=true,osThemeChanged=false,osInputSent=false,publicDistribution=false});result=0;
            }catch(Exception error){AppLog.Json("glass95-probe.json",new {passed=false,error=error.ToString()});}finally{
                if(quick!=null)quick.Close();if(hud!=null)hud.Close();if(settings!=null)settings.ReleaseIdle();if(start!=null)start.Close();if(search!=null)search.Close();if(dock!=null)dock.Close();if(scene!=null)scene.Close();if(palette!=null)palette.Dispose();if(motion!=null)motion.Dispose();if(shell!=null)shell.Dispose();Preferences.Current.UIVersion=oldUI;Preferences.Current.Blur=oldBlur;Preferences.Current.Animations=oldAnimations;app.Shutdown(result);
            }}),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
