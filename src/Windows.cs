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
        readonly Glyph wifiStatus=new Glyph("wifi",20){Color=Paint.Ink},volumeStatus=new Glyph("volume",19){Color=Paint.Ink};
        readonly Button quickAccess,trayAccess;
        readonly Button powerAccess;readonly ColumnDefinition powerColumn;double powerWidth=58;
        readonly ShellController controller;
        readonly Dictionary<IntPtr,Border> indicators=new Dictionary<IntPtr,Border>();
        bool visible=true;
        bool closed;
        internal DockHost(ShellController shell,Forms.Screen screen)
        {
            controller=shell; Screen=screen;
            foreach(var w in new GlassWindow[] {Start,Apps,Status}) {
                w.SoftCorners=true;w.Dark=false; w.ShowActivated=false; w.Radius=22; w.Surface.CornerRadius=new CornerRadius(22); w.Surface.Background=LightPanel.DockGlass;w.Surface.BorderBrush=Brushes.Transparent;w.Surface.BorderThickness=new Thickness(0);
                w.SourceInitialized+=(s,e)=> {
                    var h=new WindowInteropHelper((Window)s).Handle;
                    Native.SetWindowLong(h,-20,(Native.GetWindowLong(h,-20)|0x80)&~0x40000);
                };
            }
            Start.Title="One TaskBar · 앱 서랍"; Apps.Title="One TaskBar · 앱"; Status.Title="One TaskBar · 상태";
            var start=Paint.Button(new Glyph("apps",23) { Color=Paint.Ink },()=>controller.ToggleDrawer(this),"앱 서랍");
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
            var time=Paint.Button(clock,()=>controller.ToggleNotifications(this),"알림 · 달력");time.Padding=new Thickness(0);time.Background=Brushes.Transparent;
            Grid.SetColumn(time,3); status.Children.Add(time); Status.Surface.Child=status;
            var menu=new ContextMenu(); var settings=new MenuItem {Header="One TaskBar 설정"};settings.Click+=(s,e)=>controller.ShowWelcome();menu.Items.Add(settings);
            var resetOrder=new MenuItem {Header="앱 순서 초기화"};resetOrder.Click+=(s,e)=>LayoutOrder.Reset("dock.apps");menu.Items.Add(resetOrder);
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
            Rebuild();
        }
        internal void Rebuild()
        {
            if(closed||DragReorder.Active) return;
            if(Scale<=0) Scale=1;
            var entries=controller.DockApps();
            indicators.Clear();
            powerWidth=PowerDisplay.Slot(controller.Power,Preferences.Current.DesktopPowerWatts);powerColumn.Width=new GridLength(powerWidth);powerAccess.Visibility=powerWidth>0?Visibility.Visible:Visibility.Collapsed;Battery.Width=powerWidth==78?70:52;
            var layout=DockLayout.Compute(Screen.Bounds,Scale,Math.Max(1,entries.Count),powerWidth);
            var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
            var search=Paint.Button(new Glyph("search",20) {Color=Paint.Ink},()=>controller.ToggleSearch(this),"검색"); search.Padding=new Thickness(0); search.Background=Brushes.Transparent; search.Width=42; search.Height=42; row.Children.Add(search);
            foreach(var entry in entries.Take(layout.Capacity)) {
                var content=new Grid {Width=44,Height=42};
                FrameworkElement icon=entry.Icon!=null ? AppIcons.Visual(entry.Icon,30) : new Glyph("window",30) {Color=Paint.Ink};icon.VerticalAlignment=VerticalAlignment.Top;icon.Margin=new Thickness(0,4,0,0);
                RenderOptions.SetBitmapScalingMode(icon,BitmapScalingMode.HighQuality);
                content.Children.Add(icon);
                if(entry.Windows.Count>0&&Preferences.Current.RunningDots) {var dot=new Border {Width=4,Height=4,CornerRadius=new CornerRadius(2),Background=Paint.Ink,VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,3)};
                    dot.Margin=new Thickness(0,0,0,2);content.Children.Add(dot);foreach(var h in entry.Windows)indicators[h]=dot;}
                var app=entry;Button button=null;button=Paint.Button(content,()=>controller.ActivateApp(app,button),app.Name);button.Width=46;button.Height=42;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;
                button.ContextMenu=WindowPicker.Menu(app,controller);
                DragReorder.Attach(button,button,"dock.apps",app.Key,Orientation.Horizontal,()=>!closed,controller.MoveDock);
                var hover=new ScaleTransform(1,1);content.RenderTransform=hover;content.RenderTransformOrigin=new Point(.5,.5);button.MouseEnter+=(s,e)=>Animate(hover,1.08);button.MouseLeave+=(s,e)=>Animate(hover,1);
                row.Children.Add(button);
            }
            Apps.Surface.Child=row;
            if(controller.Desktop) {
                Start.ShowAt(Screen.Bounds,layout.StartX,layout.Y+2,40,40);
                Apps.ShowAt(Screen.Bounds,layout.AppX,layout.Y,layout.AppWidth,44);
                Status.ShowAt(Screen.Bounds,layout.StatusX,layout.Y,layout.StatusWidth,44);
            }
            UpdateActive(controller.LastApplication);
        }
        internal void UpdateActive(IntPtr active) {Border selected;indicators.TryGetValue(active,out selected);foreach(var dot in indicators.Values.Distinct()){bool current=dot==selected;dot.Width=current?12:4;dot.Background=current?Brushes.Black:Paint.Brush("#AF262C2A");}}
        internal void SetVisible(bool value) {if(closed||visible==value)return;visible=value;foreach(var w in new[]{Start,Apps,Status}) {if(value)w.Show();else w.Hide();}}
        internal void Refresh(PowerState state)
        {
            if(closed) return;
            double expected=PowerDisplay.Slot(state,Preferences.Current.DesktopPowerWatts);if(expected!=powerWidth)Rebuild();powerAccess.ToolTip=state.Known?"배터리 · 빠른 설정":"직접 입력한 PC 전력값 · 실시간 측정 아님";
            Battery.Update(state);clock.Text=DateTime.Now.ToString(Preferences.Current.Seconds?"tt h:mm:ss\nyyyy-MM-dd":"tt h:mm\nyyyy-MM-dd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));
            bool radioOff=controller.Services.WifiOn==false;string wifiKind=controller.Services.WifiPresent==false?(controller.Services.NetworkConnected?"network":"network-off"):radioOff?"wifi-off":controller.Wifi==null?"wifi-disconnected":"wifi";
            string soundKind=SoundIcon.For(controller.Audio.Value,controller.Audio.Muted,controller.Audio.Available);
            if(wifiStatus.Kind!=wifiKind){wifiStatus.Kind=wifiKind;wifiStatus.InvalidateVisual();}wifiStatus.Opacity=wifiKind=="wifi"||wifiKind=="network"?1:.55;
            if(volumeStatus.Kind!=soundKind){volumeStatus.Kind=soundKind;volumeStatus.InvalidateVisual();}volumeStatus.Opacity=controller.Audio.Available?1:.4;
            quickAccess.ToolTip=(controller.Services.WifiPresent==false?(controller.Services.NetworkConnected?"네트워크 연결됨":"네트워크 연결 안 됨"):radioOff?"Wi-Fi 꺼짐":controller.Wifi??(controller.Services.WifiOn.HasValue?"Wi-Fi 연결 안 됨":"Wi-Fi 상태 확인 중"))+" · "+(controller.Audio.Muted?"음소거":!controller.Audio.Available?"소리 장치 없음":"음량 "+controller.Audio.Value);
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
        internal object Inspect() {return new {scale=Scale,apps=Native.RegionInfo(Apps),start=Native.RegionInfo(Start),status=Native.RegionInfo(Status),blur=Apps.HasBlur&&Status.HasBlur};}
        internal void Close() { if(closed) return; closed=true; Start.Close(); Apps.Close(); Status.Close(); }
    }
    internal sealed class HudWindow : GlassWindow
    {
        readonly PillSlider slider=new PillSlider("volume") {Margin=new Thickness(10,9,10,9),IsHitTestVisible=false};
        internal HudWindow() { Title="One TaskBar · 밝기 및 음량"; Width=286; Height=62; Radius=31; Surface.CornerRadius=new CornerRadius(31); Surface.Child=slider; ShowActivated=false;
            SourceInitialized+=(s,e)=> { var h=new WindowInteropHelper(this).Handle; Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)|0x80|0x08000000); };
        }
        internal void Set(string kind,double value,bool muted) { slider.Kind=kind=="volume"?SoundIcon.For(value,muted,true):kind;slider.Value=muted?0:value; }
    }
    internal sealed class DrawerWindow : GlassWindow
    {
        readonly ShellController shell; readonly WrapPanel items=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Center};
        double cellWidth=118,cellHeight=116;
        readonly Border searchBar;
        List<AppEntry> loadedApps;string loadedQuery;double loadedWidth,loadedHeight;bool composing,searchQueued;
        readonly TextBlock count=Paint.TextBlock("",12,Paint.Muted);
        internal readonly TextBox Search=new TextBox {FontSize=15,Foreground=Paint.Ink,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(6,8,6,8)};
        internal DrawerWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · 앱";Width=1280;Height=730;Radius=32;Dark=false;StableSurface=true;Surface.CornerRadius=new CornerRadius(32);Surface.BorderThickness=new Thickness(0);Surface.Background=Paint.Brush("#6877847E");
            var root=new DockPanel {Margin=new Thickness(54,30,54,18)};
            var header=new Grid {Height=42,Margin=new Thickness(12,0,12,20)};var title=Paint.TextBlock("앱",22,Paint.Text);title.FontWeight=FontWeights.SemiBold;header.Children.Add(title);
            var close=Paint.Action("닫기",()=>Hide()); close.HorizontalAlignment=HorizontalAlignment.Right; close.Height=32; header.Children.Add(close); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            var search=new Border {Background=Paint.Brush("#DDE7ECE8"),CornerRadius=new CornerRadius(24),Padding=new Thickness(14,3,14,3),Margin=new Thickness(0,16,0,0),Width=420,MaxWidth=420};
            searchBar=search;
            var field=new Grid();Search.MaxLength=120;field.Children.Add(Search);var hint=Paint.TextBlock("무엇을 찾고 있나요?",15,Paint.Brush("#FF50665A"));hint.IsHitTestVisible=false;hint.Margin=new Thickness(6,0,0,0);field.Children.Add(hint);search.Child=field;
            Search.TextChanged+=(s,e)=> {hint.Visibility=Search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;if(!composing)QueueSearch();};
            TextCompositionManager.AddPreviewTextInputStartHandler(Search,(s,e)=>composing=true);TextCompositionManager.AddPreviewTextInputUpdateHandler(Search,(s,e)=>composing=true);Search.PreviewTextInput+=(s,e)=> {composing=false;QueueSearch();};Search.PreviewMouseLeftButtonDown+=(s,e)=> {if(Search.Text.Length==0){shell.ToggleSearch(null);e.Handled=true;}};
            var bottom=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center};bottom.Children.Add(search);count.HorizontalAlignment=HorizontalAlignment.Center;count.Margin=new Thickness(0,8,0,0);bottom.Children.Add(count);DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
            root.Children.Add(new SmoothScrollViewer {Content=items,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            Surface.Child=root;TransitionContent=root;PreviewTextInput+=(s,e)=> {if(!Search.IsKeyboardFocusWithin&&!String.IsNullOrEmpty(e.Text)&&(Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Alt))==0){Search.Text+=e.Text;QueueSearch();e.Handled=true;}};Deactivated+=(s,e)=> { if(!Program.Inspectable && IsVisible && !IsMouseOver) Hide(); };
        }
        void QueueSearch(){if(searchQueued||Search.Text.Length==0)return;searchQueued=true;Dispatcher.BeginInvoke(new Action(()=> {searchQueued=false;if(!composing&&IsVisible&&!String.IsNullOrWhiteSpace(Search.Text))shell.ToggleSearch(null,Search.Text);}),System.Windows.Threading.DispatcherPriority.Background);}
        internal void Configure(double width,double height)
        {
            searchBar.Width=Math.Max(200,Math.Min(420,width-112));
            int columns=Math.Max(3,Math.Min(10,(int)Math.Floor((width-108)/108)));cellWidth=Math.Max(84,(width-112)/columns);cellHeight=Math.Min(126,Math.Max(104,height*.18));items.Width=cellWidth*columns;
        }
        internal void Reload()
        {
            var currentQuery=Search.Text.Trim();if(loadedApps==shell.Apps&&loadedQuery==currentQuery&&loadedWidth==cellWidth&&loadedHeight==cellHeight)return;loadedApps=shell.Apps;loadedQuery=currentQuery;loadedWidth=cellWidth;loadedHeight=cellHeight;
            items.Children.Clear();
            var query=Search.Text.Trim();
            foreach(var entry in shell.Apps.Where(a=>String.IsNullOrEmpty(query) || a.Name.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0)) {
                var body=new StackPanel {Width=cellWidth-16,Height=cellHeight-12};
                if(entry.Icon!=null){var icon=AppIcons.Visual(entry.Icon,46);icon.Margin=new Thickness(0,9,0,11);body.Children.Add(icon);}
                else body.Children.Add(new Glyph("window",46) {Margin=new Thickness(0,9,0,11),HorizontalAlignment=HorizontalAlignment.Center});
                var label=Paint.TextBlock(entry.Name,12,Paint.Text);label.TextAlignment=TextAlignment.Center;label.TextWrapping=TextWrapping.Wrap;label.TextTrimming=TextTrimming.CharacterEllipsis;label.MaxHeight=34;body.Children.Add(label);
                var app=entry; var cell=Paint.Button(body,()=> { app.Launch(); Hide(); },entry.Name); cell.Padding=new Thickness(7,4,7,4); cell.Margin=new Thickness(2); cell.Background=Brushes.Transparent; items.Children.Add(cell);
                cell.Width=cellWidth-4;cell.Height=cellHeight-4;cell.ContextMenu=WindowPicker.Menu(app,shell);
            }
            if(shell.Apps.Count==0) items.Children.Add(Paint.TextBlock("설치된 앱을 불러오는 중…",14,Paint.Text));
            count.Text=items.Children.Count+"개 앱";
        }
    }
}
