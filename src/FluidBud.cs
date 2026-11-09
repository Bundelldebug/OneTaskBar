using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;

namespace OneTaskBar
{
    // The Fluid UI's arrival, step by step after the reference recording:
    //   1. a white drop forms on the bar's edge and swells into a glass drop, held to the bar by a thin neck;
    //   2. the drop widens into an empty glass pill while the neck pinches off;
    //   3. the pill grows to the size of the panel on the island's spring;
    //   4. a soft white wavefront runs through the panel from the bar's side, and the picture comes in behind it.
    // The panel's real outline moves: the backdrop, the window and the picture all follow it, nothing is scaled or
    // squashed, and the bar stays still. The window is stretched to the bar's edge for the length of the motion so the
    // drop really starts in the bar. Closing plays the same steps backwards, faster, back into the bar.
    internal sealed class FluidBud
    {
        const double Orb=30,Total=.62,CloseTime=.34;
        readonly GlassWindow window;
        readonly Stopwatch watch=new Stopwatch();
        readonly RectangleGeometry body=new RectangleGeometry();
        readonly GeometryGroup outline=new GeometryGroup {FillRule=FillRule.Nonzero};
        readonly LinearGradientBrush reveal=new LinearGradientBrush();
        readonly GradientStop revealBehind=new GradientStop(Colors.Black,0),revealAhead=new GradientStop(Colors.Transparent,1);
        FluidFlare flare;
        UIElement masked;
        bool running,closing,hooked;
        double anchor,extra;
        bool fromTop,grown;
        Action finished;
        Native.RectI original;
        double fixedWidth,fixedHeight;
        Rect lastCut;
        bool roundedOnce;
        internal FluidBud(GlassWindow owner)
        {
            window=owner;
            outline.Children.Add(body);
            reveal.GradientStops.Add(revealBehind);
            reveal.GradientStops.Add(revealAhead);
        }
        internal bool Active {get{return running;}}

        internal void Open(Point at,bool top)
        {
            Begin(at,top,false,null);
        }
        internal void Close(Point at,bool top,Action done)
        {
            Begin(at,top,true,done);
        }

        // Where the last motion was on the arrival's timeline, and when it was there. A motion that is turned around while it
        // runs (the panel pressed again, Escape, a quick reopen) continues from that point instead of jumping to the end.
        double lastAt;
        long lastStamp;
        double from,closeSpan;
        bool Interrupted {get{return lastStamp!=0&&Stopwatch.GetTimestamp()-lastStamp<Stopwatch.Frequency/8&&lastAt>0&&lastAt<Total;}}

        void Begin(Point at,bool top,bool close,Action done)
        {
            bool resume=Interrupted;
            from=close?(resume?Math.Min(lastAt,.54):.54):(resume?lastAt:0);
            closeSpan=Math.Max(.06,CloseTime*from/.54);
            Cancel();
            fromTop=top;
            // On the first open the window has just been shown and may not be laid out yet; without a size there is no
            // shape to animate, which is why a first click used to show no motion at all.
            if(window.Surface.ActualWidth<2||window.Surface.ActualHeight<2)window.UpdateLayout();
            GrowToBar();
            anchor=at.X;
            closing=close;
            finished=done;
            running=true;
            roundedOnce=false;
            // The picture is revealed along the direction the panel opens, away from the bar, measured over the whole panel
            // rather than over the picture's own box, so a line of text near the far edge waits for the front to reach it.
            reveal.MappingMode=BrushMappingMode.Absolute;
            var inside=window.Surface.Child as FrameworkElement;
            double offset=0,panel=grown?fixedHeight:window.Surface.ActualHeight;
            if(inside!=null&&inside.IsLoaded)
            {
                try{offset=inside.TranslatePoint(new Point(0,0),window.Surface).Y;}
                catch(InvalidOperationException){offset=0;}
            }
            reveal.StartPoint=new Point(0,fromTop?-offset:panel-offset);
            reveal.EndPoint=new Point(0,fromTop?panel-offset:-offset);
            // Closing plays the arrival backwards, faster: the picture recedes toward the bar behind the light, the panel
            // folds into a pill, the pill into a drop, and the drop goes back into the bar on its neck.
            AttachFlare();
            watch.Restart();
            Step(from);
            if(!hooked){hooked=true;CompositionTarget.Rendering+=Frame;}
        }

        void AttachFlare()
        {
            var layer=AdornerLayer.GetAdornerLayer(window.Surface);
            if(layer==null)return;
            flare=new FluidFlare(window.Surface);
            layer.Add(flare);
        }
        void DetachFlare()
        {
            if(flare==null)return;
            var layer=AdornerLayer.GetAdornerLayer(window.Surface);
            if(layer!=null)layer.Remove(flare);
            flare=null;
        }

        // The panel floats above the bar. For the drop to come out of the bar, the window is made as tall as the gap and
        // the panel keeps its place, at a fixed size so its contents are not measured again; the gap is transparent and
        // only the drop passes through it.
        void GrowToBar()
        {
            extra=0;grown=false;
            var handle=new WindowInteropHelper(window).Handle;
            Native.RectI rect;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out rect))return;
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            // The window reaches exactly to the bar's outer edge, so the drop leaves the bar instead of forming over it.
            var monitor=System.Windows.Forms.Screen.FromHandle(handle);
            int? bar=DockHost.BarEdge(monitor.Bounds,fromTop);
            int edge=bar??(fromTop?monitor.WorkingArea.Top:monitor.WorkingArea.Bottom);
            double gap=(fromTop?rect.Top-edge:edge-rect.Bottom)/scale;
            if(gap<6)return;
            extra=Math.Min(140,gap);
            int pixels=(int)Math.Round(extra*scale);
            original=rect;
            var surface=window.Surface;
            if(surface.ActualWidth<2||surface.ActualHeight<2)return;
            fixedWidth=surface.ActualWidth;fixedHeight=surface.ActualHeight;
            surface.Width=fixedWidth;surface.Height=fixedHeight;
            surface.VerticalAlignment=fromTop?VerticalAlignment.Bottom:VerticalAlignment.Top;
            surface.HorizontalAlignment=HorizontalAlignment.Left;
            surface.Margin=new Thickness(0);
            Native.SetWindowPos(handle,IntPtr.Zero,rect.Left,fromTop?rect.Top-pixels:rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top+pixels,0x14);
            grown=true;
        }
        void ShrinkBack()
        {
            if(!grown)return;
            grown=false;extra=0;
            var surface=window.Surface;
            surface.ClearValue(FrameworkElement.WidthProperty);
            surface.ClearValue(FrameworkElement.HeightProperty);
            surface.VerticalAlignment=VerticalAlignment.Stretch;
            surface.HorizontalAlignment=HorizontalAlignment.Stretch;
            var handle=new WindowInteropHelper(window).Handle;
            if(handle!=IntPtr.Zero)Native.SetWindowPos(handle,IntPtr.Zero,original.Left,original.Top,original.Right-original.Left,original.Bottom-original.Top,0x14);
        }

        void Frame(object sender,EventArgs e)
        {
            var rendering=e as RenderingEventArgs;
            if(rendering==null)return;
            double t=watch.Elapsed.TotalSeconds;
            bool over=closing?t>=closeSpan:from+t>=Total;
            if(!over)
            {
                // Closing runs the arrival's timeline backwards from where it stands, so it answers at once; opening runs it
                // forward from where it stands.
                Step(closing?from*(1-t/closeSpan):from+t);
                return;
            }
            lastStamp=0;
            var done=finished;
            bool wasClosing=closing;
            finished=null;
            Stop();
            // A closed window is restored after it is hidden; an open one lands on its exact shape.
            if(!wasClosing)Restore();
            if(done!=null)done();
            if(wasClosing&&!running)Restore();
        }

        static double Smooth(double a,double b,double x)
        {
            double t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));
            return t*t*(3-2*t);
        }
        static double Mix(double a,double b,double t){return a+(b-a)*t;}
        // The island's spring over a span of time, settling a little past its target.
        static double Spring(double start,double end,double t,double damping)
        {
            return SpringEase.Position(Math.Max(0,Math.Min(1,(t-start)/(end-start))),damping);
        }

        // One moment of the arrival's timeline, in seconds; closing walks the same timeline back to 0.
        void Step(double at)
        {
            lastAt=at;
            lastStamp=Stopwatch.GetTimestamp();
            double drop=Smooth(0,.12,at);
            double across=Spring(.1,.38,at,.86),down=Spring(.2,.6,at,.84);
            double pinch=1-Smooth(.12,.3,at);
            // White at first, clear glass once the drop has a shape of its own.
            double white=Smooth(0,.04,at)*(1-Smooth(.1,.34,at));
            double wave=at<.32?-1:Math.Min(1,(at-.32)/.22);
            Apply(across,down,drop,pinch,white,wave,wave<0?0:1);
        }

        // The outline for the given amounts. It is built as if the bar were below the panel, with the bar at the bottom edge
        // of the window; a window that opens downward is flipped afterwards.
        void Apply(double across,double down,double formed,double pinch,double white,double wave,double content)
        {
            double width=grown?fixedWidth:window.Surface.ActualWidth,height=grown?fixedHeight:window.Surface.ActualHeight;
            if(width<2||height<2)return;
            double total=height+extra;
            double orb=Math.Min(Orb,Math.Min(width,height));
            double cx=Math.Max(orb/2,Math.Min(width-orb/2,anchor*width));
            across=Math.Max(0,Math.Min(1.06,across));
            down=Math.Max(0,Math.Min(1.06,down));
            // Before it widens the drop forms at the bar and is pulled up on its neck, so it reads as a drop first.
            double size=orb*(.35+.65*formed),pull=orb*.4*formed*(1-Smooth(0,.45,Math.Max(across,down)));
            // The drop sits on the bar's edge and moves away from it, toward where the panel opens.
            double dropTop=total-size-pull;
            double left=Mix(cx-size/2,0,across),right=Mix(cx+size/2,width,across);
            double top=Mix(dropTop,0,down),bottom=Mix(dropTop+size,height,down);
            var shape=new Rect(left,top,Math.Max(2,right-left),Math.Max(2,bottom-top));
            shape.Intersect(new Rect(0,0,width,total));
            if(shape.IsEmpty)return;
            double radius=Math.Min(window.Radius,Math.Min(shape.Width,shape.Height)/2);
            body.Rect=shape;
            body.RadiusX=body.RadiusY=radius;
            var drawn=shape;
            Rect region=shape;
            // The neck holds the drop to the bar and pinches off as the drop swells.
            while(outline.Children.Count>1)outline.Children.RemoveAt(1);
            if(pinch>.04&&shape.Bottom<total-1)
            {
                double topHalf=Math.Min(shape.Width*.2,7)*pinch,baseHalf=Math.Min(shape.Width*.3,13)*pinch;
                double y0=shape.Bottom-radius*.5,mid=(y0+total)/2;
                var neck=new StreamGeometry();
                using(var dc=neck.Open())
                {
                    dc.BeginFigure(new Point(cx-topHalf,y0),true,true);
                    dc.LineTo(new Point(cx+topHalf,y0),true,false);
                    dc.BezierTo(new Point(cx+topHalf*.5,mid),new Point(cx+baseHalf*.4,mid),new Point(cx+baseHalf,total),true,false);
                    dc.LineTo(new Point(cx-baseHalf,total),true,false);
                    dc.BezierTo(new Point(cx-baseHalf*.4,mid),new Point(cx-topHalf*.5,mid),new Point(cx-topHalf,y0),true,false);
                }
                neck.Freeze();
                outline.Children.Add(neck);
                region=Rect.Union(region,new Rect(cx-baseHalf,y0,baseHalf*2,total-y0));
            }
            if(flare!=null)flare.Set(drawn,fromTop,height,wave,white,radius);
            if(fromTop)
            {
                outline.Transform=new MatrixTransform(1,0,0,-1,0,total);
                region=new Rect(region.X,total-region.Bottom,region.Width,region.Height);
                shape=new Rect(shape.X,total-shape.Bottom,shape.Width,shape.Height);
            }
            else outline.Transform=Transform.Identity;
            region.Intersect(new Rect(0,0,width,total));
            window.BackdropClip=shape;
            window.RegionClip=region;
            var host=window.Content as UIElement;
            if(host!=null)host.Clip=outline;
            ShowContent(wave,content);
            // Setting the window region is the costly part of a frame; it is only redone when the region moved a whole pixel.
            var cut=new Rect(Math.Round(region.X),Math.Round(region.Y),Math.Round(region.Width),Math.Round(region.Height));
            if(cut!=lastCut||!roundedOnce){lastCut=cut;roundedOnce=true;Native.Round(window,window.Radius);}
        }

        // The picture comes in behind the wavefront: a soft mask follows the front, so what the front has passed is shown.
        void ShowContent(double wave,double content)
        {
            var inside=window.Surface.Child;
            if(inside==null)return;
            if(wave<0)
            {
                if(masked!=null){masked.OpacityMask=null;masked=null;}
                inside.Opacity=0;
                return;
            }
            inside.Opacity=1;
            if(wave>=1)
            {
                if(masked!=null){masked.OpacityMask=null;masked=null;}
                return;
            }
            double front=wave*1.3-.02;
            revealBehind.Offset=Math.Max(0,Math.Min(1,front-.28));
            revealAhead.Offset=Math.Max(0,Math.Min(1,front));
            if(masked==null){inside.OpacityMask=reveal;masked=inside;}
        }

        void Stop()
        {
            if(hooked){hooked=false;CompositionTarget.Rendering-=Frame;}
            running=false;
            watch.Stop();
        }

        void Restore()
        {
            DetachFlare();
            if(masked!=null){masked.OpacityMask=null;masked=null;}
            window.BackdropClip=null;
            window.RegionClip=null;
            var host=window.Content as UIElement;
            if(host!=null)host.Clip=null;
            var inside=window.Surface.Child;
            if(inside!=null)inside.Opacity=1;
            ShrinkBack();
            Native.Round(window,window.Radius);
        }

        // Stops wherever it is and puts the window back to its normal shape.
        internal void Cancel()
        {
            bool was=running||hooked||window.RegionClip.HasValue||grown||flare!=null;
            Stop();
            finished=null;
            if(was)Restore();
        }
    }
}
