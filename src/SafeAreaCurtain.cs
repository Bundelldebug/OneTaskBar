using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SampleRect=System.Drawing.Rectangle;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // Paint the reserved edge with a maximized window's colors in GoogleBook mode,
    // or use the dark hover band for the island's fullscreen treatment.
    internal sealed class SafeAreaCurtain : Window
    {
        readonly Forms.Screen screen;
        readonly double scale;
        readonly SampleRect bounds;
        readonly bool bottom;
        IntPtr source;
        SampleRect sampleArea;
        Color[] palette;
        bool sampling,obstructedNow;
        // The strip under the bar is read a few times a second; a new colour pours in from the top edge downwards.
        readonly System.Windows.Controls.Border under=new System.Windows.Controls.Border(),over=new System.Windows.Controls.Border {Visibility=Visibility.Collapsed};
        readonly DispatcherTimer sampler=new DispatcherTimer(DispatcherPriority.Background);
        Brush pouring;
        readonly DispatcherTimer watch=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
        DateTime leftAt=DateTime.MinValue;
        bool closed,retiring;
        internal bool Revealed {get;private set;}
        bool pinned;
        internal void Pin(bool value)
        {
            if(pinned==value)return;
            pinned=value;
            if(pinned)
            {
                watch.Stop();
                Revealed=true;
            }
            else if(IsVisible)
            {
                leftAt=DateTime.MinValue;
                Watch();
                watch.Start();
            }
        }

        internal Action<bool> RevealChanged;
        internal Action<Color[]> MatchChanged;
        // True while something opened from the status bar is still on screen, so the bar stays.
        internal Func<bool> Hold;
        internal bool Fits(Forms.Screen monitor,double dpi,bool atBottom)
        {
            return screen.DeviceName==monitor.DeviceName&&bounds==monitor.Bounds&&scale==dpi&&bottom==atBottom;
        }

        internal SafeAreaCurtain(Forms.Screen monitor,double dpi,bool atBottom)
        {
            screen=monitor;scale=dpi;bottom=atBottom;bounds=monitor.Bounds;
            Title="One TaskBar · 안전 영역";
            WindowStyle=WindowStyle.None;
            ResizeMode=ResizeMode.NoResize;
            ShowInTaskbar=false;
            ShowActivated=false;
            Topmost=true;
            Focusable=false;
            AllowsTransparency=true;
            Background=Brushes.Black;
            Opacity=0;
            SourceInitialized+=(s,e)=>
            {
                var handle=new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x20|0x80|0x08000000)&~0x40000);
            };
            var layers=new System.Windows.Controls.Grid();
            layers.Children.Add(under);layers.Children.Add(over);
            Content=layers;
            sampler.Tick+=(s,e)=>Sample();
            watch.Tick+=(s,e)=>Watch();
            Closed+=(s,e)=>{closed=true;watch.Stop();sampler.Stop();};
        }

        internal void Present()
        {
            if(closed||IsVisible)return;
            double wide=bounds.Width/scale,top=bottom?bounds.Height/scale-IslandSafeArea.Height:0;
            var box=Native.PlacementBounds(bounds,scale,0,top,wide,IslandSafeArea.Height);
            Width=wide;
            Height=IslandSafeArea.Height;
            var handle=new WindowInteropHelper(this).EnsureHandle();
            Native.PrepareMonitor(this,bounds);
            Show();
            Native.SetWindowPos(handle,new IntPtr(-1),box.Left,box.Top,box.Right-box.Left,box.Bottom-box.Top,0x10);
            if(!pinned)watch.Start();
            if(Motion.Enabled)
                BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(220)){EasingFunction=SpringEase.Smooth});
            else Opacity=1;
        }

        internal void Match(IntPtr window,SampleRect area,bool obstructed)
        {
            if(source!=window||sampleArea!=area)
            {
                if(source!=window)
                {
                    palette=null;
                    ClearLayers();
                    Background=window==IntPtr.Zero?Brushes.Black:Brushes.Transparent;
                    if(MatchChanged!=null)MatchChanged(null);
                }
                source=window;
                sampleArea=area;
                if(source!=IntPtr.Zero)sampling=false;
            }
            obstructedNow=obstructed;
            if(source==IntPtr.Zero){sampler.Stop();return;}
            if(!sampler.IsEnabled)
            {
                sampler.Interval=TimeSpan.FromMilliseconds(RuntimeCadence.OnBattery?100:60);
                sampler.Start();
                Sample();
            }
        }

        void ClearLayers()
        {
            over.BeginAnimation(UIElement.OpacityProperty,null);
            over.Visibility=Visibility.Collapsed;
            over.Clip=null;
            under.Background=null;
            pouring=null;
        }

        void Sample()
        {
            if(closed||retiring||sampling||obstructedNow||source==IntPtr.Zero||!IsVisible)return;
            var window=source;var area=sampleArea;
            sampling=true;
            BackgroundWork.Run(()=>TopBarBand.Capture(area)).ContinueWith(task=>
            {
                var result=task.Status==TaskStatus.RanToCompletion?task.Result:null;
                if(task.IsFaulted)AppLog.Once("topbar-match",task.Exception.GetBaseException().GetType().Name);
                Dispatcher.BeginInvoke(new Action(()=>
                {
                    sampling=false;
                    if(closed||retiring||source!=window||sampleArea!=area||result==null||Hold!=null&&Hold())return;
                    if(palette!=null&&palette.SequenceEqual(result))return;
                    var brush=new LinearGradientBrush {StartPoint=new Point(0,.5),EndPoint=new Point(1,.5)};
                    for(int i=0;i<result.Length;i++)
                        brush.GradientStops.Add(new GradientStop(result[i],(i+.5)/result.Length));
                    brush.Freeze();
                    Background=Brushes.Transparent;
                    Pour(brush,palette!=null);
                    palette=result;
                    if(MatchChanged!=null)MatchChanged(result);
                }));
            });
        }

        // The new colour covers the old one from the top edge down. A colour that arrives mid-pour starts from what shows now.
        void Pour(Brush brush,bool animate)
        {
            if(!animate||!Motion.Enabled||ActualWidth<=0||ActualHeight<=0)
            {
                over.BeginAnimation(UIElement.OpacityProperty,null);
                over.Visibility=Visibility.Collapsed;over.Clip=null;
                under.Background=brush;pouring=null;
                return;
            }

            if(pouring!=null)under.Background=pouring;
            pouring=brush;
            over.Background=brush;over.Visibility=Visibility.Visible;
            double width=ActualWidth,height=ActualHeight;
            var reveal=new RectangleGeometry(new Rect(0,0,width,0));
            over.Clip=reveal;
            var grow=new System.Windows.Media.Animation.RectAnimation(new Rect(0,0,width,0),new Rect(0,0,width,height),TimeSpan.FromMilliseconds(170)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};
            grow.Completed+=(s,e)=>
            {
                if(pouring!=brush)return;
                under.Background=brush;
                over.Visibility=Visibility.Collapsed;over.Clip=null;pouring=null;
            };
            reveal.BeginAnimation(RectangleGeometry.RectProperty,grow);
        }

        // Fades out instead of vanishing, then closes.
        internal void Retire()
        {
            if(closed||retiring)return;
            retiring=true;
            watch.Stop();sampler.Stop();
            RevealChanged=null;
            MatchChanged=null;
            Hold=null;
            if(!Motion.Enabled){Close();return;}
            var fade=new DoubleAnimation(Opacity,0,TimeSpan.FromMilliseconds(200)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseIn}};
            fade.Completed+=(s,e)=>{if(!closed)Close();};
            BeginAnimation(OpacityProperty,fade);
        }

        void Watch()
        {
            if(pinned)return;
            var cursor=Forms.Cursor.Position;
            int band=(int)Math.Ceiling(IslandSafeArea.Height*scale)+1;
            bool inside=cursor.X>=bounds.Left&&cursor.X<bounds.Right&&(bottom?cursor.Y>=bounds.Bottom-band&&cursor.Y<bounds.Bottom:cursor.Y>=bounds.Top&&cursor.Y<bounds.Top+band);
            if(inside||Hold!=null&&Hold()){leftAt=DateTime.MinValue;Set(true);}
            else if(Revealed)
            {
                if(leftAt==DateTime.MinValue)leftAt=DateTime.UtcNow;
                else if((DateTime.UtcNow-leftAt).TotalMilliseconds>450)Set(false);
            }
        }

        void Set(bool value)
        {
            if(Revealed==value)return;
            Revealed=value;
            var changed=RevealChanged;
            if(changed!=null)changed(value);
        }
    }
}
