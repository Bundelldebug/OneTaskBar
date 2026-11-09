using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    // The Fluid UI's motion for ordinary apps: when a window is minimized a glass drop leaves it and falls into the app's
    // dock icon, and when it comes back (or the app starts from the dock) a drop rises out of the icon and swells to where
    // the window appears. The window itself is left alone; this is a light overlay that shows where it went and where it
    // comes from. The playing media app keeps its own flight into the now bar or the island.
    internal sealed class AppWindowMotion : IDisposable
    {
        delegate void EventCallback(IntPtr hook,uint id,IntPtr window,int objectId,int child,uint thread,uint time);
        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,EventCallback callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
        [StructLayout(LayoutKind.Sequential)] struct Placement {public int Length,Flags,ShowCmd;public Native.PointI Min,Max;public Native.RectI Normal;}
        [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr window,ref Placement placement);

        readonly ShellController shell;
        readonly EventCallback callback;
        readonly IntPtr hook;
        AppGhost ghost;
        bool disposed;
        internal int Played {get;private set;}

        internal AppWindowMotion(ShellController owner)
        {
            shell=owner;
            callback=OnEvent;
            // Minimize start, from any process; the restore is caught as it is asked for, before the window shows.
            hook=SetWinEventHook(0x16,0x16,IntPtr.Zero,callback,0,0,2);
            WindowActivation.Restoring+=OnRestoring;
        }

        static bool Wanted {get{return Fluid.Enabled&&Motion.Enabled;}}

        // The media app's window flies into the now bar or the island instead.
        bool MediaWindow(IntPtr window)
        {
            if(!Preferences.Current.MediaWindowAnimation||!shell.Services.Media.Active)return false;
            var app=MediaApplication.Resolve(shell.Services.Media.AppId,shell.Running.Concat(shell.Apps));
            return app!=null&&app.Windows.Contains(window);
        }

        void OnEvent(IntPtr h,uint id,IntPtr window,int objectId,int child,uint thread,uint time)
        {
            if(disposed||objectId!=0||child!=0)return;
            var dispatcher=Application.Current==null?null:Application.Current.Dispatcher;
            if(dispatcher==null)return;
            // The rectangle is read now, while the window is still where it was.
            var from=WindowBounds(window);
            dispatcher.BeginInvoke(new Action(()=>Minimizing(window,from)),DispatcherPriority.Send);
        }

        void Minimizing(IntPtr window,Rect? from)
        {
            if(disposed||!Wanted||from==null||MediaWindow(window))return;
            var icon=DockHost.IconRect(window);
            if(icon==null)return;
            Play(from.Value,icon.Value,true);
        }

        void OnRestoring(IntPtr window)
        {
            if(disposed||!Wanted||MediaWindow(window))return;
            var icon=DockHost.IconRect(window);
            var to=RestoredBounds(window);
            if(icon==null||to==null)return;
            Play(icon.Value,to.Value,false);
        }

        // An app started from its dock icon: the drop rises toward the middle of the screen, where it usually opens.
        internal void Launching(AppEntry app)
        {
            if(disposed||!Wanted||app==null)return;
            var icon=DockHost.IconRect(IntPtr.Zero,app);
            if(icon==null)return;
            var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)icon.Value.X,(int)icon.Value.Y)).WorkingArea;
            var to=new Rect(screen.X+screen.Width*.2,screen.Y+screen.Height*.16,screen.Width*.6,screen.Height*.62);
            Play(icon.Value,to,false);
        }

        static Rect? WindowBounds(IntPtr window)
        {
            Native.RectI rect;
            if(!Native.GetWindowRect(window,out rect)||rect.Right-rect.Left<40||rect.Bottom-rect.Top<40||rect.Left<=-30000)return RestoredBounds(window);
            return new Rect(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
        }

        // Where a minimized window will appear: its monitor's work area when it was maximized, its normal place otherwise.
        static Rect? RestoredBounds(IntPtr window)
        {
            var placement=new Placement {Length=Marshal.SizeOf(typeof(Placement))};
            if(!GetWindowPlacement(window,ref placement))return null;
            var normal=placement.Normal;
            bool maximized=(placement.Flags&2)!=0||placement.ShowCmd==3;
            var screen=System.Windows.Forms.Screen.FromHandle(window);
            if(maximized){var work=screen.WorkingArea;return new Rect(work.X,work.Y,work.Width,work.Height);}
            if(normal.Right-normal.Left<40||normal.Bottom-normal.Top<40)return null;
            // The normal place is in workspace coordinates; with no taskbar reserving space they are screen coordinates.
            var offset=new Point(screen.WorkingArea.X-screen.Bounds.X,screen.WorkingArea.Y-screen.Bounds.Y);
            return new Rect(normal.Left+offset.X,normal.Top+offset.Y,normal.Right-normal.Left,normal.Bottom-normal.Top);
        }

        void Play(Rect from,Rect to,bool closing)
        {
            try
            {
                if(ghost==null||ghost.IsClosed)ghost=new AppGhost();
                ghost.Run(from,to,closing);
                Played++;
            }
            catch(Exception error){AppLog.Once("app-window-motion",error.GetType().Name);}
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            WindowActivation.Restoring-=OnRestoring;
            if(hook!=IntPtr.Zero)UnhookWinEvent(hook);
            if(ghost!=null)ghost.Close();
            ghost=null;
        }
    }

    // A click-through overlay over one monitor that draws the glass drop. It is cloaked until it has drawn, so it never
    // shows a blank frame, and it hides again when the drop has finished.
    internal sealed class AppGhost : Window
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        readonly Canvas canvas=new Canvas();
        readonly Border drop=new Border();
        readonly Stopwatch watch=new Stopwatch();
        Rect from,to,host;
        double scale=1;
        bool closing,hooked,cloaked;
        int framesUntilShown;
        internal bool IsClosed {get;private set;}
        const double OpenTime=.5,CloseTime=.44;

        internal AppGhost()
        {
            Title="One TaskBar · 앱 전환";
            WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=false;Background=Brushes.Transparent;
            ShowActivated=false;ShowInTaskbar=false;Topmost=true;IsHitTestVisible=false;Focusable=false;
            drop.BorderThickness=new Thickness(1.2);
            canvas.Children.Add(drop);
            Content=canvas;
            SourceInitialized+=(s,e)=>
            {
                var handle=new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x080000A0)&~0x40000);
                int disabled=1;DwmSetWindowAttribute(handle,3,ref disabled,4);
                Native.ClearPreviewBackground(this);
            };
            Closed+=(s,e)=>{IsClosed=true;Unhook();};
        }

        internal void Run(Rect start,Rect end,bool close)
        {
            from=start;to=end;closing=close;
            var center=new System.Drawing.Point((int)((start.X+end.X)/2+start.Width/4),(int)((start.Y+end.Y)/2+start.Height/4));
            var monitor=System.Windows.Forms.Screen.FromPoint(center).Bounds;
            host=new Rect(monitor.X,monitor.Y,monitor.Width,monitor.Height);
            var handle=new WindowInteropHelper(this).EnsureHandle();
            scale=Native.PrepareMonitor(this,monitor);
            Width=host.Width/scale;Height=host.Height/scale;
            // The drop is the Fluid glass: thin tint with the glow and a lit rim.
            bool dark=Glass95.Dark;
            drop.Background=Fluid.Tint(dark);
            drop.BorderBrush=Fluid.Rim();
            drop.Opacity=0;
            if(!cloaked){int cloak=1;DwmSetWindowAttribute(handle,13,ref cloak,4);cloaked=true;}
            if(!IsVisible)Show();
            Native.SetWindowPos(handle,new IntPtr(-1),(int)host.X,(int)host.Y,(int)host.Width,(int)host.Height,0x10|0x40);
            framesUntilShown=2;
            Place(0);
            watch.Restart();
            if(!hooked){hooked=true;CompositionTarget.Rendering+=Frame;}
        }

        void Frame(object sender,EventArgs e)
        {
            if(cloaked)
            {
                if(--framesUntilShown>0){watch.Restart();return;}
                var handle=new WindowInteropHelper(this).Handle;
                int cloak=0;DwmSetWindowAttribute(handle,13,ref cloak,4);cloaked=false;
                watch.Restart();
            }
            double t=watch.Elapsed.TotalSeconds/(closing?CloseTime:OpenTime);
            if(t>=1){Unhook();drop.Opacity=0;Hide();return;}
            Place(t);
        }

        static double Smooth(double a,double b,double x){double t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));return t*t*(3-2*t);}

        // The drop's outline at a moment: it keeps a round, drop-like shape near the icon and the window's corners near
        // the window. Opening, the width leads like the panels' arrival; closing, it eases in and drops into the icon.
        void Place(double t)
        {
            double p,across,down;
            if(closing)
            {
                p=MediaFlightTiming.Smooth(t);
                across=down=p;
            }
            else
            {
                p=SpringEase.Position(t,.84);
                across=SpringEase.Position(Math.Min(1,t*1.25),.86);
                down=p;
            }
            double x=from.X+(to.X-from.X)*across,w=from.Width+(to.Width-from.Width)*across;
            double y=from.Y+(to.Y-from.Y)*down,h=from.Height+(to.Height-from.Height)*down;
            w=Math.Max(4,w);h=Math.Max(4,h);
            // Near the icon (the start when opening, the end when closing) the corners round fully into a drop.
            double nearIcon=closing?Smooth(.55,1,t):1-Smooth(0,.4,t);
            double radius=Math.Min(w,h)/2*nearIcon+14*scale*(1-nearIcon);
            Canvas.SetLeft(drop,(x-host.X)/scale);Canvas.SetTop(drop,(y-host.Y)/scale);
            drop.Width=w/scale;drop.Height=h/scale;
            drop.CornerRadius=new CornerRadius(Math.Min(radius,Math.Min(w,h)/2)/scale);
            // Opening, the drop is clear at first, brightest as it swells and fades as the window takes its place;
            // closing, it starts faint over the window and is brightest as it reaches the icon.
            drop.Opacity=closing?.25+.65*Smooth(0,.4,t)-.9*Smooth(.82,1,t):Smooth(0,.12,t)*.9*(1-Smooth(.62,1,t));
        }

        void Unhook(){if(hooked){hooked=false;CompositionTarget.Rendering-=Frame;}}
    }
}
