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
        // When the panel comes out of a dock piece, the first shape is that piece itself.
        bool useSource,detach,veiled;
        Native.RectI? source;
        Rect srcRect;
        GlassWindow piece;
        double pieceOffset,lastPieceCut=-1;
        readonly LinearGradientBrush pieceMask=new LinearGradientBrush {MappingMode=BrushMappingMode.Absolute,StartPoint=new Point(0,0),EndPoint=new Point(1,0)};
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
            source=window.MorphSource;detach=window.MorphDetach;
            if(piece!=window.MorphPiece)ReleasePiece();
            piece=window.MorphPiece;
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
            extra=0;grown=false;useSource=false;
            var handle=new WindowInteropHelper(window).Handle;
            Native.RectI rect;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out rect))return;
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            // The window reaches exactly to the bar's outer edge, so the drop leaves the bar instead of forming over it.
            var monitor=System.Windows.Forms.Screen.FromHandle(handle);
            int? bar=DockHost.BarEdge(monitor.Bounds,fromTop);
            int edge=bar??(fromTop?monitor.WorkingArea.Top:monitor.WorkingArea.Bottom);
            double gap=(fromTop?rect.Top-edge:edge-rect.Bottom)/scale;
            useSource=false;
            if(source.HasValue&&piece!=null)
            {
                // The window has to reach the far side of the piece for the shape to start inside it.
                double reach=(fromTop?rect.Top-source.Value.Top:source.Value.Bottom-rect.Bottom)/scale+2;
                if(reach>=6){gap=reach;useSource=true;}
            }
            if(gap<6)return;
            extra=Math.Min(useSource?220:140,gap);
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
            if(useSource)
            {
                double windowTop=fromTop?rect.Top-pixels:rect.Top,total=fixedHeight+extra;
                double sx=(source.Value.Left-rect.Left)/scale,sw=(source.Value.Right-source.Value.Left)/scale,sy=(source.Value.Top-windowTop)/scale,sh=(source.Value.Bottom-source.Value.Top)/scale;
                srcRect=new Rect(sx,fromTop?total-(sy+sh):sy,sw,sh);
                srcRect.Intersect(new Rect(0,0,fixedWidth,total));
                if(srcRect.IsEmpty||srcRect.Width<6||srcRect.Height<6)useSource=false;
            }
            if(useSource)
            {
                var inside=piece.Surface.Child as FrameworkElement;pieceOffset=0;
                try{if(inside!=null&&inside.IsLoaded)pieceOffset=inside.TranslatePoint(new Point(0,0),piece.Surface).X;}catch(InvalidOperationException){pieceOffset=0;}
            }
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
            if(useSource&&!closing)ApplyPiece(1);
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
            if(useSource)
            {
                // The piece itself is the first shape: it swells upward on one spring while the other edges follow, a piece that
                // stays behind is stretched toward the panel on a neck that pinches off, and the picture comes in behind a wavefront.
                double a=Spring(0,.34,at,.86),d=Spring(.03,.5,at,.84);
                double neck=detach?1-Smooth(.16,.42,at):0;
                double glow=Smooth(0,.05,at)*(1-Smooth(.08,.3,at))*.7;
                double front=at<.24?-1:Math.Min(1,(at-.24)/.24);
                Apply(a,d,1,neck,glow,front,front<0?0:1);
                ApplyPiece(Math.Max(0,Math.Min(1,a)));
                return;
            }
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
            bool fromPiece=useSource&&grown;
            double orb=Math.Min(Orb,Math.Min(width,height));
            double cx=Math.Max(orb/2,Math.Min(width-orb/2,anchor*width));
            across=Math.Max(0,Math.Min(1.06,across));
            down=Math.Max(0,Math.Min(1.06,down));
            // Before it widens the drop forms at the bar and is pulled up on its neck, so it reads as a drop first.
            double size=orb*(.35+.65*formed),pull=orb*.4*formed*(1-Smooth(0,.45,Math.Max(across,down)));
            if(fromPiece){cx=Math.Max(srcRect.Left,Math.Min(srcRect.Right,srcRect.Left+srcRect.Width/2));}
            // The drop sits on the bar's edge and moves away from it, toward where the panel opens.
            double dropTop=total-size-pull;
            double left=Mix(cx-size/2,0,across),right=Mix(cx+size/2,width,across);
            double top=Mix(dropTop,0,down),bottom=Mix(dropTop+size,height,down);
            if(fromPiece)
            {
                left=Mix(srcRect.Left,0,across);right=Mix(srcRect.Right,width,across);
                top=Mix(srcRect.Top,0,down);bottom=Mix(srcRect.Bottom,height,down);
            }
            var shape=new Rect(left,top,Math.Max(2,right-left),Math.Max(2,bottom-top));
            shape.Intersect(new Rect(0,0,width,total));
            if(shape.IsEmpty)return;
            double radius=Math.Min(fromPiece?Mix(Math.Min(srcRect.Width,srcRect.Height)/2,window.Radius,Math.Min(1,across)):window.Radius,Math.Min(shape.Width,shape.Height)/2);
            double neckBase=fromPiece?srcRect.Top+srcRect.Height*.4:total;
            body.Rect=shape;
            body.RadiusX=body.RadiusY=radius;
            var drawn=shape;
            Rect region=shape;
            // The neck holds the drop to the bar and pinches off as the drop swells.
            while(outline.Children.Count>1)outline.Children.RemoveAt(1);
            if(pinch>.04&&shape.Bottom<neckBase-1)
            {
                double topHalf=Math.Min(shape.Width*.2,7)*pinch,baseHalf=Math.Min(shape.Width*.3,13)*pinch;
                double y0=shape.Bottom-radius*.5,mid=(y0+neckBase)/2;
                var neck=new StreamGeometry();
                using(var dc=neck.Open())
                {
                    dc.BeginFigure(new Point(cx-topHalf,y0),true,true);
                    dc.LineTo(new Point(cx+topHalf,y0),true,false);
                    dc.BezierTo(new Point(cx+topHalf*.5,mid),new Point(cx+baseHalf*.4,mid),new Point(cx+baseHalf,neckBase),true,false);
                    dc.LineTo(new Point(cx-baseHalf,neckBase),true,false);
                    dc.BezierTo(new Point(cx-baseHalf*.4,mid),new Point(cx-topHalf*.5,mid),new Point(cx-topHalf,y0),true,false);
                }
                neck.Freeze();
                outline.Children.Add(neck);
                region=Rect.Union(region,new Rect(cx-baseHalf,y0,baseHalf*2,neckBase-y0));
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

        // The piece the panel came from: a piece that is the panel while it is open is hidden; one that stays gives up
        // part of its width, folding from the left, and takes it back as the panel goes.
        void ApplyPiece(double k)
        {
            if(piece==null)return;
            if(!detach){if(!veiled){veiled=true;piece.Veil(true);}return;}
            double width=piece.Surface.ActualWidth,height=piece.Surface.ActualHeight;
            if(width<8||height<8)return;
            if(k<.003){if(lastPieceCut>=0)ClearPiece();return;}
            double target=Math.Min(width*.85,Math.Max(height*1.8,width*.56));
            double shown=width-(width-target)*k;
            var cut=new Rect(width-shown,0,shown,height);
            piece.BackdropClip=cut;piece.RegionClip=cut;
            double radius=Math.Min(piece.Radius,Math.Min(cut.Width,cut.Height)/2);
            piece.PieceShape(cut,radius);
            var inside=piece.Surface.Child;
            if(inside!=null)
            {
                // The left edge of what is left fades out, so a piece of text or an icon is not cut in half.
                pieceMask.MappingMode=BrushMappingMode.Absolute;
                double left=cut.Left-pieceOffset;
                pieceMask.StartPoint=new Point(left,0);pieceMask.EndPoint=new Point(left+Math.Min(30,cut.Width*.4),0);
                if(pieceMask.GradientStops.Count==0){pieceMask.GradientStops.Add(new GradientStop(Colors.Transparent,0));pieceMask.GradientStops.Add(new GradientStop(Colors.Black,1));}
                inside.OpacityMask=pieceMask;
            }
            if(Math.Abs(lastPieceCut-Math.Round(cut.Left))>=1||lastPieceCut<0){lastPieceCut=Math.Round(cut.Left);Native.Round(piece,piece.Radius);}
        }
        void ClearPiece()
        {
            lastPieceCut=-1;
            if(piece==null)return;
            piece.BackdropClip=null;piece.RegionClip=null;
            piece.PieceShape(null,0);
            var inside=piece.Surface.Child;if(inside!=null)inside.OpacityMask=null;
            Native.Round(piece,piece.Radius);
        }
        // The piece goes back to its own shape (the panel was hidden or closed).
        internal void ReleasePiece()
        {
            if(piece==null)return;
            ClearPiece();
            if(veiled){veiled=false;piece.Veil(false);}
            piece=null;
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
