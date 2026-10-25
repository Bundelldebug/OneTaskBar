using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal sealed class QuickWidgetFrame : Grid
    {
        internal readonly string Key;
        internal readonly Border Body;
        internal readonly Thumb Mover;
        internal readonly QuickResizeGrip Resize=new QuickResizeGrip();
        readonly Border outline;
        readonly GlassTileShadow glassShadow;
        readonly Button remove;
        internal readonly Button ContentEditor;
        readonly Action<QuickWidgetSlot> shape;
        int columns,rows,sizeMotion;
        internal QuickWidgetFrame(QuickWidgetBoard board,string key,string title,UIElement content,Action<QuickWidgetSlot> resized)
        {
            Key=key;shape=resized;Background=Brushes.Transparent;RenderTransform=new TranslateTransform();
            if(Glass95.Enabled){glassShadow=new GlassTileShadow();Children.Add(glassShadow);}
            Body=new GlassTileBorder {Child=content,Optical=!QuickWidgetLayout.Slider(key)&&key!="media",Background=QuickWidgetLayout.Slider(key)?Brushes.Transparent:QuickPanel.Card,CornerRadius=new CornerRadius(32)};Children.Add(Body);
            outline=new Border {BorderThickness=new Thickness(2),BorderBrush=Paint.Brush("#88FFFFFF"),IsHitTestVisible=false,Visibility=Visibility.Collapsed};Children.Add(outline);
            var template=new FrameworkElementFactory(typeof(Border));template.SetValue(Border.BackgroundProperty,Brushes.Transparent);
            Mover=new Thumb {Template=new ControlTemplate(typeof(Thumb)){VisualTree=template},Cursor=Cursors.SizeAll,Focusable=true,FocusVisualStyle=null,Visibility=Visibility.Collapsed};Children.Add(Mover);
            remove=Paint.Action("−",()=>board.HideWidget(key));remove.Width=remove.Height=23;remove.Padding=new Thickness(0);remove.Foreground=Paint.Brush("#F66C77");((TextBlock)remove.Content).Foreground=remove.Foreground;remove.Background=Paint.ControlWhite;remove.Resources["ButtonRadius"]=new CornerRadius(12);remove.HorizontalAlignment=HorizontalAlignment.Left;remove.VerticalAlignment=VerticalAlignment.Top;remove.Visibility=Visibility.Collapsed;remove.ToolTip=title+" 숨기기";Children.Add(remove);
            Resize.Visibility=Visibility.Collapsed;Children.Add(Resize);AutomationProperties.SetName(Mover,title+" 위치 이동");AutomationProperties.SetHelpText(Mover,"드래그 또는 방향키로 이동합니다.");AutomationProperties.SetName(Resize,title+" 크기 조절");AutomationProperties.SetHelpText(Resize,"모서리를 끌거나 방향키로 크기를 조절합니다. Escape로 취소합니다.");
            if(key=="toggles"){ContentEditor=Paint.Action("편집",()=>{if(board.EditContents!=null)board.EditContents();});ContentEditor.HorizontalAlignment=HorizontalAlignment.Center;ContentEditor.VerticalAlignment=VerticalAlignment.Center;ContentEditor.Padding=new Thickness(12,5,12,5);ContentEditor.Background=Paint.Brush("#C72D353A");((TextBlock)ContentEditor.Content).Foreground=Paint.ControlWhite;ContentEditor.Visibility=Visibility.Collapsed;AutomationProperties.SetName(ContentEditor,"기능 토글 내부 편집");Children.Add(ContentEditor);}
            Mover.DragStarted+=(s,e)=>board.Begin(key,false);Mover.DragDelta+=(s,e)=>board.Drag();Mover.DragCompleted+=(s,e)=>board.End(e.Canceled);
            Resize.DragStarted+=(s,e)=>board.Begin(key,true);Resize.DragDelta+=(s,e)=>board.Drag();Resize.DragCompleted+=(s,e)=>board.End(e.Canceled);
            Mover.KeyDown+=(s,e)=>board.Key(key,false,e);Resize.KeyDown+=(s,e)=>board.Key(key,true,e);
            SizeChanged+=(s,e)=>Curvature();
            Body.SizeChanged+=(s,e)=>Curvature();
        }
        internal void Editing(bool editing){Body.IsHitTestVisible=!editing;Body.Opacity=editing?.72:1;Mover.Visibility=Resize.Visibility=remove.Visibility=outline.Visibility=editing?Visibility.Visible:Visibility.Collapsed;if(ContentEditor!=null)ContentEditor.Visibility=editing?Visibility.Visible:Visibility.Collapsed;}
        void Curvature(){double width=Body.ActualWidth,height=Body.ActualHeight,radius=columns==1||rows==1?Math.Min(width,height)/2:Math.Min(32,Math.Min(width,height)/2);Body.CornerRadius=outline.CornerRadius=new CornerRadius(radius);Body.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,width),Math.Max(0,height)),radius,radius);if(glassShadow!=null){glassShadow.Radius=radius;glassShadow.InvalidateVisual();}}
        internal void AnimateSize(Size previous,bool animate){int token=++sizeMotion;Body.BeginAnimation(WidthProperty,null);Body.BeginAnimation(HeightProperty,null);Body.Width=Body.Height=Double.NaN;Body.HorizontalAlignment=HorizontalAlignment.Stretch;Body.VerticalAlignment=VerticalAlignment.Stretch;if(!animate||!Motion.Enabled||!IsVisible)return;Body.Width=ActualWidth;Body.Height=ActualHeight;Body.HorizontalAlignment=HorizontalAlignment.Left;Body.VerticalAlignment=VerticalAlignment.Top;var height=Motion.Tween(previous.Height,ActualHeight,170);height.Completed+=(s,e)=>{if(token==sizeMotion){Body.BeginAnimation(WidthProperty,null);Body.BeginAnimation(HeightProperty,null);Body.Width=Body.Height=Double.NaN;Body.HorizontalAlignment=HorizontalAlignment.Stretch;Body.VerticalAlignment=VerticalAlignment.Stretch;}};Body.BeginAnimation(WidthProperty,Motion.Tween(previous.Width,ActualWidth,170));Body.BeginAnimation(HeightProperty,height);}
        internal void Shape(QuickWidgetSlot slot){if(columns==slot.Columns&&rows==slot.Rows)return;columns=slot.Columns;rows=slot.Rows;shape(slot);Curvature();AutomationProperties.SetItemStatus(Resize,columns+"열 "+rows+"행");}
        internal void Stop(){if(Mover.IsDragging)Mover.CancelDrag();if(Resize.IsDragging)Resize.CancelDrag();}
    }
    internal sealed class QuickWidgetBoard : Panel
    {
        internal readonly QuickWidgetLayout Layout;
        QuickWidgetLayout presentationLayout;bool mediaIdle;
        internal QuickWidgetLayout DisplayLayout {get{return presentationLayout??Layout;}}
        internal bool Expanded {get;private set;}
        internal int ExpandedToggleRows=4;
        internal bool ContentsEditing {get;private set;}
        internal Action EditContents;
        internal readonly Dictionary<string,QuickWidgetFrame> Frames=new Dictionary<string,QuickWidgetFrame>();
        internal bool Editing {get;private set;}
        internal bool GestureActive {get{return active!=null;}}
        internal bool TimerActive {get{return scroll.IsEnabled;}}
        internal event Action Changed;
        internal ScrollViewer Scroller;
        const double Gap=10;
        internal const double ControlHeight=60;
        double step=86,rowStep=74;string active;bool resize;
        Point origin;QuickWidgetSlot original;List<QuickWidgetSlot> before;
        readonly DispatcherTimer scroll=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(35)};
        internal QuickWidgetBoard(QuickWidgetLayout layout){Layout=layout;Focusable=false;Background=Brushes.Transparent;scroll.Tick+=(s,e)=>{if(active==null||Scroller==null)return;Point p=Mouse.GetPosition(Scroller);double delta=p.Y<30?-12:p.Y>Scroller.ViewportHeight-30?12:0;if(delta!=0){Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset+delta);Drag();}};}
        internal void Add(string key,string title,UIElement content,Action<QuickWidgetSlot> shape)
        {
            var frame=new QuickWidgetFrame(this,key,title,content,shape);Frames.Add(key,frame);Children.Add(frame);
        }
        internal void SetEditing(bool value){Cancel();Editing=value;if(!value)ContentsEditing=false;ApplyEditing();Reflow(true);}
        void ApplyEditing(){foreach(var frame in Frames.Values){frame.Editing(Editing&&!(ContentsEditing&&frame.Key=="toggles"));frame.IsEnabled=frame.IsHitTestVisible=!ContentsEditing||frame.Key=="toggles";}}
        internal void SetContentsEditing(bool value){Cancel();ContentsEditing=value&&Editing;ApplyEditing();}
        internal void HideWidget(string key){if(!Editing)return;Layout.SetHidden(key,true);Save();Reflow(true);}
        internal void AddWidget(string key){Layout.SetHidden(key,false);Save();Reflow(true);}
        internal void Reset(){Cancel();Layout.Reset(false);Save();Reflow(true);}
        internal void Save(){if(!Layout.Save()&&Changed!=null)Changed();}
        internal Point CellAt(Point point){return new Point(Math.Max(0,Math.Floor(point.X/step)),Math.Max(0,Math.Floor(point.Y/rowStep)));}
        internal void SetExpanded(bool value,bool animate=true){var frame=Frames["toggles"];Size previous=new Size(frame.Body.ActualWidth,frame.Body.ActualHeight);frame.AnimateSize(previous,false);Expanded=value;Reflow(animate);frame.AnimateSize(previous,animate);}
        internal void SetMediaIdle(bool value){if(mediaIdle==value)return;mediaIdle=value;var frame=Frames["media"];Size previous=new Size(frame.Body.ActualWidth,frame.Body.ActualHeight);frame.AnimateSize(previous,false);Reflow(true);frame.AnimateSize(previous,true);}
        void PresentLayout(){var media=Layout.Find("media");bool compact=mediaIdle&&!Editing&&!media.Hidden&&media.Columns==4&&media.Rows==2;if(!Expanded&&!compact){presentationLayout=null;return;}presentationLayout=new QuickWidgetLayout(null,true);presentationLayout.Restore(Layout.Copy());if(compact){presentationLayout.Resize("media",4,1,false);foreach(var item in presentationLayout.Items)if(item.Y>=media.Y+2)item.Y--;}if(Expanded)presentationLayout.Resize("toggles",4,ExpandedToggleRows,false);}
        internal void Begin(string key,bool sizing,bool autoScroll=true)
        {
            if(!Editing)return;Cancel();active=key;resize=sizing;before=Layout.Copy();original=Layout.Find(key).Copy();origin=Mouse.GetPosition(this);SetZIndex(Frames[key],5);if(autoScroll)scroll.Start();
        }
        internal void Drag()
        {
            if(active==null)return;Point p=Mouse.GetPosition(this);PreviewBy(p.X-origin.X,p.Y-origin.Y);
        }
        internal void PreviewBy(double dx,double dy)
        {
            if(active==null)return;var previous=Layout.Find(active);int w=original.Columns,h=original.Rows,x=original.X,y=original.Y;bool vertical=false;
            if(resize){w=Snap(original.Columns+dx/step,previous.Columns,4);h=Snap(original.Rows+dy/rowStep,previous.Rows,5);if(active=="media"){w=Math.Max(2,w);h=Math.Max(2,h);}vertical=QuickWidgetLayout.Slider(active)&&(w==1&&h>1||h>1&&Math.Abs(dy/rowStep)>Math.Abs(dx/step));if(QuickWidgetLayout.Slider(active)){if(vertical)w=1;else h=1;}x=Math.Min(x,4-w);}
            else{x=Math.Max(0,Math.Min(4-w,x+(int)Math.Round(dx/step)));y=Math.Max(0,Math.Min(QuickWidgetLayout.MaximumRow,y+(int)Math.Round(dy/rowStep)));}
            if(previous.X==x&&previous.Y==y&&previous.Columns==w&&previous.Rows==h)return;
            Layout.Restore(before);if(resize)Layout.Resize(active,w,h,vertical);else Layout.Move(active,x,y);
            Reflow(true);
        }
        static int Snap(double requested,int previous,int maximum){while(previous<maximum&&requested>=previous+.6)previous++;while(previous>1&&requested<=previous-.6)previous--;return Math.Max(1,Math.Min(maximum,previous));}
        internal void End(bool cancelled)
        {
            if(active==null)return;string key=active;active=null;scroll.Stop();SetZIndex(Frames[key],0);if(cancelled)Layout.Restore(before);else Save();before=null;Reflow(true);
        }
        internal void Cancel(){if(active==null)return;string key=active;End(true);Frames[key].Stop();}
        internal void Key(string key,bool sizing,KeyEventArgs e)
        {
            if(e.Key==System.Windows.Input.Key.Escape){if(active!=null)Cancel();else SetEditing(false);e.Handled=true;return;}if(active!=null)return;
            int x=e.Key==System.Windows.Input.Key.Left?-1:e.Key==System.Windows.Input.Key.Right?1:0,y=e.Key==System.Windows.Input.Key.Up?-1:e.Key==System.Windows.Input.Key.Down?1:0;if(x==0&&y==0)return;
            var item=Layout.Find(key);if(sizing)Layout.Resize(key,item.Columns+x,item.Rows+y,y!=0);else Layout.Move(key,item.X+x,item.Y+y);Save();Reflow(true);e.Handled=true;
        }
        internal void Reflow(bool animate)
        {
            var positions=Frames.Values.Where(f=>f.IsVisible).ToDictionary(f=>f.Key,f=>f.TranslatePoint(new Point(),this));PresentLayout();InvalidateMeasure();InvalidateArrange();UpdateLayout();
            foreach(var frame in Frames.Values){Point old;if(!positions.TryGetValue(frame.Key,out old)||!frame.IsVisible)continue;Point now=frame.TranslatePoint(new Point(),this);var transform=(TranslateTransform)frame.RenderTransform;double dx=old.X-now.X+transform.X,dy=old.Y-now.Y+transform.Y;transform.BeginAnimation(TranslateTransform.XProperty,null);transform.BeginAnimation(TranslateTransform.YProperty,null);transform.X=transform.Y=0;if(animate&&Motion.Enabled&&IsVisible){transform.BeginAnimation(TranslateTransform.XProperty,Motion.Tween(dx,0,170));transform.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(dy,0,170));}}
        }
        protected override Size MeasureOverride(Size available)
        {
            double width=Double.IsInfinity(available.Width)?344:Math.Max(40,available.Width);step=(width+Gap)/4;rowStep=Math.Min(ControlHeight,step-Gap)+Gap;int bottom=0;
            foreach(var item in DisplayLayout.Items){QuickWidgetFrame frame;if(!Frames.TryGetValue(item.Key,out frame))continue;frame.Visibility=item.Hidden?Visibility.Collapsed:Visibility.Visible;if(item.Hidden)continue;frame.Shape(item);frame.Measure(Bounds(item).Size);bottom=Math.Max(bottom,item.Y+item.Rows);}
            return new Size(width,Math.Max(0,bottom*rowStep-Gap));
        }
        Rect Bounds(QuickWidgetSlot item){double width=item.Columns==1?Math.Min(step-Gap,rowStep-Gap):item.Columns*step-Gap;return new Rect(item.X*step+(item.Columns==1?(step-Gap-width)/2:0),item.Y*rowStep,Math.Max(0,width),Math.Max(0,item.Rows*rowStep-Gap));}
        protected override Size ArrangeOverride(Size size)
        {
            foreach(var item in DisplayLayout.Items){QuickWidgetFrame frame;if(!item.Hidden&&Frames.TryGetValue(item.Key,out frame))frame.Arrange(Bounds(item));}return size;
        }
    }
}
