using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace OneTaskBar
{
    internal static class DragReorder
    {
        const string format="OneTaskBar.LayoutMove";
        sealed class Session {internal string Id=Guid.NewGuid().ToString("N"),Scope,Key;internal Func<string,bool> PendingDrop;}
        static Session active;static InsertionMark mark;static AdornerLayer layer;
        internal static bool Active {get{return active!=null;}}
        internal static int StartedMoves,CompletedMoves;
        internal static event Action Finished;
        internal static void DropArea(FrameworkElement target,string scope,Func<bool> enabled,Func<string,Point,bool> drop,Action<string,Point> preview=null)
        {
            target.AllowDrop=true;
            DragEventHandler over=(s,e)=>{if(e.Handled||!Matches(e.Data,scope)||!enabled())return;e.Handled=true;e.Effects=DragDropEffects.Move;ClearMark();Point point=e.GetPosition(target);active.PendingDrop=key=>drop(key,point);if(preview!=null)preview(active.Key,point);};target.DragEnter+=over;target.DragOver+=over;
            target.Drop+=(s,e)=>{if(e.Handled||!Matches(e.Data,scope)||!enabled())return;e.Handled=true;ClearMark();e.Effects=drop(active.Key,e.GetPosition(target))?DragDropEffects.Move:DragDropEffects.None;};
        }
        internal static void Attach(FrameworkElement target,FrameworkElement handle,string scope,string key,Orientation direction,Func<bool> enabled,Func<string,string,bool,bool> move,Action<string,string,bool> preview=null)
        {
            if(String.IsNullOrEmpty(key))return;
            Point origin=new Point();bool armed=false;target.AllowDrop=true;
            handle.PreviewMouseLeftButtonDown+=(s,e)=> {if(e.Handled||e.StylusDevice!=null||!enabled())return;origin=e.GetPosition(handle);armed=true;};
            handle.PreviewMouseLeftButtonUp+=(s,e)=>armed=false;
            handle.PreviewMouseMove+=(s,e)=> {
                if(!armed||Active||!enabled()||e.LeftButton!=MouseButtonState.Pressed)return;
                Point now=e.GetPosition(handle);if(Math.Abs(now.X-origin.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(now.Y-origin.Y)<SystemParameters.MinimumVerticalDragDistance)return;
                armed=false;e.Handled=true;Mouse.Capture(null);active=new Session {Scope=scope,Key=key};double opacity=target.Opacity;DragGhost ghost=null;GiveFeedbackEventHandler feedback=null;
                try {if(scope=="dock.apps"){ghost=new DragGhost(target);ghost.Show();ghost.Follow();feedback=(sender,args)=>{ghost.Follow();args.UseDefaultCursors=false;Mouse.SetCursor(Cursors.Arrow);args.Handled=true;};handle.GiveFeedback+=feedback;}target.Opacity=.28;StartedMoves++;if(DragDrop.DoDragDrop(handle,new DataObject(format,active.Id),DragDropEffects.Move)!=DragDropEffects.None)CompletedMoves++;}
                catch(Exception error){AppLog.Write("layout-drag",error.Message);}
                finally {if(feedback!=null)handle.GiveFeedback-=feedback;if(ghost!=null)ghost.Close();Mouse.SetCursor(null);target.Opacity=opacity;ClearMark();active=null;var notify=Finished;if(notify!=null)notify();}
            };
            DragEventHandler over=(s,e)=> {
                if(!Matches(e.Data,scope)||!enabled())return;e.Handled=true;e.Effects=DragDropEffects.Move;
                ScrollAtEdge(target,e);if(active.Key==key){ClearMark();return;}bool after=After(e.GetPosition(target),target,direction);if(preview!=null){active.PendingDrop=source=>move(source,key,after);ClearMark();preview(active.Key,key,after);}else ShowMark(target,direction,after);
            };
            target.DragEnter+=over;target.DragOver+=over;
            target.DragLeave+=(s,e)=> {if(mark!=null&&mark.AdornedElement==target)ClearMark();};
            target.Drop+=(s,e)=> {
                if(!Matches(e.Data,scope)||!enabled())return;e.Handled=true;ClearMark();
                bool moved=key==active.Key&&active.PendingDrop!=null?active.PendingDrop(active.Key):move(active.Key,key,After(e.GetPosition(target),target,direction));e.Effects=moved?DragDropEffects.Move:DragDropEffects.None;
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
        internal sealed class DragGhost : Window
        {
            [DllImport("user32.dll")]static extern bool EnableWindow(IntPtr window,bool enabled);
            System.Drawing.Point last=new System.Drawing.Point(Int32.MinValue,Int32.MinValue);
            bool inputDisabled;
            internal DragGhost(FrameworkElement source)
            {
                var presentation=PresentationSource.FromVisual(source);double scale=presentation==null?1:presentation.CompositionTarget.TransformToDevice.M11;Width=Math.Max(1,source.ActualWidth);Height=Math.Max(1,source.ActualHeight);
                var bitmap=new RenderTargetBitmap((int)Math.Ceiling(Width*scale),(int)Math.Ceiling(Height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(source);bitmap.Freeze();
                Content=new Image {Source=bitmap,Stretch=Stretch.Fill};WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Focusable=false;IsHitTestVisible=false;Opacity=.94;
                SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)|0x80|0x08000000|0x20);HwndSource.FromHwnd(h).AddHook((IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled)=>{if(msg==0x84){handled=true;return new IntPtr(-1);}if(msg==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});};
            }
            internal void Follow(){var point=System.Windows.Forms.Cursor.Position;if(point==last)return;last=point;var h=new WindowInteropHelper(this).Handle;if(!inputDisabled){EnableWindow(h,false);inputDisabled=true;}double scale=Math.Max(96,Native.GetDpiForWindow(h))/96.0;Native.SetWindowPos(h,new IntPtr(-1),point.X-(int)(Width*scale/2),point.Y-(int)(Height*scale/2),(int)Math.Ceiling(Width*scale),(int)Math.Ceiling(Height*scale),0x10);}
        }
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
