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
        readonly EventCallback shownCallback;
        readonly System.Collections.Generic.HashSet<IntPtr> before=new System.Collections.Generic.HashSet<IntPtr>();
        IntPtr[] shownHooks;
        delegate bool WindowCallback(IntPtr window,IntPtr parameter);
        [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback,IntPtr parameter);
        DispatcherTimer launchTimer;
        AppGhost ghost;
        bool disposed;
        internal int Played {get;private set;}
        internal int Heard {get;private set;}

        internal AppWindowMotion(ShellController owner)
        {
            shell=owner;
            callback=OnEvent;
            // Minimize start, from any process; the restore is caught as it is asked for, before the window shows.
            hook=SetWinEventHook(0x16,0x16,IntPtr.Zero,callback,0,0,2);
            shownCallback=OnShown;
            WindowActivation.Restoring+=OnRestoring;
            // The first drop would otherwise pay for building a full-screen window, a visible delay after the click.
            var dispatcher=Application.Current==null?null:Application.Current.Dispatcher;
            if(dispatcher!=null)dispatcher.BeginInvoke(new Action(Warm),DispatcherPriority.ApplicationIdle);
        }

        void Warm()
        {
            try
            {
                if(disposed||!Wanted||ghost!=null)return;
                ghost=new AppGhost();
                ghost.Warm();
            }
            catch(Exception error){AppLog.Once("app-window-warm",error.GetType().Name);}
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
            Heard++;
            if(disposed)return;
            if(!Wanted){Note("skipped: motion off");return;}
            if(from==null||MediaWindow(window)){Note("minimize skipped: "+(from==null?"no bounds":"media window"));return;}
            var icon=DockHost.IconRect(window);
            if(icon==null){Note("minimize skipped: no dock icon");return;}
            Play(from.Value,icon.Value,true);
        }

        void OnRestoring(IntPtr window)
        {
            if(disposed)return;
            if(!Wanted){Note("skipped: motion off");return;}
            if(MediaWindow(window)){Note("restore skipped: media window");return;}
            var icon=DockHost.IconRect(window);
            var to=RestoredBounds(window);
            if(icon==null||to==null){Note("restore skipped: "+(icon==null?"no dock icon":"no bounds"));return;}
            Play(icon.Value,to.Value,false,RestoreTime);
        }

        // Why a motion did not play, once per reason, so a real session can be read afterwards.
        static void Note(string reason){AppLog.Once("app-motion-"+reason,reason);}

        // An app started from its dock icon: a drop rises out of the icon and waits above it while the app starts, then
        // spreads over the window the moment that window appears. Where the window opens is not guessed.
        // Returns whether the motion took over; the caller then starts the app off the interface thread, because a slow
        // start blocking it is what made the drop arrive late.
        internal bool Launching(AppEntry app)
        {
            if(disposed||!Wanted||app==null)return false;
            var icon=DockHost.IconRect(IntPtr.Zero,app);
            if(icon==null)return false;
            var r=icon.Value;
            var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(r.X+r.Width/2),(int)(r.Y+r.Height/2))).Bounds;
            double w=r.Width*4.4,h=r.Width*3.6;
            double x=Math.Max(screen.X+16,Math.Min(screen.Right-w-16,r.X+r.Width/2-w/2));
            var hover=new Rect(x,Math.Max(screen.Y+16,r.Y-h-r.Width*.3),w,h);
            try
            {
                if(ghost==null||ghost.IsClosed)ghost=new AppGhost();
                ghost.Launch(r,hover);
                Played++;
                WatchForWindow();
            }
            catch(Exception error){AppLog.Once("app-window-launch",error.GetType().Name);return false;}
            return true;
        }

        // While an app starts, the first suitable window to be shown is the one the drop spreads over.
        // A window can appear three ways: shown (0x8002), uncloaked (0x8018, how packaged apps surface their frame) or made
        // the foreground window (0x0003). Windows that were already on screen when the app was started are not candidates.
        void WatchForWindow()
        {
            StopWatching();
            before.Clear();
            EnumWindows((w,l)=>{if(Ordinary(w))before.Add(w);return true;},IntPtr.Zero);
            shownHooks=new[]
            {
                SetWinEventHook(0x8002,0x8002,IntPtr.Zero,shownCallback,0,0,2),
                SetWinEventHook(0x8018,0x8018,IntPtr.Zero,shownCallback,0,0,2),
                SetWinEventHook(0x0003,0x0003,IntPtr.Zero,shownCallback,0,0,2)
            };
            launchTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(4)};
            launchTimer.Tick+=(s,e)=>StopWatching();
            launchTimer.Start();
        }

        void StopWatching()
        {
            if(shownHooks!=null){foreach(var one in shownHooks)if(one!=IntPtr.Zero)UnhookWinEvent(one);shownHooks=null;}
            if(launchTimer!=null){launchTimer.Stop();launchTimer=null;}
        }

        void OnShown(IntPtr h,uint id,IntPtr window,int objectId,int child,uint thread,uint time)
        {
            if(disposed||ghost==null||objectId!=0||child!=0||before.Contains(window)||!Ordinary(window))return;
            var bounds=WindowBounds(window);
            if(bounds==null||bounds.Value.Width<240||bounds.Value.Height<160)return;
            StopWatching();
            ghost.Arrive(bounds.Value);
        }

        // A top-level window a person would call an app window: visible, not a tool window or tooltip, not cloaked.
        static bool Ordinary(IntPtr window)
        {
            if(window==IntPtr.Zero||!Native.IsWindowVisible(window)||Native.IsIconic(window))return false;
            if(Native.GetAncestor(window,2)!=window)return false;
            int style=Native.GetWindowLong(window,-20);
            if((style&0x80)!=0||(style&0x08000000)!=0)return false;
            int cloaked;
            if(Native.DwmGetWindowAttribute(window,14,out cloaked,4)==0&&cloaked!=0)return false;
            return true;
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

        // Windows draws a restored window within a few frames, so the restore drop is quick and only leads it out of the icon.
        const double RestoreTime=.32;
        void Play(Rect from,Rect to,bool closing,double time=0)
        {
            try
            {
                if(ghost==null||ghost.IsClosed)ghost=new AppGhost();
                ghost.Run(from,to,closing,time);
                Played++;
            }
            catch(Exception error){AppLog.Once("app-window-motion",error.GetType().Name);}
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            WindowActivation.Restoring-=OnRestoring;
            StopWatching();
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
        readonly Border drop=new Border(),outline=new Border();
        readonly Stopwatch watch=new Stopwatch();
        // Normal: a timed drop. Launch: Rise out of the icon, Hold above it while the app starts, Arrive over the window.
        enum Phase {Normal,Rise,Hold,Arrive}
        Phase phase;
        readonly Stopwatch holdWatch=new Stopwatch();
        Rect from,to,host,shown;
        Rect? pending;
        double scale=1;
        bool closing,hooked,cloaked;
        double duration;
        int framesUntilShown;
        internal bool IsClosed {get;private set;}
        // Frames drawn uncloaked, across all runs; live-state reports it so a real session shows the drop was on screen.
        internal static int Frames;
        // The close matches the system's own minimize, which shrinks the real window in about a quarter of a second; the
        // longer drop left the glass shrinking after the window had gone.
        const double OpenTime=.5,CloseTime=.2,RiseTime=.26,ArriveTime=.36,HoldLimit=2.6;

        internal AppGhost()
        {
            Title="One TaskBar · 앱 전환";
            WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=false;Background=Brushes.Transparent;
            ShowActivated=false;ShowInTaskbar=false;Topmost=true;IsHitTestVisible=false;Focusable=false;
            drop.BorderThickness=new Thickness(1.5);
            outline.BorderThickness=new Thickness(1);
            outline.BorderBrush=edge;
            outline.Child=drop;
            canvas.Children.Add(outline);
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

        // Builds the window once, cloaked, so the first real drop does not pay for it.
        internal void Warm()
        {
            var handle=new WindowInteropHelper(this).EnsureHandle();
            int cloak=1;DwmSetWindowAttribute(handle,13,ref cloak,4);cloaked=true;
            Width=8;Height=8;Left=0;Top=0;
            Show();
            Dispatcher.BeginInvoke(new Action(()=>{if(!hooked)Hide();}),DispatcherPriority.Background);
        }

        // A drop that rises out of the icon to the hover rectangle and waits there for the app's window.
        internal void Launch(Rect icon,Rect hover)
        {
            Run(icon,hover,false,RiseTime);
            phase=Phase.Rise;
            pending=null;
        }

        // The app's window has appeared: the drop spreads over it and fades.
        internal void Arrive(Rect window)
        {
            if(phase==Phase.Hold)StartArrive(window);
            else if(phase==Phase.Rise)pending=window;
        }

        void StartArrive(Rect window)
        {
            from=shown;to=window;closing=false;duration=ArriveTime;phase=Phase.Arrive;pending=null;
            watch.Restart();
        }

        internal void Run(Rect start,Rect end,bool close,double time)
        {
            phase=Phase.Normal;pending=null;
            from=start;to=end;closing=close;duration=time>0?time:close?CloseTime:OpenTime;
            var center=new System.Drawing.Point((int)((start.X+end.X)/2+start.Width/4),(int)((start.Y+end.Y)/2+start.Height/4));
            var monitor=System.Windows.Forms.Screen.FromPoint(center).Bounds;
            host=new Rect(monitor.X,monitor.Y,monitor.Width,monitor.Height);
            var handle=new WindowInteropHelper(this).EnsureHandle();
            scale=Native.PrepareMonitor(this,monitor);
            Width=host.Width/scale;Height=host.Height/scale;
            // Glass, not paint: almost clear, a highlight across the top, the glow only where it pools at the bottom and a
            // lit rim. A tinted fill would read as a colored card laid over the window instead of glass.
            drop.Background=GlassFill(Glass95.Dark);
            drop.BorderBrush=Rim();
            outline.Opacity=0;
            if(!cloaked){int cloak=1;DwmSetWindowAttribute(handle,13,ref cloak,4);cloaked=true;}
            if(!IsVisible)Show();
            Native.SetWindowPos(handle,new IntPtr(-1),(int)host.X,(int)host.Y,(int)host.Width,(int)host.Height,0x10|0x40);
            framesUntilShown=1;
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
            Frames++;
            if(phase==Phase.Hold)
            {
                // Nothing came up in time (the app has no window of its own): the drop settles away where it is.
                if(holdWatch.Elapsed.TotalSeconds>HoldLimit){StartArrive(new Rect(shown.X-shown.Width*.04,shown.Y-shown.Height*.04,shown.Width*1.08,shown.Height*1.08));return;}
                Place(1);
                return;
            }
            double t=watch.Elapsed.TotalSeconds/duration;
            if(t>=1)
            {
                if(phase==Phase.Rise)
                {
                    phase=Phase.Hold;holdWatch.Restart();Place(1);
                    if(pending.HasValue)StartArrive(pending.Value);
                    return;
                }
                Unhook();outline.Opacity=0;phase=Phase.Normal;Hide();
                return;
            }
            Place(t);
        }

        static double Smooth(double a,double b,double x){double t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));return t*t*(3-2*t);}

        static Brush fill;static int fillKey;static bool fillDark;
        static Brush GlassFill(bool dark)
        {
            int key=Fluid.StyleKey;
            if(fill!=null&&fillKey==key&&fillDark==dark)return fill;
            var brush=new LinearGradientBrush {StartPoint=new Point(.5,0),EndPoint=new Point(.5,1)};
            // Strong enough to read over a white page as well as a dark one: a bright sheen, a frosted body and the glow pooled
            // at the bottom. The real test showed the earlier, nearly clear fill vanishing over bright windows.
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(dark?120:150),255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(dark?56:72),230,236,246),.3));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(dark?48:64),214,222,236),.72));
            brush.GradientStops.Add(new GradientStop(Fluid.GlowOn?Fluid.Glow(dark?150:130):Color.FromArgb(90,236,240,248),1));
            brush.Freeze();
            fill=brush;fillKey=key;fillDark=dark;
            return brush;
        }

        // A lit rim: bright along the top where light catches the glass, fading down the sides.
        static Brush rim;
        static Brush Rim()
        {
            if(rim!=null)return rim;
            var brush=new LinearGradientBrush {StartPoint=new Point(.5,0),EndPoint=new Point(.5,1)};
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(235,255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(120,255,255,255),.4));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(170,255,255,255),1));
            brush.Freeze();
            return rim=brush;
        }
        // A thin dark edge outside the rim keeps the shape visible over white pages.
        static readonly Brush edge=Frozen(Color.FromArgb(70,0,0,0));
        static Brush Frozen(Color color){var brush=new SolidColorBrush(color);brush.Freeze();return brush;}

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
            // Near the icon (the start when opening, the end when closing) it keeps the icon's own rounded square, so the
            // icon is what becomes the window; near the window it takes the window's corners.
            double nearIcon=closing?Smooth(.55,1,t):1-Smooth(0,.4,t);
            double radius=Math.Min(w,h)*.28*nearIcon+12*scale*(1-nearIcon);
            if(phase==Phase.Rise){nearIcon=1-Smooth(0,.7,t);radius=Math.Min(w,h)*.28*nearIcon+22*scale*(1-nearIcon);}
            else if(phase==Phase.Hold)radius=22*scale;
            else if(phase==Phase.Arrive)radius=(22-10*Smooth(0,1,t))*scale;
            shown=new Rect(x,y,w,h);
            Canvas.SetLeft(outline,(x-host.X)/scale-1);Canvas.SetTop(outline,(y-host.Y)/scale-1);
            outline.Width=w/scale+2;outline.Height=h/scale+2;
            double corner=Math.Min(radius,Math.Min(w,h)/2)/scale;
            drop.CornerRadius=new CornerRadius(corner);
            outline.CornerRadius=new CornerRadius(corner+1);
            // Opening, the drop leads the window out of the icon and is gone before the window settles, so it never washes
            // over the restored window; closing, it covers the shrinking window and stays bright until it reaches the icon.
            if(phase==Phase.Rise)outline.Opacity=Smooth(0,.25,t);
            else if(phase==Phase.Hold)outline.Opacity=.88+.1*Math.Sin(holdWatch.Elapsed.TotalSeconds*6);
            else if(phase==Phase.Arrive)outline.Opacity=1-Smooth(.4,.95,t);
            else outline.Opacity=closing?.55+.45*Smooth(0,.3,t)-Smooth(.86,1,t):Smooth(0,.08,t)*(1-Smooth(.22,.48,t));
        }

        void Unhook(){if(hooked){hooked=false;CompositionTarget.Rendering-=Frame;}}
    }
}
