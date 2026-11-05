using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace OneTaskBar
{
    // Light text on thin glass is hard to read over a white window. While a liquid panel is open, this reads
    // the average brightness of a thin strip of screen just outside each edge of the panel, so it never reads
    // the panel's own glass, and steps the tint darker in a few stages. Four tiny reads twice a second, only
    // while the panel is visible; nothing runs when it is hidden or the option is off.
    internal static class LiquidReadability
    {
        // Nearest-neighbour sampling: a halftone shrink of a tall strip of a 4K screen takes tens of milliseconds.
        const int Steps=5,Grid=8;
        const double Gap=8,Strip=28,Hysteresis=.045;
        [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int width,int height);
        [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr item);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr item);
        [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")]static extern int SetStretchBltMode(IntPtr dc,int mode);
        [DllImport("gdi32.dll")]static extern bool StretchBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int sx,int sy,int sw,int sh,uint operation);
        [DllImport("gdi32.dll")]static extern uint GetPixel(IntPtr dc,int x,int y);

        sealed class Watcher
        {
            internal readonly GlassWindow Window;
            internal readonly DispatcherTimer Timer;
            internal bool Dark;
            internal double Last=-1;
            internal int Level;
            internal Watcher(GlassWindow window)
            {
                Window=window;
                Timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(500)};
                Timer.Tick+=(s,e)=>Sample();
                window.IsVisibleChanged+=(s,e)=>Follow();
                window.Closed+=(s,e)=>Timer.Stop();
            }
            internal void Follow()
            {
                if(Window.IsVisible&&LiquidGlass.Enabled)
                {
                    Last=-1;
                    Sample();
                    Timer.Start();
                }
                else Timer.Stop();
            }
            internal void Sample()
            {
                if(!Window.IsVisible||!LiquidGlass.Enabled)return;
                double brightness=Brightness(Window);
                if(brightness<0)return;
                if(Last>=0&&Math.Abs(brightness-Last)<Hysteresis)return;
                Last=brightness;
                int level=Level;
                int next=LevelFor(brightness);
                if(next==Level)return;
                Level=next;
                Window.LiquidLevel=next;
                Window.Surface.Background=LiquidGlass.Tint(Dark,next);
            }
        }
        // Dark and mid backdrops need nothing; the tint deepens as the backdrop approaches white.
        internal static int LevelFor(double brightness){return brightness<.55?0:(int)Math.Min(Steps-1,1+Math.Floor((brightness-.55)/.1));}
        static readonly ConditionalWeakTable<GlassWindow,Watcher> watchers=new ConditionalWeakTable<GlassWindow,Watcher>();
        internal static void Watch(GlassWindow window,bool dark)
        {
            var watcher=watchers.GetValue(window,w=>new Watcher(w));
            watcher.Dark=dark;
            watcher.Follow();
        }
        internal static void Release(GlassWindow window)
        {
            Watcher watcher;
            if(!watchers.TryGetValue(window,out watcher))return;
            watcher.Timer.Stop();
            watcher.Level=0;
            window.LiquidLevel=0;
        }
        internal static double Measure(GlassWindow window){return Brightness(window);}
        static double Brightness(GlassWindow window)
        {
            Native.RectI box;
            var handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if(handle==IntPtr.Zero)return -11;if(!Native.GetWindowRect(handle,out box))return -12;
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            int gap=(int)Math.Round(Gap*scale),strip=(int)Math.Round(Strip*scale);
            var screen=System.Windows.Forms.Screen.FromHandle(handle).Bounds;
            var strips=new[]
            {
                new System.Drawing.Rectangle(box.Left-gap-strip,box.Top,strip,box.Bottom-box.Top),
                new System.Drawing.Rectangle(box.Right+gap,box.Top,strip,box.Bottom-box.Top),
                new System.Drawing.Rectangle(box.Left,box.Top-gap-strip,box.Right-box.Left,strip),
                new System.Drawing.Rectangle(box.Left,box.Bottom+gap,box.Right-box.Left,strip)
            };
            var screenDc=GetDC(IntPtr.Zero);
            if(screenDc==IntPtr.Zero)return -13;
            var memory=CreateCompatibleDC(screenDc);
            var bitmap=CreateCompatibleBitmap(screenDc,Grid,Grid);
            var old=SelectObject(memory,bitmap);
            double total=0;int count=0;
            try
            {
                SetStretchBltMode(memory,3);
                foreach(var area in strips)
                {
                    var clipped=System.Drawing.Rectangle.Intersect(area,screen);
                    if(clipped.Width<10||clipped.Height<10)continue;
                    if(!StretchBlt(memory,0,0,Grid,Grid,screenDc,clipped.X,clipped.Y,clipped.Width,clipped.Height,0x00CC0020))continue;
                    for(int y=0;y<Grid;y++)
                    {
                        for(int x=0;x<Grid;x++)
                        {
                            uint pixel=GetPixel(memory,x,y);
                            double r=pixel&0xFF,g=(pixel>>8)&0xFF,b=(pixel>>16)&0xFF;
                            total+=(.2126*r+.7152*g+.0722*b)/255.0;
                            count++;
                        }
                    }
                }
            }
            finally
            {
                SelectObject(memory,old);
                DeleteObject(bitmap);
                DeleteDC(memory);
                ReleaseDC(IntPtr.Zero,screenDc);
            }
            return count==0?-1:total/count;
        }
    }
}
