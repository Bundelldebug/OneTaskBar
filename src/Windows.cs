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
    internal sealed class DockHost
    {
        internal readonly Forms.Screen Screen;
        internal double Scale;
        internal readonly GlassWindow Start=new GlassWindow(),Apps=new GlassWindow(),Status=new GlassWindow();
        internal readonly BatteryPill Battery=new BatteryPill();
        readonly TextBlock clock=Paint.TextBlock("",12,Paint.Ink);
        readonly Glyph wifiStatus=new Glyph("wifi",22){Color=Paint.Ink},volumeStatus=new Glyph("volume",19){Color=Paint.Ink};
        readonly Button quickAccess,trayAccess;
        readonly Button startButton;readonly Glyph startGlyph=new Glyph("apps",23){Color=Paint.Ink};
        internal readonly Button TaskViewButton;
        readonly Button powerAccess;readonly ColumnDefinition powerColumn;double powerWidth=58;
        readonly ShellController controller;
        readonly Dictionary<IntPtr,Border> indicators=new Dictionary<IntPtr,Border>();
        sealed class DockCell {internal AppEntry Entry;internal Button Button;internal Border Dot;internal FrameworkElement Icon;internal TranslateTransform Offset=new TranslateTransform();internal double X=Double.NaN;internal bool Leaving;internal int Token;}
        readonly Canvas appCanvas=new Canvas {Height=42,VerticalAlignment=VerticalAlignment.Center,Background=Brushes.Transparent};
        readonly Border pinDivider=new Border {Width=1,Height=24,CornerRadius=new CornerRadius(.5),Background=Brushes.Black,IsHitTestVisible=false};
        readonly TranslateTransform dividerOffset=new TranslateTransform();double dividerX=Double.NaN;
        string dragPreview;bool resetPreview;
        readonly Dictionary<string,DockCell> cells=new Dictionary<string,DockCell>(StringComparer.OrdinalIgnoreCase);
        readonly System.Windows.Threading.DispatcherTimer resizeTimer=new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(16)};
        readonly Stopwatch resizeWatch=new Stopwatch();
        double currentX,currentWidth,currentY,currentStartX,fromX,fromWidth,fromY,fromStartX,resizeDelay;DockLayout targetLayout;bool placed;
        bool visible=true;
        IntPtr pressedForeground;
        bool closed;internal NowBar MediaBar {get;private set;}
        internal DockHost(ShellController shell,Forms.Screen screen)
        {
            controller=shell; Screen=screen;
            pinDivider.RenderTransform=dividerOffset;Canvas.SetTop(pinDivider,9);Panel.SetZIndex(pinDivider,2);appCanvas.Children.Add(pinDivider);DragReorder.DropArea(appCanvas,"dock.apps",()=>!closed,(key,point)=>controller.MoveDockGroup(key,point.X<dividerX),(key,point)=>PreviewGroup(key,point.X<dividerX));DragReorder.Finished+=EndDragPreview;
            var search=Paint.Button(new Glyph("search",20){Color=Paint.Ink},()=>controller.ToggleSearch(this),"검색");search.Padding=new Thickness(0);search.Background=Brushes.Transparent;search.Width=42;search.Height=42;Canvas.SetLeft(search,10);appCanvas.Children.Add(search);Apps.Surface.Child=appCanvas;
            var desktops=Paint.Button(new Glyph("desktops",19){Color=Paint.Ink},()=>controller.OpenTaskView(),"데스크톱 전환 · 작업 보기");TaskViewButton=desktops;desktops.Padding=new Thickness(0);desktops.Background=Brushes.Transparent;desktops.Width=desktops.Height=42;Canvas.SetLeft(desktops,52);appCanvas.Children.Add(desktops);
            resizeTimer.Tick+=(sender,args)=>ResizeFrame();

            foreach(var w in new GlassWindow[] {Start,Apps,Status}) {
                w.SoftCorners=true;w.Dark=false; w.ShowActivated=false; w.Radius=22; w.Surface.CornerRadius=new CornerRadius(22); w.Surface.Background=LightPanel.DockGlass;w.Surface.BorderBrush=Brushes.Transparent;w.Surface.BorderThickness=new Thickness(0);
                w.SourceInitialized+=(s,e)=> {
                    var h=new WindowInteropHelper((Window)s).Handle;
                    Native.SetWindowLong(h,-20,(Native.GetWindowLong(h,-20)|0x80|0x08000000)&~0x40000);
                    HwndSource.FromHwnd(h).AddHook((IntPtr window,int message,IntPtr wp,IntPtr lp,ref bool handled)=>{if(message==0x21){pressedForeground=Native.GetForegroundWindow();handled=true;return new IntPtr(3);}return IntPtr.Zero;});
                };
            }
            Start.Title="One TaskBar · 앱 서랍"; Apps.Title="One TaskBar · 앱"; Status.Title="One TaskBar · 상태";
            var start=Paint.Button(startGlyph,()=>controller.ToggleStartButton(this),"앱 목록");startButton=start;
            start.Width=start.Height=40; start.Padding=new Thickness(0); start.Background=Brushes.Transparent;
            var transform=new ScaleTransform(1,1); start.RenderTransform=transform; start.RenderTransformOrigin=new Point(.5,.5);
            start.MouseEnter+=(s,e)=>Animate(transform,1.09); start.MouseLeave+=(s,e)=>Animate(transform,1);
            Start.Surface.Child=start;
            var status=new Grid { Margin=new Thickness(7,0,7,0) };
            status.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(32)});
            status.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            powerColumn=new ColumnDefinition {Width=new GridLength(58)};status.ColumnDefinitions.Add(powerColumn);
            status.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(94)});
            var native=Paint.Button(new Glyph("up",15) {Color=Paint.Ink},()=>controller.ShowNative("tray",this),"Windows 숨겨진 아이콘 · 트레이");
            trayAccess=native;
            native.Padding=new Thickness(0); native.Background=Brushes.Transparent; status.Children.Add(native);
            var qsContent=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
            wifiStatus.Margin=new Thickness(2,0,10,0);qsContent.Children.Add(wifiStatus);qsContent.Children.Add(volumeStatus);
            var qs=Paint.Button(qsContent,()=>controller.ToggleQuick(this),"빠른 설정"); qs.Padding=new Thickness(0); qs.Background=Brushes.Transparent;
            Grid.SetColumn(qs,1); status.Children.Add(qs);
            quickAccess=qs;
            Battery.MorphChanged=v=> {native.Opacity=qs.Opacity=Math.Max(0,1-v);};
            var battery=Paint.Button(Battery,()=>controller.ToggleQuick(this),"배터리 · 빠른 설정"); battery.Padding=new Thickness(0); battery.Background=Brushes.Transparent;
            powerAccess=battery;
            Grid.SetColumn(battery,2);Panel.SetZIndex(battery,10);status.Children.Add(battery);
            var time=Paint.Button(clock,()=>controller.ToggleNotifications(this),"알림센터");time.Padding=new Thickness(0);time.Background=Brushes.Transparent;
            Grid.SetColumn(time,3); status.Children.Add(time); Status.Surface.Child=status;
            var menu=new ContextMenu(); var settings=new MenuItem {Header="One TaskBar 설정",Icon=new Glyph("settings",18){Color=Paint.Ink}};settings.Click+=(s,e)=>controller.ShowWelcome();menu.Items.Add(settings);
            var resetOrder=new MenuItem {Header="앱 순서 초기화"};resetOrder.Click+=(s,e)=>controller.ResetDockOrder();menu.Items.Add(resetOrder);
            var restore=new MenuItem {Header="Windows 작업표시줄로 돌아가기"};restore.Click+=(s,e)=>controller.SetDesktop(false);menu.Items.Add(restore);Start.ContextMenu=menu;
            Rebuild();
        }
        static void Animate(ScaleTransform transform,double value)
        {
            if(!Preferences.Current.Hover||!Preferences.Current.Animations)value=1;
            var a=new DoubleAnimation(value,TimeSpan.FromMilliseconds(140)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};
            transform.BeginAnimation(ScaleTransform.ScaleXProperty,a); transform.BeginAnimation(ScaleTransform.ScaleYProperty,a);
        }
        internal void Show()
        {
            new WindowInteropHelper(Apps).EnsureHandle();
            Native.SetWindowPos(new WindowInteropHelper(Apps).Handle,IntPtr.Zero,Screen.Bounds.Left+12,Screen.Bounds.Top+12,0,0,0x1|0x4|0x10);
            Scale=Math.Max(96,Native.GetDpiForWindow(new WindowInteropHelper(Apps).Handle))/96.0;
            placed=false;Rebuild();
        }
        internal void Rebuild()
        {
            if(closed||DragReorder.Active)return;if(Scale<=0)Scale=1;
            startGlyph.Kind=Preferences.Current.NativeStartButton?"windows-start":"apps";startGlyph.Color=Preferences.Current.NativeStartButton?Paint.Brush("#FF1975DD"):Paint.Ink;startGlyph.InvalidateVisual();startButton.ToolTip=Preferences.Current.NativeStartButton?"Windows 시작":"앱 목록";System.Windows.Automation.AutomationProperties.SetName(startButton,(string)startButton.ToolTip);
            var entries=controller.DockApps();powerWidth=PowerDisplay.Slot(controller.Power,Preferences.Current.DesktopPowerWatts);powerColumn.Width=new GridLength(powerWidth);powerAccess.Visibility=powerWidth>0?Visibility.Visible:Visibility.Collapsed;Battery.Width=powerWidth==78?70:52;
            var layout=DockLayout.Compute(Screen.Bounds,Scale,Math.Max(1,entries.Count),powerWidth);var shown=entries.Skip(Math.Max(0,entries.Count-layout.Capacity)).Take(layout.Capacity).ToArray();var keys=new HashSet<string>(shown.Select(e=>e.Key),StringComparer.OrdinalIgnoreCase);bool animate=placed&&visible&&Apps.IsVisible&&Motion.Enabled;
            int pinned=shown.Count(a=>a.Pinned);double nextDivider=DockLayout.AppOffset+pinned*46+6,oldDivider=Double.IsNaN(dividerX)?nextDivider:dividerX+dividerOffset.X;dividerX=nextDivider;Canvas.SetLeft(pinDivider,dividerX);dividerOffset.BeginAnimation(TranslateTransform.XProperty,null);dividerOffset.X=0;if(animate&&Math.Abs(oldDivider-nextDivider)>.01)dividerOffset.BeginAnimation(TranslateTransform.XProperty,Motion.PanelTween(oldDivider-nextDivider,0,330));
            foreach(var cell in cells.Values.Where(c=>!keys.Contains(c.Entry.Key)).ToArray()){
                if(cell.Leaving)continue;cell.Leaving=true;cell.Button.IsHitTestVisible=false;int token=++cell.Token;
                if(!animate){appCanvas.Children.Remove(cell.Button);cells.Remove(cell.Entry.Key);continue;}
                var fade=Motion.Tween(cell.Button.Opacity,0,120);cell.Button.Opacity=0;fade.Completed+=(sender,args)=>{if(cell.Leaving&&cell.Token==token){appCanvas.Children.Remove(cell.Button);cells.Remove(cell.Entry.Key);}};cell.Button.BeginAnimation(UIElement.OpacityProperty,fade);cell.Offset.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(cell.Offset.Y,7,120));
            }
            indicators.Clear();
            for(int i=0;i<shown.Length;i++){
                var entry=shown[i];DockCell cell;bool added=!cells.TryGetValue(entry.Key,out cell);if(added){cell=CreateCell(entry);cells.Add(entry.Key,cell);appCanvas.Children.Add(cell.Button);}else cell.Entry=entry;
                bool returning=cell.Leaving;cell.Leaving=false;cell.Token++;cell.Button.IsHitTestVisible=true;
                double x=DockLayout.AppOffset+i*46+(i>=pinned?12:0),oldX=Double.IsNaN(cell.X)?x+16:cell.X+cell.Offset.X;
                if(added||returning||resetPreview||Math.Abs(cell.X-x)>.01){cell.X=x;Canvas.SetLeft(cell.Button,x);cell.Offset.BeginAnimation(TranslateTransform.XProperty,null);cell.Offset.X=0;cell.Offset.BeginAnimation(TranslateTransform.YProperty,null);cell.Offset.Y=0;
                    if(animate){cell.Offset.BeginAnimation(TranslateTransform.XProperty,Motion.PanelTween(oldX-x,0,330));if(added||returning){cell.Offset.BeginAnimation(TranslateTransform.YProperty,Motion.PanelTween(6,0,330));var enter=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromMilliseconds(330),FillBehavior=FillBehavior.Stop};enter.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));enter.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(65))));enter.KeyFrames.Add(new SplineDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)),new KeySpline(.2,.7,.2,1)));cell.Button.BeginAnimation(UIElement.OpacityProperty,null);cell.Button.Opacity=1;cell.Button.BeginAnimation(UIElement.OpacityProperty,enter);}}else{cell.Button.BeginAnimation(UIElement.OpacityProperty,null);cell.Button.Opacity=1;}}
                cell.Dot.Visibility=entry.Windows.Count>0&&Preferences.Current.RunningDots?Visibility.Visible:Visibility.Collapsed;foreach(var window in entry.Windows)indicators[window]=cell.Dot;
            }
            if(controller.Desktop)ResizeTo(layout,animate);UpdateActive(controller.LastApplication);
        }
        DockCell CreateCell(AppEntry entry)
        {
            var cell=new DockCell {Entry=entry};var content=new Grid {Width=44,Height=42};cell.Icon=entry.Icon!=null?AppIcons.Visual(entry.Icon,30):new Glyph("window",30){Color=Paint.Ink};cell.Icon.VerticalAlignment=VerticalAlignment.Top;cell.Icon.Margin=new Thickness(0,4,0,0);RenderOptions.SetBitmapScalingMode(cell.Icon,BitmapScalingMode.HighQuality);content.Children.Add(cell.Icon);
            cell.Dot=new Border {Width=4,Height=4,CornerRadius=new CornerRadius(2),Background=Paint.Ink,VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,2)};content.Children.Add(cell.Dot);
            cell.Button=Paint.Button(content,()=>{controller.Previews.Close();var before=pressedForeground;pressedForeground=IntPtr.Zero;controller.ActivateApp(cell.Entry,cell.Button,before);},entry.Name);cell.Button.Width=46;cell.Button.Height=42;cell.Button.Padding=new Thickness(0);cell.Button.Background=Brushes.Transparent;cell.Button.RenderTransform=cell.Offset;
            cell.Button.MouseEnter+=(s,e)=>{ToolTipService.SetIsEnabled(cell.Button,cell.Entry.Windows.Count==0);controller.Previews.Enter(cell.Entry,cell.Button,this);};cell.Button.MouseLeave+=(s,e)=>{controller.Previews.Leave();ToolTipService.SetIsEnabled(cell.Button,true);};cell.Button.PreviewMouseLeftButtonDown+=(s,e)=>controller.Previews.Close();cell.Button.ContextMenuOpening+=(s,e)=>controller.Previews.Close();
            cell.Button.ContextMenuOpening+=(sender,args)=>cell.Button.ContextMenu=WindowPicker.Menu(cell.Entry,controller);cell.Button.ContextMenu=WindowPicker.Menu(entry,controller);Motion.Dock(cell.Button,cell.Icon);DragReorder.Attach(cell.Button,cell.Button,"dock.apps",entry.Key,Orientation.Horizontal,()=>!closed,controller.MoveDock,PreviewDock);return cell;
        }
        internal void PreviewDock(string source,string target,bool after)
        {
            DockCell moving,anchor;if(!cells.TryGetValue(source,out moving)||!cells.TryGetValue(target,out anchor)||moving==anchor)return;var order=cells.Values.Where(c=>!c.Leaving&&c!=moving).OrderBy(c=>c.X).ToList();int index=order.IndexOf(anchor);order.Insert(index+(after?1:0),moving);PreviewOrder(order,moving,anchor.Entry.Pinned,source+"|"+target+"|"+after);
        }
        void PreviewGroup(string source,bool pinned)
        {
            DockCell moving;if(!cells.TryGetValue(source,out moving))return;var order=cells.Values.Where(c=>!c.Leaving&&c!=moving).OrderBy(c=>c.X).ToList();order.Add(moving);PreviewOrder(order,moving,pinned,source+"|group|"+pinned);
        }
        void PreviewOrder(List<DockCell> order,DockCell moving,bool pinned,string signature)
        {
            if(dragPreview==signature)return;dragPreview=signature;order=order.OrderBy(c=>(c==moving?pinned:c.Entry.Pinned)?0:1).ToList();int count=order.Count(c=>c==moving?pinned:c.Entry.Pinned);
            for(int i=0;i<order.Count;i++){var cell=order[i];double next=DockLayout.AppOffset+i*46+(i>=count?12:0)-cell.X,from=cell.Offset.X;cell.Offset.BeginAnimation(TranslateTransform.XProperty,null);cell.Offset.X=next;if(Motion.Enabled)cell.Offset.BeginAnimation(TranslateTransform.XProperty,Motion.Tween(from,next,160));}
            double end=DockLayout.AppOffset+count*46+6-dividerX,start=dividerOffset.X;dividerOffset.BeginAnimation(TranslateTransform.XProperty,null);dividerOffset.X=end;if(Motion.Enabled)dividerOffset.BeginAnimation(TranslateTransform.XProperty,Motion.Tween(start,end,160));
        }
        internal void EndDragPreview(){if(closed||dragPreview==null)return;dragPreview=null;resetPreview=true;Rebuild();resetPreview=false;}
        void ResizeTo(DockLayout layout,bool animate)
        {
            Status.ShowAt(Screen.Bounds,layout.StatusX,layout.Y,layout.StatusWidth,44);
            if(!placed){placed=true;targetLayout=layout;currentX=layout.AppX;currentWidth=layout.AppWidth;currentY=layout.Y;currentStartX=layout.StartX;Start.ShowAt(Screen.Bounds,currentStartX,currentY+2,40,40);Apps.ShowAt(Screen.Bounds,currentX,currentY,currentWidth,44);return;}
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
            Apps.Width=currentWidth;Apps.Height=44;Start.Width=Start.Height=40;Native.Place(Apps,Screen.Bounds,currentX,currentY,currentWidth,44);Native.Place(Start,Screen.Bounds,currentStartX,currentY+2,40,40);Native.Round(Apps,22);if(MediaBar!=null)MediaBar.ConstrainDock(currentStartX,currentY);
        }
        internal string[] ShownKeys {get{return cells.Values.Where(c=>!c.Leaving).OrderBy(c=>c.X).Select(c=>c.Entry.Key).ToArray();}}
        internal Button AppButton(string key){DockCell cell;return cells.TryGetValue(key,out cell)?cell.Button:null;}
        internal double PinBoundary {get{return dividerX;}}
        internal void UpdateActive(IntPtr active) {Border selected;indicators.TryGetValue(active,out selected);foreach(var dot in indicators.Values.Distinct()){bool current=dot==selected;dot.Width=current?12:4;dot.Background=current?Brushes.Black:Paint.Brush("#AF262C2A");}}
        internal void SetVisible(bool value) {if(closed||visible==value)return;visible=value;foreach(var w in new[]{Start,Apps,Status}) {if(value)w.Show();else w.Hide();}}
        internal void Refresh(PowerState state)
        {
            if(closed) return;
            if(MediaBar==null&&controller.Services.Media.Active&&controller.Desktop)MediaBar=new NowBar(controller,Screen);
            if(MediaBar!=null)MediaBar.Update(controller.Services.Media,Scale,currentStartX,currentY,visible&&controller.Desktop&&controller.DisplayAwake);
            double expected=PowerDisplay.Slot(state,Preferences.Current.DesktopPowerWatts);if(expected!=powerWidth)Rebuild();powerAccess.ToolTip=state.Known?"배터리 · 빠른 설정":"직접 입력한 PC 전력값 · 실시간 측정 아님";
            Battery.Update(state);clock.Text=DateTime.Now.ToString(Preferences.Current.Seconds?"tt h:mm:ss\nyyyy-MM-dd":"tt h:mm\nyyyy-MM-dd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));
            bool radioOff=controller.Services.WifiOn==false;string wifiKind=controller.Services.WifiPresent==false?(controller.Services.NetworkConnected?"network":"network-off"):radioOff?"wifi-off":controller.Wifi==null?"wifi-disconnected":"wifi";
            string soundKind=SoundIcon.For(controller.Audio.Value,controller.Audio.Muted,controller.Audio.Available);
            if(wifiStatus.Kind!=wifiKind){wifiStatus.Kind=wifiKind;wifiStatus.InvalidateVisual();}wifiStatus.Opacity=wifiKind=="wifi"||wifiKind=="network"?1:.55;
            int generation=wifiKind=="wifi"?controller.WifiGeneration:0;if(wifiStatus.Generation!=generation){wifiStatus.Generation=generation;wifiStatus.InvalidateVisual();}
            int signal=wifiKind=="wifi"?controller.WifiSignal:0,traffic=wifiKind=="wifi"?controller.WifiTraffic:-1;if(wifiStatus.Signal!=signal||wifiStatus.Traffic!=traffic){wifiStatus.Signal=signal;wifiStatus.Traffic=traffic;wifiStatus.InvalidateVisual();}
            if(volumeStatus.Kind!=soundKind){volumeStatus.Kind=soundKind;volumeStatus.InvalidateVisual();}volumeStatus.Opacity=controller.Audio.Available?1:.4;
            quickAccess.ToolTip=(controller.Services.WifiPresent==false?(controller.Services.NetworkConnected?"네트워크 연결됨":"네트워크 연결 안 됨"):radioOff?"Wi-Fi 꺼짐":controller.Wifi??(controller.Services.WifiOn.HasValue?"Wi-Fi 연결 안 됨":"Wi-Fi 상태 확인 중"))+" · "+(controller.Audio.Muted?"음소거":!controller.Audio.Available?"소리 장치 없음":"음량 "+controller.Audio.Value);
            if(generation>0)quickAccess.ToolTip="Wi-Fi "+generation+" · "+quickAccess.ToolTip;
            if(Battery.ActualWidth>0){double right=Battery.TranslatePoint(new Point(Battery.ActualWidth,0),Status.Surface).X;Battery.ExpandedWidth=Math.Max(Battery.ActualWidth,Math.Min(164,right-12));}
        }
        internal Point BatteryCenter()
        {
            var p=Battery.PointToScreen(new Point(Battery.ActualWidth/2,Battery.ActualHeight/2)); return p;
        }
        internal TrayPlacement TrayAnchor()
        {
            // PointToScreen already returns physical pixels under per-monitor DPI awareness.
            return new TrayPlacement(trayAccess.PointToScreen(new Point(trayAccess.ActualWidth/2,0)),Screen.Bounds,Scale);
        }
        internal object Inspect() {return new {scale=Scale,apps=Native.RegionInfo(Apps),start=Native.RegionInfo(Start),status=Native.RegionInfo(Status),blur=Apps.HasBlur&&Status.HasBlur,nowBar=MediaBar!=null?Native.RegionInfo(MediaBar):null};}
        internal void Close() { if(closed) return; closed=true;DragReorder.Finished-=EndDragPreview;resizeTimer.Stop();if(MediaBar!=null)MediaBar.Close();cells.Clear(); Start.Close(); Apps.Close(); Status.Close(); }
    }
    internal sealed class HudWindow : GlassWindow
    {
        readonly PillSlider slider=new PillSlider("volume") {Margin=new Thickness(10,9,10,9),IsHitTestVisible=false,AnimateValue=false,Light=true};
        internal HudWindow() { Title="One TaskBar · 밝기 및 음량"; Width=286; Height=62; Radius=31;Dark=false;Surface.Background=GrayPanel.Glass; Surface.CornerRadius=new CornerRadius(31); Surface.Child=slider; ShowActivated=false;
            SourceInitialized+=(s,e)=> { var h=new WindowInteropHelper(this).Handle; Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)|0x80|0x08000000); };
        }
        internal void Set(string kind,double value,bool muted) { slider.Kind=kind=="volume"?SoundIcon.For(value,muted,true):kind;slider.Value=muted?0:value; }
    }
    internal sealed class DrawerWindow : GlassWindow
    {
        readonly ShellController shell; readonly System.Windows.Controls.Primitives.UniformGrid items=new System.Windows.Controls.Primitives.UniformGrid {Columns=6,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top};
        double cellWidth=118,cellHeight=116;
        readonly Border searchBar;
        List<AppEntry> loadedApps;double loadedWidth;bool composing,searchQueued,closed,renaming;
        List<StartTile> tiles=new List<StartTile>();string folder,preview,group;
        readonly Dictionary<string,Button> buttons=new Dictionary<string,Button>(StringComparer.OrdinalIgnoreCase);
        readonly Grid folderHeading;readonly TextBox folderName;
        readonly TextBlock count=Paint.TextBlock("",12,Paint.Muted);
        internal readonly TextBox Search=new TextBox {FontSize=15,Foreground=Paint.Ink,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(6,8,6,8)};
        internal DrawerWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · 앱";Width=720;Height=680;Radius=32;Dark=true;StableSurface=true;Surface.CornerRadius=new CornerRadius(32);Surface.BorderBrush=Paint.Brush("#58FFFFFF");Surface.BorderThickness=new Thickness(.7);Surface.Background=QuickPanel.Glass;
            var root=new DockPanel {Margin=new Thickness(24,20,24,16)};
            var header=new Grid {Height=42,Margin=new Thickness(12,0,12,20)};var title=Paint.TextBlock("앱",22,Paint.Text);title.FontWeight=FontWeights.SemiBold;header.Children.Add(title);
            var close=Paint.Action("닫기",()=>Hide()); close.HorizontalAlignment=HorizontalAlignment.Right; close.Height=32; header.Children.Add(close); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            folderHeading=new Grid {Height=42,Margin=new Thickness(6,0,6,8),Visibility=Visibility.Collapsed};folderHeading.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});folderHeading.ColumnDefinitions.Add(new ColumnDefinition());
            var back=Paint.Button(new Glyph("back",20),()=>CloseFolder(),"모든 앱으로 돌아가기 · 앱을 놓으면 폴더 밖으로 이동");back.Width=36;back.Background=Brushes.Transparent;folderHeading.Children.Add(back);
            folderName=new TextBox {FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=Paint.Text,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(5,7,5,7),MaxLength=48,ToolTip="폴더 이름 변경"};Grid.SetColumn(folderName,1);folderHeading.Children.Add(folderName);DockPanel.SetDock(folderHeading,Dock.Top);root.Children.Add(folderHeading);
            folderName.LostKeyboardFocus+=(s,e)=>CommitName();folderName.PreviewKeyDown+=(s,e)=> {if(e.Key==Key.Enter){CommitName();Keyboard.ClearFocus();e.Handled=true;}};
            DragReorder.DropArea(folderHeading,"drawer.apps",()=>folder!=null,(key,point)=> {bool moved=shell.AppScreen.Extract(key);if(moved)folder=null;return moved;},(key,point)=>preview="extract");
            var search=new Border {Background=Paint.Brush("#EDFFFFFF"),CornerRadius=new CornerRadius(24),Padding=new Thickness(17,3,17,3),Margin=new Thickness(0,16,0,0),Width=420,MaxWidth=420};
            searchBar=search;
            var field=new Grid();field.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(27)});field.ColumnDefinitions.Add(new ColumnDefinition());field.Children.Add(new Glyph("search",20){Color=LightPanel.Muted,VerticalAlignment=VerticalAlignment.Center});Search.MaxLength=120;Grid.SetColumn(Search,1);field.Children.Add(Search);var hint=Paint.TextBlock("검색",15,LightPanel.Muted);hint.IsHitTestVisible=false;hint.Margin=new Thickness(6,0,0,0);Grid.SetColumn(hint,1);field.Children.Add(hint);search.Child=field;
            Search.TextChanged+=(s,e)=> {hint.Visibility=Search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;if(!composing)QueueSearch();};
            TextCompositionManager.AddPreviewTextInputStartHandler(Search,(s,e)=>composing=true);TextCompositionManager.AddPreviewTextInputUpdateHandler(Search,(s,e)=>composing=true);Search.PreviewTextInput+=(s,e)=> {composing=false;QueueSearch();};Search.PreviewMouseLeftButtonDown+=(s,e)=> {if(Search.Text.Length==0){shell.ToggleSearch(null);e.Handled=true;}};
            var bottom=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center};bottom.Children.Add(search);count.HorizontalAlignment=HorizontalAlignment.Center;count.Margin=new Thickness(0,8,0,0);bottom.Children.Add(count);DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
            root.Children.Add(new SmoothScrollViewer {Content=items,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
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
        async void Launch(AppEntry app){if(await DismissAsync())app.Launch();}
        internal void OpenFolder(string key){if(!tiles.Any(t=>t.Key==key&&t.Folder))return;folder=key;RenderApps();if(Motion.Enabled)Motion.Fade(items,.55,1,160);}
        internal void CloseFolder(){CommitName();folder=null;RenderApps();}
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
