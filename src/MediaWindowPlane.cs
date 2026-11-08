using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace OneTaskBar
{
    // One texture and one fixed HWND. The compositor moves the textured plane;
    // resizing a native thumbnail every frame cannot reproduce a tilted window.
    internal sealed class MediaWindowPlane : Window
    {
        readonly Viewport3D viewport=new Viewport3D();
        readonly Image occlusion=new Image {Stretch=Stretch.Fill,IsHitTestVisible=false};
        readonly Image defocused=new Image {Stretch=Stretch.Fill,IsHitTestVisible=false,Opacity=0};
        readonly ScaleTransform3D scale=new ScaleTransform3D();
        readonly AxisAngleRotation3D tilt=new AxisAngleRotation3D(new Vector3D(0,1,0),0);
        readonly AxisAngleRotation3D turn=new AxisAngleRotation3D(new Vector3D(0,0,1),0);
        readonly TranslateTransform3D translation=new TranslateTransform3D();
        readonly MeshGeometry3D mesh=new MeshGeometry3D();
        readonly Rect host;
        readonly Rect sourceBounds;
        readonly double dpi;
        LiveBackdrop backdrop;

        internal MediaWindowPlane(BitmapSource snapshot,Rect initial,Rect capsule)
        {
            Title="One TaskBar · 미디어 전환";
            WindowStyle=WindowStyle.None;
            ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;
            Background=Brushes.Transparent;
            ShowActivated=false;
            ShowInTaskbar=false;
            Topmost=true;
            IsHitTestVisible=false;
            var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(initial.X+initial.Width/2),(int)(initial.Y+initial.Height/2)));
            var area=Rect.Union(initial,capsule);
            area.Inflate(Math.Min(96,Math.Max(initial.Width,initial.Height)*.08),
                Math.Min(96,Math.Max(initial.Width,initial.Height)*.08));
            var desktop=System.Windows.Forms.SystemInformation.VirtualScreen;
            area.Intersect(new Rect(desktop.X,desktop.Y,desktop.Width,desktop.Height));
            host=new Rect(Math.Floor(area.Left),Math.Floor(area.Top),Math.Ceiling(area.Right)-Math.Floor(area.Left),
                Math.Ceiling(area.Bottom)-Math.Floor(area.Top));
            sourceBounds=initial;
            var root=new Grid();
            root.Children.Add(occlusion);
            root.Children.Add(defocused);
            root.Children.Add(viewport);
            Content=root;
            double distance=host.Width/(2*Math.Tan(Math.PI/8));
            viewport.Camera=new PerspectiveCamera(new Point3D(0,0,distance),new Vector3D(0,0,-distance),new Vector3D(0,1,0),45);
            mesh.Positions.Add(new Point3D());
            mesh.TextureCoordinates.Add(new Point(.5,.5));
            for(int i=0;i<52;i++)
            {
                mesh.Positions.Add(new Point3D());
                mesh.TextureCoordinates.Add(new Point());
                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(i+1);
                mesh.TriangleIndices.Add((i+1)%52+1);
            }
            var image=new ImageBrush(snapshot) {Stretch=Stretch.Fill};
            RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.Fant);image.Freeze();
            // Round the small mesh instead of repainting a large texture while
            // its corners morph. The artwork stays one frozen GPU resource.
            var material=new DiffuseMaterial(image);
            material.Freeze();
            var transforms=new Transform3DGroup();
            transforms.Children.Add(scale);
            transforms.Children.Add(new RotateTransform3D(tilt));
            transforms.Children.Add(new RotateTransform3D(turn));
            transforms.Children.Add(translation);
            var scene=new Model3DGroup();
            scene.Children.Add(new AmbientLight(Colors.White));
            scene.Children.Add(new GeometryModel3D(mesh,material) {BackMaterial=material,Transform=transforms});
            viewport.Children.Add(new ModelVisual3D {Content=scene});
            SourceInitialized+=(s,e)=> {
                var window=new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(window,-20,(Native.GetWindowLong(window,-20)|0x080000A0)&~0x40000);
                int disabled=1;
                DwmSetWindowAttribute(window,3,ref disabled,4);
                Native.SetWindowPos(window,new IntPtr(-1),(int)host.X,(int)host.Y,(int)host.Width,(int)host.Height,0x10);
            };
            Closed+=(s,e)=> {
                if(backdrop!=null)backdrop.Dispose();
                backdrop=null;
                viewport.Children.Clear();
                occlusion.Source=null;
                defocused.Source=null;
                Content=null;
            };
            dpi=Native.PrepareMonitor(this,screen.Bounds);
            Width=host.Width/dpi;
            Height=host.Height/dpi;
            Measure(new Size(Width,Height));
            Arrange(new Rect(0,0,Width,Height));
            UpdateLayout();
        }

        // A built plane stays good while the window is where it was and the pill is still inside the plane's room.
        // The pill drifts by a pixel or two as its title and wave change; rebuilding then, at the moment the window
        // is minimized, is what made the window vanish and reappear halfway.
        internal bool CanServe(Rect initial,Rect capsule)
        {
            return Math.Abs(sourceBounds.X-initial.X)<=2&&Math.Abs(sourceBounds.Y-initial.Y)<=2&&Math.Abs(sourceBounds.Width-initial.Width)<=2&&Math.Abs(sourceBounds.Height-initial.Height)<=2&&host.Contains(capsule);
        }

        internal void Present(IntPtr recipient=default(IntPtr))
        {
            // A prepared flight owns no full-display blur target while idle.
            // Restores already have a frozen blurred cover and need no second one.
            if(!HasOcclusion&&backdrop==null)
            {
                try
                {
                    backdrop=new LiveBackdrop(this,0);
                    backdrop.SetOpacity(0);
                }
                catch(Exception error)
                {
                    AppLog.Once("media-flight-backdrop",error.GetType().Name);
                }
            }
            Show();
            // Order the temporary plane below the recipient instead of raising
            // the recipient and its separate blur HWND through the new plane.
            Native.SetWindowPos(new WindowInteropHelper(this).Handle,recipient==IntPtr.Zero?new IntPtr(-1):recipient,
                (int)host.X,(int)host.Y,(int)host.Width,(int)host.Height,0x10|0x200);
        }

        internal bool CoverRestoringWindow(IntPtr source)
        {
            // A foreign app cannot be DWM-cloaked. While it is still minimized,
            // retain the desktop behind it and cover its native restore. Input,
            // placement and opacity of that app remain untouched.
            if(!Native.IsIconic(source))return false;
            occlusion.Source=MediaWindowSnapshot.CaptureBackdrop(source,host);
            defocused.Source=MediaFlightBackdrop.Blur(occlusion.Source as BitmapSource,host.Width/dpi);
            // Never bake a stationary copy of the dock into its receiving
            // animation. Cover only the native window that is being restored.
            // The restoring window also draws a shadow and a thin frame outside its rectangle. Cover those too,
            // so no outline shows where the window will appear. The taskbar area is left alone.
            double margin=Math.Ceiling(40*dpi);
            var visible=new Rect(sourceBounds.X-margin,sourceBounds.Y-margin,sourceBounds.Width+margin*2,sourceBounds.Height+margin*2);
            visible.Intersect(host);
            var work=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(sourceBounds.X+sourceBounds.Width/2),(int)(sourceBounds.Y+sourceBounds.Height/2))).WorkingArea;
            var allowed=new Rect(work.X,work.Y,work.Width,work.Height);
            if(!visible.IsEmpty&&!allowed.IsEmpty)visible.Intersect(allowed);
            if(visible.IsEmpty)visible=Rect.Intersect(sourceBounds,host);
            var clip=new RectangleGeometry(new Rect((visible.X-host.X)/dpi,(visible.Y-host.Y)/dpi,
                visible.Width/dpi,visible.Height/dpi));
            clip.Freeze();
            occlusion.Clip=clip;
            defocused.Clip=clip;
            return occlusion.Source!=null;
        }

        internal bool HasOcclusion {get{return occlusion.Source!=null;}}

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);

        internal void Frame(Rect initial,Rect capsule,double progress)
        {
            var pose=MediaWindowPose.At(initial,capsule,progress);
            // Rebuilding the rounded mesh is the costliest part of a frame. A change smaller than a thousandth of the
            // plane is invisible, so the mesh is only touched when the corner shape really moved.
            double rx=pose.Radius/pose.Width,ry=pose.Radius/pose.Height;
            if(Double.IsNaN(shapedX)||Math.Abs(rx-shapedX)>.0008||Math.Abs(ry-shapedY)>.0008||progress<=0||progress>=1){Shape(rx,ry);shapedX=rx;shapedY=ry;}
            scale.ScaleX=pose.Width;
            scale.ScaleY=pose.Height;
            translation.OffsetX=pose.Center.X-host.X-host.Width/2;
            translation.OffsetY=host.Height/2-(pose.Center.Y-host.Y);
            tilt.Angle=pose.Tilt;
            turn.Angle=pose.Turn;
            viewport.Opacity=pose.Opacity;
            // The blur layer is a full-display surface: its opacity moves in small steps and only when a step is crossed.
            double blur=Math.Round(pose.Defocus*40)/40;
            if(blur==lastBlur)return;
            lastBlur=blur;
            if(HasOcclusion)defocused.Opacity=blur;
            else if(backdrop!=null)backdrop.SetOpacity(blur);
        }

        void Shape(double rx,double ry)
        {
            // Detach both collections while editing. Otherwise every vertex
            // propagates another mesh/bounds invalidation through the 3D scene.
            var positions=mesh.Positions;
            var coordinates=mesh.TextureCoordinates;
            mesh.Positions=null;
            mesh.TextureCoordinates=null;
            for(int corner=0;corner<4;corner++)
            {
                double cx=corner==0||corner==3?.5-rx:-.5+rx;
                double cy=corner<2?.5-ry:-.5+ry;
                for(int step=0;step<=12;step++)
                {
                    double angle=(corner+step/12.0)*Math.PI/2;
                    double x=cx+Math.Cos(angle)*rx,y=cy+Math.Sin(angle)*ry;
                    int index=corner*13+step+1;
                    positions[index]=new Point3D(x,y,0);
                    coordinates[index]=new Point(x+.5,.5-y);
                }
            }
            mesh.Positions=positions;
            mesh.TextureCoordinates=coordinates;
        }

        double shapedX=Double.NaN,shapedY,lastBlur=-1;
        internal FrameworkElement Scene {get{return viewport;}}
    }

}
