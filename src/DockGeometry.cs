using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace OneTaskBar
{
    internal static class DockGeometry
    {
        internal static int Edge {get{return Preferences.Current.DockEdge>=0&&Preferences.Current.DockEdge<=3?Preferences.Current.DockEdge:3;}}
        internal static bool Vertical(int edge){return edge==0||edge==2;}
        internal const double SideStatusWidth=44;
        internal static double SideStatusLength(double powerWidth){return powerWidth>0?128:106;}
        internal static string ClockLabel(DateTime time,bool seconds,bool vertical){return time.ToString(vertical?seconds?"tt\nh:mm:ss\nMM-dd":"tt\nh:mm\nMM-dd":seconds?"tt h:mm:ss\nyyyy-MM-dd":"tt h:mm\nyyyy-MM-dd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));}
        internal static Rect Rect(System.Drawing.Rectangle screen,double scale,int edge,Rect logical)
        {
            double width=screen.Width/scale,height=screen.Height/scale;
            if(edge==1)return new Rect(logical.X,height-logical.Bottom,logical.Width,logical.Height);
            if(edge==0)return new Rect(width-logical.Bottom,logical.X,logical.Height,logical.Width);
            if(edge==2)return new Rect(logical.Y,logical.X,logical.Height,logical.Width);return logical;
        }
        internal static double Reserved(System.Drawing.Rectangle screen,Native.RectI work,double scale,int edge)
        {
            double gap=edge==0?work.Left-screen.Left:edge==1?work.Top-screen.Top:edge==2?screen.Right-work.Right:screen.Bottom-work.Bottom;return Math.Max(68,gap/scale);
        }
        internal static Native.RectI Reveal(System.Drawing.Rectangle screen,int edge)
        {
            return new Native.RectI {Left=edge==2?screen.Right-2:screen.Left,Top=edge==3?screen.Bottom-2:screen.Top,Right=edge==0?screen.Left+2:screen.Right,Bottom=edge==1?screen.Top+2:screen.Bottom};
        }
        internal static Rect Popup(System.Drawing.Rectangle screen,double scale,double width,double height,int edge)
        {
            double w=screen.Width/scale,h=screen.Height/scale,x=w-width-12,y=h-height-76;
            if(edge==1)y=76;else if(edge==0){x=76;y=h-height-12;}else if(edge==2){x=w-width-76;y=h-height-12;}
            return new Rect(Math.Max(12,Math.Min(w-width-12,x)),Math.Max(12,Math.Min(h-height-12,y)),width,height);
        }
        internal static Rect Beside(Rect anchor,Size popup,Rect work,int edge,double gap=10)
        {
            double x=anchor.X+(anchor.Width-popup.Width)/2,y=anchor.Top-popup.Height-gap;
            if(edge==1)y=anchor.Bottom+gap;else if(edge==0){x=anchor.Right+gap;y=anchor.Y+(anchor.Height-popup.Height)/2;}else if(edge==2){x=anchor.Left-popup.Width-gap;y=anchor.Y+(anchor.Height-popup.Height)/2;}
            return new Rect(Math.Max(work.Left+8,Math.Min(work.Right-popup.Width-8,x)),Math.Max(work.Top+8,Math.Min(work.Bottom-popup.Height-8,y)),popup.Width,popup.Height);
        }
        internal static Rect ScreenRect(FrameworkElement element)
        {
            Point a=element.PointToScreen(new Point()),b=element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight));return new Rect(new Point(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y)),new Point(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)));
        }
    }
    internal sealed partial class DockHost
    {
        internal readonly int Edge;
        static readonly RotateTransform upright=new RotateTransform(-90);
        void ConfigureEdge()
        {
            if(!DockGeometry.Vertical(Edge))return;appCanvas.LayoutTransform=new RotateTransform(90);var status=(Grid)Status.Surface.Child;status.Margin=new Thickness(3,3,3,3);status.ColumnDefinitions.Clear();status.ColumnDefinitions.Add(new ColumnDefinition());status.RowDefinitions.Clear();
            foreach(double height in new[]{22.0,38,22,0,40})status.RowDefinitions.Add(new RowDefinition {Height=new GridLength(height)});
            int index=0;foreach(FrameworkElement child in status.Children){Grid.SetColumn(child,0);Grid.SetRow(child,index++);child.HorizontalAlignment=HorizontalAlignment.Center;child.VerticalAlignment=VerticalAlignment.Center;child.Height=status.RowDefinitions[index-1].Height.Value;child.Width=38;}clock.FontSize=8.5;clock.TextAlignment=TextAlignment.Center;
        }
        void OrientIcons()
        {
            if(!DockGeometry.Vertical(Edge))return;foreach(var button in appCanvas.Children.OfType<Button>()){var content=button.Content as FrameworkElement;if(content!=null&&content.LayoutTransform!=upright)content.LayoutTransform=upright;}
        }
        void PlaceDock(GlassWindow window,Rect logical,bool show)
        {
            var rect=DockGeometry.Rect(Screen.Bounds,Scale,Edge,logical);window.DockBounds=Native.PlacementBounds(Screen.Bounds,Scale,rect.X,rect.Y,rect.Width,rect.Height);window.Width=rect.Width;window.Height=rect.Height;if(show)window.ShowAt(Screen.Bounds,rect.X,rect.Y,rect.Width,rect.Height);else Native.Place(window,Screen.Bounds,rect.X,rect.Y,rect.Width,rect.Height);
        }
        void PlaceStatus(DockLayout layout,bool show,double y=Double.NaN)
        {
            if(Double.IsNaN(y))y=layout.Y;Rect rect;
            if(DockGeometry.Vertical(Edge)){var position=DockGeometry.Rect(Screen.Bounds,Scale,Edge,new Rect(layout.StatusX,y,layout.StatusWidth,DockGeometry.SideStatusWidth));rect=new Rect(position.X,position.Y,DockGeometry.SideStatusWidth,layout.StatusWidth);}
            else rect=DockGeometry.Rect(Screen.Bounds,Scale,Edge,new Rect(layout.StatusX,y,layout.StatusWidth,44));
            Status.DockBounds=Native.PlacementBounds(Screen.Bounds,Scale,rect.X,rect.Y,rect.Width,rect.Height);Status.Width=rect.Width;Status.Height=rect.Height;if(show)Status.ShowAt(Screen.Bounds,rect.X,rect.Y,rect.Width,rect.Height);else Native.Place(Status,Screen.Bounds,rect.X,rect.Y,rect.Width,rect.Height);
        }
    }
}
