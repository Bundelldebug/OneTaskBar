using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // While a window is maximized under the island, the whole reserved top band is plain black, like the strip
    // beside a notch. The status bar stays out of sight until the pointer touches the top edge.
    internal sealed class SafeAreaCurtain : Window
    {
        readonly Forms.Screen screen;
        readonly double scale;
        readonly bool bottom;
        readonly DispatcherTimer watch=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
        DateTime leftAt=DateTime.MinValue;
        bool closed,retiring;
        internal bool Revealed {get;private set;}
        bool pinned;
        // Keeps the status bar showing over the band for good.
        internal void Pin(){pinned=true;Revealed=true;}
        internal Action<bool> RevealChanged;
        // True while something opened from the status bar is still on screen, so the bar stays.
        internal Func<bool> Hold;
        internal string Monitor {get{return screen.DeviceName;}}

        internal SafeAreaCurtain(Forms.Screen monitor,double dpi,bool atBottom)
        {
            screen=monitor;scale=dpi;bottom=atBottom;
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
                Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x80|0x08000000)&~0x40000);
            };
            watch.Tick+=(s,e)=>Watch();
            Closed+=(s,e)=>{closed=true;watch.Stop();};
        }

        internal void Present()
        {
            if(closed)return;
            double wide=screen.Bounds.Width/scale,top=bottom?screen.Bounds.Height/scale-IslandSafeArea.Height:0;
            var box=Native.PlacementBounds(screen.Bounds,scale,0,top,wide,IslandSafeArea.Height);
            Width=wide;Height=IslandSafeArea.Height;
            new WindowInteropHelper(this).EnsureHandle();
            Native.PrepareMonitor(this,screen.Bounds);
            bool fresh=!IsVisible;
            if(fresh)Show();
            Native.SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),box.Left,box.Top,box.Right-box.Left,box.Bottom-box.Top,fresh?0x10u:0x14u);
            if(fresh)
            {
                watch.Start();
                BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            }
        }

        // Fades out instead of vanishing, then closes.
        internal void Retire()
        {
            if(closed||retiring)return;
            retiring=true;
            watch.Stop();
            RevealChanged=null;
            var fade=new DoubleAnimation(Opacity,0,TimeSpan.FromMilliseconds(200)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseIn}};
            fade.Completed+=(s,e)=>{if(!closed)Close();};
            BeginAnimation(OpacityProperty,fade);
        }

        void Watch()
        {
            if(pinned)return;
            var cursor=Forms.Cursor.Position;
            var bounds=screen.Bounds;
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
