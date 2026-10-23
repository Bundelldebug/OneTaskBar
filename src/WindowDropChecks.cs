using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class WindowDropChecks
    {
        [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Native.PointI point);
        [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
        [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
        static bool Near(Native.RectI actual,System.Drawing.Rectangle expected){return Math.Abs(actual.Left-expected.Left)<=4&&Math.Abs(actual.Top-expected.Top)<=4&&Math.Abs(actual.Right-expected.Right)<=4&&Math.Abs(actual.Bottom-expected.Bottom)<=4;}
        sealed class GestureResult {internal bool Started,Preview,ClickThrough,Cancelled,Blur;internal WindowDropKind Kind;internal double BeforeEnergy,AfterEnergy,AfterMean;}
        static double[] Texture(System.Drawing.Rectangle sample)
        {
            using(var bitmap=new System.Drawing.Bitmap(sample.Width,sample.Height)){
                using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen(sample.Location,System.Drawing.Point.Empty,sample.Size);
                double energy=0,mean=0;for(int y=0;y<sample.Height;y++)for(int x=0;x<sample.Width;x++){var pixel=bitmap.GetPixel(x,y);double value=(pixel.R+pixel.G+pixel.B)/3.0;mean+=value;if(x>0){var previous=bitmap.GetPixel(x-1,y);energy+=Math.Abs(value-(previous.R+previous.G+previous.B)/3.0);}if(y>0){var previous=bitmap.GetPixel(x,y-1);energy+=Math.Abs(value-(previous.R+previous.G+previous.B)/3.0);}}
                return new[]{energy/(sample.Width*sample.Height*2),mean/(sample.Width*sample.Height)};
            }
        }
        static async Task<GestureResult> Drag(DockHost dock,Button button,System.Drawing.Point destination,IntPtr behind,bool escape=false)
        {
            var result=new GestureResult();var handle=new WindowInteropHelper(dock.Apps).Handle;var origin=button.PointToScreen(new Point(12,21));var cursor=System.Windows.Forms.Cursor.Position;var dispatcher=button.Dispatcher;var sourceBounds=Native.VisibleArrangeBounds(behind);var sample=new System.Drawing.Rectangle(sourceBounds.Left+80,sourceBounds.Top+120,64,64);
            await Task.Run(()=>{bool down=false;try{
                Native.SetWindowPos(handle,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10);if(Native.GetAncestor(WindowFromPoint(new Native.PointI {X=(int)origin.X,Y=(int)origin.Y}),2)!=handle)return;
                if(Native.GetAncestor(WindowFromPoint(new Native.PointI {X=sample.Left+32,Y=sample.Top+32}),2)==behind)result.BeforeEnergy=Texture(sample)[0];
                SetCursorPos((int)origin.X,(int)origin.Y);mouse_event(2,0,0,0,UIntPtr.Zero);down=true;for(int i=1;i<=25;i++){double t=i/25.0;SetCursorPos((int)Math.Round(origin.X+(destination.X-origin.X)*t),(int)Math.Round(origin.Y+(destination.Y-origin.Y)*t));System.Threading.Thread.Sleep(15);}System.Threading.Thread.Sleep(220);
                dispatcher.Invoke(new Action(()=>{result.Started=DockDragController.Active;result.Preview=dock.DragApps.WindowPreviewVisible;result.Blur=dock.DragApps.WindowPreviewBlur;result.Kind=dock.DragApps.Plan==null?WindowDropKind.None:dock.DragApps.Plan.Kind;var bounds=Native.VisibleArrangeBounds(behind);var point=new Native.PointI {X=(bounds.Left+bounds.Right)/2,Y=(bounds.Top+bounds.Bottom)/2};var hit=Native.GetAncestor(WindowFromPoint(point),2);result.ClickThrough=hit==behind;uint hitPid;Native.GetWindowThreadProcessId(hit,out hitPid);var title=new System.Text.StringBuilder(200);if(hitPid==System.Diagnostics.Process.GetCurrentProcess().Id)Native.GetWindowText(hit,title,200);AppLog.Write("drop-hit","expected="+behind+" actual="+hit+" pid="+hitPid+" own="+System.Diagnostics.Process.GetCurrentProcess().Id+" title="+title+" style="+Native.GetWindowLong(hit,-16)+" point="+point.X+","+point.Y);}));
                if(result.ClickThrough&&result.Kind==WindowDropKind.Left){var texture=Texture(sample);result.AfterEnergy=texture[0];result.AfterMean=texture[1];}
                if(escape&&result.Started){keybd_event(0x1B,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(90);keybd_event(0x1B,0,2,UIntPtr.Zero);System.Threading.Thread.Sleep(100);dispatcher.Invoke(new Action(()=>result.Cancelled=!DockDragController.Active));}
            }finally{if(down){mouse_event(4,0,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(130);}SetCursorPos(cursor.X,cursor.Y);}});await Task.Delay(330);return result;
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;DockHost dock=null;var windows=new List<Window>();int result=1;bool old=Preferences.Current.DockWindowDrop;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    Program.Inspectable=true;Preferences.Current.DockWindowDrop=true;shell=new ShellController(true);shell.Desktop=true;var screen=System.Windows.Forms.Screen.PrimaryScreen;double scale=Native.DropMonitorScale(screen.Bounds);var work=screen.WorkingArea;
                    var textureDrawing=new DrawingGroup();textureDrawing.Children.Add(new GeometryDrawing(Brushes.White,null,new RectangleGeometry(new Rect(0,0,16,16))));textureDrawing.Children.Add(new GeometryDrawing(Brushes.Black,null,new RectangleGeometry(new Rect(0,0,8,8))));textureDrawing.Children.Add(new GeometryDrawing(Brushes.Black,null,new RectangleGeometry(new Rect(8,8,8,8))));textureDrawing.Freeze();var textureBrush=new DrawingBrush(textureDrawing){TileMode=TileMode.Tile,Viewport=new Rect(0,0,16,16),ViewportUnits=BrushMappingMode.Absolute};textureBrush.Freeze();
                    Func<string,int,Window> fixture=(name,x)=>{var window=new Window {Title=name,ShowInTaskbar=false,Topmost=true,Width=300,Height=260,MinWidth=120,MinHeight=100,Background=textureBrush,ResizeMode=ResizeMode.CanResize};windows.Add(window);window.Show();Native.Place(window,screen.Bounds,x,220,300,260);return window;};
                    var first=fixture("One TaskBar · 드래그 테스트 A",210);var second=fixture("One TaskBar · 드래그 테스트 B",600);var h1=new WindowInteropHelper(first).Handle;var h2=new WindowInteropHelper(second).Handle;var source=new AppEntry {Name="테스트 앱 A",AppID="fixture.window.a",Icon=ArtworkPalette.Sample(Colors.CornflowerBlue),StartedUtcTicks=1};source.Windows.Add(h1);var partner=new AppEntry {Name="테스트 앱 B",AppID="fixture.window.b",Icon=ArtworkPalette.Sample(Colors.Coral),StartedUtcTicks=2};partner.Windows.Add(h2);shell.Running=new List<AppEntry>{source,partner};shell.LastApplication=h2;dock=new DockHost(shell,screen);dock.Show();await Task.Delay(400);Native.Place(dock.Apps,screen.Bounds,30,100,dock.Apps.Width,44);await Task.Delay(140);int baseline=LiveBackdrop.InstanceCount;
                    Native.Activate(h1);Native.Activate(h2);await Task.Delay(100);var leftPoint=new System.Drawing.Point(work.Left+20,work.Top+work.Height/2);var left=await Drag(dock,dock.AppButton(source.Key),leftPoint,h1);var leftPlan=WindowDropPlan.At(work,leftPoint,scale);var leftActual=Native.VisibleArrangeBounds(h1);var partnerActual=Native.VisibleArrangeBounds(h2);bool leftPlaced=Near(leftActual,leftPlan.Bounds)&&Near(partnerActual,leftPlan.OtherHalf);AppLog.Json("drop-left-bounds.json",new {actual=leftActual,other=partnerActual,expected=leftPlan.Bounds,expectedOther=leftPlan.OtherHalf});bool cleanLeft=LiveBackdrop.InstanceCount==baseline&&LiveBackdrop.OrphanCount==0;
                    Native.Activate(h2);var centerPoint=new System.Drawing.Point(work.Left+work.Width/2,work.Top+work.Height/2);var popup=await Drag(dock,dock.AppButton(source.Key),centerPoint,h1);var popupPlan=WindowDropPlan.At(work,centerPoint,scale);bool popupPlaced=Near(Native.VisibleArrangeBounds(h1),popupPlan.Bounds);
                    Native.Activate(h2);var rightPoint=new System.Drawing.Point(work.Right-20,work.Top+work.Height/2);var right=await Drag(dock,dock.AppButton(source.Key),rightPoint,h1);var rightPlan=WindowDropPlan.At(work,rightPoint,scale);bool rightPlaced=Near(Native.VisibleArrangeBounds(h1),rightPlan.Bounds)&&Near(Native.VisibleArrangeBounds(h2),rightPlan.OtherHalf);
                    first.WindowState=WindowState.Maximized;await Task.Delay(240);Native.Activate(h2);await Drag(dock,dock.AppButton(source.Key),centerPoint,h1);bool maximized=first.WindowState==WindowState.Normal&&Near(Native.VisibleArrangeBounds(h1),popupPlan.Bounds);first.WindowState=WindowState.Minimized;await Task.Delay(120);Native.Activate(h2);await Drag(dock,dock.AppButton(source.Key),rightPoint,h2);bool minimized=first.WindowState==WindowState.Normal&&Near(Native.VisibleArrangeBounds(h1),rightPlan.Bounds);
                    int placements=shell.Placement.Placements;var before=Native.VisibleArrangeBounds(h1);Native.Activate(h2);var cancelled=await Drag(dock,dock.AppButton(source.Key),centerPoint,h1,true);bool escape=cancelled.Cancelled&&shell.Placement.Placements==placements&&Native.VisibleArrangeBounds(h1).Equals(before)&&!DragReorder.Active;
                    Preferences.Current.DockWindowDrop=false;var disabled=await Drag(dock,dock.AppButton(source.Key),centerPoint,h1);bool off=disabled.Started&&!disabled.Preview&&disabled.Kind==WindowDropKind.None&&shell.Placement.Placements==placements;Preferences.Current.DockWindowDrop=true;
                    int elevated=0,ordinary=0;shell.ElevationDispatch=e=>elevated++;shell.LaunchDispatch=e=>ordinary++;shell.LaunchApp(source,ModifierKeys.Control|ModifierKeys.Shift);bool admin=elevated==1&&ordinary==0;shell.ElevationDispatch=null;
                    var staleWindow=fixture("One TaskBar · 닫힌 창 테스트",300);var stale=new WindowInteropHelper(staleWindow).Handle;staleWindow.Close();Window fresh=null;bool freshLaunch=false;var unopened=new AppEntry {Name="新規テスト",AppID="fixture.window.new",Icon=source.Icon,StartedUtcTicks=3};unopened.Windows.Add(stale);shell.Running.Add(unopened);dock.Rebuild();await Task.Delay(380);Native.Place(dock.Apps,screen.Bounds,30,100,dock.Apps.Width,44);shell.LaunchDispatch=async e=>{freshLaunch=e.Windows.Count==0;ordinary++;await Task.Delay(170);fresh=fixture("One TaskBar · 새 창 테스트",300);unopened.Windows.Add(new WindowInteropHelper(fresh).Handle);};var newDrag=await Drag(dock,dock.AppButton(unopened.Key),centerPoint,h1);await Task.Delay(350);bool launched=freshLaunch&&ordinary==1&&fresh!=null&&Near(Native.VisibleArrangeBounds(new WindowInteropHelper(fresh).Handle),popupPlan.Bounds)&&!shell.Placement.Busy;
                    bool clean=LiveBackdrop.InstanceCount==baseline&&LiveBackdrop.OrphanCount==0&&!Application.Current.Windows.OfType<TransientGlass>().Any(w=>w.IsVisible)&&!DragReorder.Active;bool keyReleased=Native.RegisterHotKey(new WindowInteropHelper(dock.Apps).Handle,0x6175,0x4000,0x1B);if(keyReleased)Native.UnregisterHotKey(new WindowInteropHelper(dock.Apps).Handle,0x6175);
                    bool visibleBlur=left.BeforeEnergy>3&&left.AfterEnergy<left.BeforeEnergy*.3&&left.AfterMean>65&&left.AfterMean<180;
                    bool passed=left.Started&&left.Preview&&left.Blur&&visibleBlur&&left.Kind==WindowDropKind.Left&&left.ClickThrough&&leftPlaced&&cleanLeft&&popup.Kind==WindowDropKind.Popup&&popupPlaced&&right.Kind==WindowDropKind.Right&&rightPlaced&&maximized&&minimized&&escape&&off&&admin&&launched&&clean&&keyReleased;result=passed?0:1;
                    AppLog.Json("window-drop-probe.json",new {passed=passed,actualLeftSplit=leftPlaced,actualRightSplit=rightPlaced,actualPopup=popupPlaced,maximizedWindowRestoredAndPlaced=maximized,minimizedWindowRestoredAndPlaced=minimized,leftPreview=left.Preview,liveBlurAvailable=left.Blur,checkerboardActuallyBlurred=visibleBlur,textureBefore=left.BeforeEnergy,textureAfter=left.AfterEnergy,blurMean=left.AfterMean,leftKind=left.Kind.ToString(),clickThroughPreviewAndBlur=left.ClickThrough,previewAndBlurReleased=cleanLeft&&clean,escapeCancelsWithoutMoving=escape,settingDisablesWindowDrop=off,ctrlShiftRoutesToElevation=admin,delayedNewWindowPositioned=launched,closedWindowHandleRelaunched=freshLaunch,escapeRegistrationReleased=keyReleased,placements=shell.Placement.Placements,applicationsLaunched=false,userWindowsMoved=false,fixtureWindows=windows.Count});
                }catch(Exception error){AppLog.Json("window-drop-probe.json",new {passed=false,error=error.ToString()});}
                finally{Preferences.Current.DockWindowDrop=old;if(dock!=null)dock.Close();if(shell!=null)shell.Dispose();foreach(var window in windows)window.Close();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
