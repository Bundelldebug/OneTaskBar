using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;

namespace OneTaskBar
{
    // A geometry-clipped visual, behind the WPF content. No window-wide acrylic rectangle.
    internal sealed class LiveBackdrop : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct QueueOptions {public int Size,ThreadType,Apartment;}
        [DllImport("coremessaging.dll")] static extern int CreateDispatcherQueueController(QueueOptions options,[MarshalAs(UnmanagedType.IUnknown)]out object queue);
        [ComImport,Guid("29E691FA-4567-4DCA-B319-D0F207EB6807"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDesktopInterop {void CreateDesktopWindowTarget(IntPtr window,[MarshalAs(UnmanagedType.Bool)]bool topmost,out IntPtr target);}
        static object dispatcherQueue;
        static Compositor compositor;
        static int unavailableFixtures;
        internal static IDisposable WithoutHostForFixture(){if(!Program.Inspectable)throw new InvalidOperationException("Backdrop fixture requires inspectable mode");return new UnavailableFixture();}
        sealed class UnavailableFixture:IDisposable {bool disposed;internal UnavailableFixture(){unavailableFixtures++;}public void Dispose(){if(!disposed){disposed=true;unavailableFixtures--;}}}
        static readonly System.Collections.Generic.HashSet<LiveBackdrop> instances=new System.Collections.Generic.HashSet<LiveBackdrop>();
        internal static int InstanceCount {get{return instances.Count;}}
        internal static int OrphanCount {get{int count=0;foreach(var item in instances)if(item.background!=IntPtr.Zero&&Native.IsWindowVisible(item.background)&&!item.Visible)count++;return count;}}
        internal static int VisibleCount {get{int count=0;foreach(var item in instances)if(item.background!=IntPtr.Zero&&Native.IsWindowVisible(item.background))count++;return count;}}
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateWindowEx(int ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr data);
        [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll")]static extern bool EnableWindow(IntPtr window,bool enabled);
        [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        IntPtr background;
        DesktopWindowTarget target;
        ContainerVisual root;
        SpriteVisual blur;
        CompositionRoundedRectangleGeometry shape;
        GlassRefraction refraction;
        string refractionLayout;
        bool refractionUnavailable;
        readonly Window window;
        readonly ContextMenu menu;
        readonly IntPtr popupHandle;
        readonly DispatcherTimer popupTimer;
        readonly double radius;
        int width,height,corner,popupX=Int32.MinValue,popupY=Int32.MinValue;
        Native.RectI placedBounds;bool shown,enabled=true;double opacity=1;Rect? lastClip;
        Native.RectI? inputClip;
        internal bool Realtime {get{return blur!=null&&blur.Brush is CompositionBackdropBrush;}}
        internal int RefractedRegions {get{return refraction==null?0:refraction.Count;}}
        internal LiveBackdrop(Window window,double radius)
        {
            this.window=window;this.radius=radius;
            try{Initialize(window.Topmost);}catch{Dispose();throw;}
            window.IsVisibleChanged+=(s,e)=>Sync();window.LocationChanged+=(s,e)=>Sync();
            window.Activated+=(s,e)=>Sync(true);
            window.LayoutUpdated+=RefreshRefraction;
        }
        internal LiveBackdrop(ContextMenu menu,double radius)
        {
            this.menu=menu;this.radius=radius;
            var source=PresentationSource.FromVisual(menu) as HwndSource;if(source==null)throw new InvalidOperationException("Menu host is not ready");popupHandle=source.Handle;
            try{Initialize(true);}catch{Dispose();throw;}popupTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};popupTimer.Tick+=(s,e)=>Resize();popupTimer.Start();
        }
        IntPtr Overlay {get{return window!=null?new WindowInteropHelper(window).Handle:popupHandle;}}
        bool Visible {get{return window!=null?window.IsVisible:menu.IsOpen;}}
        bool Bounds(out Native.RectI bounds)
        {
            if(!Native.GetWindowRect(Overlay,out bounds))return false;
            if(menu!=null){int inset=(int)Math.Round(10*Math.Max(96,Native.GetDpiForWindow(Overlay))/96.0);bounds.Left+=inset;bounds.Top+=inset;bounds.Right-=inset;bounds.Bottom-=inset;}
            return bounds.Right>bounds.Left&&bounds.Bottom>bounds.Top;
        }
        void Initialize(bool topmost)
        {
            if(compositor==null) {
                Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(new QueueOptions {Size=12,ThreadType=2,Apartment=2},out dispatcherQueue));
                compositor=new Compositor();
            }
            // WPF owns its HWND's compositor target. Put the backdrop in a separate, non-input,
            // no-redirection HWND and make the transparent WPF window its owner-ordered overlay.
            bool passThrough=window is TransientGlass;
            background=CreateWindowEx(0x00200000|0x08000000|0x80|0x20|(passThrough?0x80000:0)|(topmost?0x8:0),"static","One TaskBar · 블러",0x88000000,0,0,1,1,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
            if(background==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
            // The composition host is visual-only. WS_EX_TRANSPARENT alone does
            // not exclude a non-layered HWND from every native hit-test path.
            EnableWindow(background,false);
            if(passThrough&&!SetLayeredWindowAttributes(background,0,255,2))throw new System.ComponentModel.Win32Exception();
            int host=1,result=unavailableFixtures>0?unchecked((int)0x80070057):DwmSetWindowAttribute(background,17,ref host,4);if(result<0)throw new NotSupportedException("Windows host backdrop is unavailable ("+result+")");
            if(window!=null&&!Program.CaptureReview)new WindowInteropHelper(window).Owner=background;
            IntPtr raw;
            ((IDesktopInterop)(object)compositor).CreateDesktopWindowTarget(background,true,out raw);
            try {target=(DesktopWindowTarget)Marshal.GetObjectForIUnknown(raw);}finally{Marshal.Release(raw);}
            root=compositor.CreateContainerVisual();shape=compositor.CreateRoundedRectangleGeometry();root.Clip=compositor.CreateGeometricClip(shape);
            root.BorderMode=CompositionBorderMode.Soft;
            blur=compositor.CreateSpriteVisual();blur.BorderMode=CompositionBorderMode.Soft;blur.Brush=compositor.CreateHostBackdropBrush();root.Children.InsertAtTop(blur);target.Root=root;
            instances.Add(this);Resize();
        }
        internal void Sync(bool force=false)
        {
            if(background==IntPtr.Zero||target==null)return;if(!Visible){root.IsVisible=false;Native.ShowWindow(background,0);shown=false;return;}root.IsVisible=true;
            Native.RectI bounds;var overlay=Overlay;if(!Bounds(out bounds))return;
            if(!force&&shown&&bounds.Left==placedBounds.Left&&bounds.Top==placedBounds.Top&&bounds.Right==placedBounds.Right&&bounds.Bottom==placedBounds.Bottom)return;
            Native.SetWindowPos(background,overlay,bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top,0x10|0x40);placedBounds=bounds;shown=true;
        }
        internal void EnsureDockVisible()
        {
            if(background==IntPtr.Zero||!Visible)return;
            if(!Native.IsWindowVisible(background)||Native.IsIconic(background)||(Native.GetWindowLong(background,-20)&8)==0){Native.ShowWindow(background,4);Native.SetWindowPos(background,new IntPtr(-1),0,0,0,0,0x53);Sync(true);}
        }
        internal double Opacity {get{return root!=null?root.Opacity:0;}}
        internal void Enable(bool value){enabled=value;ApplyOpacity();}
        internal void SetOpacity(double value){opacity=Math.Max(0,Math.Min(1,value));ApplyOpacity();}
        void ApplyOpacity(){if(root!=null)root.Opacity=enabled?(float)opacity:0;}
        internal void Resize()
        {
            if(target==null)return;Native.RectI bounds;var handle=Overlay;
            if(!Bounds(out bounds))return;
            int w=bounds.Right-bounds.Left,h=bounds.Bottom-bounds.Top,c=(int)Math.Round(radius*Math.Max(96,Native.GetDpiForWindow(handle))/96.0);
            var glass=window as GlassWindow;Rect? clip=glass!=null?glass.BackdropClip:null;
            if(w==width&&h==height&&c==corner&&clip==lastClip){Sync();return;}
            popupX=bounds.Left;popupY=bounds.Top;lastClip=clip;
            if(w!=width||h!=height){var size=new Vector2(w,h);root.Size=size;blur.Size=size;}width=w;height=h;corner=c;
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;float cw=clip.HasValue?(float)(clip.Value.Width*scale):w,ch=clip.HasValue?(float)(clip.Value.Height*scale):h;
            if(clip.HasValue){var boundsClip=Native.InputBounds(clip.Value,scale);if(!inputClip.HasValue||!inputClip.Value.Equals(boundsClip)){if(Native.ClipWindow(background,boundsClip))inputClip=boundsClip;}}
            shape.Offset=clip.HasValue?new Vector2((float)(clip.Value.X*scale),(float)(clip.Value.Y*scale)):Vector2.Zero;shape.Size=new Vector2(cw,ch);shape.CornerRadius=new Vector2(Math.Min(c,cw/2f),Math.Min(c,ch/2f));
            Sync();
            RefreshRefraction(null,EventArgs.Empty);
        }
        void RefreshRefraction(object sender,EventArgs args)
        {
            var glass=window as GlassWindow;
            bool active=root!=null&&glass!=null&&!(glass is DesktopOverview)&&glass.GlassMaterial&&Glass95.Enabled&&Glass95.Transparent&&enabled;
            if(!active){if(refraction!=null){refraction.Begin();refraction.End();}refractionLayout=null;return;}
            if(refractionUnavailable||width<4||height<4)return;
            double scale=Math.Max(96,Native.GetDpiForWindow(Overlay))/96.0;
            var regions=new System.Collections.Generic.List<Rect>();
            var quick=window as QuickWindow;
            if(quick!=null&&quick.GridUI!=null){
                foreach(var frame in quick.GridUI.Board.Frames.Values){
                    var body=frame.Body;
                    if(!body.IsVisible||body.ActualWidth<4||body.ActualHeight<4)continue;
                    var point=body.TranslatePoint(new Point(),window);
                    regions.Add(new Rect(point.X*scale,point.Y*scale,body.ActualWidth*scale,body.ActualHeight*scale));
                }
            }
            var signature=width+":"+height+":"+corner+":"+String.Join(";",regions.ConvertAll(r=>r.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if(signature==refractionLayout)return;
            try {
                if(refraction==null)refraction=new GlassRefraction(compositor,root,blur);
                refraction.Begin();
                refraction.Add(0,0,width,height,corner,(float)(1.5*scale));
                foreach(var region in regions)refraction.Add((float)region.X,(float)region.Y,(float)region.Width,(float)region.Height,(float)Math.Min(30*scale,Math.Min(region.Width,region.Height)/2),(float)(2.2*scale));
                refraction.End();refractionLayout=signature;
            }catch(Exception error){
                refractionUnavailable=true;
                if(refraction!=null){refraction.Dispose();refraction=null;}
                AppLog.Once("glass-refraction",error.ToString());
            }
        }
        public void Dispose() {instances.Remove(this);if(window!=null)window.LayoutUpdated-=RefreshRefraction;if(popupTimer!=null)popupTimer.Stop();if(refraction!=null){refraction.Dispose();refraction=null;}if(target!=null){target.Root=null;target.Dispose();target=null;}if(root!=null)root.Children.RemoveAll();if(blur!=null){var brush=blur.Brush;blur.Brush=null;blur.Dispose();blur=null;if(brush!=null)brush.Dispose();}if(root!=null){root.Dispose();root=null;}if(shape!=null){shape.Dispose();shape=null;}if(background!=IntPtr.Zero){DestroyWindow(background);background=IntPtr.Zero;}}
    }
}
