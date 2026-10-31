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
        readonly ScaleTransform3D scale=new ScaleTransform3D();
        readonly AxisAngleRotation3D tilt=new AxisAngleRotation3D(new Vector3D(0,1,0),0);
        readonly AxisAngleRotation3D turn=new AxisAngleRotation3D(new Vector3D(0,0,1),0);
        readonly TranslateTransform3D translation=new TranslateTransform3D();
        readonly Rect host;
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
            host=new Rect(screen.Bounds.X,screen.Bounds.Y,screen.Bounds.Width,screen.Bounds.Height);
            Content=viewport;
            double distance=host.Width/(2*Math.Tan(Math.PI/8));
            viewport.Camera=new PerspectiveCamera(new Point3D(0,0,distance),new Vector3D(0,0,-distance),new Vector3D(0,1,0),45);
            var mesh=new MeshGeometry3D {
                Positions=new Point3DCollection {new Point3D(-.5,-.5,0),new Point3D(.5,-.5,0),new Point3D(.5,.5,0),new Point3D(-.5,.5,0)},
                TextureCoordinates=new PointCollection {new Point(0,1),new Point(1,1),new Point(1,0),new Point(0,0)},
                TriangleIndices=new Int32Collection {0,1,2,0,2,3}
            };
            mesh.Freeze();
            var image=new ImageBrush(snapshot) {Stretch=Stretch.Fill};
            image.Freeze();
            // Transparent rounded corners are in the texture, rather than a
            // screen-aligned clip that cuts off the rotated plane.
            var drawing=new GeometryDrawing(image,null,new RectangleGeometry(new Rect(0,0,initial.Width,initial.Height),18,18));
            drawing.Freeze();
            var texture=new DrawingBrush(drawing) {Stretch=Stretch.Fill};
            texture.Freeze();
            var material=new DiffuseMaterial(texture);
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
                try
                {
                    backdrop=new LiveBackdrop(this,0);
                    backdrop.SetOpacity(0);
                }
                catch(Exception error)
                {
                    AppLog.Once("media-flight-backdrop",error.GetType().Name);
                }
            };
            Closed+=(s,e)=> {
                if(backdrop!=null)backdrop.Dispose();
                backdrop=null;
                viewport.Children.Clear();
                Content=null;
            };
            dpi=Native.PrepareMonitor(this,screen.Bounds);
            Width=host.Width/dpi;
            Height=host.Height/dpi;
            Measure(new Size(Width,Height));
            Arrange(new Rect(0,0,Width,Height));
            UpdateLayout();
        }

        internal void Present()
        {
            Show();
            Native.SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),(int)host.X,(int)host.Y,(int)host.Width,(int)host.Height,0x10);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);

        internal void Frame(Rect initial,Rect capsule,double progress)
        {
            var pose=MediaWindowPose.At(initial,capsule,progress);
            scale.ScaleX=pose.Width;
            scale.ScaleY=pose.Height;
            translation.OffsetX=pose.Center.X-host.X-host.Width/2;
            translation.OffsetY=host.Height/2-(pose.Center.Y-host.Y);
            tilt.Angle=pose.Tilt;
            turn.Angle=pose.Turn;
            viewport.Opacity=pose.Opacity;
            if(backdrop!=null)backdrop.SetOpacity(.72*Math.Sin(Math.PI*progress));
        }

        internal FrameworkElement Scene {get{return viewport;}}
    }

    internal sealed class MediaWindowPose
    {
        internal Point Center;
        internal double Width,Height,Tilt,Turn,Opacity;

        // Geometric path only. Timing is directional, so the real window is
        // reached at rest on restore rather than at maximum velocity.
        internal static MediaWindowPose At(Rect window,Rect capsule,double progress)
        {
            double p=Math.Max(0,Math.Min(1,progress));
            double t=p;
            var a=new Point(window.X+window.Width/2,window.Y+window.Height/2);
            var b=new Point(capsule.X+capsule.Width/2,capsule.Y+capsule.Height/2);
            double direction=b.X>=a.X?1:-1;
            double u=1-t;
            var control=new Point(a.X+(b.X-a.X)*.66,a.Y+(b.Y-a.Y)*.18);
            var center=new Point(u*u*a.X+2*u*t*control.X+t*t*b.X,u*u*a.Y+2*u*t*control.Y+t*t*b.Y);
            double end=Math.Max(.006,Math.Min(capsule.Width/window.Width,capsule.Height/window.Height)*.8);
            // Interpolate visible size, not its logarithm. Logarithmic scaling
            // spends most of the flight almost invisible and balloons on restore.
            double zoom=1+(end-1)*t;
            double envelope=Math.Sin(Math.PI*p);
            double fade=Math.Max(0,Math.Min(1,(p-.92)/.08));
            return new MediaWindowPose {Center=center,Width=window.Width*zoom,Height=window.Height*zoom,Tilt=-18*direction*envelope,Turn=-18*direction*envelope,Opacity=1-fade*fade*(3-2*fade)};
        }
    }
}
