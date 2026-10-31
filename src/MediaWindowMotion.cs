using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal sealed class MediaWindowMotion : IDisposable
    {
        delegate void EventCallback(IntPtr hook,uint id,IntPtr window,int objectId,int child,uint thread,uint time);
        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,EventCallback callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]
        static extern bool UnhookWinEvent(IntPtr hook);

        readonly EventCallback callback;
        readonly IntPtr hook;
        readonly ShellController shell;
        readonly Dictionary<IntPtr,Rect> normal=new Dictionary<IntPtr,Rect>();
        readonly MediaWindowLease lease=new MediaWindowLease();
        SwitchThumbnail preview;
        MediaWindowPlane plane,prepared;
        BitmapSource snapshot,preparedImage;
        IntPtr snapshotWindow,proactiveRestore,observedForeground;
        DateTime proactiveRestoreUntil;
        Rect snapshotBounds,preparedBounds,preparedCapsule,from,to;
        TimeSpan? started,lastFrame;
        bool capturing,minimizeHover,disposed,placing,restore,finishing;
        int captureGeneration;

        internal bool Active {get{return preview!=null||plane!=null;}}
        internal int FrameCount {get;private set;}
        internal double FirstProgress {get;private set;}
        internal double MaximumFrameGap {get;private set;}
        internal IntPtr PreviewHandle
        {
            get
            {
                if(plane!=null)return new WindowInteropHelper(plane).Handle;
                return preview==null?IntPtr.Zero:new WindowInteropHelper(preview).Handle;
            }
        }

        internal MediaWindowMotion(ShellController controller)
        {
            shell=controller;
            callback=OnEvent;
            hook=SetWinEventHook(0x16,0x17,IntPtr.Zero,callback,0,0,2);
            if(shell!=null)WindowActivation.Restoring+=PrepareRestore;
        }

        internal void Observe(IntPtr foreground)
        {
            bool resumed=observedForeground!=foreground;
            observedForeground=foreground;
            if(disposed||!Motion.Enabled||!ReleaseFeatures.GlassUI||!Preferences.Current.MediaWindowAnimation)
            {
                Retire();
                return;
            }
            // Media-session refreshes may briefly report inactive as the app
            // minimizes. Let a committed flight finish; a refresh must not
            // uncloak its source halfway through the handoff.
            if(Active)return;
            if(shell==null||!shell.Services.Media.Active||shell.FullscreenMediaVisible)
            {
                Retire();
                return;
            }
            var app=MediaApplication.Resolve(shell.Services.Media.AppId,shell.Running.Concat(shell.Apps));
            if(app==null)
            {
                Retire();
                return;
            }
            if(snapshotWindow!=IntPtr.Zero&&!app.Windows.Contains(snapshotWindow))Retire();
            if(!app.Windows.Contains(foreground)||Native.IsIconic(foreground))return;
            Rect capsule;
            IntPtr capsuleOwner;
            if(!Capsule(foreground,out capsule,out capsuleOwner))
            {
                Retire();
                return;
            }
            Native.RectI rect;
            if(!Native.GetWindowRect(foreground,out rect)||rect.Right<=rect.Left||rect.Bottom<=rect.Top)return;
            if(normal.Count>8)
            {
                Retire();
            }
            var bounds=new Rect(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
            if(snapshotWindow==foreground&&snapshotBounds!=bounds)lease.Dispose();
            normal[foreground]=bounds;
            var cursor=System.Windows.Forms.Cursor.Position;
            double dpi=Math.Max(96,Native.GetDpiForWindow(foreground))/96.0;
            double caption=46*dpi;
            bool hover=cursor.X>=rect.Right-3*caption&&cursor.X<rect.Right-2*caption&&cursor.Y>=rect.Top&&cursor.Y<rect.Top+40*dpi;
            if(resumed||snapshotWindow!=foreground||snapshotBounds!=bounds||hover&&!minimizeHover)Capture(foreground,bounds);
            else if(!Active)Warm(foreground,bounds);
            minimizeHover=hover;
        }

        async void Capture(IntPtr window,Rect bounds)
        {
            if(capturing)return;
            capturing=true;
            int generation=captureGeneration;
            try
            {
                await System.Threading.Tasks.Task.Delay(70);
                if(disposed||generation!=captureGeneration||Active)return;
                var image=await BackgroundWork.Run(()=>MediaWindowSnapshot.Capture(window,bounds));
                if(disposed||generation!=captureGeneration||image==null)return;
                snapshot=image;
                snapshotWindow=window;
                snapshotBounds=bounds;
                if(!Active)Warm(window,bounds);
            }
            catch(Exception error)
            {
                AppLog.Once("media-window-snapshot",error.GetType().Name);
            }
            finally
            {
                capturing=false;
            }
        }

        bool Capsule(IntPtr window,out Rect bounds,out IntPtr owner)
        {
            bounds=Rect.Empty;
            owner=IntPtr.Zero;
            var bar=shell.MediaMotionTarget(window);
            if(bar==null||!bar.IsVisible)return false;
            var point=bar.Surface.PointToScreen(new Point());
            double dpi=VisualTreeHelper.GetDpi(bar).DpiScaleX;
            bounds=new Rect(point.X,point.Y,bar.Surface.ActualWidth*dpi,bar.Surface.ActualHeight*dpi);
            owner=new WindowInteropHelper(bar).Handle;
            return bounds.Width>0&&bounds.Height>0;
        }

        void Warm(IntPtr window,Rect bounds)
        {
            Rect capsule;
            IntPtr owner;
            if(snapshot==null||snapshotWindow!=window||!Capsule(window,out capsule,out owner))return;
            if(prepared!=null&&preparedImage==snapshot&&preparedBounds==bounds&&preparedCapsule==capsule)
            {
                if(!lease.Arm(window))DropPrepared();
                return;
            }
            DropPrepared();
            try
            {
                // Create the HWND, mesh and compositor host before the user
                // minimizes. No visible host, frame loop or repeated capture.
                prepared=new MediaWindowPlane(snapshot,bounds,capsule);
                prepared.Frame(bounds,capsule,0);
                preparedImage=snapshot;
                preparedBounds=bounds;
                preparedCapsule=capsule;
                if(!lease.Arm(window))DropPrepared();
            }
            catch(Exception error)
            {
                DropPrepared();
                AppLog.Once("media-flight-warm",error.GetType().Name);
            }
        }

        void PrepareRestore(IntPtr window)
        {
            if(disposed||!Application.Current.Dispatcher.CheckAccess()||!normal.ContainsKey(window))return;
            if(Transition(window,false))
            {
                proactiveRestore=window;
                proactiveRestoreUntil=DateTime.UtcNow.AddSeconds(2);
            }
        }

        void OnEvent(IntPtr h,uint id,IntPtr window,int objectId,int child,uint thread,uint time)
        {
            if(disposed||objectId!=0||child!=0)return;
            if(Application.Current.Dispatcher.CheckAccess())HandleEvent(window,id);
            else Application.Current.Dispatcher.BeginInvoke(new Action(()=>HandleEvent(window,id)),DispatcherPriority.Send);
        }

        void HandleEvent(IntPtr window,uint id)
        {
            if(disposed||!normal.ContainsKey(window))return;
            // Own activation prepares the texture and cloaks the restored app
            // before ShowWindowAsync. Its later WinEvent must not restart it.
            if(id==0x17&&proactiveRestore==window)
            {
                proactiveRestore=IntPtr.Zero;
                if(DateTime.UtcNow<=proactiveRestoreUntil)return;
            }
            Transition(window,id==0x16);
        }

        bool Transition(IntPtr window,bool minimize)
        {
            if(disposed||!Motion.Enabled||!Preferences.Current.MediaWindowAnimation||shell.FullscreenMediaVisible||!shell.Services.Media.Active)return false;
            var app=MediaApplication.Resolve(shell.Services.Media.AppId,shell.Running.Concat(shell.Apps));
            Rect bounds,capsule;
            IntPtr owner;
            if(app==null||!app.Windows.Contains(window)||!normal.TryGetValue(window,out bounds)||!Capsule(window,out capsule,out owner))return false;
            if(snapshot==null||snapshotWindow!=window)return false;
            Native.RectI restored;
            if(!minimize&&!Native.IsIconic(window)&&Native.GetWindowRect(window,out restored)&&restored.Right>restored.Left&&restored.Left>-30000)
            {
                bounds=new Rect(restored.Left,restored.Top,restored.Right-restored.Left,restored.Bottom-restored.Top);
            }
            Begin(window,app,owner,minimize?bounds:capsule,minimize?capsule:bounds,!minimize||!Native.IsIconic(window),!minimize);
            return Active;
        }

        internal void Begin(IntPtr source,AppEntry app,IntPtr owner,Rect start,Rect end,bool hideSource=false,bool restoring=false,BitmapSource fixture=null)
        {
            if(disposed)return;
            Stop();
            from=start;
            to=end;
            restore=restoring;
            started=lastFrame=null;
            finishing=false;
            FrameCount=0;
            FirstProgress=Double.NaN;
            MaximumFrameGap=0;
            var initial=restore?end:start;
            var capsule=restore?start:end;
            var image=fixture??(snapshotWindow==source?snapshot:null);
            try
            {
                if(image!=null)
                {
                    if(prepared!=null&&preparedImage==image&&preparedBounds==initial&&preparedCapsule==capsule)
                    {
                        plane=prepared;
                        prepared=null;
                        preparedImage=null;
                    }
                    else plane=new MediaWindowPlane(image,initial,capsule);
                    plane.Frame(initial,capsule,restore?1:0);
                }
                // Construct and lay out before hiding the real window. Disk
                // journaling still precedes cloaking, preserving crash recovery.
                if(hideSource&&(!lease.Arm(source)||!lease.HideRepresentation(source)))
                {
                    Stop();
                    return;
                }
                if(plane!=null)plane.Present();
                else
                {
                    preview=new SwitchThumbnail(new SwitchItem {Window=source,App=app},owner,()=>{},()=>{},n=>{});
                    preview.MotionPreview=true;
                    var window=preview;
                    window.SourceInitialized+=(s,e)=> {
                        var handle=new WindowInteropHelper(window).Handle;
                        Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x20);
                    };
                    preview.IsHitTestVisible=false;
                    Place(0);
                }
                if(Active)CompositionTarget.Rendering+=Frame;
            }
            catch(Exception error)
            {
                AppLog.Once("media-window-begin",error.GetType().Name);
                Stop();
            }
        }

        void Frame(object sender,EventArgs args)
        {
            if(placing)return;
            var rendering=args as RenderingEventArgs;
            if(rendering==null||lastFrame==rendering.RenderingTime)return;
            if(finishing)
            {
                Stop();
                return;
            }
            if(!started.HasValue)started=rendering.RenderingTime;
            if(lastFrame.HasValue)MaximumFrameGap=Math.Max(MaximumFrameGap,(rendering.RenderingTime-lastFrame.Value).TotalMilliseconds);
            lastFrame=rendering.RenderingTime;
            double duration=plane==null?360:restore?MediaFlightTiming.RestoreMilliseconds:MediaFlightTiming.MinimizeMilliseconds;
            double p=Math.Min(1,(rendering.RenderingTime-started.Value).TotalMilliseconds/duration);
            if(FrameCount++==0)FirstProgress=p;
            try
            {
                if(plane!=null)plane.Frame(restore?to:from,restore?from:to,MediaFlightTiming.Position(p,restore));
                else Place(p);
            }
            catch(Exception error)
            {
                AppLog.Once("media-window-frame",error.GetType().Name);
                Stop();
                return;
            }
            // Let WPF present the exact endpoint before removing the texture.
            // Construction time never consumes any of the animation timeline.
            if(p>=1)finishing=true;
        }

        void Place(double progress)
        {
            if(preview==null||placing)return;
            placing=true;
            try
            {
                double t=1-Math.Pow(1-progress,3);
                var bounds=Interpolate(from,to,t);
                preview.SetPreviewOpacity(restore?(byte)255:(byte)Math.Max(0,255*(1-Math.Pow(progress,5))));
                preview.PlacePixels((int)bounds.X,(int)bounds.Y,Math.Max(1,(int)bounds.Width),Math.Max(1,(int)bounds.Height));
            }
            finally
            {
                placing=false;
            }
        }

        internal static Rect Interpolate(Rect a,Rect b,double t)
        {
            t=Math.Max(0,Math.Min(1,t));
            return new Rect(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Width+(b.Width-a.Width)*t,a.Height+(b.Height-a.Height)*t);
        }

        internal void Stop()
        {
            CompositionTarget.Rendering-=Frame;
            lease.RevealAll();
            var previous=preview;
            var oldPlane=plane;
            preview=null;
            plane=null;
            if(previous!=null||oldPlane!=null)MediaWindowLease.FlushFirstFrame();
            if(previous!=null)previous.Close();
            if(oldPlane!=null)oldPlane.Close();
        }

        void DropPrepared()
        {
            if(prepared!=null)prepared.Close();
            prepared=null;
            preparedImage=null;
        }

        void Retire()
        {
            Stop();
            DropPrepared();
            lease.Dispose();
            normal.Clear();
            snapshot=null;
            snapshotWindow=IntPtr.Zero;
            proactiveRestore=IntPtr.Zero;
            captureGeneration++;
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            WindowActivation.Restoring-=PrepareRestore;
            Retire();
            if(hook!=IntPtr.Zero)UnhookWinEvent(hook);
        }
    }

    internal sealed partial class ShellController
    {
        MediaWindowMotion mediaMotion;
        internal NowBar MediaMotionTarget(IntPtr window)
        {
            var screen=System.Windows.Forms.Screen.FromHandle(window);
            return docks.Where(d=>d.Screen.DeviceName==screen.DeviceName).Select(d=>d.MediaBar).FirstOrDefault(b=>b!=null);
        }
        void ObserveMediaMotion(IntPtr foreground)
        {
            if(!ReleaseFeatures.GlassUI||Demo||Program.TestProcess)return;
            if(mediaMotion==null)mediaMotion=new MediaWindowMotion(this);
            mediaMotion.Observe(foreground);
        }
    }
}
