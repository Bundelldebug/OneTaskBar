using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace OneTaskBar
{
    internal static class ScrollEdges
    {
        internal static void Attach(ScrollViewer viewer)
        {
            LinearGradientBrush mask=null;bool top=false,bottom=false;double fraction=0;ScrollBlur edge=null;AdornerLayer layer=null;
            Action update=()=>{
                bool a=viewer.VerticalOffset>1,b=viewer.ScrollableHeight-viewer.VerticalOffset>1;if(edge!=null)edge.Update(a,b);
                if(viewer.ScrollableHeight<=1||viewer.ViewportHeight<=0){if(mask!=null)viewer.OpacityMask=null;top=bottom=false;return;}
                double f=Math.Min(.10,18/Math.Max(1,viewer.ViewportHeight));if(mask==null)mask=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1),GradientStops=new GradientStopCollection {new GradientStop(Colors.Black,0),new GradientStop(Colors.Black,f),new GradientStop(Colors.Black,1-f),new GradientStop(Colors.Black,1)}};
                if(viewer.OpacityMask==mask&&a==top&&b==bottom&&Math.Abs(f-fraction)<.0001)return;top=a;bottom=b;fraction=f;mask.GradientStops[0].Color=a?Colors.Transparent:Colors.Black;mask.GradientStops[1].Offset=f;mask.GradientStops[2].Offset=1-f;mask.GradientStops[3].Color=b?Colors.Transparent:Colors.Black;viewer.OpacityMask=mask;
            };viewer.ScrollChanged+=(s,e)=>update();viewer.SizeChanged+=(s,e)=>update();viewer.Loaded+=(s,e)=>{Preferences.Changed+=update;layer=AdornerLayer.GetAdornerLayer(viewer);if(layer!=null&&edge==null){edge=new ScrollBlur(viewer);layer.Add(edge);}update();};viewer.Unloaded+=(s,e)=>{Preferences.Changed-=update;if(edge!=null){edge.Release();if(layer!=null)layer.Remove(edge);edge=null;layer=null;}};
        }
        sealed class ScrollBlur : Adorner
        {
            readonly ScrollViewer viewer;readonly VisualBrush topBrush=new VisualBrush(),bottomBrush=new VisualBrush();readonly Border top,bottom;readonly VisualCollection children;Rect clip;
            static Brush Fade(bool first){var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};brush.GradientStops.Add(new GradientStop(Colors.Transparent,0));brush.GradientStops.Add(new GradientStop(Color.FromArgb(150,0,0,0),first?.3:.7));brush.GradientStops.Add(new GradientStop(Colors.Transparent,1));brush.Freeze();return brush;}
            internal ScrollBlur(ScrollViewer owner):base(owner){viewer=owner;IsHitTestVisible=false;children=new VisualCollection(this);top=new Border {Background=topBrush,Effect=new BlurEffect {Radius=5,RenderingBias=RenderingBias.Performance},OpacityMask=Fade(true),IsHitTestVisible=false};bottom=new Border {Background=bottomBrush,Effect=new BlurEffect {Radius=5,RenderingBias=RenderingBias.Performance},OpacityMask=Fade(false),IsHitTestVisible=false};children.Add(top);children.Add(bottom);foreach(var brush in new[]{topBrush,bottomBrush}){brush.ViewboxUnits=BrushMappingMode.Absolute;brush.Stretch=Stretch.Fill;}}
            internal void Update(bool first,bool last)
            {
                var visual=viewer.Content as Visual;bool enabled=Preferences.Current.Blur&&!SystemParameters.HighContrast&&visual!=null;top.Visibility=enabled&&first?Visibility.Visible:Visibility.Collapsed;bottom.Visibility=enabled&&last?Visibility.Visible:Visibility.Collapsed;topBrush.Visual=bottomBrush.Visual=visual;double width=Math.Max(1,viewer.ViewportWidth),height=Math.Max(1,viewer.ViewportHeight),strip=Math.Min(18,height/4);topBrush.Viewbox=new Rect(viewer.HorizontalOffset,viewer.VerticalOffset,width,strip);bottomBrush.Viewbox=new Rect(viewer.HorizontalOffset,Math.Max(0,viewer.VerticalOffset+height-strip),width,strip);var rect=new Rect(0,0,Math.Max(0,viewer.ActualWidth),Math.Max(0,viewer.ActualHeight));if(rect!=clip){clip=rect;Clip=new RectangleGeometry(rect);}InvalidateArrange();
            }
            protected override int VisualChildrenCount {get{return children==null?0:children.Count;}}
            protected override Visual GetVisualChild(int index){return children[index];}
            protected override Size MeasureOverride(Size available){var size=new Size(Math.Max(0,viewer.ViewportWidth),Math.Min(18,Math.Max(0,viewer.ViewportHeight)/4));top.Measure(size);bottom.Measure(size);return viewer.RenderSize;}
            protected override Size ArrangeOverride(Size final){double width=Math.Max(0,viewer.ViewportWidth),height=Math.Max(0,viewer.ViewportHeight),strip=Math.Min(18,height/4);top.Arrange(new Rect(0,0,width,strip));bottom.Arrange(new Rect(0,height-strip,width,strip));return final;}
            internal void Release(){topBrush.Visual=bottomBrush.Visual=null;}
        }
    }
}
