using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    // Sample the composed, empty panel margins, never text or media content.
    // Two local brushes change; the glass and selected controls keep their palette.
    internal sealed class BackdropInk : IDisposable
    {
        readonly GlassWindow owner;
        readonly Func<Task<double?>> fixtureSample;
        readonly DispatcherTimer timer;
        readonly BackdropInkPalette palette=new BackdropInkPalette();
        readonly BackdropInkTone tone=new BackdropInkTone();
        readonly Stopwatch elapsed=Stopwatch.StartNew();
        internal SolidColorBrush Primary {get{return palette.Primary;}}
        internal SolidColorBrush Secondary {get{return palette.Secondary;}}
        bool disposed,busy;
        int generation;
        internal int SampleCount {get;private set;}
        internal bool Running {get{return timer.IsEnabled;}}
        internal bool DarkInk {get{return tone.Dark;}}

        internal BackdropInk(GlassWindow owner,Func<Task<double?>> fixtureSample=null)
        {
            if(fixtureSample!=null&&!Program.Inspectable)throw new InvalidOperationException("Fixture sampler requires inspectable mode");
            this.owner=owner;
            this.fixtureSample=fixtureSample;
            timer=new DispatcherTimer(DispatcherPriority.Background,owner.Dispatcher) {Interval=TimeSpan.FromMilliseconds(90)};
            timer.Tick+=Tick;
            owner.IsVisibleChanged+=VisibilityChanged;
            owner.Closed+=Closed;
            Refresh();
        }
        bool Eligible
        {
            get
            {
                return fixtureSample!=null||!Program.TestProcess&&ReleaseFeatures.GlassUI&&Preferences.Current.Blur&&!SystemParameters.HighContrast&&!owner.OpaqueSurface&&
                    (PanelMaterial.ReadablePanel(owner)||PanelMaterial.NotificationPopup(owner))&&(!(owner is DetailWindow)||((DetailWindow)owner).PageKey!="sound");
            }
        }
        internal void Refresh()
        {
            if(disposed)return;
            if(!owner.IsVisible||owner.IsDismissing)
            {
                ++generation;
                timer.Stop();
                tone.CancelPending();
                palette.Stop();
                return;
            }
            if(!Eligible)
            {
                ++generation;
                timer.Stop();
                if(tone.HasSample)Apply(owner.Surface,true);
                tone.Reset();
                return;
            }
            if(tone.HasSample)Apply(owner.Surface,false);
            if(!timer.IsEnabled)
            {
                timer.Interval=TimeSpan.FromMilliseconds(90);
                timer.Start();
            }
        }
        void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs args){Refresh();}
        void Closed(object sender,EventArgs args){Dispose();}
        async void Tick(object sender,EventArgs args)
        {
            if(disposed||!owner.IsVisible||owner.IsDismissing||!Eligible){Refresh();return;}
            timer.Interval=TimeSpan.FromMilliseconds(950);
            if(busy)return;
            // A fading panel includes the desktop in the readback. It is not
            // the final material that its text must contrast against.
            if(owner.Opacity<.98||owner.Surface.Opacity<.98)
            {
                timer.Interval=TimeSpan.FromMilliseconds(90);
                tone.CancelPending();
                return;
            }
            var handle=new WindowInteropHelper(owner).Handle;
            Native.RectI bounds;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out bounds)||bounds.Right-bounds.Left<80||bounds.Bottom-bounds.Top<60)return;
            int token=generation;
            busy=true;
            try
            {
                var luminance=fixtureSample!=null?await fixtureSample():await BackgroundWork.Run(()=>Sample(bounds));
                if(disposed||token!=generation||!owner.IsVisible||owner.IsDismissing||!Eligible||!luminance.HasValue)return;
                Native.RectI current;
                if(!Native.GetWindowRect(handle,out current)||current.Left!=bounds.Left||current.Top!=bounds.Top||current.Right!=bounds.Right||current.Bottom!=bounds.Bottom||owner.Opacity<.98||owner.Surface.Opacity<.98)
                {
                    tone.CancelPending();
                    timer.Interval=TimeSpan.FromMilliseconds(90);
                    return;
                }
                ++SampleCount;
                if(tone.Observe(luminance.Value,elapsed.Elapsed.TotalMilliseconds))palette.Set(tone.Dark,true);
                if(tone.Pending)timer.Interval=TimeSpan.FromMilliseconds(200);
                Apply(owner.Surface,false);
            }
            catch(Exception error){AppLog.Once("backdrop-ink",error.ToString());}
            finally{busy=false;}
        }
        internal static bool ChooseDark(double luminance,bool? previous)
        {
            return BackdropInkTone.ChooseDark(luminance,previous);
        }
        Brush Convert(Brush brush,bool restore)
        {
            if(brush==Primary)return restore?Paint.Text:Primary;
            if(brush==Secondary)return restore?Paint.Muted:Secondary;
            if(restore)return brush;
            if(brush==Paint.Text||brush==Glass95.Text||brush==PanelMaterial.DarkText)return Primary;
            if(brush==Paint.Muted||brush==Glass95.Muted||brush==PanelMaterial.DarkMuted)return Secondary;
            return brush;
        }
        void Apply(DependencyObject node,bool restore)
        {
            if(node is MediaPlayerView||node is PillSlider||node is AudioOutputView)return;
            var text=node as TextBlock;
            if(text!=null)text.Foreground=Convert(text.Foreground,restore);
            var control=node as Control;
            if(control!=null)control.Foreground=Convert(control.Foreground,restore);
            var glyph=node as Glyph;
            if(glyph!=null)
            {
                var next=Convert(glyph.Color,restore);
                if(next!=glyph.Color){glyph.Color=next;glyph.InvalidateVisual();}
            }
            foreach(object child in LogicalTreeHelper.GetChildren(node))
            {
                var element=child as DependencyObject;
                if(element!=null)Apply(element,restore);
            }
        }
        static double Channel(byte value){double v=value/255.0;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
        static double? Sample(Native.RectI bounds)
        {
            IntPtr dc=GetDC(IntPtr.Zero);
            if(dc==IntPtr.Zero)return null;
            try
            {
                int width=bounds.Right-bounds.Left,height=bounds.Bottom-bounds.Top;
                int sampledWidth=Math.Min(4096,width);
                // One small readback. Calling screen GetPixel repeatedly can
                // stall the GPU separately for every point during video playback.
                using(var strip=new System.Drawing.Bitmap(sampledWidth,3,System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                {
                    using(var graphics=System.Drawing.Graphics.FromImage(strip))
                    {
                        IntPtr target=graphics.GetHdc();
                        try
                        {
                            SetStretchBltMode(target,3);
                            if(!StretchBlt(target,0,0,sampledWidth,3,dc,bounds.Left,bounds.Top+height/5,width,Math.Max(1,height*3/5),0x00CC0020))return null;
                        }
                        finally{graphics.ReleaseHdc(target);}
                    }
                    int inset=Math.Max(1,Math.Min(sampledWidth/4,14*sampledWidth/width));
                    var values=new double[6];
                    int count=0;
                    for(int y=0;y<3;y++)
                        foreach(int x in new[]{inset,sampledWidth-inset-1})
                        {
                            var color=strip.GetPixel(x,y);
                            values[count++]=.2126*Channel(color.R)+.7152*Channel(color.G)+.0722*Channel(color.B);
                        }
                    Array.Sort(values);
                    return values[3];
                }
            }
            finally{ReleaseDC(IntPtr.Zero,dc);}
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            ++generation;
            timer.Stop();
            timer.Tick-=Tick;
            owner.IsVisibleChanged-=VisibilityChanged;
            owner.Closed-=Closed;
            tone.CancelPending();
            palette.Stop();
        }
        [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")]static extern int SetStretchBltMode(IntPtr dc,int mode);
        [DllImport("gdi32.dll")]static extern bool StretchBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int sx,int sy,int sw,int sh,uint operation);
    }
}
