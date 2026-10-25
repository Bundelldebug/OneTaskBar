using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace OneTaskBar
{
    internal static class ScrollEdges
    {
        // Feather the viewport directly without redrawing the full content for each edge.
        internal static void Attach(ScrollViewer viewer)
        {
            LinearGradientBrush mask=null;bool top=false,bottom=false;double fraction=0;
            Action update=()=>{
                if(SystemParameters.HighContrast||viewer.ScrollableHeight<=1||viewer.ViewportHeight<=0){if(mask!=null)viewer.OpacityMask=null;top=bottom=false;return;}
                bool a=viewer.VerticalOffset>1,b=viewer.ScrollableHeight-viewer.VerticalOffset>1;double f=Math.Min(.10,18/Math.Max(1,viewer.ViewportHeight));
                if(mask==null)mask=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1),GradientStops=new GradientStopCollection {new GradientStop(Colors.Black,0),new GradientStop(Colors.Black,f),new GradientStop(Colors.Black,1-f),new GradientStop(Colors.Black,1)}};
                if(viewer.OpacityMask==mask&&a==top&&b==bottom&&Math.Abs(f-fraction)<.0001)return;
                top=a;bottom=b;fraction=f;mask.GradientStops[0].Color=a?Colors.Transparent:Colors.Black;mask.GradientStops[1].Offset=f;mask.GradientStops[2].Offset=1-f;mask.GradientStops[3].Color=b?Colors.Transparent:Colors.Black;viewer.OpacityMask=mask;
            };
            viewer.ScrollChanged+=(s,e)=>update();viewer.SizeChanged+=(s,e)=>update();viewer.Loaded+=(s,e)=>update();viewer.Unloaded+=(s,e)=>{if(viewer.OpacityMask==mask)viewer.OpacityMask=null;};
        }
    }
}