using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class DragReorder
    {
        const string format="OneTaskBar.LayoutMove";
        sealed class Session {internal string Id=Guid.NewGuid().ToString("N"),Scope,Key;}
        static Session active;static InsertionMark mark;static AdornerLayer layer;
        internal static bool Active {get{return active!=null;}}
        internal static event Action Finished;
        internal static void Attach(FrameworkElement target,FrameworkElement handle,string scope,string key,Orientation direction,Func<bool> enabled,Func<string,string,bool,bool> move)
        {
            if(String.IsNullOrEmpty(key))return;
            Point origin=new Point();bool armed=false;target.AllowDrop=true;
            handle.PreviewMouseLeftButtonDown+=(s,e)=> {if(e.Handled||e.StylusDevice!=null||!enabled())return;origin=e.GetPosition(handle);armed=true;};
            handle.PreviewMouseLeftButtonUp+=(s,e)=>armed=false;
            handle.PreviewMouseMove+=(s,e)=> {
                if(!armed||Active||!enabled()||e.LeftButton!=MouseButtonState.Pressed)return;
                Point now=e.GetPosition(handle);if(Math.Abs(now.X-origin.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(now.Y-origin.Y)<SystemParameters.MinimumVerticalDragDistance)return;
                armed=false;e.Handled=true;Mouse.Capture(null);active=new Session {Scope=scope,Key=key};double opacity=target.Opacity;target.Opacity=.48;
                try {DragDrop.DoDragDrop(handle,new DataObject(format,active.Id),DragDropEffects.Move);}
                catch(Exception error){AppLog.Write("layout-drag",error.Message);}
                finally {target.Opacity=opacity;ClearMark();active=null;var notify=Finished;if(notify!=null)notify();}
            };
            DragEventHandler over=(s,e)=> {
                if(!Matches(e.Data,scope)||!enabled())return;e.Handled=true;e.Effects=DragDropEffects.Move;
                ScrollAtEdge(target,e);if(active.Key==key){ClearMark();return;}bool after=After(e.GetPosition(target),target,direction);ShowMark(target,direction,after);
            };
            target.DragEnter+=over;target.DragOver+=over;
            target.DragLeave+=(s,e)=> {if(mark!=null&&mark.AdornedElement==target)ClearMark();};
            target.Drop+=(s,e)=> {
                if(!Matches(e.Data,scope)||!enabled())return;e.Handled=true;ClearMark();
                e.Effects=move(active.Key,key,After(e.GetPosition(target),target,direction))?DragDropEffects.Move:DragDropEffects.None;
            };
        }
        static bool Matches(IDataObject data,string scope) {return active!=null&&active.Scope==scope&&data.GetDataPresent(format,false)&&String.Equals(data.GetData(format,false) as string,active.Id,StringComparison.Ordinal);}
        static bool After(Point point,FrameworkElement target,Orientation direction) {return direction==Orientation.Horizontal?point.X>=target.ActualWidth/2:point.Y>=target.ActualHeight/2;}
        static void ScrollAtEdge(DependencyObject target,DragEventArgs e)
        {
            for(DependencyObject node=target;node!=null;node=VisualTreeHelper.GetParent(node)) {
                var scroll=node as ScrollViewer;if(scroll==null)continue;double y=e.GetPosition(scroll).Y;
                if(y<28)scroll.ScrollToVerticalOffset(Math.Max(0,scroll.VerticalOffset-7));else if(y>scroll.ActualHeight-28)scroll.ScrollToVerticalOffset(scroll.VerticalOffset+7);break;
            }
        }
        static void ShowMark(FrameworkElement target,Orientation direction,bool after)
        {
            if(mark!=null&&mark.AdornedElement==target&&mark.After==after)return;ClearMark();layer=AdornerLayer.GetAdornerLayer(target);if(layer==null)return;mark=new InsertionMark(target,direction,after);layer.Add(mark);
        }
        static void ClearMark() {if(layer!=null&&mark!=null)layer.Remove(mark);mark=null;layer=null;}
        sealed class InsertionMark : Adorner
        {
            readonly Orientation direction;internal readonly bool After;
            internal InsertionMark(UIElement target,Orientation axis,bool after):base(target) {direction=axis;After=after;IsHitTestVisible=false;}
            protected override void OnRender(DrawingContext dc)
            {
                Size size=AdornedElement.RenderSize;var pen=new Pen(Paint.Brush("#FF7DDDC8"),3){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
                if(direction==Orientation.Horizontal){double x=After?size.Width-1:1;dc.DrawLine(pen,new Point(x,7),new Point(x,Math.Max(7,size.Height-7)));}
                else {double y=After?size.Height-3:1;dc.DrawLine(pen,new Point(8,y),new Point(Math.Max(8,size.Width-8),y));}
            }
        }
    }
    internal sealed class LayoutItem
    {
        internal string Key;internal FrameworkElement Element;
    }
    internal sealed class ReorderGroup
    {
        readonly string scope;readonly Panel panel;readonly Func<bool> enabled;readonly Orientation axis;readonly Action<FrameworkElement,int> place;
        readonly List<LayoutItem> items=new List<LayoutItem>();
        internal ReorderGroup(string name,Panel owner,Orientation direction,Func<bool> canMove,Action<FrameworkElement,int> position=null) {scope=name;panel=owner;axis=direction;enabled=canMove;place=position;}
        internal void Add(string key,FrameworkElement element,FrameworkElement handle=null)
        {
            items.Add(new LayoutItem {Key=key,Element=element});panel.Children.Add(element);
            DragReorder.Attach(element,handle??element,scope,key,axis,enabled,(source,target,after)=>LayoutOrder.Move(scope,LayoutOrder.Arrange(scope,items,i=>i.Key).Select(i=>i.Key),source,target,after));
        }
        internal string[] Keys {get{return LayoutOrder.Arrange(scope,items,i=>i.Key).Select(i=>i.Key).ToArray();}}
        internal IEnumerable<LayoutItem> Items {get{return items;}}
        internal void Apply()
        {
            var ordered=LayoutOrder.Arrange(scope,items,i=>i.Key);bool same=panel.Children.Count==ordered.Count&&ordered.Where((item,i)=>panel.Children[i]!=item.Element).Count()==0;
            if(!same){panel.Children.Clear();foreach(var item in ordered)panel.Children.Add(item.Element);}
            if(place!=null)for(int i=0;i<ordered.Count;i++)place(ordered[i].Element,i);
        }
    }
}
