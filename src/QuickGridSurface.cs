using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace OneTaskBar
{
    internal sealed class QuickWidgetTile : Grid
    {
        internal readonly Glyph Icon;
        internal readonly QuickToggleButton Toggle;
        readonly Button main;
        readonly StackPanel words;
        readonly TextBlock name,caption;
        readonly bool hasDetail;
        QuickWidgetSlot slot;
        internal QuickWidgetTile(string icon,string title,Action action,Action iconAction=null)
        {
            hasDetail=iconAction!=null;main=Paint.Button(new Border(),action,title);main.Background=Brushes.Transparent;main.Padding=new Thickness(0);main.HorizontalContentAlignment=HorizontalAlignment.Stretch;main.VerticalContentAlignment=VerticalAlignment.Stretch;Children.Add(main);
            Icon=new Glyph(icon,25);Toggle=(QuickToggleButton)Paint.Button(Icon,iconAction??action,title,true);Toggle.Padding=new Thickness(0);Toggle.Resources["ButtonRadius"]=new CornerRadius(40);Children.Add(Toggle);
            if(hasDetail){var menu=new ContextMenu();var detail=new MenuItem {Header=title+" 연결 관리"};detail.Click+=(s,e)=>action();menu.Items.Add(detail);ContextMenu=menu;AutomationProperties.SetHelpText(Toggle,"켜기·끄기. 오른쪽 클릭 또는 Shift+F10으로 연결 관리를 엽니다.");}
            name=Paint.TextBlock(title,13,Paint.Text);name.FontWeight=FontWeights.SemiBold;caption=Paint.TextBlock("",10.5,Paint.Muted);name.TextTrimming=caption.TextTrimming=TextTrimming.CharacterEllipsis;words=new StackPanel {IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center};words.Children.Add(name);words.Children.Add(caption);Children.Add(words);SizeChanged+=(s,e)=>Place();
        }
        internal string Caption {get{return caption.Text;}}
        internal string Title {get{return name.Text;}}
        internal void Text(string title,string subtitle){name.Text=title;caption.Text=subtitle??"";main.ToolTip=title+(String.IsNullOrWhiteSpace(subtitle)?"":" · "+subtitle);Toggle.ToolTip=hasDetail?main.ToolTip+" · 오른쪽 클릭: 연결 관리":main.ToolTip;AutomationProperties.SetName(main,title);}
        internal void State(bool enabled,bool available){Toggle.RefreshState(enabled,available);main.IsEnabled=available;main.Opacity=available?1:.45;}
        internal void Shape(QuickWidgetSlot value){slot=value.Copy();Place();}
        void Place()
        {
            if(slot==null)return;bool solo=slot.Columns==1&&slot.Rows==1,tall=slot.Rows>1;double size=solo?Math.Max(28,Math.Min(ActualWidth,ActualHeight)-2):Math.Min(42,Math.Max(28,Math.Min(ActualWidth-12,ActualHeight-10)));Toggle.Width=Toggle.Height=size;Toggle.HorizontalAlignment=solo?HorizontalAlignment.Center:HorizontalAlignment.Left;Toggle.VerticalAlignment=tall?VerticalAlignment.Top:VerticalAlignment.Center;Toggle.Margin=solo?new Thickness(0):new Thickness(tall?5:7,tall?7:0,0,0);Icon.Width=Icon.Height=solo?25:22;
            words.Visibility=solo?Visibility.Collapsed:Visibility.Visible;words.VerticalAlignment=tall?VerticalAlignment.Bottom:VerticalAlignment.Center;words.Margin=tall?new Thickness(10,0,10,14):new Thickness(size+18,0,8,0);name.TextAlignment=caption.TextAlignment=tall?TextAlignment.Right:TextAlignment.Left;
        }
    }
    internal sealed class QuickGridSurface
    {
        readonly QuickWindow host;readonly ShellController shell;
        internal readonly QuickWidgetBoard Board;
        internal readonly DockPanel Root=new DockPanel {Margin=new Thickness(18,19,18,14)};
        internal readonly PillSlider Brightness=new PillSlider("brightness") {Height=Double.NaN,MinWidth=0,Light=true,NoTrackBorder=true,LinearFill=true},Volume=new PillSlider("volume") {Height=Double.NaN,MinWidth=0,Light=true,NoTrackBorder=true,LinearFill=true};
        internal readonly Dictionary<string,QuickToggleButton> Toggles=new Dictionary<string,QuickToggleButton>();
        readonly Dictionary<string,QuickWidgetTile> tiles=new Dictionary<string,QuickWidgetTile>();
        readonly BatteryPill battery=new BatteryPill {Width=62,Height=25};
        readonly TextBlock time=Paint.TextBlock("",23,Paint.Text),date=Paint.TextBlock("",11,Paint.Muted),notice=Paint.TextBlock("",11,Paint.Muted);
        readonly Button batteryButton,samsung;internal readonly Button EditButton;
        readonly Border hint;readonly Button groupMore;
        readonly UniformGrid actionGrid=new UniformGrid();
        readonly List<StackPanel> actionItems=new List<StackPanel>();
        readonly MediaPlayerView media;
        readonly Grid mediaBody=new Grid(),miniMedia=new Grid();readonly Viewbox richMedia;
        readonly Border miniArt=new Border {Width=36,Height=36,CornerRadius=new CornerRadius(10)};
        readonly Button miniArtwork,miniPlay,miniOutput;
        readonly Glyph miniPlayGlyph=new Glyph("play",22);
        readonly Glyph missingArt=new Glyph("volume",22);
        readonly TextBlock miniTitle=Paint.TextBlock("재생 중인 미디어 없음",12,Paint.Text);
        ContextMenu addMenu;bool closed,budsRequested,miniArtReady,presentedExpanded,mediaIdle;ImageSource budsSymbol,samsungIcon,miniArtSource;string samsungKey;long clock=-1,day=-1;int mediaColumns=4,mediaRows=2,presentedColumns,presentedRows;
        internal string BluetoothCaption {get{return tiles["bluetooth"].Caption;}}
        internal string DevicesLabel {get{return tiles["devices"].Title;}}
        internal bool BudsIconLoaded {get{return tiles["devices"].Icon.HasDeviceIcon;}}
        internal bool SamsungIconLoaded {get{return samsungIcon!=null;}}
        internal bool MediaBudsIcon {get{return media.OutputBudsIcon;}}
        internal bool Expanded {get{return Board.Expanded;}}
        internal int VisibleActions {get{return actionItems.Count(i=>i.Visibility==Visibility.Visible);}}
        internal QuickGridSurface(QuickWindow window,ShellController controller)
        {
            host=window;shell=controller;Board=new QuickWidgetBoard(new QuickWidgetLayout(Path.Combine(AppLog.Data,"quick-layout-9.json")));Board.Changed+=()=>Feedback("배치를 저장하지 못했습니다. 다시 시도해 주세요.");
            var header=new Grid {Height=47,Margin=new Thickness(4,0,2,12)};var clockText=new StackPanel();time.FontWeight=FontWeights.SemiBold;clockText.Children.Add(time);clockText.Children.Add(date);header.Children.Add(clockText);var tools=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
            samsung=Tool("settings","삼성 설정",shell.OpenSamsungSettings);tools.Children.Add(samsung);var power=Tool("power","전원",()=>{});power.Click+=(s,e)=>host.BuildPowerMenu(power).IsOpen=true;tools.Children.Add(power);tools.Children.Add(Tool("home","One TaskBar 설정",shell.OpenHomeSettings));header.Children.Add(tools);DockPanel.SetDock(header,Dock.Top);Root.Children.Add(header);
            var footer=new Grid {Height=31,Margin=new Thickness(3,9,3,0)};batteryButton=Paint.Button(battery,()=>shell.ShowDetail("power"),"배터리 및 전원");batteryButton.Background=Brushes.Transparent;batteryButton.Padding=new Thickness(0);batteryButton.HorizontalAlignment=HorizontalAlignment.Left;footer.Children.Add(batteryButton);EditButton=Paint.Action("배치 편집",()=>SetEditing(!Board.Editing));EditButton.HorizontalAlignment=HorizontalAlignment.Center;EditButton.Padding=new Thickness(10,3,10,3);((TextBlock)EditButton.Content).FontSize=11;footer.Children.Add(EditButton);var notifications=Paint.Action("알림  ›",()=>shell.ToggleNotifications(null));notifications.HorizontalAlignment=HorizontalAlignment.Right;notifications.Background=Brushes.Transparent;notifications.Padding=new Thickness(8,3,0,3);footer.Children.Add(notifications);DockPanel.SetDock(footer,Dock.Bottom);Root.Children.Add(footer);
            var scrolled=new DockPanel();notice.TextWrapping=TextWrapping.Wrap;notice.Visibility=Visibility.Collapsed;notice.Margin=new Thickness(4,3,4,9);DockPanel.SetDock(notice,Dock.Top);scrolled.Children.Add(notice);
            var editor=new StackPanel {Margin=new Thickness(3,2,3,10)};var instructions=Paint.TextBlock("메뉴 드래그 · 모서리로 크기 조절 · Esc로 취소",10.5,Paint.Muted);instructions.TextWrapping=TextWrapping.Wrap;editor.Children.Add(instructions);var editTools=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,6,0,0)};editTools.Children.Add(Paint.Action("항목 추가",ShowAdd));var reset=Paint.Action("9.0 배치 초기화",()=>Board.Reset());reset.Margin=new Thickness(6,0,0,0);editTools.Children.Add(reset);editor.Children.Add(editTools);hint=new Border {Child=editor,Visibility=Visibility.Collapsed};DockPanel.SetDock(hint,Dock.Top);scrolled.Children.Add(hint);
            var viewer=new SmoothScrollViewer {Content=Board,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false};Board.Scroller=viewer;scrolled.Children.Add(viewer);Root.Children.Add(scrolled);
            AddTile("wifi","wifi","Wi-Fi",()=>shell.ShowDetail("wifi"),()=>host.InvokeToggle("wifi"));AddTile("bluetooth","bluetooth","블루투스",()=>shell.ShowDetail("bluetooth"),()=>host.InvokeToggle("bluetooth"));
            AddTile("theme","moon","다크 모드",()=>host.InvokeToggle("theme"));AddTile("mute","volume","음소거",()=>host.InvokeToggle("mute"));
            AddTile("devices","devices","주변 기기 연결",shell.OpenSamsungDevices);AddTile("windows","windows-settings","Windows 설정",shell.OpenWindowsSettings);AddTile("capture","screenshot","화면 캡처",shell.OpenScreenCapture);AddTile("manager","task-manager","작업 관리자",shell.OpenTaskManager);
            var grouped=new Grid();grouped.Children.Add(actionGrid);groupMore=Paint.Button(new Border {Width=27,Height=2.5,CornerRadius=new CornerRadius(1.25),Background=Paint.Brush("#88FFFFFF")},()=>ExpandToggles(!Expanded),"기능 토글 펼치기");groupMore.Width=52;groupMore.Height=13;groupMore.Padding=new Thickness(0);groupMore.Background=Brushes.Transparent;groupMore.HorizontalAlignment=HorizontalAlignment.Center;groupMore.VerticalAlignment=VerticalAlignment.Bottom;grouped.Children.Add(groupMore);
            foreach(var item in new[]{new[]{"mute","volume","음소거"},new[]{"theme","moon","다크 모드"},new[]{"dnd","dnd","방해 금지"},new[]{"efficiency","leaf","전력 절약"},new[]{"awake","window","화면 유지"},new[]{"wifi","wifi","Wi-Fi"},new[]{"bluetooth","bluetooth","블루투스"},new[]{"animations","play","독 애니메이션"},new[]{"startup","startup","자동 실행"},new[]{"slider-scroll","scroll","스크롤 조절"}}){string key=item[0];var content=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Tag=key};var button=(QuickToggleButton)Paint.Button(new Glyph(item[1],19),()=>host.InvokeToggle(key),item[2],true);button.Width=button.Height=36;button.Padding=new Thickness(0);button.HorizontalAlignment=HorizontalAlignment.Center;button.Resources["ButtonRadius"]=new CornerRadius(18);Toggles[key]=button;content.Children.Add(button);var label=Paint.TextBlock(item[2],9.5,Paint.Muted);label.HorizontalAlignment=HorizontalAlignment.Center;label.TextTrimming=TextTrimming.CharacterEllipsis;content.Children.Add(label);actionItems.Add(content);}
            Board.Add("toggles","기능 토글",grouped,PresentToggles);
            Board.Add("brightness","밝기",Brightness,slot=>{Brightness.Vertical=slot.Rows>1;Brightness.InvalidateVisual();});Board.Add("volume","음량",Volume,slot=>{Volume.Vertical=slot.Rows>1;Volume.InvalidateVisual();});Brightness.Changed+=shell.Bright;Volume.Changed+=shell.Volume;
            media=new MediaPlayerView(shell.Media,shell.SeekMedia,()=>shell.ShowDetail("sound"),condensed:true,openApp:shell.OpenMediaApplication,dense:true);media.Width=300;richMedia=new Viewbox {Child=media,Stretch=Stretch.Uniform};mediaBody.Children.Add(richMedia);
            miniMedia.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(46)});miniMedia.ColumnDefinitions.Add(new ColumnDefinition());miniMedia.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(35)});miniMedia.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(28)});miniMedia.Margin=new Thickness(10,5,8,5);miniArtwork=Paint.Button(miniArt,()=>shell.OpenMediaApplication(shell.Services.Media.AppId),"현재 재생 앱 열기");miniArtwork.Padding=new Thickness(0);miniArtwork.Background=Brushes.Transparent;miniMedia.Children.Add(miniArtwork);miniTitle.TextTrimming=TextTrimming.CharacterEllipsis;miniTitle.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(miniTitle,1);miniMedia.Children.Add(miniTitle);miniPlay=Paint.Button(miniPlayGlyph,()=>shell.Media("play"),"재생 · 일시 정지");miniPlay.Padding=new Thickness(0);miniPlay.Background=Brushes.Transparent;Grid.SetColumn(miniPlay,2);miniMedia.Children.Add(miniPlay);miniOutput=Tool("audio-output","출력 기기 변경",()=>shell.ShowDetail("sound"));Grid.SetColumn(miniOutput,3);miniMedia.Children.Add(miniOutput);mediaBody.Children.Add(miniMedia);
            Board.Add("media","미디어 플레이어",mediaBody,slot=>{mediaColumns=slot.Columns;mediaRows=slot.Rows;MediaShape();});
            host.IsVisibleChanged+=(s,e)=>{if(!host.IsVisible){Board.Cancel();ExpandToggles(false,false);SetEditing(false);CloseMenus();}};host.Closed+=(s,e)=>{closed=true;Board.Cancel();ExpandToggles(false,false);CloseMenus();};
        }
        static Button Tool(string icon,string label,Action action){var button=Paint.Button(new Glyph(icon,21),action,label);button.Width=button.Height=32;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;return button;}
        void AddTile(string key,string icon,string title,Action action,Action circle=null){var tile=new QuickWidgetTile(icon,title,action,circle);tiles[key]=tile;if(key=="wifi"||key=="bluetooth"||key=="theme"||key=="mute")Toggles[key]=tile.Toggle;Board.Add(key,title,tile,tile.Shape);}
        void MediaShape(){bool rich=mediaRows>1&&mediaColumns>1;richMedia.Visibility=rich?Visibility.Visible:Visibility.Collapsed;miniMedia.Visibility=rich?Visibility.Collapsed:Visibility.Visible;bool single=mediaColumns==1;miniArtwork.Visibility=miniTitle.Visibility=single?Visibility.Collapsed:Visibility.Visible;miniOutput.Visibility=mediaColumns>=3?Visibility.Visible:Visibility.Collapsed;miniPlay.Visibility=mediaIdle?Visibility.Collapsed:Visibility.Visible;miniMedia.ColumnDefinitions[0].Width=new GridLength(single?0:46);miniMedia.ColumnDefinitions[2].Width=mediaIdle?new GridLength(0):single?new GridLength(1,GridUnitType.Star):new GridLength(35);Grid.SetColumn(miniPlay,single?1:2);miniMedia.ColumnDefinitions[3].Width=new GridLength(mediaColumns>=3?28:0);}
        internal void SetEditing(bool value){if(value&&Expanded)ExpandToggles(false,false);Board.SetEditing(value);((TextBlock)EditButton.Content).Text=value?"완료":"배치 편집";hint.Visibility=value?Visibility.Visible:Visibility.Collapsed;}
        void ShowAdd(){if(addMenu!=null)addMenu.IsOpen=false;var menu=new ContextMenu {PlacementTarget=EditButton};addMenu=menu;foreach(var item in Board.Layout.Items.Where(i=>i.Hidden)){string key=item.Key;var add=new MenuItem {Header=Title(key)};add.Click+=(s,e)=>Board.AddWidget(key);menu.Items.Add(add);}if(menu.Items.Count==0)menu.Items.Add(new MenuItem {Header="모든 항목이 표시 중입니다",IsEnabled=false});menu.IsOpen=true;}
        void CloseMenus(){if(addMenu!=null)addMenu.IsOpen=false;foreach(var tile in tiles.Values)if(tile.ContextMenu!=null)tile.ContextMenu.IsOpen=false;}
        string Title(string key){return tiles.ContainsKey(key)?tiles[key].Title:key=="brightness"?"밝기":key=="volume"?"음량":key=="media"?"미디어 플레이어":"기능 토글";}
        void PresentToggles(QuickWidgetSlot slot){if(presentedColumns==slot.Columns&&presentedRows==slot.Rows&&presentedExpanded==Expanded)return;presentedColumns=slot.Columns;presentedRows=slot.Rows;presentedExpanded=Expanded;actionGrid.Children.Clear();actionGrid.Columns=slot.Columns;actionGrid.Rows=slot.Rows;actionGrid.Margin=new Thickness(3,1,3,13);int count=0;foreach(var item in actionItems){string key=(string)item.Tag;bool eligible=Expanded||(key!="mute"&&key!="theme"&&key!="wifi"&&key!="bluetooth"),visible=eligible&&count<slot.Columns*slot.Rows;item.Visibility=visible?Visibility.Visible:Visibility.Collapsed;if(visible){count++;actionGrid.Children.Add(item);}((TextBlock)item.Children[1]).Visibility=Expanded||slot.Rows>1?Visibility.Visible:Visibility.Collapsed;}groupMore.ToolTip=Expanded?"기능 토글 접기":"기능 토글 펼치기";AutomationProperties.SetName(groupMore,(string)groupMore.ToolTip);}
        internal void ExpandToggles(bool value,bool animate=true){Board.SetExpanded(value,animate);PresentToggles(Board.DisplayLayout.Find("toggles"));if(animate&&host.IsVisible)Motion.Fade(actionGrid,.7,1,170);}
        internal void Feedback(string text){notice.Text=text;notice.Visibility=Visibility.Visible;}
        async void LoadBuds(){budsRequested=true;try{var symbol=await BackgroundWork.Run(BudsDeviceData.ShortcutIcon);if(!closed){budsSymbol=symbol;ApplyBuds();}}catch(Exception e){AppLog.Once("quick9-buds-symbol",e.GetType().Name);}}
        void ApplyBuds(){tiles["devices"].Icon.SetDeviceIcon(Preferences.Current.QuickGalaxyBuds?budsSymbol:null);Volume.SetDeviceIcon(Volume.IsEnabled&&BudsInfo.IsBuds(shell.Services.OutputName)?budsSymbol:null);}
        async void SamsungIcon(AppEntry app){try{var image=app.Icon??await BackgroundWork.Run(()=>AppIcons.Get(app));if(!closed&&app.Key==samsungKey){samsungIcon=image;if(image!=null)samsung.Content=AppIcons.Visual(image,24);}}catch{}}
        internal void Refresh(PowerState power,int bright,double sound,bool muted,bool audioAvailable,bool brightAvailable)
        {
            var now=DateTime.Now;if(now.Ticks/TimeSpan.TicksPerSecond!=clock){clock=now.Ticks/TimeSpan.TicksPerSecond;time.Text=now.ToString("H:mm:ss");}if(day!=now.Date.Ticks){day=now.Date.Ticks;date.Text=now.ToString("M월 d일 dddd");}battery.Update(power);batteryButton.Visibility=PowerDisplay.Slot(power,Preferences.Current.DesktopPowerWatts)>0?Visibility.Visible:Visibility.Collapsed;
            Brightness.IsEnabled=brightAvailable;Brightness.Value=bright<0?0:bright;Volume.IsEnabled=audioAvailable;Volume.Value=muted?0:sound;Volume.Kind=SoundIcon.For(sound,muted,audioAvailable);Volume.InvalidateVisual();Brightness.WheelAdjust=Volume.WheelAdjust=Preferences.Current.QuickSliderScroll;
            bool wifi=shell.Services.WifiOn??shell.Wifi!=null,bluetooth=shell.Services.BluetoothOn??false;tiles["wifi"].Text("Wi-Fi",shell.Wifi??(wifi?"연결 안 됨":"꺼짐"));tiles["bluetooth"].Text("블루투스",shell.Services.BluetoothOn.HasValue?(bluetooth?(shell.Buds.ConnectedCaption??"연결 안 됨"):"꺼짐"):"연결 설정");
            var theme=WindowsTheme.Read();tiles["theme"].Text("다크 모드",theme.Dark?"켜짐":"꺼짐");tiles["mute"].Text("음소거",muted?"켜짐":"꺼짐");tiles["mute"].Icon.Update(SoundIcon.For(sound,muted,audioAvailable),tiles["mute"].Toggle.Checked?Paint.Ink:Paint.ControlWhite);((Glyph)Toggles["mute"].Content).Update(SoundIcon.For(sound,muted,audioAvailable),Toggles["mute"].Checked?Paint.Ink:Paint.ControlWhite);
            var efficiency=shell.Toggles.Power();var disturb=shell.Toggles.Disturb.State;foreach(var pair in Toggles){string key=pair.Key;bool on=key=="wifi"?wifi:key=="bluetooth"?bluetooth:key=="theme"?theme.Dark:key=="mute"?muted:key=="efficiency"?efficiency.Selected==QuickActions.EfficientMode:key=="dnd"?disturb.Enabled:key=="awake"?shell.Toggles.Awake:key=="animations"?Preferences.Current.Animations:key=="startup"?shell.Toggles.Startup:Preferences.Current.QuickSliderScroll;bool available=!host.TogglePending(key)&&(key=="wifi"?shell.Services.WifiPresent!=false:key=="bluetooth"?shell.Services.BluetoothPresent!=false:key=="theme"?theme.Available:key=="mute"?audioAvailable:key=="efficiency"?efficiency.Available:key=="dnd"?disturb.Available:true);if(tiles.ContainsKey(key))tiles[key].State(on,available);pair.Value.RefreshState(on,available);}
            bool buds=Preferences.Current.QuickGalaxyBuds;tiles["devices"].Text(buds?"Galaxy Buds":"주변 기기 연결",buds?shell.Buds.ConnectedCaption:"");tiles["devices"].Icon.Update(buds?"bluetooth":"devices",Paint.Text);tiles["devices"].State(false,shell.QuickDevicesApplication()!=null);if(!budsRequested&&(buds||BudsInfo.IsBuds(shell.Services.OutputName)))LoadBuds();ApplyBuds();
            var app=shell.SamsungSettings();samsung.Visibility=app!=null?Visibility.Visible:Visibility.Collapsed;if(app!=null&&samsungKey!=app.Key){samsungKey=app.Key;SamsungIcon(app);}media.Update(shell.Services.Media,shell.Services.OutputName);var state=shell.Services.Media??new MediaState();miniTitle.Text=state.Active?state.Title:"재생 중인 미디어 없음";miniPlayGlyph.Update(state.Playing?"pause":"play",Paint.Text);miniPlay.IsEnabled=state.Available&&state.CanPlay;miniArtwork.IsEnabled=state.Active&&state.Artwork!=null&&!String.IsNullOrEmpty(state.AppId);if(!miniArtReady||miniArtSource!=state.Artwork){miniArtReady=true;miniArtSource=state.Artwork;miniArt.Background=state.Artwork==null?(Brush)Brushes.Transparent:new ImageBrush(state.Artwork){Stretch=Stretch.UniformToFill};miniArt.Child=state.Artwork==null?missingArt:null;}bool changed=mediaIdle!=!state.Active;mediaIdle=!state.Active;Board.SetMediaIdle(mediaIdle);if(changed)MediaShape();
        }
        internal object Snapshot(){return new {ui="9.0",editing=Board.Editing,expanded=Expanded,inlineToggles=true,controlHeight=QuickWidgetBoard.ControlHeight,gestureActive=Board.GestureActive,noOverlap=Board.DisplayLayout.Fits(),separateSliderButtons=true,borderlessSliders=Brightness.NoTrackBorder&&Volume.NoTrackBorder,items=Board.Layout.Items.Select(i=>new {key=i.Key,x=i.X,y=i.Y,columns=i.Columns,rows=i.Rows,hidden=i.Hidden}).ToArray()};}
    }
}
