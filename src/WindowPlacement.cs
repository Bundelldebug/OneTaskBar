using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal sealed class WindowPlacement : IDisposable
    {
        readonly ShellController shell;CancellationTokenSource pending;bool disposed;
        internal int Placements {get;private set;}internal bool Busy {get{return pending!=null;}}
        internal WindowPlacement(ShellController owner){shell=owner;Preferences.Changed+=OptionsChanged;}
        void OptionsChanged(){if(!Preferences.Current.DockWindowDrop&&pending!=null)pending.Cancel();}
        internal async void Open(AppEntry entry,WindowDropPlan plan,IntPtr partner)
        {
            if(disposed||!plan.Valid)return;if(pending!=null)pending.Cancel();var source=new CancellationTokenSource();pending=source;
            try{
                IntPtr target=entry.Windows.FirstOrDefault(h=>h==partner&&Native.IsWindow(h));if(target==IntPtr.Zero)target=entry.Windows.FirstOrDefault(Native.IsWindow);if(target==IntPtr.Zero){var launch=entry.Copy();launch.Windows.Clear();shell.LaunchApp(launch,System.Windows.Input.ModifierKeys.None);var wait=Stopwatch.StartNew();while(wait.ElapsedMilliseconds<6500&&target==IntPtr.Zero){await Task.Delay(100,source.Token);target=shell.Running.Where(a=>Catalog.Same(a,entry)).SelectMany(a=>a.Windows).FirstOrDefault(Native.IsWindow);}}
                source.Token.ThrowIfCancellationRequested();if(target==IntPtr.Zero){ActionNotice.Show("배치할 앱 창을 찾지 못했습니다. 앱에서 새 창을 열어 다시 시도해 주세요.");return;}
                if(!Native.CanArrangeWindow(target)){Native.Activate(target);ActionNotice.Show("이 앱은 창 크기 변경을 지원하지 않습니다.");return;}
                shell.RecentApps.Record(entry);bool other=(plan.Kind==WindowDropKind.Left||plan.Kind==WindowDropKind.Right)&&partner!=target&&Native.CanArrangeWindow(partner)&&shell.Running.Any(a=>a.Windows.Contains(partner));
                if(other){var monitor=System.Windows.Forms.Screen.FromHandle(partner);other=monitor.WorkingArea.IntersectsWith(plan.Work);}
                await Task.WhenAll(Arrange(target,plan.Bounds,source.Token),other?Arrange(partner,plan.OtherHalf,source.Token):Task.FromResult(true));source.Token.ThrowIfCancellationRequested();Native.Activate(target);Placements++;
            }catch(OperationCanceledException){}catch(Exception error){AppLog.Write("window-drop",error.Message);if(!disposed)ActionNotice.Show("창을 배치하지 못했습니다. 앱의 크기 제한이나 권한을 확인해 주세요.");}
            finally{if(pending==source)pending=null;source.Dispose();}
        }
        internal static async Task Arrange(IntPtr window,System.Drawing.Rectangle visible,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();var target=Native.OuterWindowBounds(window,visible);Native.RectI initial;if(!Native.GetWindowRect(window,out initial))throw new InvalidOperationException("Window has closed");
            if(Native.IsIconic(window)||Native.ArrangeZoomed(window)){Native.RestoreAt(window,target);await Task.Delay(35,token);target=Native.OuterWindowBounds(window,visible);if(!Native.GetWindowRect(window,out initial))throw new InvalidOperationException("Window has closed");}
            if(Motion.Enabled){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<180){token.ThrowIfCancellationRequested();double t=Math.Min(1,watch.Elapsed.TotalMilliseconds/180),ease=1-Math.Pow(1-t,3);Native.ArrangeWindow(window,(int)Math.Round(initial.Left+(target.Left-initial.Left)*ease),(int)Math.Round(initial.Top+(target.Top-initial.Top)*ease),(int)Math.Round((initial.Right-initial.Left)+(target.Width-initial.Right+initial.Left)*ease),(int)Math.Round((initial.Bottom-initial.Top)+(target.Height-initial.Bottom+initial.Top)*ease));await Task.Delay(16,token);}}
            token.ThrowIfCancellationRequested();if(!Native.ArrangeWindow(window,target.Left,target.Top,target.Width,target.Height))throw new System.ComponentModel.Win32Exception();
            await Task.Delay(35,token);var corrected=Native.OuterWindowBounds(window,visible);if(corrected!=target&&!Native.ArrangeWindow(window,corrected.Left,corrected.Top,corrected.Width,corrected.Height))throw new System.ComponentModel.Win32Exception();
        }
        public void Dispose(){disposed=true;Preferences.Changed-=OptionsChanged;if(pending!=null)pending.Cancel();}
    }
    internal static partial class Native
    {
        [StructLayout(LayoutKind.Sequential)]struct ArrangePlacement {internal int Length,Flags,Show;internal PointI Min,Max;internal RectI Normal;}
        [DllImport("user32.dll",SetLastError=true)]static extern bool SetWindowPlacement(IntPtr window,ref ArrangePlacement placement);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]static extern int ArrangeFrame(IntPtr window,int attribute,out RectI bounds,int size);
        internal static bool ArrangeZoomed(IntPtr window){return IsZoomed(window);}
        internal static bool CanArrangeWindow(IntPtr window){return window!=IntPtr.Zero&&IsWindow(window)&&(GetWindowLong(window,-16)&0x40000)!=0;}
        internal static RectI VisibleArrangeBounds(IntPtr window){RectI frame;if(ArrangeFrame(window,9,out frame,16)==0)return frame;GetWindowRect(window,out frame);return frame;}
        internal static System.Drawing.Rectangle OuterWindowBounds(IntPtr window,System.Drawing.Rectangle visible){RectI outer,frame;if(GetWindowRect(window,out outer)&&!IsIconic(window)&&!IsZoomed(window)&&ArrangeFrame(window,9,out frame,16)==0)return new System.Drawing.Rectangle(visible.Left-(frame.Left-outer.Left),visible.Top-(frame.Top-outer.Top),visible.Width+(frame.Left-outer.Left)+(outer.Right-frame.Right),visible.Height+(frame.Top-outer.Top)+(outer.Bottom-frame.Bottom));return visible;}
        internal static void RestoreAt(IntPtr window,System.Drawing.Rectangle target){var screen=System.Windows.Forms.Screen.FromRectangle(target);int dx=screen.WorkingArea.Left-screen.Bounds.Left,dy=screen.WorkingArea.Top-screen.Bounds.Top;var placement=new ArrangePlacement {Length=Marshal.SizeOf(typeof(ArrangePlacement)),Flags=4,Show=1,Normal=new RectI {Left=target.Left-dx,Top=target.Top-dy,Right=target.Right-dx,Bottom=target.Bottom-dy}};SetWindowPlacement(window,ref placement);}
        internal static bool ArrangeWindow(IntPtr window,int x,int y,int width,int height){return SetWindowPos(window,IntPtr.Zero,x,y,Math.Max(1,width),Math.Max(1,height),0x4000|0x4|0x10);}
    }
}
