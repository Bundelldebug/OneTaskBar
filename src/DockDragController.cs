using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    // Capture is limited to the active gesture. There is no full-screen input
    // window, permanent hook, or idle pointer polling timer.
    internal sealed class DockDragController : IDisposable
    {
        static DockDragController current;internal static bool Active {get{return current!=null;}}
        readonly DockHost dock;readonly ShellController shell;readonly FrameworkElement canvas;
        readonly Action<string,Point> previewOrder;readonly Func<string,Point,bool> dropOrder;readonly Action<string> restoreOrder;
        Button source;AppEntry entry;DragReorder.DragGhost ghost;WindowDropPreview preview;WindowDropPlan plan;IntPtr partner,captureWindow;
        HwndSource hotkeySource;HwndSourceHook hotkey;bool registered,disposed,ending;double sourceOpacity;
        string scaleMonitor;double cachedScale;
        internal bool WindowPreviewVisible {get{return preview!=null&&preview.Visible;}}
        internal bool WindowPreviewBlur {get{return preview!=null&&preview.BlurAvailable;}}
        internal WindowDropPlan Plan {get{return plan;}}
        internal DockDragController(DockHost owner,ShellController controller,FrameworkElement target,Action<string,Point> reorder,Func<string,Point,bool> drop,Action<string> restore){dock=owner;shell=controller;canvas=target;previewOrder=reorder;dropOrder=drop;restoreOrder=restore;Preferences.Changed+=OptionsChanged;}
        internal void Attach(Button button,Func<AppEntry> item)
        {
            bool armed=false;System.Drawing.Point origin=new System.Drawing.Point();Point grab=new Point();IntPtr beforeFocus=IntPtr.Zero;
            button.PreviewMouseLeftButtonDown+=(s,e)=>{if(e.Handled||disposed||DragReorder.Active)return;origin=Forms.Cursor.Position;grab=e.GetPosition(button);beforeFocus=dock.WindowDragPartner;armed=true;};
            button.PreviewMouseMove+=(s,e)=>{
                if(disposed||ending)return;
                if(current==this&&source==button){if(e.LeftButton!=MouseButtonState.Pressed){End(false);return;}Move();e.Handled=true;return;}
                if(!armed||DragReorder.Active||e.LeftButton!=MouseButtonState.Pressed)return;var point=Forms.Cursor.Position;
                if(Math.Abs(point.X-origin.X)<SystemParameters.MinimumHorizontalDragDistance*dock.Scale&&Math.Abs(point.Y-origin.Y)<SystemParameters.MinimumVerticalDragDistance*dock.Scale)return;
                armed=false;e.Handled=true;Start(button,item(),grab,beforeFocus);Move();
            };
            button.PreviewMouseLeftButtonUp+=(s,e)=>{armed=false;if(current==this&&source==button){e.Handled=true;End(true);}};
            button.LostMouseCapture+=(s,e)=>{armed=false;if(current==this&&source==button&&!ending)End(false);};
            button.Unloaded+=(s,e)=>{armed=false;if(current==this&&source==button)End(false);};
        }
        void Start(Button button,AppEntry app,Point grab,IntPtr beforeFocus)
        {
            if(app==null||disposed)return;source=button;entry=app;sourceOpacity=button.Opacity;partner=beforeFocus;current=this;shell.Previews.Close();if(Program.Inspectable)AppLog.Write("drop-start","partner="+partner+" source="+app.Key);
            try{ghost=new DragReorder.DragGhost(button,grab);ghost.Show();ghost.Follow();button.Opacity=.28;var owner=Window.GetWindow(button);captureWindow=owner==null?IntPtr.Zero:new WindowInteropHelper(owner).Handle;if(captureWindow!=IntPtr.Zero)Native.SetForegroundWindow(captureWindow);if(!Mouse.Capture(button,CaptureMode.Element)){End(false);return;}DragReorder.PointerStarted();InstallEscape();}
            catch(Exception error){AppLog.Write("dock-drag",error.Message);End(false);}
        }
        void InstallEscape()
        {
            hotkeySource=HwndSource.FromHwnd(new WindowInteropHelper(dock.Apps).Handle);if(hotkeySource==null)return;
            hotkey=(IntPtr h,int msg,IntPtr wp,IntPtr lp,ref bool handled)=>{if(msg==0x312&&wp.ToInt32()==0x6174&&current==this){handled=true;End(false);}return IntPtr.Zero;};hotkeySource.AddHook(hotkey);registered=Native.RegisterHotKey(hotkeySource.Handle,0x6174,0x4000,0x1B);
        }
        void Move()
        {
            if(current!=this||source==null)return;ghost.Follow();var cursor=Forms.Cursor.Position;var point=canvas.PointFromScreen(new Point(cursor.X,cursor.Y));
            bool inDock=point.X>=-12&&point.X<=canvas.ActualWidth+12&&point.Y>=-12&&point.Y<=canvas.ActualHeight+18;
            if(inDock){plan=null;if(preview!=null)preview.Hide();previewOrder(entry.Key,point);return;}
            restoreOrder(entry.Key);if(!Preferences.Current.DockWindowDrop){plan=null;if(preview!=null)preview.Hide();return;}
            var screen=Forms.Screen.FromPoint(cursor);if(scaleMonitor!=screen.DeviceName){scaleMonitor=screen.DeviceName;cachedScale=Native.DropMonitorScale(screen.Bounds);}double scale=cachedScale;var previous=plan!=null&&plan.Work==screen.WorkingArea?plan.Kind:WindowDropKind.None;plan=WindowDropPlan.At(screen.WorkingArea,cursor,scale,previous);
            if(preview==null)preview=new WindowDropPreview();preview.Update(plan,entry,screen);
        }
        void End(bool drop)
        {
            if(current!=this||ending)return;if(drop)Move();ending=true;bool committed=false;var selected=entry;var selectedPlan=plan;var selectedPartner=partner;
            try{
                if(drop&&selected!=null){var cursor=Forms.Cursor.Position;var point=canvas.PointFromScreen(new Point(cursor.X,cursor.Y));bool inDock=point.X>=-12&&point.X<=canvas.ActualWidth+12&&point.Y>=-12&&point.Y<=canvas.ActualHeight+18;
                    if(inDock)committed=dropOrder(selected.Key,point);else committed=Preferences.Current.DockWindowDrop&&selectedPlan!=null&&selectedPlan.Valid;}
            }catch(Exception error){AppLog.Write("dock-drop",error.Message);}
            finally{
                current=null;if(hotkeySource!=null){if(registered)Native.UnregisterHotKey(hotkeySource.Handle,0x6174);if(hotkey!=null)hotkeySource.RemoveHook(hotkey);}hotkeySource=null;hotkey=null;registered=false;
                if(source!=null){source.Opacity=sourceOpacity;if(Mouse.Captured==source)Mouse.Capture(null);}if(ghost!=null)ghost.Close();if(preview!=null){preview.Hide();preview.Dispose();}if((!committed||selectedPlan==null||!selectedPlan.Valid)&&Native.GetForegroundWindow()==captureWindow&&Native.IsWindow(selectedPartner))Native.Activate(selectedPartner);captureWindow=IntPtr.Zero;ghost=null;preview=null;source=null;entry=null;plan=null;ending=false;DragReorder.PointerFinished(committed);
            }
            if(committed)dock.Overflow.Hide();else dock.Overflow.AfterDrag();if(committed&&selectedPlan!=null&&selectedPlan.Valid&&Preferences.Current.DockWindowDrop)shell.Placement.Open(selected,selectedPlan,selectedPartner);
        }
        void OptionsChanged(){if(!Preferences.Current.DockWindowDrop&&current==this&&plan!=null)End(false);}
        public void Dispose(){if(disposed)return;disposed=true;Preferences.Changed-=OptionsChanged;if(current==this)End(false);}
    }
    internal static partial class Native
    {
        [System.Runtime.InteropServices.DllImport("shcore.dll")]static extern int GetDpiForMonitor(IntPtr monitor,int kind,out uint x,out uint y);
        internal static double DropMonitorScale(System.Drawing.Rectangle screen){try{uint x,y;if(GetDpiForMonitor(MonitorFromPoint(new PointI {X=screen.Left+screen.Width/2,Y=screen.Top+screen.Height/2},2),0,out x,out y)==0&&x>0)return x/96.0;}catch{}return 1;}
    }
}
