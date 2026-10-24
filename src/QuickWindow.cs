using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal sealed class QuickWindow : GlassWindow
    {
        readonly ShellController shell;
        readonly QuickGridSurface gridUI;
        internal bool Modern {get{return gridUI!=null;}}
        internal QuickGridSurface GridUI {get{return gridUI;}}
        internal bool TogglePending(string key){return pendingToggles.Contains(key);}
        readonly PillSlider brightness=new PillSlider("brightness"),volume=new PillSlider("volume");
        readonly BatteryPill battery=new BatteryPill {Width=62,Height=25};
        readonly TextBlock time=Paint.TextBlock("",23,Paint.Text),date=Paint.TextBlock("",11,Paint.Muted);
        readonly TextBlock wifiName=Paint.TextBlock("연결 안 됨",11,Paint.Muted),bluetoothState=Paint.TextBlock("기기 연결",11,Paint.Muted);
        internal string BluetoothCaption {get{return Modern?gridUI.BluetoothCaption:bluetoothState.Text;}}
        TextBlock devicesName;Glyph devicesIcon;Border devicesIconHost;bool budsIconRequested,budsAudio;int budsIconRevision;ImageSource budsSymbol;readonly Image devicesNativeIcon=new Image {Width=22,Height=22,Stretch=Stretch.Uniform};internal string DevicesLabel {get{return Modern?gridUI.DevicesLabel:devicesName.Text;}}
        internal bool BudsIconLoaded {get{return Modern?gridUI.BudsIconLoaded:devicesIconHost.Child is Image;}}
        internal bool VolumeBudsIcon {get{return Modern?gridUI.Volume.HasDeviceIcon:volume.HasDeviceIcon;}}
        internal bool MediaBudsIcon {get{return Modern?gridUI.MediaBudsIcon:mediaPlayer.OutputBudsIcon;}}
        async void LoadBudsIcon(){budsIconRequested=true;int request=++budsIconRevision;try{var image=await BackgroundWork.Run(BudsDeviceData.ShortcutIcon);if(image!=null&&request==budsIconRevision){budsSymbol=image;UpdateBudsIcons();}}catch(Exception error){AppLog.Once("quick-buds-icon",error.GetType().Name);}}
        void UpdateBudsIcons(){if(Preferences.Current.QuickGalaxyBuds&&budsSymbol!=null){devicesNativeIcon.Source=budsSymbol;if(devicesIconHost.Child!=devicesNativeIcon)devicesIconHost.Child=devicesNativeIcon;}else if(devicesIconHost.Child!=devicesIcon)devicesIconHost.Child=devicesIcon;volume.SetDeviceIcon(budsAudio?budsSymbol:null);}
        readonly MediaPlayerView mediaPlayer;
        readonly Border wifiCircle,bluetoothCircle;
        readonly Glyph wifiGlyph=new Glyph("wifi",20),bluetoothGlyph=new Glyph("bluetooth",20);
        readonly Button mute,samsungSettings;
        readonly Button footerPower,topPower;readonly FrameworkElement wifiTile,bluetoothTile,devicesTile,windowsTile;readonly Grid connections;readonly UniformGrid shortcuts;
        readonly Dictionary<string,FrameworkElement> sectionFrames=new Dictionary<string,FrameworkElement>();
        readonly Button themeToggle;
        readonly Glyph themeGlyph=new Glyph("moon",21);
        readonly Border themeCircle;
        readonly List<Glyph> soundGlyphs=new List<Glyph>();
        readonly Dictionary<string,QuickToggleButton> toggleButtons=new Dictionary<string,QuickToggleButton>();
        readonly HashSet<string> pendingToggles=new HashSet<string>();
        internal QuickToggleButton FindToggle(string key){return Modern?gridUI.Toggles[key]:toggleButtons[key];}
        internal string[] ToggleKeys {get{return Modern?gridUI.Toggles.Keys.ToArray():toggleButtons.Keys.ToArray();}}
        readonly List<TextBlock> actionNames=new List<TextBlock>();
        readonly TextBlock notice=Paint.TextBlock("",11,Paint.Muted);
        bool themeBusy;bool? darkMode;long clockSecond=-1;DateTime calendarDate;
        System.Windows.Media.ImageSource samsungIcon;string samsungIconKey;
        internal bool SamsungIconLoaded {get{return Modern?gridUI.SamsungIconLoaded:samsungIcon!=null;}}
        async void LoadSamsungIcon(AppEntry app)
        {
            try{var icon=app.Icon??await BackgroundWork.Run(()=>AppIcons.Get(app));if(icon!=null&&samsungIconKey==app.Key){samsungIcon=icon;samsungSettings.Content=AppIcons.Visual(icon,24);}}catch(Exception error){AppLog.Once("samsung-settings-icon",error.Message);}
        }
        readonly Border muteCircle;
        readonly StackPanel body=new StackPanel();
        readonly List<ReorderGroup> groups=new List<ReorderGroup>();
        readonly List<ColumnDefinition> gripColumns=new List<ColumnDefinition>();
        readonly List<FrameworkElement> grips=new List<FrameworkElement>();
        readonly ReorderGroup sections;
        readonly UniformGrid actionButtons;
        readonly Border actionCard;
        readonly Grid actionFrame;
        readonly Border actionOutline;
        readonly QuickResizeGrip resizeGrip=new QuickResizeGrip();
        readonly Button actionEdit,actionHandle;
        readonly Border actionHandleBar;
        readonly TextBlock actionEditBack=Paint.TextBlock("크기 조절로 돌아가기",10,Paint.Text);
        readonly DispatcherTimer sizeSave=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
        bool editingActions,resizingActions;
        int resizeColumns,resizeRows;
        double resizeWidth,resizeHeight,resizeAvailable;
        Point resizeOrigin;
        readonly TextBlock editLabel=Paint.TextBlock("배치 편집",11,Paint.Text);
        readonly Border editHint;
        bool editing,layoutDirty;
        internal bool Editing {get{return Modern?gridUI.Board.Editing:editing;}}
        internal bool Expanded {get{return Modern?gridUI.Expanded:expanded;}}
        bool expanded;int actionMotion;readonly Grid actionArea=new Grid {Height=62,ClipToBounds=true};
        internal QuickWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · 빠른 설정";Width=380;Height=650;Radius=30;Dark=true;Surface.CornerRadius=new CornerRadius(30);Surface.Background=QuickPanel.Glass;Surface.BorderBrush=Paint.Brush("#48FFFFFF");Surface.BorderThickness=new Thickness(.7);
            if(Preferences.Current.UIVersion>=90){gridUI=new QuickGridSurface(this,shell);Surface.Child=gridUI.Root;Deactivated+=(s,e)=>{if(!Program.Inspectable&&IsVisible&&!IsMouseOver&&!gridUI.Board.GestureActive)Hide();};return;}
            var root=new DockPanel {Margin=new Thickness(18,19,18,14)};
            var header=new Grid {Height=47,Margin=new Thickness(4,0,2,12)};
            var heading=new StackPanel();time.FontWeight=FontWeights.SemiBold;heading.Children.Add(time);heading.Children.Add(date);header.Children.Add(heading);
            var tools=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
            samsungSettings=Paint.Button(samsungIcon!=null?AppIcons.Visual(samsungIcon,24):(UIElement)new Glyph("settings",22),()=>shell.OpenSamsungSettings(),"삼성 설정");samsungSettings.Width=samsungSettings.Height=34;samsungSettings.Padding=new Thickness(0);samsungSettings.Background=Brushes.Transparent;tools.Children.Add(samsungSettings);
            tools.Children.Add(topPower=IconButton("power",()=>ShowPowerMenu(topPower),"전원",34));
            tools.Children.Add(IconButton("home",()=>shell.OpenHomeSettings(),"One TaskBar 설정",34));header.Children.Add(tools);
            DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var footer=new Grid {Height=31,Margin=new Thickness(3,9,3,0)};
            var power=Paint.Button(battery,()=>shell.ShowDetail("power"),"배터리 및 전원");power.Background=Brushes.Transparent;power.Padding=new Thickness(0);power.HorizontalAlignment=HorizontalAlignment.Left;footer.Children.Add(power);
            footerPower=power;
            var edit=Paint.Button(editLabel,()=>EditLayout(!editing),"빠른 설정 항목 · 묶음 위치 바꾸기");edit.HorizontalAlignment=HorizontalAlignment.Center;edit.Padding=new Thickness(9,3,9,3);footer.Children.Add(edit);
            var native=Paint.Action("알림  ›",()=>shell.ToggleNotifications(null));native.HorizontalAlignment=HorizontalAlignment.Right;native.Background=Brushes.Transparent;native.Padding=new Thickness(8,3,0,3);footer.Children.Add(native);
            DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            sections=new ReorderGroup("quick.sections",body,Orientation.Vertical,()=>editing);groups.Add(sections);
            var scrolled=new DockPanel();
            notice.Visibility=Visibility.Collapsed;notice.TextWrapping=TextWrapping.Wrap;notice.Margin=new Thickness(5,5,5,9);DockPanel.SetDock(notice,Dock.Top);scrolled.Children.Add(notice);
            var help=new Grid {Margin=new Thickness(5,5,5,9)};var message=Paint.TextBlock("아이콘 또는 ⋮⋮ 묶음을 드래그",10,Paint.Muted);message.VerticalAlignment=VerticalAlignment.Center;help.Children.Add(message);
            var reset=Paint.Action("초기화",()=>{SetCollapsedGrid(4,1);LayoutOrder.Reset("quick.");});reset.FontSize=10;reset.Padding=new Thickness(8,5,8,5);reset.HorizontalAlignment=HorizontalAlignment.Right;help.Children.Add(reset);
            var editor=new StackPanel();editor.Children.Add(help);
            var sizeHelp=Paint.TextBlock("모서리를 끌어 크기 조절 · 편집을 눌러 토글 순서 변경",10,Paint.Muted);sizeHelp.TextWrapping=TextWrapping.Wrap;sizeHelp.Margin=new Thickness(5,0,5,10);editor.Children.Add(sizeHelp);
            editHint=new Border {Child=editor,Visibility=Visibility.Collapsed};DockPanel.SetDock(editHint,Dock.Top);scrolled.Children.Add(editHint);scrolled.Children.Add(body);
            connections=new Grid {Height=62,Margin=new Thickness(0,0,0,10)};connections.ColumnDefinitions.Add(new ColumnDefinition());connections.ColumnDefinitions.Add(new ColumnDefinition());
            var pair=new ReorderGroup("quick.connections",connections,Orientation.Horizontal,()=>editing,PlacePair);groups.Add(pair);
            wifiTile=Connection("Wi-Fi",wifiName,wifiGlyph,()=>shell.Radio("wifi"),()=>shell.ShowDetail("wifi"),out wifiCircle);pair.Add("wifi",wifiTile);
            bluetoothTile=Connection("블루투스",bluetoothState,bluetoothGlyph,()=>shell.Radio("bluetooth"),()=>shell.ShowDetail("bluetooth"),out bluetoothCircle);pair.Add("bluetooth",bluetoothTile);AddSection("connections",connections);
            var actions=new UniformGrid {Columns=4,Rows=3,Height=210,Margin=new Thickness(7,7,7,4),VerticalAlignment=VerticalAlignment.Top};actionButtons=actions;
            var actionOrder=new ReorderGroup("quick.actions",actions,Orientation.Horizontal,()=>editing&&editingActions);groups.Add(actionOrder);
            actionOrder.Add("mute",RoundAction("volume","음소거","mute"));
            actionOrder.Add("theme",RoundAction("moon","다크 모드","theme"));
            actionOrder.Add("dnd",RoundAction("dnd","방해 금지","dnd"));
            actionOrder.Add("efficiency",RoundAction("leaf","전력 절약","efficiency"));
            actionOrder.Add("awake",RoundAction("window","화면 유지","awake"));
            actionOrder.Add("wifi",RoundAction("wifi","Wi-Fi","wifi"));
            actionOrder.Add("bluetooth",RoundAction("bluetooth","블루투스","bluetooth"));
            actionOrder.Add("animations",RoundAction("play","독 애니메이션","animations"));
            actionOrder.Add("startup",RoundAction("startup","자동 실행","startup"));
            actionOrder.Add("slider-scroll",RoundAction("scroll","스크롤 조절","slider-scroll"));
            actionArea.Children.Add(actions);
            actionHandleBar=new Border {Width=28,Height=3,CornerRadius=new CornerRadius(2),Background=Paint.Brush("#70F4F8F5")};
            actionHandle=Paint.Button(actionHandleBar,()=>{if(editing)EditActions(false);else ExpandActions();},"빠른 설정 더 보기 · 접기");actionHandle.Background=Brushes.Transparent;actionHandle.Height=11;actionHandle.Padding=new Thickness(20,4,20,4);actionHandle.VerticalAlignment=VerticalAlignment.Bottom;actionArea.Children.Add(actionHandle);
            actionCard=Card(actionArea,34,new Thickness(0));actionFrame=new Grid {Margin=new Thickness(0,0,0,10),HorizontalAlignment=HorizontalAlignment.Left};actionFrame.Children.Add(actionCard);
            actionOutline=new Border {CornerRadius=new CornerRadius(34),BorderBrush=Paint.Brush("#90FFFFFF"),BorderThickness=new Thickness(2),IsHitTestVisible=false,Visibility=Visibility.Collapsed};actionFrame.Children.Add(actionOutline);
            actionEdit=Paint.Action("편집",()=>EditActions(true));actionEdit.HorizontalAlignment=HorizontalAlignment.Center;actionEdit.VerticalAlignment=VerticalAlignment.Center;actionEdit.Background=Paint.Brush("#75000000");actionEdit.Padding=new Thickness(13,5,13,5);actionEdit.FontSize=11;actionEdit.Resources["ButtonRadius"]=new CornerRadius(18);actionEdit.Visibility=Visibility.Collapsed;actionFrame.Children.Add(actionEdit);
            resizeGrip.Visibility=Visibility.Collapsed;AutomationProperties.SetName(resizeGrip,"기능 토글 크기 조절");AutomationProperties.SetHelpText(resizeGrip,"모서리를 끌거나 방향키로 가로 열 수와 세로 행 수를 조절합니다.");actionFrame.Children.Add(resizeGrip);
            resizeGrip.DragStarted+=(s,e)=>{BeginToggleResize();resizeOrigin=Mouse.GetPosition(Surface);};resizeGrip.DragDelta+=(s,e)=>{var point=Mouse.GetPosition(Surface);ResizeToggleBy(point.X-resizeOrigin.X,point.Y-resizeOrigin.Y);};resizeGrip.DragCompleted+=(s,e)=>FinishToggleResize(e.Canceled);
            resizeGrip.KeyDown+=(s,e)=>{if(resizeGrip.IsDragging)return;int x=e.Key==Key.Left?-1:e.Key==Key.Right?1:0,y=e.Key==Key.Up?-1:e.Key==Key.Down?1:0;if(x==0&&y==0)return;SetCollapsedGrid(QuickToggleSize.Columns(Preferences.Current)+x,QuickToggleSize.Rows(Preferences.Current)+y);e.Handled=true;};
            AddSection("actions",actionFrame);sectionFrames["actions"].SizeChanged+=(s,e)=>ApplyActionWidth(false);
            themeToggle=Paint.Button(themeGlyph,()=>ToggleTheme(),"Windows 다크 모드 전환");themeToggle.Width=themeToggle.Height=44;themeToggle.Padding=new Thickness(0);themeToggle.Background=Brushes.Transparent;themeToggle.Resources["ButtonRadius"]=new CornerRadius(22);
            themeCircle=new Border {Width=44,Height=44,CornerRadius=new CornerRadius(22),Background=Paint.Brush(QuickPanel.Inactive),Child=themeToggle};AddSection("brightness",SliderRow(brightness,themeCircle));
            mute=IconButton("volume",()=>shell.Mute(),"음소거 전환",44);soundGlyphs.Add((Glyph)mute.Content);muteCircle=new Border {Width=44,Height=44,CornerRadius=new CornerRadius(22),Background=Paint.Brush(QuickPanel.Inactive),Child=mute};
            AddSection("volume",SliderRow(volume,muteCircle,()=>shell.ShowDetail("sound")));
            mediaPlayer=new MediaPlayerView(shell.Media,shell.SeekMedia,()=>shell.ShowDetail("sound"),condensed:true,openApp:shell.OpenMediaApplication);
            var mediaCard=Card(mediaPlayer,28,new Thickness(0,0,0,10));mediaPlayer.PaletteChanged=palette=>{mediaCard.Background=mediaPlayer.IsCompact?QuickPanel.Card:mediaPlayer.MediaBackground;mediaCard.CornerRadius=new CornerRadius(mediaPlayer.IsCompact?34:28);};AddSection("media",mediaCard);
            shortcuts=new UniformGrid {Columns=2};var bottomOrder=new ReorderGroup("quick.shortcuts",shortcuts,Orientation.Horizontal,()=>editing,PlacePair);groups.Add(bottomOrder);
            devicesTile=WideTile("devices","주변 기기 연결","samsung-devices",new Thickness(0));windowsTile=WideTile("windows-settings","Windows 설정","windows-settings",new Thickness(0));bottomOrder.Add("devices",devicesTile);bottomOrder.Add("focus",windowsTile);AddSection("shortcuts",shortcuts);
            var utilities=new Grid {Margin=new Thickness(0,10,0,10)};utilities.ColumnDefinitions.Add(new ColumnDefinition());utilities.ColumnDefinitions.Add(new ColumnDefinition());var capture=WideTile("screenshot","화면 캡처","screen-capture",new Thickness(0,0,5,0));var manager=WideTile("task-manager","작업 관리자","task-manager",new Thickness(5,0,0,0));Grid.SetColumn(manager,1);utilities.Children.Add(capture);utilities.Children.Add(manager);AddSection("utilities",utilities);
            root.Children.Add(new SmoothScrollViewer {Content=scrolled,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Surface.Child=root;brightness.Light=volume.Light=true;brightness.DarkTrack=volume.DarkTrack=true;ApplyOrder();
            sizeSave.Tick+=(s,e)=>FlushSize();LayoutOrder.Changed+=OrderChanged;DragReorder.Finished+=DragFinished;IsVisibleChanged+=(s,e)=>{if(!IsVisible&&resizeGrip.IsDragging)resizeGrip.CancelDrag();};Closed+=(s,e)=> {budsIconRevision++;if(resizeGrip.IsDragging)resizeGrip.CancelDrag();FlushSize();LayoutOrder.Changed-=OrderChanged;DragReorder.Finished-=DragFinished;};
            brightness.Changed+=v=>shell.Bright(v);volume.Changed+=v=>shell.Volume(v);
            Deactivated+=(s,e)=> {if(!Program.Inspectable&&IsVisible&&!IsMouseOver&&!DragReorder.Active)Hide();};
        }
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if(Modern&&e.Key==Key.Escape&&(gridUI.Board.GestureActive||gridUI.Board.Editing)){if(gridUI.Board.ContentsEditing)gridUI.EditActions(false);else if(gridUI.Board.GestureActive)gridUI.Board.Cancel();else gridUI.SetEditing(false);e.Handled=true;return;}base.OnPreviewKeyDown(e);
        }
        internal void SetCollapsedGrid(int columns,int rows)
        {
            if(Modern){gridUI.Board.Layout.Resize("toggles",columns,rows,false);gridUI.Board.Save();gridUI.Board.Reflow(true);return;}
            columns=QuickToggleSize.Columns(columns);rows=QuickToggleSize.Rows(rows);
            bool changed=columns!=QuickToggleSize.Columns(Preferences.Current)||rows!=QuickToggleSize.Rows(Preferences.Current);
            Preferences.Current.QuickToggleColumns=columns;Preferences.Current.QuickToggleRows=rows;expanded=false;editingActions=false;PresentActions(false);
            if(changed){sizeSave.Stop();sizeSave.Start();}
        }
        internal void BeginToggleResize()
        {
            if(Modern){gridUI.Board.Begin("toggles",true);return;}
            if(!editing||editingActions||resizingActions)return;resizingActions=true;
            resizeColumns=QuickToggleSize.Columns(Preferences.Current);resizeRows=QuickToggleSize.Rows(Preferences.Current);resizeAvailable=ActionAvailableWidth;resizeWidth=QuickToggleSize.Width(resizeAvailable,resizeColumns);resizeHeight=QuickToggleSize.Height(resizeRows);
        }
        internal void ResizeToggleBy(double horizontal,double vertical)
        {
            if(Modern){gridUI.Board.Drag();return;}
            if(!resizingActions)return;int columns=QuickToggleSize.SnapColumns(resizeWidth+horizontal,resizeAvailable,QuickToggleSize.Columns(Preferences.Current)),rows=QuickToggleSize.SnapRows(resizeHeight+vertical,QuickToggleSize.Rows(Preferences.Current));
            if(columns!=QuickToggleSize.Columns(Preferences.Current)||rows!=QuickToggleSize.Rows(Preferences.Current))SetCollapsedGrid(columns,rows);
        }
        internal void FinishToggleResize(bool cancelled)
        {
            if(Modern){gridUI.Board.End(cancelled);return;}
            if(!resizingActions)return;resizingActions=false;if(cancelled)SetCollapsedGrid(resizeColumns,resizeRows);FlushSize();
        }
        void FlushSize(){if(!sizeSave.IsEnabled)return;sizeSave.Stop();Preferences.Save();}
        internal void EditActions(bool value)
        {
            if(Modern){gridUI.EditActions(value);return;}
            if(!editing)return;editingActions=value;expanded=false;PresentActions(false);
        }
        bool AllActions {get{return expanded||(editing&&editingActions);}}
        double ActionAvailableWidth {get{return sectionFrames["actions"].ActualWidth-(editing?18:0);}}
        void ApplyActionWidth(bool animate)
        {
            if(actionFrame==null)return;double available=ActionAvailableWidth;if(available<=0)return;
            double target=AllActions?available:QuickToggleSize.Width(available,QuickToggleSize.Columns(Preferences.Current)),from=actionFrame.ActualWidth;
            if(!Double.IsNaN(actionFrame.Width)&&Math.Abs(actionFrame.Width-target)<.1)return;actionFrame.BeginAnimation(WidthProperty,null);actionFrame.Width=target;
            if(animate&&Motion.Enabled&&IsVisible&&from>0)actionFrame.BeginAnimation(WidthProperty,Motion.PanelTween(from,target,280));
        }
        static void PlacePair(FrameworkElement element,int index) {Grid.SetColumn(element,index);element.Margin=index==0?new Thickness(0,0,5,0):new Thickness(5,0,0,0);}
        void AddSection(string key,FrameworkElement content)
        {
            var wrapper=new Grid {Background=Brushes.Transparent};var column=new ColumnDefinition {Width=new GridLength(0)};wrapper.ColumnDefinitions.Add(column);wrapper.ColumnDefinitions.Add(new ColumnDefinition());gripColumns.Add(column);
            var handle=new Border {Background=Brushes.Transparent,Child=new Glyph("grip",13){Color=Paint.Muted},Cursor=System.Windows.Input.Cursors.SizeAll,Visibility=Visibility.Collapsed,ToolTip="이 묶음 드래그하여 이동"};grips.Add(handle);wrapper.Children.Add(handle);Grid.SetColumn(content,1);wrapper.Children.Add(content);sections.Add(key,wrapper,handle);sectionFrames[key]=wrapper;
        }
        internal void EditLayout(bool value)
        {
            if(Modern){gridUI.SetEditing(value);return;}
            if(resizeGrip.IsDragging)resizeGrip.CancelDrag();editing=value;editingActions=false;expanded=false;if(!value)FlushSize();editLabel.Text=value?"완료":"배치 편집";editHint.Visibility=value?Visibility.Visible:Visibility.Collapsed;
            foreach(var column in gripColumns)column.Width=new GridLength(value?18:0);foreach(var grip in grips)grip.Visibility=value?Visibility.Visible:Visibility.Collapsed;
            SetHitTesting(body,!value);PresentActions(false);body.InvalidateMeasure();body.InvalidateArrange();
        }
        static void SetHitTesting(DependencyObject root,bool enabled)
        {
            if(root is Button||root is PillSlider)((UIElement)root).IsHitTestVisible=enabled;
            foreach(object child in LogicalTreeHelper.GetChildren(root)){var element=child as DependencyObject;if(element!=null)SetHitTesting(element,enabled);}
        }
        void OrderChanged(string scope) {if(!scope.StartsWith("quick.",StringComparison.Ordinal))return;if(DragReorder.Active){layoutDirty=true;return;}ApplyOrder();}
        void DragFinished() {if(layoutDirty){layoutDirty=false;Dispatcher.BeginInvoke(new Action(ApplyOrder));}}
        void ApplyOrder() {foreach(var group in groups)group.Apply();PresentActions(false);body.InvalidateMeasure();body.InvalidateArrange();}
        internal bool LayoutFits()
        {
            if(Modern)return gridUI.Board.Layout.Fits();
            double bottom=0;foreach(FrameworkElement element in body.Children){double top=element.TranslatePoint(new Point(),body).Y;if(top<bottom-.5)return false;bottom=top+element.ActualHeight;}return true;
        }
        internal double ActionWidth {get{return Modern?gridUI.Board.Frames["toggles"].ActualWidth:actionFrame.ActualWidth;}}
        internal double ActionHeight {get{return Modern?gridUI.Board.Frames["toggles"].ActualHeight:actionArea.ActualHeight;}}
        internal QuickResizeGrip ResizeGrip {get{return Modern?gridUI.Board.Frames["toggles"].Resize:resizeGrip;}}
        internal Button ActionEditor {get{return Modern?gridUI.Board.Frames["toggles"].ContentEditor:actionEdit;}}
        internal int VisibleActions {get{return Modern?gridUI.VisibleActions:actionButtons.Children.Cast<FrameworkElement>().Count(e=>e.Visibility==Visibility.Visible);}}
        internal bool ActionsFit {get{return Modern?gridUI.Board.Layout.Fits():ButtonsUnclipped();}}
        internal object ActionBounds() {if(Modern)return gridUI.Snapshot();return new {width=actionButtons.ActualWidth,height=actionButtons.ActualHeight,items=actionButtons.Children.Cast<StackPanel>().Where(i=>i.Visibility==Visibility.Visible).Select(i=>new {width=i.ActualWidth,height=i.ActualHeight,button=i.Children.OfType<Button>().First().TransformToAncestor(actionButtons).TransformBounds(new Rect(0,0,43,43))}).ToArray()};}
        internal object LayoutSnapshot() {if(Modern)return gridUI.Snapshot();return new {editing=editing,editingActions=editingActions,expanded=expanded,visibleActionLabels=actionNames.Count(n=>n.Visibility==Visibility.Visible),visibleActions=VisibleActions,columns=QuickToggleSize.Columns(Preferences.Current),rows=QuickToggleSize.Rows(Preferences.Current),resizeHandle=resizeGrip.IsVisible,circleRadius=21.5,buttonsUnclipped=ButtonsUnclipped(),sections=sections.Keys,groups=groups.Skip(1).Select(g=>g.Keys).ToArray(),noOverlap=LayoutFits(),actionHeight=actionArea.ActualHeight,actionWidth=actionFrame.ActualWidth,soundIcon=volume.Kind,themeAvailable=themeToggle.IsEnabled,dark=darkMode,sizes=sections.Items.Select(i=>new {key=i.Key,height=i.Element.ActualHeight,y=i.Element.TranslatePoint(new Point(),body).Y}).ToArray()};}
        bool ButtonsUnclipped()
        {
            var clip=VisualTreeHelper.GetClip(actionButtons);Rect bounds=clip!=null?clip.Bounds:new Rect(0,0,actionButtons.ActualWidth,actionButtons.ActualHeight);
            foreach(StackPanel item in actionButtons.Children.Cast<StackPanel>().Where(i=>i.Visibility==Visibility.Visible)){var button=item.Children.OfType<Button>().FirstOrDefault();if(button==null)continue;Rect rect=button.TransformToAncestor(actionButtons).TransformBounds(new Rect(0,0,button.ActualWidth,button.ActualHeight));if(rect.Top<bounds.Top||rect.Bottom>bounds.Bottom||rect.Left<bounds.Left||rect.Right>bounds.Right)return false;}return true;
        }
        void ExpandActions()
        {
            SetExpanded(!expanded);
        }
        internal void SetExpanded(bool value) {if(Modern){gridUI.ExpandToggles(value);return;}if(editing){EditActions(value);return;}expanded=value;bool originalShape=QuickToggleSize.Columns(Preferences.Current)==4&&QuickToggleSize.Rows(Preferences.Current)==1;PresentActions(originalShape);if(!originalShape)Motion.Fade(actionButtons,.7,1,170);}
        internal object AudioSnapshot() {if(Modern)return new {slider=gridUI.Volume.Kind,buttons=new[]{((Glyph)gridUI.Toggles["mute"].Content).Kind}};return new {slider=volume.Kind,buttons=soundGlyphs.Select(g=>g.Kind).ToArray()};}
        void PresentActions(bool animate)
        {
            ++actionMotion;bool show=AllActions;int columns=QuickToggleSize.Columns(Preferences.Current),rows=QuickToggleSize.Rows(Preferences.Current);double from=actionArea.Height,target=show?232:QuickToggleSize.Height(rows);actionArea.BeginAnimation(HeightProperty,null);actionArea.Height=target;
            actionButtons.Columns=show?4:columns;actionButtons.Rows=show?3:rows;actionButtons.Height=show?(editing?200:210):target-18;
            int index=0;foreach(FrameworkElement item in actionButtons.Children){item.Visibility=show||index<columns*rows?Visibility.Visible:Visibility.Collapsed;item.VerticalAlignment=show?VerticalAlignment.Top:VerticalAlignment.Center;item.Opacity=editing&&!editingActions?.65:1;index++;}ApplyActionWidth(animate);
            if(animate&&Motion.Enabled&&IsVisible)actionArea.BeginAnimation(HeightProperty,Motion.PanelTween(from,target,330));
            foreach(var name in actionNames){name.BeginAnimation(OpacityProperty,null);if(show){name.Visibility=Visibility.Visible;Motion.Fade(name,animate?0:1,1,220);}else {name.Visibility=Visibility.Collapsed;name.Opacity=0;}}
            actionOutline.Visibility=editing?Visibility.Visible:Visibility.Collapsed;actionEdit.Visibility=editing&&!editingActions?Visibility.Visible:Visibility.Collapsed;actionEdit.IsHitTestVisible=editing&&!editingActions;resizeGrip.Visibility=editing&&!editingActions?Visibility.Visible:Visibility.Collapsed;
            actionHandle.Visibility=editing&&!editingActions?Visibility.Collapsed:Visibility.Visible;actionHandle.IsHitTestVisible=!editing||editingActions;actionHandle.Content=editing?(UIElement)actionEditBack:actionHandleBar;actionHandle.Height=editing?24:11;actionHandle.Padding=editing?new Thickness(7,2,7,2):new Thickness(20,4,20,4);
            resizeGrip.ToolTip="기능 토글 "+columns+"열 × "+rows+"행 · 드래그 또는 방향키로 조절";AutomationProperties.SetItemStatus(resizeGrip,columns+"열 "+rows+"행");
        }
        void ToggleTheme(){InvokeToggle("theme");}
        internal async void InvokeToggle(string key)
        {
            if(pendingToggles.Contains(key))return;pendingToggles.Add(key);if(key=="theme")themeBusy=true;Refresh(shell.Power,shell.Brightness,shell.Audio.Value,shell.Audio.Muted,shell.Audio.Available,shell.Brightness>=0);
            try {
                if(key=="mute"){bool before=shell.Audio.Muted;shell.Mute();shell.Audio.Read();if(shell.Audio.Muted==before)throw new InvalidOperationException("Windows 음소거 상태를 변경하지 못했습니다.");}
                else if(key=="theme"){var before=WindowsTheme.Read(true);if(!before.Available)throw new InvalidOperationException("Windows 색 모드를 읽지 못했습니다.");await Task.Run(()=>WindowsTheme.SetDark(!before.Dark));WindowsTheme.Invalidate();}
                else if(key=="efficiency")shell.Toggles.ToggleEfficiency();
                else if(key=="dnd")await shell.Toggles.Disturb.Toggle();
                else if(key=="awake")shell.Toggles.SetAwake(!shell.Toggles.Awake);
                else if(key=="animations")Preferences.Set(p=>p.Animations=!p.Animations);
                else if(key=="startup")shell.Toggles.ToggleStartup();
                else if(key=="slider-scroll")Preferences.Set(p=>p.QuickSliderScroll=!p.QuickSliderScroll);
                else if(key=="wifi"||key=="bluetooth"){if(!await shell.Services.ToggleRadio(key))throw new InvalidOperationException("Windows가 이 무선 장치의 제어를 허용하지 않았습니다.");await shell.Services.Refresh();}
                notice.Visibility=Visibility.Collapsed;
            }catch(Exception error){Feedback(error.Message);}
            finally {pendingToggles.Remove(key);if(key=="theme")themeBusy=false;Refresh(shell.Power,shell.Brightness,shell.Audio.Value,shell.Audio.Muted,shell.Audio.Available,shell.Brightness>=0);}
        }
        internal void Feedback(string text) {if(Modern){gridUI.Feedback(text);return;}notice.Text=text;notice.Visibility=Visibility.Visible;}
        static Border Card(UIElement content,double radius,Thickness margin) {return new Border {CornerRadius=new CornerRadius(radius),Background=QuickPanel.Card,Child=content,Margin=margin};}
        static Button IconButton(string icon,Action action,string hint,double size)
        {
            var result=Paint.Button(new Glyph(icon,20),action,hint);result.Width=result.Height=size;result.Padding=new Thickness(0);result.Background=Brushes.Transparent;result.Resources["ButtonRadius"]=new CornerRadius(size/2);return result;
        }
        static Border Connection(string title,TextBlock subtitle,Glyph glyph,Action toggle,Action settings,out Border circle)
        {
            var grid=new Grid {Margin=new Thickness(10,9,7,9)};grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(45)});grid.ColumnDefinitions.Add(new ColumnDefinition());
            circle=new Border {Width=43,Height=43,CornerRadius=new CornerRadius(21.5),Background=Paint.Brush(QuickPanel.Inactive),BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Child=glyph};
            var action=Paint.Button(circle,toggle,title+" 켜기 · 끄기");action.Padding=new Thickness(0);action.Background=Brushes.Transparent;grid.Children.Add(action);
            var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};var label=Paint.TextBlock(title,14,Paint.Text);label.FontWeight=FontWeights.SemiBold;words.Children.Add(label);subtitle.TextTrimming=TextTrimming.CharacterEllipsis;words.Children.Add(subtitle);
            var details=Paint.Button(words,settings,title+" 연결 목록 · 설정");details.HorizontalContentAlignment=HorizontalAlignment.Left;details.Padding=new Thickness(7,0,0,0);details.Background=Brushes.Transparent;Grid.SetColumn(details,1);grid.Children.Add(details);return Card(grid,34,new Thickness(0));
        }
        FrameworkElement RoundAction(string icon,string label,string page)
        {
            var content=new StackPanel {Margin=new Thickness(0,1,0,0),Background=Brushes.Transparent};
            var button=(QuickToggleButton)Paint.Button(new Glyph(icon,20),()=>InvokeToggle(page),label+" 켜기 · 끄기",true);button.Width=button.Height=43;button.Padding=new Thickness(0);button.Resources["ButtonRadius"]=new CornerRadius(21.5);button.Tag=page;button.HorizontalAlignment=HorizontalAlignment.Center;toggleButtons[page]=button;content.Children.Add(button);if(icon=="volume")soundGlyphs.Add((Glyph)button.Content);
            var name=Paint.TextBlock(label,10.5,Paint.Muted);name.HorizontalAlignment=HorizontalAlignment.Center;name.Margin=new Thickness(0,5,0,0);actionNames.Add(name);content.Children.Add(name);return content;
        }
        static Border SliderRow(PillSlider slider,UIElement action,Action output=null)
        {
            var grid=new Grid {Margin=new Thickness(12,9,12,9)};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(52)});
            slider.Height=44;slider.Margin=new Thickness(0,0,8,0);grid.Children.Add(output==null?(UIElement)slider:slider.WithOutputAction(output));Grid.SetColumn(action,1);grid.Children.Add(action);return Card(grid,34,new Thickness(0,0,0,10));
        }
        Border WideTile(string icon,string label,string page,Thickness margin)
        {
            var row=new StackPanel {Orientation=Orientation.Horizontal};var glyph=new Glyph(icon,22) {Margin=new Thickness(0,0,9,0)};var text=Paint.TextBlock(label,11,Paint.Text);if(page=="samsung-devices"){devicesName=text;devicesIcon=glyph;glyph.Margin=new Thickness(0);devicesIconHost=new Border {Width=22,Height=22,Margin=new Thickness(0,0,9,0),Child=glyph};row.Children.Add(devicesIconHost);}else row.Children.Add(glyph);row.Children.Add(text);
            var button=Paint.Button(row,()=>OpenPage(page),label);button.Background=Brushes.Transparent;button.Height=59;button.Padding=new Thickness(9,7,9,7);return Card(button,30,margin);
        }
        void OpenPage(string page)
        {
            if(page=="screen-capture"){shell.OpenScreenCapture();return;}if(page=="task-manager"){shell.OpenTaskManager();return;}
            if(page=="mute"){shell.Mute();return;}if(page=="notifications"){shell.ToggleNotifications(null);return;}if(page=="apps"){shell.ToggleDrawer(null);return;}if(page=="settings"){shell.OpenHomeSettings();return;}if(page=="windows-settings"){shell.OpenWindowsSettings();return;}if(page=="samsung-devices"){shell.OpenSamsungDevices();return;}
            if(page=="desktop"){foreach(var entry in shell.Running)foreach(var h in entry.Windows)Native.ShowWindow(h,6);Hide();return;}shell.ShowDetail(page);
        }
        void ShowPowerMenu(FrameworkElement target) {BuildPowerMenu(target).IsOpen=true;}
        internal ContextMenu BuildPowerMenu(FrameworkElement target)
        {
            var menu=new ContextMenu {PlacementTarget=target,Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom};
            foreach(var action in new[]{SystemPowerAction.ShutDown,SystemPowerAction.Restart,SystemPowerAction.Sleep,SystemPowerAction.Lock}){
                var choice=action;string label=action==SystemPowerAction.ShutDown?"전원 끄기":action==SystemPowerAction.Restart?"다시 시작":action==SystemPowerAction.Sleep?"절전":"잠금";
                var item=new MenuItem {Header=label,Tag=action,Icon=new Glyph(action==SystemPowerAction.ShutDown?"power":action==SystemPowerAction.Restart?"rotation":action==SystemPowerAction.Sleep?"moon":"lock",18) {Color=Paint.Ink}};
                item.Click+=async(s,e)=>{if(!await DismissAsync())return;try{SystemPower.Execute(choice);}catch(Exception error){shell.ShowWelcome();shell.Welcome.Feedback(error.Message);}};menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());var settings=new MenuItem {Header="배터리 및 전원",Icon=new Glyph("battery",18) {Color=Paint.Ink}};settings.Click+=(s,e)=>shell.ShowDetail("power");menu.Items.Add(settings);return menu;
        }
        internal void Refresh(PowerState state,int bright,double sound,bool muted,bool audioAvailable,bool brightAvailable)
        {
            if(Modern){gridUI.Refresh(state,bright,sound,muted,audioAvailable,brightAvailable);return;}
            brightness.WheelAdjust=volume.WheelAdjust=Preferences.Current.QuickSliderScroll;
            var samsung=shell.SamsungSettings();if(samsung!=null&&samsungIconKey!=samsung.Key){samsungIconKey=samsung.Key;samsungSettings.Content=AppIcons.Visual(samsung,24);LoadSamsungIcon(samsung);}
            samsungSettings.Visibility=samsung!=null?Visibility.Visible:Visibility.Collapsed;
            bool buds=Preferences.Current.QuickGalaxyBuds,devices=shell.QuickDevicesApplication()!=null;string devicesTitle=buds?"Galaxy Buds":"주변 기기 연결";if(devicesName.Text!=devicesTitle){devicesName.Text=devicesTitle;devicesIcon.Update(buds?"bluetooth":"devices",Paint.Text);var shortcut=((Border)devicesTile).Child;shortcut.SetValue(FrameworkElement.ToolTipProperty,devicesTitle);AutomationProperties.SetName(shortcut,devicesTitle);}budsAudio=audioAvailable&&BudsInfo.IsBuds(shell.Services.OutputName);if((buds&&devices||budsAudio)&&!budsIconRequested)LoadBudsIcon();UpdateBudsIcons();devicesTile.Visibility=devices?Visibility.Visible:Visibility.Collapsed;shortcuts.Columns=devices?2:1;if(!devices)windowsTile.Margin=new Thickness(0);
            wifiTile.Visibility=shell.Services.WifiPresent==false?Visibility.Collapsed:Visibility.Visible;bluetoothTile.Visibility=shell.Services.BluetoothPresent==false?Visibility.Collapsed:Visibility.Visible;
            var availableConnections=connections.Children.OfType<FrameworkElement>().Where(e=>e.Visibility==Visibility.Visible).ToArray();sectionFrames["connections"].Visibility=availableConnections.Length==0?Visibility.Collapsed:Visibility.Visible;
            connections.ColumnDefinitions[1].Width=availableConnections.Length>1?new GridLength(1,GridUnitType.Star):new GridLength(0);for(int i=0;i<availableConnections.Length;i++)PlacePair(availableConnections[i],i);
            if(availableConnections.Length==1)availableConnections[0].Margin=new Thickness(0);
            sectionFrames["brightness"].Visibility=brightAvailable?Visibility.Visible:Visibility.Collapsed;
            double powerSlot=PowerDisplay.Slot(state,Preferences.Current.DesktopPowerWatts);footerPower.Visibility=powerSlot>0?Visibility.Visible:Visibility.Collapsed;battery.Width=powerSlot==78?70:62;footerPower.ToolTip=state.Known?"배터리 및 전원":"직접 입력한 PC 전력값 · 실시간 측정 아님";
            var now=DateTime.Now;long second=now.Ticks/TimeSpan.TicksPerSecond;if(second!=clockSecond){clockSecond=second;time.Text=now.ToString("H:mm:ss");}if(calendarDate!=now.Date){calendarDate=now.Date;date.Text=now.ToString("M월 d일 dddd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));}battery.Update(state);
            brightness.Value=Math.Max(0,bright);brightness.IsEnabled=brightAvailable;brightness.ToolTip=brightAvailable?"밝기":"연결된 디스플레이는 Windows 밝기 조절을 지원하지 않습니다";
            string soundKind=SoundIcon.For(sound,muted,audioAvailable);if(volume.Kind!=soundKind){volume.Kind=soundKind;volume.InvalidateVisual();}volume.Value=muted?0:sound;volume.IsEnabled=audioAvailable;
            foreach(var glyph in soundGlyphs)if(glyph.Kind!=soundKind){glyph.Kind=soundKind;glyph.InvalidateVisual();Motion.Fade(glyph,.45,1,150);}Motion.Background(muteCircle,soundKind=="mute"?QuickPanel.Inactive:Paint.ControlWhiteColor);
            var muteGlyph=(Glyph)mute.Content;muteGlyph.Update(muteGlyph.Kind,soundKind=="mute"?Paint.ControlWhite:Paint.Ink);
            var theme=WindowsTheme.Read();themeToggle.IsEnabled=theme.Available&&!themeBusy;themeToggle.ToolTip=theme.Available?(theme.Dark?"다크 모드 켜짐 · 라이트 모드로 전환":"다크 모드 꺼짐 · 다크 모드로 전환"):"Windows 색 모드를 읽지 못했습니다";AutomationProperties.SetItemStatus(themeToggle,theme.Dark?"켜짐":"꺼짐");
            if(darkMode!=theme.Dark){darkMode=theme.Dark;themeGlyph.Kind=theme.Dark?"moon":"brightness";themeGlyph.InvalidateVisual();Motion.Background(themeCircle,theme.Dark?Paint.ControlWhiteColor:QuickPanel.Inactive);Motion.Fade(themeGlyph,.35,1,180);}
            themeGlyph.Update(themeGlyph.Kind,theme.Dark?Paint.Ink:Paint.ControlWhite);
            bool wifi=shell.Services.WifiOn??shell.Wifi!=null,bluetooth=shell.Services.BluetoothOn??false;
            Motion.Background(wifiCircle,wifi?Paint.ControlWhiteColor:QuickPanel.Inactive);Motion.Background(bluetoothCircle,bluetooth?Paint.ControlWhiteColor:QuickPanel.Inactive);
            wifiGlyph.Update(wifiGlyph.Kind,wifi?Paint.Ink:Paint.ControlWhite);bluetoothGlyph.Update(bluetoothGlyph.Kind,bluetooth?Paint.Ink:Paint.ControlWhite);
            wifiGlyph.Opacity=wifi?1:.5;bluetoothGlyph.Opacity=bluetooth?1:.5;wifiName.Text=shell.Wifi??(wifi?"연결 안 됨":"꺼짐");bluetoothState.Text=shell.Services.BluetoothOn.HasValue?(bluetooth?(shell.Buds.ConnectedCaption??"연결 안 됨"):"꺼짐"):"연결 설정";bluetoothState.ToolTip=bluetoothState.Text;
            var efficiency=shell.Toggles.Power();var disturb=shell.Toggles.Disturb.State;
            foreach(var pair in toggleButtons){string key=pair.Key;bool value=key=="mute"?muted:key=="theme"?theme.Dark:key=="dnd"?disturb.Enabled:key=="efficiency"?efficiency.Selected==QuickActions.EfficientMode:key=="awake"?shell.Toggles.Awake:key=="wifi"?wifi:key=="bluetooth"?bluetooth:key=="animations"?Preferences.Current.Animations:key=="slider-scroll"?Preferences.Current.QuickSliderScroll:shell.Toggles.Startup;
                bool available=!pendingToggles.Contains(key)&&(key=="mute"?audioAvailable:key=="theme"?theme.Available&&!themeBusy:key=="dnd"?disturb.Available:key=="efficiency"?efficiency.Available:key=="wifi"?shell.Services.WifiPresent!=false:key=="bluetooth"?shell.Services.BluetoothPresent!=false:true);pair.Value.RefreshState(value,available);}
            mediaPlayer.Update(shell.Services.Media,shell.Services.OutputName);
        }
    }
}
