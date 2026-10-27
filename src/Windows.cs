using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed partial class DockHost
    {
        internal readonly Forms.Screen Screen;
        internal double Scale;
        internal readonly double Zoom=DockSizing.Zoom;
        internal double ContentScale {get{return Scale*Zoom;}}
        internal readonly GlassWindow Start=new GlassWindow(),Apps=new GlassWindow(),Status=new GlassWindow();
        internal readonly BatteryPill Battery=new BatteryPill();
        readonly DockClock clockFace=new DockClock();
        readonly TextBlock clock;
        readonly Glyph wifiStatus=new Glyph("wifi",22){Color=Glass95.DockForeground},volumeStatus=new Glyph("volume",19){Color=Glass95.DockForeground};
        readonly Grid statusIcons=new Grid {Height=34};readonly StackPanel classicStatus=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};readonly DuoStatusGlyph duoStatus=new DuoStatusGlyph {HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        readonly Button quickAccess,trayAccess,clockAccess;
        readonly Button startButton;readonly Glyph startGlyph=new Glyph("apps",23){Color=Glass95.DockForeground};
        readonly Action pulseStart;
        internal int StartPulses {get;private set;}
        internal void AnimateStartIcon(){StartPulses++;if(pulseStart!=null)pulseStart();}
        internal readonly Button TaskViewButton;
        internal readonly Button SearchButton;
        internal readonly Button OverflowButton;internal readonly DockOverflow Overflow;internal readonly DockDragController DragApps;
        readonly Button powerAccess;readonly ColumnDefinition powerColumn;double powerWidth=58;
        readonly ShellController controller;
        readonly Dictionary<IntPtr,Border> indicators=new Dictionary<IntPtr,Border>();
        sealed class DockCell {internal AppEntry Entry;internal Button Button;internal Border Dot;internal Border Icon;internal ImageSource IconSource;internal TranslateTransform Offset=new TranslateTransform();internal double X=Double.NaN;internal bool Leaving;internal int Token;}
        readonly Canvas appCanvas=new Canvas {Height=42,VerticalAlignment=VerticalAlignment.Center,Background=Brushes.Transparent};
        readonly Border pinDivider=new Border {Width=1,Height=24,CornerRadius=new CornerRadius(.5),Background=Brushes.Black,IsHitTestVisible=false};
        readonly TranslateTransform dividerOffset=new TranslateTransform();double dividerX=Double.NaN;bool unpinnedOverflow;
        internal bool PinDividerVisible {get{return pinDivider.Visibility==Visibility.Visible;}}
        string dragPreview;bool resetPreview;
        sealed class DockDrop {internal string Target;internal bool After,Pinned;}
        DockDrop pendingDockDrop;
        readonly Dictionary<string,DockCell> cells=new Dictionary<string,DockCell>(StringComparer.OrdinalIgnoreCase);
        readonly System.Windows.Threading.DispatcherTimer resizeTimer=new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(16)};
        readonly Stopwatch resizeWatch=new Stopwatch();
        readonly System.Windows.Threading.DispatcherTimer visibilityTimer=new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(16)};
        readonly Stopwatch visibilityWatch=new Stopwatch();double reveal=1,revealFrom,revealTarget=1;int revealDuration;
        double currentX,currentWidth,currentY,currentStartX,fromX,fromWidth,fromY,fromStartX,resizeDelay;DockLayout targetLayout;bool placed;
        bool visible=true,ready,mediaSpace;
        bool autoHide,fullScreen;DockRevealEdge revealEdge;
        readonly System.Windows.Threading.DispatcherTimer hideDelay=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(900)};
        DateTime interactionUntil,placementAt;
        internal bool DockVisible {get{return visible;}}internal bool EdgeVisible {get{return revealEdge!=null&&revealEdge.Visible;}}
        internal bool AutoHideAnimating {get{return visibilityTimer.IsEnabled;}}internal double RevealProgress {get{return reveal;}}
        IntPtr pressedForeground;
        internal IntPtr WindowDragPartner {get{var foreground=Native.GetForegroundWindow();if(controller.Running.Any(a=>a.Windows.Contains(foreground)))return foreground;if(controller.Running.Any(a=>a.Windows.Contains(pressedForeground)))return pressedForeground;return controller.LastApplication;}}
        bool closed;internal NowBar MediaBar {get;private set;}
        internal DockHost(ShellController shell,Forms.Screen screen)
        {
            controller=shell; Screen=screen;Edge=DockGeometry.Edge;clock=clockFace.View;
            hideDelay.Tick+=(s,e)=>CompleteAutoHide(InUse());
            pinDivider.RenderTransform=dividerOffset;Canvas.SetTop(pinDivider,9);Panel.SetZIndex(pinDivider,2);appCanvas.Children.Add(pinDivider);DragApps=new DockDragController(this,shell,appCanvas,PreviewAt,DropAt,PreviewHome);DragReorder.Finished+=EndDragPreview;
            var search=Paint.Button(new Glyph("dock-search",22){Color=Glass95.DockForeground},()=>controller.ToggleSearch(this),"검색");SearchButton=search;search.Padding=new Thickness(0);search.Background=Brushes.Transparent;search.Width=42;search.Height=42;Motion.Dock(search,(FrameworkElement)search.Content);Canvas.SetLeft(search,10);appCanvas.Children.Add(search);Apps.Surface.Child=appCanvas;
            var desktops=Paint.Button(new Glyph("dock-desktops",22){Color=Glass95.DockForeground},()=>controller.OpenTaskView(),"데스크톱 전환 · 작업 보기");TaskViewButton=desktops;desktops.Padding=new Thickness(0);desktops.Background=Brushes.Transparent;desktops.Width=desktops.Height=42;Motion.Dock(desktops,(FrameworkElement)desktops.Content);Canvas.SetLeft(desktops,52);appCanvas.Children.Add(desktops);
            OverflowButton=Paint.Button(new Glyph("more",23){Color=Glass95.DockForeground},()=>Overflow.Toggle(),"숨겨진 앱");OverflowButton.Width=46;OverflowButton.Height=42;OverflowButton.Padding=new Thickness(0);OverflowButton.Background=Brushes.Transparent;OverflowButton.Visibility=Visibility.Collapsed;Panel.SetZIndex(OverflowButton,3);appCanvas.Children.Add(OverflowButton);Overflow=new DockOverflow(shell,this,OverflowButton,QueueHide);
            resizeTimer.Tick+=(sender,args)=>ResizeFrame();
            visibilityTimer.Tick+=(sender,args)=>VisibilityFrame();

            foreach(var w in new GlassWindow[] {Start,Apps,Status}) {
                w.MouseEnter+=(s,e)=>{interactionUntil=DateTime.UtcNow.AddMilliseconds(1000);hideDelay.Stop();if(autoHide&&!visible&&!fullScreen)Reveal();};w.MouseLeave+=(s,e)=>QueueHide();w.PreviewMouseDown+=(s,e)=>interactionUntil=DateTime.UtcNow.AddMilliseconds(1200);
                w.SoftCorners=true;w.Dark=false;w.DockSurface=true; w.ShowActivated=false; w.Radius=22*Zoom; w.Surface.CornerRadius=new CornerRadius(22);w.Surface.LayoutTransform=new ScaleTransform(Zoom,Zoom); w.SetMaterial(LightPanel.DockGlass,LightPanel.DockSolid);w.Surface.BorderBrush=Brushes.Transparent;w.Surface.BorderThickness=new Thickness(0);
                w.SourceInitialized+=(s,e)=> {
                    var h=new WindowInteropHelper((Window)s).Handle;
                    Native.SetWindowLong(h,-20,(Native.GetWindowLong(h,-20)|0x80|0x08000000)&~0x40000);
                    HwndSource.FromHwnd(h).AddHook((IntPtr window,int message,IntPtr wp,IntPtr lp,ref bool handled)=>{if(message==0x21){pressedForeground=Native.GetForegroundWindow();handled=true;return new IntPtr(3);}return IntPtr.Zero;});
                };
            }
            Start.Title="One TaskBar · 앱 서랍"; Apps.Title="One TaskBar · 앱"; Status.Title="One TaskBar · 상태";
            var start=Paint.Button(startGlyph,()=>controller.ToggleStartButton(this),"앱 목록");startButton=start;pulseStart=Motion.Dock(start,startGlyph);
            start.Width=start.Height=40; start.Padding=new Thickness(0); start.Background=Brushes.Transparent;
            var transform=new ScaleTransform(1,1); start.RenderTransform=transform; start.RenderTransformOrigin=new Point(.5,.5);
            start.MouseEnter+=(s,e)=>Animate(transform,1.09); start.MouseLeave+=(s,e)=>Animate(transform,1);
            Start.Surface.Child=start;
            var status=new Grid { Margin=new Thickness(7,0,7,0) };
            status.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(32)});
            status.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            powerColumn=new ColumnDefinition {Width=new GridLength(58)};status.ColumnDefinitions.Add(powerColumn);
            status.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(0)});
            status.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(94)});
            var native=Paint.Button(new Glyph("up",15) {Color=Glass95.DockForeground},()=>controller.ShowNative("tray",this),"숨겨진 아이콘 · 트레이");
            trayAccess=native;
            native.Padding=new Thickness(0); native.Background=Brushes.Transparent; status.Children.Add(native);
            bool vertical=DockGeometry.Vertical(Edge);if(vertical){wifiStatus.Width=wifiStatus.Height=18;volumeStatus.Width=volumeStatus.Height=16;}wifiStatus.Margin=vertical?new Thickness(0,0,2,0):new Thickness(2,0,10,0);classicStatus.Children.Add(wifiStatus);classicStatus.Children.Add(volumeStatus);statusIcons.Children.Add(classicStatus);statusIcons.Children.Add(duoStatus);
            var qs=Paint.Button(statusIcons,()=>controller.ToggleQuick(this),"빠른 설정"); qs.Padding=new Thickness(0); qs.Background=Brushes.Transparent;
            Grid.SetColumn(qs,1); status.Children.Add(qs);
            quickAccess=qs;
            Battery.MorphChanged=v=> {native.Opacity=qs.Opacity=Preferences.Current.DuoStatusIcon?1:Math.Max(0,1-v);};
            var battery=Paint.Button(Battery,()=>controller.ToggleQuick(this),"배터리 · 빠른 설정"); battery.Padding=new Thickness(0); battery.Background=Brushes.Transparent;
            powerAccess=battery;
            Grid.SetColumn(battery,2);Panel.SetZIndex(battery,10);status.Children.Add(battery);
            Grid.SetColumn(extraStatus,3);status.Children.Add(extraStatus);
            var time=Paint.Button(clock,()=>controller.ToggleNotifications(this),"알림센터");time.Padding=new Thickness(0);time.Background=Brushes.Transparent;time.BorderBrush=Brushes.Transparent;time.BorderThickness=new Thickness(0);
            clockAccess=time;
            Grid.SetColumn(time,4); status.Children.Add(time); Status.Surface.Child=status;
            var menu=new ContextMenu(); var settings=new MenuItem {Header="One TaskBar 설정",Icon=new Glyph("home",18){Color=Glass95.DockForeground}};settings.Click+=(s,e)=>controller.ShowWelcome();menu.Items.Add(settings);
            var resetOrder=new MenuItem {Header="앱 순서 초기화"};resetOrder.Click+=(s,e)=>controller.ResetDockOrder();menu.Items.Add(resetOrder);
            var restore=new MenuItem {Header="Windows 작업표시줄로 돌아가기"};restore.Click+=(s,e)=>controller.SetDesktop(false);menu.Items.Add(restore);Start.ContextMenu=menu;
            ConfigureEdge();RebuildStatusItems();Rebuild();
        }
        static void Animate(ScaleTransform transform,double value)
        {
            if(!Preferences.Current.Hover||!Preferences.Current.Animations)value=1;
            var a=new DoubleAnimation(value,TimeSpan.FromMilliseconds(140)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};
            transform.BeginAnimation(ScaleTransform.ScaleXProperty,a); transform.BeginAnimation(ScaleTransform.ScaleYProperty,a);
        }
        internal void Show()
        {
            Scale=Native.PrepareMonitor(Apps,Screen.Bounds);Native.PrepareMonitor(Start,Screen.Bounds);Native.PrepareMonitor(Status,Screen.Bounds);
            ready=true;placed=false;Rebuild();
        }
        internal void Rebuild()
        {
            if(closed||DragReorder.Active)return;if(Scale<=0)Scale=1;
            RebuildStatusItems();
            Battery.Compact=DockGeometry.Vertical(Edge);Battery.Height=Battery.Compact?18:23;
            var clockGrid=(Grid)Status.Surface.Child;
            if(DockGeometry.Vertical(Edge)){clockGrid.RowDefinitions[4].Height=new GridLength(DockClock.SideHeight);clockAccess.Height=DockClock.SideHeight;}
            else clockGrid.ColumnDefinitions[4].Width=new GridLength(DockClock.Width);
            clockFace.Update(DateTime.Now,Edge);
            foreach(var surface in new[]{Start,Apps,Status}){surface.SetMaterial(LightPanel.DockGlass,LightPanel.DockSolid);Glass95.DockInk(surface.Surface);}pinDivider.Background=Glass95.DockForeground;pinDivider.Effect=DockAppearance.Readability;duoStatus.Effect=DockAppearance.Readability;
            startGlyph.Kind=Preferences.Current.NativeStartButton?"windows-start":"apps";startGlyph.Color=Preferences.Current.NativeStartButton?Paint.Brush("#FF1975DD"):Glass95.DockForeground;startGlyph.InvalidateVisual();startButton.ToolTip=Preferences.Current.NativeStartButton?"Windows 시작":"앱 목록";System.Windows.Automation.AutomationProperties.SetName(startButton,(string)startButton.ToolTip);
            var entries=controller.DockApps();powerWidth=PowerDisplay.DockSlot(controller.Power,Preferences.Current.DesktopPowerWatts);powerColumn.Width=new GridLength(powerWidth);powerAccess.Visibility=powerWidth>0?Visibility.Visible:Visibility.Collapsed;Battery.Width=DockGeometry.Vertical(Edge)?38:powerWidth==78?70:52;if(DockGeometry.Vertical(Edge))((Grid)Status.Surface.Child).RowDefinitions[2].Height=new GridLength(powerWidth>0?22:0);classicStatus.Visibility=Preferences.Current.DuoStatusIcon?Visibility.Collapsed:Visibility.Visible;duoStatus.Visibility=Preferences.Current.DuoStatusIcon?Visibility.Visible:Visibility.Collapsed;statusIcons.Width=Preferences.Current.DuoStatusIcon?32:DockGeometry.Vertical(Edge)?36:56;if(!Preferences.Current.Animations||!Preferences.Current.ChargeAnimation||!Preferences.Current.DuoStatusIcon)duoStatus.StopPulse();
            mediaSpace=controller.Desktop&&(controller.Services.Media.Active||MediaBar!=null&&MediaBar.ReservesSpace);
            var layout=DockLayout.Compute(Screen.Bounds,ContentScale,Math.Max(1,entries.Count),powerWidth,mediaSpace,68,Edge,Preferences.Current.DuoStatusIcon,Math.Max(44,68/Zoom-24));bool overflow=entries.Count>layout.Capacity;int slots=Math.Max(0,layout.Capacity-(overflow?1:0));var shown=entries.Take(slots).ToArray();Overflow.Update(entries.Skip(slots));OverflowButton.Visibility=overflow?Visibility.Visible:Visibility.Collapsed;OverflowButton.ToolTip="숨겨진 앱 "+Overflow.Count+"개 · 마우스를 올려 펼치기";var keys=new HashSet<string>(shown.Select(e=>e.Key),StringComparer.OrdinalIgnoreCase);bool animate=placed&&visible&&Apps.IsVisible&&Motion.Enabled;
            int pinned=shown.Count(a=>a.Pinned);unpinnedOverflow=entries.Skip(slots).Any(a=>!a.Pinned);pinDivider.Visibility=pinned>0&&entries.Any(a=>!a.Pinned)?Visibility.Visible:Visibility.Collapsed;double nextDivider=DockLayout.AppOffset+pinned*46+6,oldDivider=Double.IsNaN(dividerX)?nextDivider:dividerX+dividerOffset.X;dividerX=nextDivider;Canvas.SetLeft(pinDivider,dividerX);dividerOffset.BeginAnimation(TranslateTransform.XProperty,null);dividerOffset.X=0;if(animate&&Math.Abs(oldDivider-nextDivider)>.01)dividerOffset.BeginAnimation(TranslateTransform.XProperty,Motion.PanelTween(oldDivider-nextDivider,0,330));
            TaskViewButton.Visibility=overflow&&layout.Capacity==0?Visibility.Collapsed:Visibility.Visible;Canvas.SetLeft(OverflowButton,layout.Capacity==0?52:DockLayout.AppOffset+shown.Length*46+12);
            foreach(var cell in cells.Values.Where(c=>!keys.Contains(c.Entry.Key)).ToArray()){
                if(cell.Leaving)continue;cell.Leaving=true;cell.Button.IsHitTestVisible=false;int token=++cell.Token;
                if(!animate){appCanvas.Children.Remove(cell.Button);cells.Remove(cell.Entry.Key);continue;}
                var fade=Motion.Tween(cell.Button.Opacity,0,120);cell.Button.Opacity=0;fade.Completed+=(sender,args)=>{if(cell.Leaving&&cell.Token==token){appCanvas.Children.Remove(cell.Button);cells.Remove(cell.Entry.Key);}};cell.Button.BeginAnimation(UIElement.OpacityProperty,fade);cell.Offset.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(cell.Offset.Y,7,120));
            }
            indicators.Clear();
            for(int i=0;i<shown.Length;i++){
                var entry=shown[i];DockCell cell;bool added=!cells.TryGetValue(entry.Key,out cell);if(added){cell=CreateCell(entry);cells.Add(entry.Key,cell);appCanvas.Children.Add(cell.Button);}else{cell.Entry=entry;if(!Object.ReferenceEquals(cell.IconSource,entry.Icon)){cell.IconSource=entry.Icon;cell.Icon.Child=entry.Icon!=null?AppIcons.Visual(entry.Icon,30):new Glyph("window",30){Color=Glass95.DockForeground};}if((string)cell.Button.ToolTip!=entry.Name){cell.Button.ToolTip=entry.Name;System.Windows.Automation.AutomationProperties.SetName(cell.Button,entry.Name);}}
                cell.Icon.Effect=cell.Icon.Child is Glyph?null:DockAppearance.Readability;var fallback=cell.Icon.Child as Glyph;if(fallback!=null)fallback.Effect=DockAppearance.Readability;cell.Dot.Effect=DockAppearance.Readability;
                bool returning=cell.Leaving;cell.Leaving=false;cell.Token++;cell.Button.IsHitTestVisible=true;
                double x=DockLayout.AppOffset+i*46+(i>=pinned?12:0),oldX=Double.IsNaN(cell.X)?x+16:cell.X+cell.Offset.X;
                if(added||returning||resetPreview||Math.Abs(cell.X-x)>.01){cell.X=x;Canvas.SetLeft(cell.Button,x);cell.Offset.BeginAnimation(TranslateTransform.XProperty,null);cell.Offset.X=0;cell.Offset.BeginAnimation(TranslateTransform.YProperty,null);cell.Offset.Y=0;
                    if(animate){cell.Offset.BeginAnimation(TranslateTransform.XProperty,Motion.PanelTween(oldX-x,0,330));if(added||returning){cell.Offset.BeginAnimation(TranslateTransform.YProperty,Motion.PanelTween(6,0,330));var enter=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromMilliseconds(330),FillBehavior=FillBehavior.Stop};enter.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));enter.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(65))));enter.KeyFrames.Add(new SplineDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)),new KeySpline(.2,.7,.2,1)));cell.Button.BeginAnimation(UIElement.OpacityProperty,null);cell.Button.Opacity=1;cell.Button.BeginAnimation(UIElement.OpacityProperty,enter);}}else{cell.Button.BeginAnimation(UIElement.OpacityProperty,null);cell.Button.Opacity=1;}}
                cell.Dot.Visibility=entry.Windows.Count>0&&Preferences.Current.RunningDots?Visibility.Visible:Visibility.Collapsed;foreach(var window in entry.Windows)indicators[window]=cell.Dot;
            }
            OrientIcons();if(controller.Desktop&&ready)ResizeTo(layout,animate);UpdateActive(controller.LastApplication);
        }
        DockCell CreateCell(AppEntry entry)
        {
            var cell=new DockCell {Entry=entry,IconSource=entry.Icon};var content=new Grid {Width=44,Height=42};cell.Icon=new Border {Width=30,Height=30,Child=entry.Icon!=null?AppIcons.Visual(entry.Icon,30):new Glyph("window",30){Color=Glass95.DockForeground}};cell.Icon.VerticalAlignment=VerticalAlignment.Top;cell.Icon.Margin=new Thickness(0,4,0,0);RenderOptions.SetBitmapScalingMode(cell.Icon,BitmapScalingMode.HighQuality);content.Children.Add(cell.Icon);
            cell.Dot=new Border {Width=4,Height=4,CornerRadius=new CornerRadius(2),Background=Glass95.DockForeground,VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,2)};content.Children.Add(cell.Dot);
            cell.Button=Paint.Button(content,()=>{controller.Previews.Close();var before=pressedForeground;pressedForeground=IntPtr.Zero;controller.ActivateApp(cell.Entry,cell.Button,before);},entry.Name);cell.Button.Width=46;cell.Button.Height=42;cell.Button.Padding=new Thickness(0);cell.Button.Background=Brushes.Transparent;cell.Button.RenderTransform=cell.Offset;
            cell.Button.MouseEnter+=(s,e)=>{ToolTipService.SetIsEnabled(cell.Button,cell.Entry.Windows.Count==0);controller.Previews.Enter(cell.Entry,cell.Button,this);};cell.Button.MouseLeave+=(s,e)=>{controller.Previews.Leave();ToolTipService.SetIsEnabled(cell.Button,true);};cell.Button.PreviewMouseLeftButtonDown+=(s,e)=>controller.Previews.Close();cell.Button.ContextMenuOpening+=(s,e)=>controller.Previews.Close();
            cell.Button.ContextMenuOpening+=(sender,args)=>cell.Button.ContextMenu=WindowPicker.Menu(cell.Entry,controller);cell.Button.ContextMenu=WindowPicker.Menu(entry,controller);Motion.Dock(cell.Button,cell.Icon);DragApps.Attach(cell.Button,()=>cell.Entry);return cell;
        }
        internal void PreviewDock(string source,string target,bool after)
        {
            DockCell moving,anchor;if(!cells.TryGetValue(source,out moving)||!cells.TryGetValue(target,out anchor)||moving==anchor)return;var order=cells.Values.Where(c=>!c.Leaving&&c!=moving).OrderBy(c=>c.X).ToList();int index=order.IndexOf(anchor);order.Insert(index+(after?1:0),moving);PreviewOrder(order,moving,anchor.Entry.Pinned);
        }
        DockDrop DropTarget(string source,Point point)
        {
            bool pinned=point.X<dividerX;if(pendingDockDrop!=null&&Math.Abs(point.X-dividerX)<4)pinned=pendingDockDrop.Pinned;var candidates=cells.Values.Where(c=>!c.Leaving&&c.Entry.Key!=source&&c.Entry.Pinned==pinned).OrderBy(c=>c.X).ToList();if(candidates.Count==0)return new DockDrop {Pinned=pinned};
            if(pendingDockDrop!=null&&pendingDockDrop.Pinned==pinned){var previous=candidates.FirstOrDefault(c=>c.Entry.Key==pendingDockDrop.Target);if(previous!=null&&Math.Abs(point.X-(previous.X+23))<4)return pendingDockDrop;}
            var target=candidates.FirstOrDefault(c=>point.X<c.X+23)??candidates.Last();double middle=target.X+23;bool after=point.X>=middle;
            if(pendingDockDrop!=null&&pendingDockDrop.Target==target.Entry.Key&&pendingDockDrop.Pinned==pinned&&Math.Abs(point.X-middle)<4)after=pendingDockDrop.After;
            return new DockDrop {Target=target.Entry.Key,After=after,Pinned=pinned};
        }
        void PreviewAt(string source,Point point)
        {
            // Hit test the saved slot centers, not neighbors currently sliding under
            // the pointer. Otherwise each animation can trigger another reorder.
            pendingDockDrop=DropTarget(source,point);if(pendingDockDrop.Target==null)PreviewGroup(source,pendingDockDrop.Pinned);else PreviewDock(source,pendingDockDrop.Target,pendingDockDrop.After);
        }
        bool DropAt(string source,Point point){var target=DropTarget(source,point);return target.Target==null?controller.MoveDockGroup(source,target.Pinned):controller.MoveDock(source,target.Target,target.After);}
        void PreviewHome(string source){DockCell moving;if(!cells.TryGetValue(source,out moving))return;pendingDockDrop=null;PreviewOrder(cells.Values.Where(c=>!c.Leaving).OrderBy(c=>c.X).ToList(),moving,moving.Entry.Pinned);}
        void PreviewGroup(string source,bool pinned)
        {
            DockCell moving;if(!cells.TryGetValue(source,out moving))return;var order=cells.Values.Where(c=>!c.Leaving&&c!=moving).OrderBy(c=>c.X).ToList();order.Add(moving);PreviewOrder(order,moving,pinned);
        }
        void PreviewOrder(List<DockCell> order,DockCell moving,bool pinned)
        {
            order=order.OrderBy(c=>(c==moving?pinned:c.Entry.Pinned)?0:1).ToList();string previewKey=String.Join("|",order.Select(c=>c.Entry.Key))+"|"+moving.Entry.Key+"|"+pinned;if(dragPreview==previewKey)return;dragPreview=previewKey;int count=order.Count(c=>c==moving?pinned:c.Entry.Pinned);
            for(int i=0;i<order.Count;i++){var cell=order[i];double next=DockLayout.AppOffset+i*46+(i>=count?12:0)-cell.X,from=cell.Offset.X;cell.Offset.BeginAnimation(TranslateTransform.XProperty,null);cell.Offset.X=next;if(Motion.Enabled)cell.Offset.BeginAnimation(TranslateTransform.XProperty,Motion.Tween(from,next,160));}
            pinDivider.Visibility=count>0&&(count<order.Count||unpinnedOverflow)?Visibility.Visible:Visibility.Collapsed;double end=DockLayout.AppOffset+count*46+6-dividerX,start=dividerOffset.X;dividerOffset.BeginAnimation(TranslateTransform.XProperty,null);dividerOffset.X=end;if(Motion.Enabled)dividerOffset.BeginAnimation(TranslateTransform.XProperty,Motion.Tween(start,end,160));
        }
        internal void EndDragPreview(){pendingDockDrop=null;if(closed||dragPreview==null)return;dragPreview=null;resetPreview=true;Rebuild();resetPreview=false;}
        void ResizeTo(DockLayout layout,bool animate)
        {
            PlaceStatus(layout,visible);
            if(!placed){placed=true;targetLayout=layout;currentX=layout.AppX;currentWidth=layout.AppWidth;currentY=layout.Y;currentStartX=layout.StartX;PlaceDock(Start,new Rect(currentStartX,currentY+2,40,40),true);PlaceDock(Apps,new Rect(currentX,currentY,currentWidth,44),true);appCanvas.Width=currentWidth;return;}
            if(resizeTimer.IsEnabled&&targetLayout!=null&&Math.Abs(targetLayout.AppX-layout.AppX)<.01&&Math.Abs(targetLayout.AppWidth-layout.AppWidth)<.01&&Math.Abs(targetLayout.Y-layout.Y)<.01)return;
            targetLayout=layout;resizeTimer.Stop();
            if(!animate){currentX=layout.AppX;currentWidth=layout.AppWidth;currentY=layout.Y;currentStartX=layout.StartX;PlaceFrame();return;}
            if(Math.Abs(currentX-layout.AppX)<.01&&Math.Abs(currentWidth-layout.AppWidth)<.01&&Math.Abs(currentY-layout.Y)<.01)return;
            fromX=currentX;fromWidth=currentWidth;fromY=currentY;fromStartX=currentStartX;resizeDelay=layout.AppWidth<currentWidth?85:0;resizeWatch.Restart();resizeTimer.Start();
        }
        void ResizeFrame()
        {
            if(closed||targetLayout==null){resizeTimer.Stop();return;}double progress=Math.Max(0,Math.Min(1,(resizeWatch.Elapsed.TotalMilliseconds-resizeDelay)/(330-resizeDelay))),ease=1-Math.Pow(1-progress,3);
            currentX=fromX+(targetLayout.AppX-fromX)*ease;currentWidth=fromWidth+(targetLayout.AppWidth-fromWidth)*ease;currentY=fromY+(targetLayout.Y-fromY)*ease;currentStartX=fromStartX+(targetLayout.StartX-fromStartX)*ease;PlaceFrame();if(progress>=1)resizeTimer.Stop();
        }
        void PlaceFrame()
        {
            double y=currentY+(1-reveal)*8;appCanvas.Width=currentWidth;PlaceDock(Apps,new Rect(currentX,y,currentWidth,44),false);PlaceDock(Start,new Rect(currentStartX,y+2,40,40),false);if(targetLayout!=null)PlaceStatus(targetLayout,false,y);Native.Round(Apps,22*Zoom);if(MediaBar!=null)RefreshMedia();
        }
        internal string[] ShownKeys {get{return cells.Values.Where(c=>!c.Leaving).OrderBy(c=>c.X).Select(c=>c.Entry.Key).ToArray();}}
        internal Button AppButton(string key){DockCell cell;return cells.TryGetValue(key,out cell)?cell.Button:null;}
        internal double PinBoundary {get{return dividerX;}}
        internal void UpdateActive(IntPtr active) {Border selected;indicators.TryGetValue(active,out selected);foreach(var dot in indicators.Values.Distinct()){bool current=dot==selected;dot.Width=current?12:4;dot.Background=Glass95.DockForeground;dot.Opacity=current?1:.55;}}
        internal void SetVisible(bool value)
        {
            if(!value&&Overflow!=null)Overflow.Hide();
            if(closed)return;if(visible==value&&!visibilityTimer.IsEnabled){if(value)EnsureVisibleWindows();return;}visibilityTimer.Stop();visible=value;reveal=value?1:0;
            // Fullscreen/capture can reverse before a popup fade finishes.
            foreach(var w in new[]{Start,Apps,Status}){w.Opacity=1;Native.BlurOpacity(w,1);if(value)w.Show();else w.HideImmediately();}if(ready)PlaceFrame();RefreshMedia();
        }
        void AnimateAutoHide(bool value)
        {
            if(closed)return;if(!Motion.Enabled){SetVisible(value);if(!value&&revealEdge!=null)revealEdge.Show(true);return;}if(visible==value&&!visibilityTimer.IsEnabled){if(value)EnsureVisibleWindows();return;}
            bool hidden=!Apps.IsVisible;visible=value;if(hidden){reveal=0;foreach(var w in new[]{Start,Apps,Status}){w.Opacity=0;Native.BlurOpacity(w,0);w.Show();}}
            revealFrom=reveal;revealTarget=value?1:0;revealDuration=value?230:160;visibilityWatch.Restart();visibilityTimer.Start();ApplyReveal();
        }
        void EnsureVisibleWindows(bool raise=false){foreach(var window in new[]{Start,Apps,Status})Native.EnsureDockVisible(window,raise);}
        void RecoverPlacement()
        {
            if(targetLayout==null||resizeTimer.IsEnabled||visibilityTimer.IsEnabled)return;
            foreach(var window in new[]{Start,Apps,Status})
            {
                Native.RectI actual;
                if(window.DockBounds.HasValue&&(!Native.GetWindowRect(new WindowInteropHelper(window).Handle,out actual)||!actual.Equals(window.DockBounds.Value)))
                {
                    PlaceFrame();
                    return;
                }
            }
        }
        void ApplyReveal()
        {
            foreach(var w in new[]{Start,Apps,Status}){w.Opacity=reveal;w.Surface.IsHitTestVisible=visible&&reveal>.1;Native.BlurOpacity(w,reveal);}PlaceFrame();
        }
        void VisibilityFrame()
        {
            if(closed){visibilityTimer.Stop();return;}double t=Math.Max(0,Math.Min(1,visibilityWatch.Elapsed.TotalMilliseconds/revealDuration)),ease=1-Math.Pow(1-t,3);reveal=revealFrom+(revealTarget-revealFrom)*ease;ApplyReveal();if(t<1)return;
            visibilityTimer.Stop();if(!visible){foreach(var w in new[]{Start,Apps,Status})w.HideImmediately();RefreshMedia();if(revealEdge!=null&&!fullScreen&&autoHide)revealEdge.Show(true);}else{foreach(var w in new[]{Start,Apps,Status})w.Surface.IsHitTestVisible=true;QueueHide();}
        }
        bool InUse()
        {
            if(DateTime.UtcNow<interactionUntil||DragReorder.Active||Overflow.IsOpen||Start.IsMouseOver||Apps.IsMouseOver||Status.IsMouseOver||(Start.ContextMenu!=null&&Start.ContextMenu.IsOpen)||cells.Values.Any(c=>c.Button.ContextMenu!=null&&c.Button.ContextMenu.IsOpen)||controller.DockInUse(this))return true;
            if(!visible)return false;var pointer=Forms.Cursor.Position;var point=new Point((pointer.X-Screen.Bounds.Left)/ContentScale,(pointer.Y-Screen.Bounds.Top)/ContentScale);return targetLayout!=null&&DockGeometry.Rect(Screen.Bounds,ContentScale,Edge,new Rect(currentStartX-12,currentY-12,targetLayout.StatusX+targetLayout.StatusWidth-currentStartX+24,68)).Contains(point);
        }
        internal void HideAfterMediaDismissed(){if(autoHide&&!fullScreen){hideDelay.Stop();interactionUntil=DateTime.MinValue;AnimateAutoHide(false);}}
        internal void CompleteAutoHide(bool interacting){hideDelay.Stop();if(autoHide&&!fullScreen&&!interacting)AnimateAutoHide(false);}
        internal IntPtr RevealHandle {get{return revealEdge!=null?revealEdge.Handle:IntPtr.Zero;}}
        void QueueHide(){if(autoHide&&!fullScreen&&visible&&!hideDelay.IsEnabled&&!InUse())hideDelay.Start();}
        internal void Reveal()
        {
            if(closed||!autoHide||fullScreen||!controller.DisplayAwake)return;interactionUntil=DateTime.UtcNow.AddMilliseconds(1200);if(revealEdge!=null)revealEdge.Show(false);AnimateAutoHide(true);Refresh(controller.Power);QueueHide();
        }
        internal void UpdateVisibility(bool obscured,bool automatic,bool desktopIdle=false)
        {
            if(closed)return;bool changed=autoHide!=automatic;fullScreen=obscured;autoHide=automatic;if(changed){placementAt=DateTime.MinValue;if(ready)Rebuild();}
            if(!autoHide&&revealEdge!=null){revealEdge.Dispose();revealEdge=null;}
            if(fullScreen){hideDelay.Stop();SetVisible(false);if(revealEdge!=null)revealEdge.Show(false);return;}
            if(!autoHide)
            {
                hideDelay.Stop();
                SetVisible(true);
                if(DateTime.UtcNow>=placementAt)
                {
                    placementAt=DateTime.UtcNow.AddSeconds(2);
                    RecoverPlacement();
                    if(desktopIdle)EnsureVisibleWindows(true);
                }
                return;
            }
            if(revealEdge==null)revealEdge=new DockRevealEdge(Screen.Bounds,Reveal,Edge);
            if(visible&&!visibilityTimer.IsEnabled)EnsureVisibleWindows();
            revealEdge.Show(!visible);
            if(!visible&&!controller.Demo)
            {
                var pointer=Forms.Cursor.Position;
                revealEdge.ObservePointer(new Native.PointI {X=pointer.X,Y=pointer.Y});
            }
            QueueHide();
        }
        internal void Refresh(PowerState state)
        {
            if(closed) return;
            bool reserve=controller.Desktop&&(controller.Services.Media.Active||MediaBar!=null&&MediaBar.ReservesSpace);double expected=PowerDisplay.DockSlot(state,Preferences.Current.DesktopPowerWatts);if(expected!=powerWidth||reserve!=mediaSpace)Rebuild();
            if(MediaBar!=null&&MediaBar.InterfaceVersion!=Preferences.Current.UIVersion){MediaBar.Close();MediaBar=null;}
            if(MediaBar==null&&controller.Services.Media.Active&&controller.Desktop)MediaBar=new NowBar(controller,Screen){DismissedOutside=HideAfterMediaDismissed};
            RefreshMedia();powerAccess.ToolTip=state.Known?"배터리 · 빠른 설정":"직접 입력한 PC 전력값 · 실시간 측정 아님";
            Battery.Update(state);var now=DateTime.Now;clockFace.Update(now,Edge);clockAccess.ToolTip=now.ToString("yyyy-MM-dd ddd tt h:mm:ss",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"))+" · 알림센터";
            bool radioOff=controller.Services.WifiOn==false;string wifiKind=NetworkRouteInfo.Glyph(controller.Services.NetworkRoute,controller.Services.WifiPresent,radioOff,controller.Wifi!=null,controller.Services.NetworkConnected);
            string soundKind=SoundIcon.For(controller.Audio.Value,controller.Audio.Muted,controller.Audio.Available);
            if(wifiStatus.Kind!=wifiKind){wifiStatus.Kind=wifiKind;wifiStatus.InvalidateVisual();}wifiStatus.Opacity=wifiKind=="wifi"||wifiKind=="network"||wifiKind=="ethernet"?1:.55;
            int generation=wifiKind=="wifi"?controller.WifiGeneration:0;if(wifiStatus.Generation!=generation){wifiStatus.Generation=generation;wifiStatus.InvalidateVisual();}
            int signal=wifiKind=="wifi"?controller.WifiSignal:0,traffic=wifiKind=="wifi"?controller.WifiTraffic:-1;if(wifiStatus.Signal!=signal||wifiStatus.Traffic!=traffic){wifiStatus.Signal=signal;wifiStatus.Traffic=traffic;wifiStatus.InvalidateVisual();}
            if(volumeStatus.Kind!=soundKind){volumeStatus.Kind=soundKind;volumeStatus.InvalidateVisual();}volumeStatus.Opacity=controller.Audio.Available?1:.4;
            if(Preferences.Current.DuoStatusIcon)duoStatus.Update(state,wifiKind,signal,controller.Audio.Value,controller.Audio.Muted,controller.Audio.Available,Glass95.DockForeground);
            quickAccess.ToolTip=(wifiKind=="ethernet"?"유선랜 연결됨":wifiKind=="ethernet-off"?"유선랜 연결 안 됨":wifiKind=="network"?"네트워크 연결됨":radioOff?"Wi-Fi 꺼짐":controller.Wifi??(controller.Services.WifiOn.HasValue?"Wi-Fi 연결 안 됨":"Wi-Fi 상태 확인 중"))+" · "+(controller.Audio.Muted?"음소거":!controller.Audio.Available?"소리 장치 없음":"음량 "+controller.Audio.Value);
            if(generation>0)quickAccess.ToolTip="Wi-Fi "+generation+" · "+quickAccess.ToolTip;
            if(Preferences.Current.DuoStatusIcon&&state.Known)quickAccess.ToolTip+=" · 배터리 "+state.Level+"%";System.Windows.Automation.AutomationProperties.SetHelpText(quickAccess,(string)quickAccess.ToolTip);
            if(Battery.ActualWidth>0){double right=Battery.TranslatePoint(new Point(Battery.ActualWidth,0),Status.Surface).X;Battery.ExpandedWidth=Math.Max(Battery.ActualWidth,Math.Min(164,right-12));}
        }
        void RefreshMedia(){if(MediaBar!=null){double start=currentStartX*Zoom,y=(DockGeometry.Vertical(Edge)?12:currentY)*Zoom;y+=(1-reveal)*8*Zoom;MediaBar.ConstrainDock(start,y);MediaBar.Update(controller.Services.Media,Scale,start,y,(visible||visibilityTimer.IsEnabled)&&controller.Desktop&&controller.DisplayAwake&&start-24>=44);MediaBar.Opacity=reveal;Native.BlurOpacity(MediaBar,reveal);}}
        internal Point BatteryCenter()
        {
            FrameworkElement element=Preferences.Current.DuoStatusIcon?(FrameworkElement)duoStatus:Battery;return element.PointToScreen(new Point(element.ActualWidth/2,element.ActualHeight/2));
        }
        internal void DuoChargePulse(){duoStatus.ChargePulse();}
        internal TrayPlacement TrayAnchor()
        {
            // PointToScreen already returns physical pixels under per-monitor DPI awareness.
            return new TrayPlacement(trayAccess.PointToScreen(new Point(trayAccess.ActualWidth/2,0)),Screen.Bounds,Scale,Edge);
        }
        internal object Inspect() {return new {scale=Scale,visible=visible,autoHide=autoHide,revealEdge=EdgeVisible,apps=Native.RegionInfo(Apps),start=Native.RegionInfo(Start),status=Native.RegionInfo(Status),blur=Apps.HasBlur&&Status.HasBlur,nowBar=MediaBar!=null?Native.RegionInfo(MediaBar):null};}
        internal void Close() { if(closed) return; closed=true;DragApps.Dispose();DragReorder.Finished-=EndDragPreview;resizeTimer.Stop();visibilityTimer.Stop();hideDelay.Stop();Overflow.Dispose();if(revealEdge!=null){revealEdge.Dispose();revealEdge=null;}if(MediaBar!=null)MediaBar.Close();cells.Clear(); Start.Close(); Apps.Close(); Status.Close(); }
    }
    internal sealed class HudWindow : GlassWindow
    {
        readonly PillSlider slider=new PillSlider("volume") {Margin=new Thickness(10,9,10,9),IsHitTestVisible=false,AnimateValue=false,Light=true};
        internal readonly bool Modern=Preferences.Current.UIVersion>=90;
        internal double DisplayHeight {get{return Modern?QuickWidgetBoard.ControlHeight:62;}}
        internal PillSlider Slider {get{return slider;}}
        internal HudWindow() { Title="One TaskBar · 밝기 및 음량"; Width=286; Height=DisplayHeight; Radius=DisplayHeight/2;Dark=Glass95.Dark;UnifiedFade=true;Surface.Background=QuickPanel.Glass; Surface.CornerRadius=new CornerRadius(Radius); Surface.Child=slider; ShowActivated=false;
            if(Modern&&!Glass95.Enabled)slider.ValueNumberInk=Paint.ControlWhite;
            slider.DarkTrack=!Modern;
            if(Glass95.Enabled){slider.GlassStandalone=false;slider.ValueNumberInk=Paint.Text;}if(Modern){slider.Margin=new Thickness(0);slider.Height=Double.NaN;slider.MinWidth=0;slider.NoTrackBorder=true;slider.LinearFill=true;Surface.BorderThickness=new Thickness(0);Surface.BorderBrush=Brushes.Transparent;}
            SourceInitialized+=(s,e)=> { var h=new WindowInteropHelper(this).Handle; Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)|0x80|0x08000000);HwndSource.FromHwnd(h).CompositionTarget.RenderMode=RenderMode.SoftwareOnly; };
        }
        internal void Reveal(){Show();Surface.InvalidateVisual();Native.SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),0,0,0,0,0x53);}
        bool budsOutput;string deviceModel;ImageSource deviceSymbol;
        internal bool BudsIcon {get{return slider.HasDeviceIcon;}}
        async void LoadDeviceSymbol(string name){string model=BudsDeviceData.SymbolFile(name);deviceModel=model;try{var image=await BackgroundWork.Run(()=>BudsDeviceData.ShortcutIcon(name));if(deviceModel!=model)return;deviceSymbol=image;slider.SetDeviceIcon(budsOutput?deviceSymbol:null);}catch(Exception error){AppLog.Once("hud-device-symbol",error.GetType().Name);}}
        internal void Set(string kind,double value,bool muted,string outputName=null){string icon=kind=="volume"?SoundIcon.For(value,muted,true):kind;if(slider.Kind!=icon){slider.Kind=icon;slider.InvalidateVisual();}slider.Value=muted?0:value;budsOutput=kind=="volume"&&BudsInfo.IsBuds(outputName);if(budsOutput&&deviceModel!=BudsDeviceData.SymbolFile(outputName))LoadDeviceSymbol(outputName);slider.SetDeviceIcon(budsOutput?deviceSymbol:null);}
    }
    internal sealed class DrawerWindow : GlassWindow
    {
        readonly ShellController shell; readonly System.Windows.Controls.Primitives.UniformGrid items=new System.Windows.Controls.Primitives.UniformGrid {Columns=6,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top};
        double cellWidth=118,cellHeight=116;
        readonly Border searchBar;readonly SmoothScrollViewer appScroll;
        List<AppEntry> loadedApps;double loadedWidth;bool composing,searchQueued,closed,renaming;
        List<StartTile> tiles=new List<StartTile>();string folder,preview,group;
        readonly Dictionary<string,Button> buttons=new Dictionary<string,Button>(StringComparer.OrdinalIgnoreCase);
        readonly Grid folderHeading;readonly TextBox folderName;
        readonly TextBlock count=Paint.TextBlock("",12,Paint.Muted);
        internal readonly TextBox Search=new TextBox {FontSize=15,Foreground=Paint.Ink,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(6,8,6,8)};
        internal DrawerWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · 앱";Width=720;Height=680;Radius=32;Dark=true;StableSurface=true;SmoothLauncher=true;Surface.CornerRadius=new CornerRadius(32);Surface.BorderBrush=Paint.Brush("#58FFFFFF");Surface.BorderThickness=new Thickness(.7);Surface.Background=QuickPanel.Glass;
            var root=new DockPanel {Margin=new Thickness(24,20,24,16)};
            var header=new Grid {Height=42,Margin=new Thickness(12,0,12,20)};var title=Paint.TextBlock("앱",22,Paint.Text);title.FontWeight=FontWeights.SemiBold;header.Children.Add(title);
            var close=Paint.Action("닫기",()=>Hide()); close.HorizontalAlignment=HorizontalAlignment.Right; close.Height=32; header.Children.Add(close); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            folderHeading=new Grid {Height=42,Margin=new Thickness(6,0,6,8),Visibility=Visibility.Collapsed};folderHeading.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});folderHeading.ColumnDefinitions.Add(new ColumnDefinition());
            var back=Paint.Button(new Glyph("back",20),()=>CloseFolder(),"모든 앱으로 돌아가기 · 앱을 놓으면 폴더 밖으로 이동");back.Width=36;back.Background=Brushes.Transparent;folderHeading.Children.Add(back);
            folderName=new TextBox {FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=Paint.Text,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(5,7,5,7),MaxLength=48,ToolTip="폴더 이름 변경"};Grid.SetColumn(folderName,1);folderHeading.Children.Add(folderName);DockPanel.SetDock(folderHeading,Dock.Top);root.Children.Add(folderHeading);
            folderName.LostKeyboardFocus+=(s,e)=>CommitName();folderName.PreviewKeyDown+=(s,e)=> {if(e.Key==Key.Enter){CommitName();Keyboard.ClearFocus();e.Handled=true;}};
            DragReorder.DropArea(folderHeading,"drawer.apps",()=>folder!=null,(key,point)=> {bool moved=shell.AppScreen.Extract(key);if(moved)folder=null;return moved;},(key,point)=>preview="extract");
            var search=LauncherSearchField.Create(Search,()=>{Search.Clear();Search.Focus();});search.Margin=new Thickness(0,16,0,0);search.Width=search.MaxWidth=420;Search.MaxLength=120;
            searchBar=search;
            Search.TextChanged+=(s,e)=> {if(!composing)QueueSearch();};
            TextCompositionManager.AddPreviewTextInputStartHandler(Search,(s,e)=>composing=true);TextCompositionManager.AddPreviewTextInputUpdateHandler(Search,(s,e)=>composing=true);Search.PreviewTextInput+=(s,e)=> {composing=false;QueueSearch();};Search.PreviewMouseLeftButtonDown+=(s,e)=> {if(Search.Text.Length==0){shell.ToggleSearch(null);e.Handled=true;}};
            var bottom=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center};bottom.Children.Add(search);count.HorizontalAlignment=HorizontalAlignment.Center;count.Margin=new Thickness(0,8,0,0);bottom.Children.Add(count);DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
            appScroll=new SmoothScrollViewer {Content=items,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(appScroll);
            DragReorder.DropArea(items,"drawer.apps",()=>!closed&&IsVisible,DropApp,PreviewAppDrop,true);

            Surface.Child=root;TransitionContent=root;PreviewTextInput+=(s,e)=> {if(!Search.IsKeyboardFocusWithin&&!folderName.IsKeyboardFocusWithin&&!String.IsNullOrEmpty(e.Text)&&(Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Alt))==0){Search.Text+=e.Text;QueueSearch();e.Handled=true;}};Deactivated+=(s,e)=> { if(!Program.Inspectable&&!DragReorder.Active&&IsVisible&&!IsMouseOver&&!buttons.Values.Any(b=>b.ContextMenu!=null&&b.ContextMenu.IsOpen))Hide(); };
            DragReorder.Finished+=FinishDrag;Closed+=(s,e)=> {closed=true;DragReorder.Finished-=FinishDrag;};
        }
        void QueueSearch(){if(searchQueued||Search.Text.Length==0)return;searchQueued=true;Dispatcher.BeginInvoke(new Action(()=> {searchQueued=false;if(!composing&&IsVisible&&!String.IsNullOrWhiteSpace(Search.Text))shell.ToggleSearch(null,Search.Text);}),System.Windows.Threading.DispatcherPriority.Background);}
        internal void Configure(double width,double height)
        {
            searchBar.Width=Math.Max(200,Math.Min(420,width-112));
            int columns=Math.Max(1,Math.Min(6,(int)Math.Floor((width-64)/104)));items.Columns=columns;cellWidth=Math.Max(64,(width-64)/columns);cellHeight=112;items.Width=cellWidth*columns;
        }
        internal void Reload()
        {
            if(loadedApps==shell.Apps&&loadedWidth==cellWidth&&folder==null)return;loadedApps=shell.Apps;loadedWidth=cellWidth;folder=null;shell.AppScreen.Reconcile(shell.Apps);RefreshApps();
        }
        internal void RefreshApps(){tiles=shell.AppScreen.Tiles(shell.Apps);if(folder!=null&&!tiles.Any(t=>t.Key==folder&&t.Folder))folder=null;RenderApps();}
        internal void RestoreSearch(string value){bool previous=composing;composing=true;try{Search.Text=value??"";}finally{composing=previous;}}
        List<StartTile> Current(){var node=tiles.FirstOrDefault(t=>t.Key==folder&&t.Folder);return node==null?tiles:node.Children;}
        void RenderApps()
        {
            items.Children.Clear();buttons.Clear();folderHeading.Visibility=folder==null?Visibility.Collapsed:Visibility.Visible;if(folder!=null)folderName.Text=tiles.First(t=>t.Key==folder).Name;
            foreach(var value in Current()) {
                var tile=value;
                var body=new StackPanel {Width=cellWidth-16,Height=cellHeight-12};
                double iconSize=Math.Min(48,cellHeight-58);
                var icon=tile.Folder?StartWindow.FolderIcon(tile):AppIcons.Visual(tile.App,iconSize);icon.Margin=new Thickness(0,7,0,7);icon.HorizontalAlignment=HorizontalAlignment.Center;body.Children.Add(icon);
                var label=Paint.TextBlock(tile.Name,12,Paint.Text);label.TextAlignment=TextAlignment.Center;label.TextWrapping=TextWrapping.Wrap;label.TextTrimming=TextTrimming.CharacterEllipsis;label.MaxHeight=34;body.Children.Add(label);
                var cell=Paint.Button(body,()=> {if(tile.Folder)OpenFolder(tile.Key);else Launch(tile.App);},tile.Folder?tile.Name+" · 앱 "+tile.Children.Count+"개":tile.Name);cell.Padding=new Thickness(7,4,7,4);cell.Margin=new Thickness(2);cell.Background=Brushes.Transparent;items.Children.Add(cell);
                cell.Width=cellWidth-4;cell.Height=cellHeight-4;cell.ContextMenu=AppMenu(tile);cell.RenderTransform=new TranslateTransform();buttons[tile.Key]=cell;
                DragReorder.Attach(cell,cell,"drawer.apps",tile.Key,Orientation.Horizontal,()=>!closed&&IsVisible,MoveApp,PreviewMove);
            }
            if(shell.Apps.Count==0) items.Children.Add(Paint.TextBlock("설치된 앱을 불러오는 중…",14,Paint.Text));
            count.Text=(folder==null?shell.Apps.Count:Current().Count)+"개 앱";
        }
        async void Launch(AppEntry app){var modifiers=Keyboard.Modifiers;if(await DismissAsync())shell.LaunchApp(app,modifiers);}
        internal void OpenFolder(string key){if(!tiles.Any(t=>t.Key==key&&t.Folder))return;folder=key;RenderApps();LauncherMotion.Page(appScroll);}
        internal void CloseFolder(){CommitName();folder=null;RenderApps();LauncherMotion.Page(appScroll);}
        void CommitName(){if(renaming||folder==null)return;renaming=true;try{if(shell.AppScreen.Rename(folder,folderName.Text))tiles=shell.AppScreen.Tiles(shell.Apps);var node=tiles.FirstOrDefault(t=>t.Key==folder);if(node!=null)folderName.Text=node.Name;}finally{renaming=false;}}
        internal string[] AppKeys {get{return Current().Select(t=>t.Key).ToArray();}}
        internal Button AppButton(string key){Button button;return buttons.TryGetValue(key,out button)?button:null;}
        internal TextBox FolderNameEditor {get{return folderName;}}
        internal string OpenFolderKey {get{return folder;}}
        internal bool MoveApp(string source,string target,bool after){return folder==null?shell.AppScreen.Move(source,target,after):shell.AppScreen.MoveWithin(folder,source,target,after);}
        ContextMenu AppMenu(StartTile tile)
        {
            var menu=tile.Folder?new ContextMenu():WindowPicker.Menu(tile.App,shell,allowAdmin:true);
            if(tile.Folder){var rename=new MenuItem {Header="폴더 이름 변경"};rename.Click+=(s,e)=> {OpenFolder(tile.Key);folderName.Focus();folderName.SelectAll();};menu.Items.Add(rename);var dissolve=new MenuItem {Header="폴더 풀기 · 앱 유지"};dissolve.Click+=(s,e)=> {if(shell.AppScreen.Dissolve(tile.Key))RefreshApps();};menu.Items.Add(dissolve);}
            else if(folder!=null){menu.Items.Add(new Separator());var extract=new MenuItem {Header="폴더 밖으로 꺼내기"};extract.Click+=(s,e)=> {if(shell.AppScreen.Extract(tile.Key)){folder=null;RefreshApps();}};menu.Items.Add(extract);}
            else {var groupMenu=new MenuItem {Header="폴더로 묶기"};groupMenu.SubmenuOpened+=(s,e)=> {groupMenu.Items.Clear();foreach(var other in tiles.Where(t=>t.Key!=tile.Key)){var target=other;var item=new MenuItem {Header=target.Folder?target.Name+"에 추가":target.Name+"와 새 폴더"};item.Click+=(sender,args)=> {if(shell.AppScreen.Combine(tile.Key,target.Key))RefreshApps();};groupMenu.Items.Add(item);}};menu.Items.Add(new Separator());menu.Items.Add(groupMenu);}return menu;
        }
        string Target(Point point,out Point local)
        {
            foreach(var pair in buttons){var slot=System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(pair.Value);if(slot.Contains(point)){local=new Point(point.X-slot.Left-pair.Value.Margin.Left,point.Y-slot.Top-pair.Value.Margin.Top);return pair.Key;}}local=new Point();return null;
        }
        bool Groupable(string source,string target,Point point){return folder==null&&source!=target&&tiles.Any(t=>t.Key==source&&!t.Folder)&&buttons.ContainsKey(target)&&point.X>buttons[target].ActualWidth*.27&&point.X<buttons[target].ActualWidth*.73&&point.Y>8&&point.Y<buttons[target].ActualHeight*.68;}
        internal bool DropApp(string source,Point point){Point local;var target=Target(point,out local);if(target==null||target==source)return false;return Groupable(source,target,local)?shell.AppScreen.Combine(source,target):MoveApp(source,target,local.X>=buttons[target].ActualWidth/2);}
        void PreviewAppDrop(string source,Point point)
        {
            Point local;string target=Target(point,out local);if(group!=null&&buttons.ContainsKey(group))buttons[group].Background=Brushes.Transparent;group=null;
            if(target==null||target==source){foreach(var button in buttons.Values)Animate(button,0,0);preview="reset";return;}
            if(Groupable(source,target,local)){group=target;preview="folder|"+target;foreach(var button in buttons.Values)Animate(button,0,0);buttons[target].Background=Paint.Brush("#38FFFFFF");}else PreviewMove(source,target,local.X>=buttons[target].ActualWidth/2);
        }
        internal void PreviewMove(string source,string target,bool after)
        {
            string signature=source+"|"+target+"|"+after;if(preview==signature||!buttons.ContainsKey(source)||!buttons.ContainsKey(target))return;preview=signature;var keys=AppKeys.ToList();var original=keys.ToArray();var slots=keys.ToDictionary(k=>k,k=>System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(buttons[k]).TopLeft,StringComparer.OrdinalIgnoreCase);keys.Remove(source);keys.Insert(keys.IndexOf(target)+(after?1:0),source);
            for(int i=0;i<keys.Count;i++){var from=slots[keys[i]];var to=slots[original[i]];Animate(buttons[keys[i]],to.X-from.X,to.Y-from.Y);}
        }
        static void Animate(Button button,double x,double y){var transform=(TranslateTransform)button.RenderTransform;double fromX=transform.X,fromY=transform.Y;transform.BeginAnimation(TranslateTransform.XProperty,null);transform.BeginAnimation(TranslateTransform.YProperty,null);transform.X=x;transform.Y=y;if(Motion.Enabled){transform.BeginAnimation(TranslateTransform.XProperty,Motion.Tween(fromX,x,170));transform.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(fromY,y,170));}}
        internal void FinishDrag()
        {
            if(closed||preview==null)return;preview=null;group=null;var positions=buttons.ToDictionary(p=>p.Key,p=>p.Value.TranslatePoint(new Point(),items),StringComparer.OrdinalIgnoreCase);RefreshApps();items.UpdateLayout();foreach(var pair in buttons){Point old;if(!positions.TryGetValue(pair.Key,out old))continue;var at=pair.Value.TranslatePoint(new Point(),items);var shift=(TranslateTransform)pair.Value.RenderTransform;shift.X=old.X-at.X;shift.Y=old.Y-at.Y;Animate(pair.Value,0,0);}
        }
    }
}
