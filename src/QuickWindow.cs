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
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    internal sealed class QuickWindow : GlassWindow
    {
        readonly ShellController shell;
        readonly PillSlider brightness=new PillSlider("brightness"),volume=new PillSlider("volume");
        readonly BatteryPill battery=new BatteryPill {Width=62,Height=25};
        readonly TextBlock time=Paint.TextBlock("",23,Paint.Text),date=Paint.TextBlock("",11,Paint.Muted);
        readonly TextBlock wifiName=Paint.TextBlock("연결 안 됨",11,Paint.Muted),bluetoothState=Paint.TextBlock("기기 연결",11,Paint.Muted);
        readonly TextBlock mediaTitle=Paint.TextBlock("재생 중인 미디어 없음",14,Paint.Text),artist=Paint.TextBlock("음악이나 동영상을 재생해 보세요",11,Paint.Muted);
        readonly Border wifiCircle,bluetoothCircle;
        readonly ImageBrush artworkBrush=new ImageBrush {Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center};
        readonly Border artwork=new Border {Width=52,Height=52,CornerRadius=new CornerRadius(13)};
        readonly Glyph playGlyph=new Glyph("play",22),wifiGlyph=new Glyph("wifi",20),bluetoothGlyph=new Glyph("bluetooth",20);
        readonly Button play,next,previous,mute,samsungSettings;
        readonly Button footerPower,topPower;readonly FrameworkElement wifiTile,bluetoothTile,devicesTile,windowsTile;readonly Grid connections;readonly UniformGrid shortcuts;
        readonly Dictionary<string,FrameworkElement> sectionFrames=new Dictionary<string,FrameworkElement>();
        readonly Button themeToggle;
        readonly Glyph themeGlyph=new Glyph("moon",21);
        readonly Border themeCircle;
        readonly List<Glyph> soundGlyphs=new List<Glyph>();
        readonly Dictionary<string,QuickToggleButton> toggleButtons=new Dictionary<string,QuickToggleButton>();
        readonly HashSet<string> pendingToggles=new HashSet<string>();
        internal QuickToggleButton FindToggle(string key){return toggleButtons[key];}
        internal string[] ToggleKeys {get{return toggleButtons.Keys.ToArray();}}
        readonly List<TextBlock> actionNames=new List<TextBlock>();
        readonly TextBlock notice=Paint.TextBlock("",11,Paint.Muted);
        bool themeBusy;string currentMedia;bool? darkMode;
        System.Windows.Media.ImageSource samsungIcon;
        internal bool SamsungIconLoaded {get{return samsungIcon!=null;}}
        readonly Border muteCircle;
        readonly StackPanel body=new StackPanel();
        readonly List<ReorderGroup> groups=new List<ReorderGroup>();
        readonly List<ColumnDefinition> gripColumns=new List<ColumnDefinition>();
        readonly List<FrameworkElement> grips=new List<FrameworkElement>();
        readonly ReorderGroup sections;
        readonly UniformGrid actionButtons;
        readonly TextBlock editLabel=Paint.TextBlock("배치 편집",11,Paint.Text);
        readonly Border editHint;
        bool editing,layoutDirty;
        internal bool Editing {get{return editing;}}
        internal bool Expanded {get{return expanded;}}
        bool expanded;int actionMotion;readonly Grid actionArea=new Grid {Height=62,ClipToBounds=true};
        internal QuickWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · 빠른 설정";Width=380;Height=650;Radius=30;Dark=true;Surface.CornerRadius=new CornerRadius(30);Surface.Background=QuickPanel.Glass;Surface.BorderBrush=Paint.Brush("#48FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var root=new DockPanel {Margin=new Thickness(18,19,18,14)};
            var header=new Grid {Height=47,Margin=new Thickness(4,0,2,12)};
            var heading=new StackPanel();time.FontWeight=FontWeights.SemiBold;heading.Children.Add(time);heading.Children.Add(date);header.Children.Add(heading);
            var tools=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
            samsungSettings=Paint.Button(samsungIcon!=null?AppIcons.Visual(samsungIcon,24):(UIElement)new Glyph("settings",22),()=>shell.OpenSamsungSettings(),"삼성 설정");samsungSettings.Width=samsungSettings.Height=34;samsungSettings.Padding=new Thickness(0);samsungSettings.Background=Brushes.Transparent;tools.Children.Add(samsungSettings);
            tools.Children.Add(topPower=IconButton("power",()=>ShowPowerMenu(topPower),"전원",34));
            tools.Children.Add(IconButton("settings",()=>shell.OpenHomeSettings(),"One TaskBar 설정",34));header.Children.Add(tools);
            DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var footer=new Grid {Height=31,Margin=new Thickness(3,9,3,0)};
            var power=Paint.Button(battery,()=>shell.ShowDetail("power"),"배터리 및 전원");power.Background=Brushes.Transparent;power.Padding=new Thickness(0);power.HorizontalAlignment=HorizontalAlignment.Left;footer.Children.Add(power);
            footerPower=power;
            var edit=Paint.Button(editLabel,()=>EditLayout(!editing),"빠른 설정 항목 · 묶음 위치 바꾸기");edit.HorizontalAlignment=HorizontalAlignment.Center;edit.Padding=new Thickness(9,3,9,3);footer.Children.Add(edit);
            var native=Paint.Action("알림 · 달력  ›",()=>shell.ToggleNotifications(null));native.HorizontalAlignment=HorizontalAlignment.Right;native.Background=Brushes.Transparent;native.Padding=new Thickness(8,3,0,3);footer.Children.Add(native);
            DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            sections=new ReorderGroup("quick.sections",body,Orientation.Vertical,()=>editing);groups.Add(sections);
            var scrolled=new DockPanel();
            notice.Visibility=Visibility.Collapsed;notice.TextWrapping=TextWrapping.Wrap;notice.Margin=new Thickness(5,5,5,9);DockPanel.SetDock(notice,Dock.Top);scrolled.Children.Add(notice);
            var help=new Grid {Margin=new Thickness(5,5,5,9)};var message=Paint.TextBlock("아이콘 또는 ⋮⋮ 묶음을 드래그",10,Paint.Muted);message.VerticalAlignment=VerticalAlignment.Center;help.Children.Add(message);
            var reset=Paint.Action("초기화",()=>LayoutOrder.Reset("quick."));reset.FontSize=10;reset.Padding=new Thickness(8,5,8,5);reset.HorizontalAlignment=HorizontalAlignment.Right;help.Children.Add(reset);
            editHint=new Border {Child=help,Visibility=Visibility.Collapsed};DockPanel.SetDock(editHint,Dock.Top);scrolled.Children.Add(editHint);scrolled.Children.Add(body);
            connections=new Grid {Height=62,Margin=new Thickness(0,0,0,10)};connections.ColumnDefinitions.Add(new ColumnDefinition());connections.ColumnDefinitions.Add(new ColumnDefinition());
            var pair=new ReorderGroup("quick.connections",connections,Orientation.Horizontal,()=>editing,PlacePair);groups.Add(pair);
            wifiTile=Connection("Wi-Fi",wifiName,wifiGlyph,()=>shell.Radio("wifi"),()=>shell.ShowDetail("wifi"),out wifiCircle);pair.Add("wifi",wifiTile);
            bluetoothTile=Connection("블루투스",bluetoothState,bluetoothGlyph,()=>shell.Radio("bluetooth"),()=>shell.ShowDetail("bluetooth"),out bluetoothCircle);pair.Add("bluetooth",bluetoothTile);AddSection("connections",connections);
            var actions=new UniformGrid {Columns=4,Rows=2,Height=140,Margin=new Thickness(7,7,7,4),VerticalAlignment=VerticalAlignment.Top};actionButtons=actions;
            var actionOrder=new ReorderGroup("quick.actions",actions,Orientation.Horizontal,()=>editing);groups.Add(actionOrder);
            actionOrder.Add("mute",RoundAction("volume","음소거","mute"));
            actionOrder.Add("theme",RoundAction("moon","다크 모드","theme"));
            actionOrder.Add("efficiency",RoundAction("leaf","전력 절약","efficiency"));
            actionOrder.Add("awake",RoundAction("window","화면 유지","awake"));
            actionOrder.Add("wifi",RoundAction("wifi","Wi-Fi","wifi"));
            actionOrder.Add("bluetooth",RoundAction("bluetooth","블루투스","bluetooth"));
            actionOrder.Add("animations",RoundAction("play","독 애니메이션","animations"));
            actionOrder.Add("startup",RoundAction("startup","자동 실행","startup"));
            actionArea.Children.Add(actions);
            var handle=Paint.Button(new Border {Width=28,Height=3,CornerRadius=new CornerRadius(2),Background=Paint.Brush("#70F4F8F5")},()=>ExpandActions(),"빠른 설정 더 보기 · 접기");handle.Background=Brushes.Transparent;handle.Height=11;handle.Padding=new Thickness(20,4,20,4);handle.VerticalAlignment=VerticalAlignment.Bottom;actionArea.Children.Add(handle);
            AddSection("actions",Card(actionArea,34,new Thickness(0,0,0,10)));
            themeToggle=Paint.Button(themeGlyph,()=>ToggleTheme(),"Windows 다크 모드 전환");themeToggle.Width=themeToggle.Height=44;themeToggle.Padding=new Thickness(0);themeToggle.Background=Brushes.Transparent;themeToggle.Resources["ButtonRadius"]=new CornerRadius(22);
            themeCircle=new Border {Width=44,Height=44,CornerRadius=new CornerRadius(22),Background=Paint.Brush(QuickPanel.Inactive),Child=themeToggle};AddSection("brightness",SliderRow(brightness,themeCircle));
            mute=IconButton("volume",()=>shell.Mute(),"음소거 전환",44);soundGlyphs.Add((Glyph)mute.Content);muteCircle=new Border {Width=44,Height=44,CornerRadius=new CornerRadius(22),Background=Paint.Brush(QuickPanel.Inactive),Child=mute};
            AddSection("volume",SliderRow(volume,muteCircle));
            var media=new Grid {MinHeight=90,Margin=new Thickness(15,10,15,10)};
            media.ColumnDefinitions.Add(new ColumnDefinition());media.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(60)});
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,0,8,0)};mediaTitle.FontWeight=FontWeights.SemiBold;mediaTitle.TextTrimming=TextTrimming.CharacterEllipsis;artist.TextTrimming=TextTrimming.CharacterEllipsis;
            labels.Children.Add(mediaTitle);labels.Children.Add(artist);media.Children.Add(labels);
            artwork.Background=artworkBrush;artwork.VerticalAlignment=VerticalAlignment.Top;Grid.SetColumn(artwork,1);media.Children.Add(artwork);
            var transport=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(-6,44,0,0)};
            previous=IconButton("previous",()=>shell.Media("previous"),"이전 곡",40);next=IconButton("next",()=>shell.Media("next"),"다음 곡",40);
            play=Paint.Button(playGlyph,()=>shell.Media("play"),"재생 · 일시 정지");play.Width=48;play.Height=40;play.Padding=new Thickness(0);play.Background=Brushes.Transparent;play.BorderBrush=Brushes.Transparent;play.BorderThickness=new Thickness(0);
            transport.Children.Add(previous);transport.Children.Add(play);transport.Children.Add(next);media.Children.Add(transport);
            var output=Paint.Action("출력  ›",()=>shell.ShowDetail("sound"));output.FontSize=11;output.HorizontalAlignment=HorizontalAlignment.Right;output.VerticalAlignment=VerticalAlignment.Bottom;output.Padding=new Thickness(8,6,8,6);Grid.SetColumn(output,1);media.Children.Add(output);
            AddSection("media",Card(media,28,new Thickness(0,0,0,10)));
            shortcuts=new UniformGrid {Columns=2};var bottomOrder=new ReorderGroup("quick.shortcuts",shortcuts,Orientation.Horizontal,()=>editing,PlacePair);groups.Add(bottomOrder);
            devicesTile=WideTile("devices","주변 기기 연결","samsung-devices",new Thickness(0));windowsTile=WideTile("windows-settings","Windows 설정","windows-settings",new Thickness(0));bottomOrder.Add("devices",devicesTile);bottomOrder.Add("focus",windowsTile);AddSection("shortcuts",shortcuts);
            root.Children.Add(new SmoothScrollViewer {Content=scrolled,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Surface.Child=root;brightness.Light=volume.Light=true;brightness.DarkTrack=volume.DarkTrack=true;ApplyOrder();
            LayoutOrder.Changed+=OrderChanged;DragReorder.Finished+=DragFinished;Closed+=(s,e)=> {LayoutOrder.Changed-=OrderChanged;DragReorder.Finished-=DragFinished;};
            brightness.Changed+=v=>shell.Bright(v);volume.Changed+=v=>shell.Volume(v);
            Deactivated+=(s,e)=> {if(!Program.Inspectable&&IsVisible&&!IsMouseOver&&!DragReorder.Active)Hide();};
        }
        static void PlacePair(FrameworkElement element,int index) {Grid.SetColumn(element,index);element.Margin=index==0?new Thickness(0,0,5,0):new Thickness(5,0,0,0);}
        void AddSection(string key,FrameworkElement content)
        {
            var wrapper=new Grid {Background=Brushes.Transparent};var column=new ColumnDefinition {Width=new GridLength(0)};wrapper.ColumnDefinitions.Add(column);wrapper.ColumnDefinitions.Add(new ColumnDefinition());gripColumns.Add(column);
            var handle=new Border {Background=Brushes.Transparent,Child=new Glyph("grip",13){Color=Paint.Muted},Cursor=System.Windows.Input.Cursors.SizeAll,Visibility=Visibility.Collapsed,ToolTip="이 묶음 드래그하여 이동"};grips.Add(handle);wrapper.Children.Add(handle);Grid.SetColumn(content,1);wrapper.Children.Add(content);sections.Add(key,wrapper,handle);sectionFrames[key]=wrapper;
        }
        internal void EditLayout(bool value)
        {
            editing=value;editLabel.Text=value?"완료":"배치 편집";editHint.Visibility=value?Visibility.Visible:Visibility.Collapsed;
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
            double bottom=0;foreach(FrameworkElement element in body.Children){double top=element.TranslatePoint(new Point(),body).Y;if(top<bottom-.5)return false;bottom=top+element.ActualHeight;}return true;
        }
        internal object LayoutSnapshot() {return new {editing=editing,expanded=expanded,visibleActionLabels=actionNames.Count(n=>n.Visibility==Visibility.Visible),circleRadius=21.5,buttonsUnclipped=ButtonsUnclipped(),sections=sections.Keys,groups=groups.Skip(1).Select(g=>g.Keys).ToArray(),noOverlap=LayoutFits(),actionHeight=actionArea.ActualHeight,soundIcon=volume.Kind,themeAvailable=themeToggle.IsEnabled,dark=darkMode,sizes=sections.Items.Select(i=>new {key=i.Key,height=i.Element.ActualHeight,y=i.Element.TranslatePoint(new Point(),body).Y}).ToArray()};}
        bool ButtonsUnclipped()
        {
            var clip=VisualTreeHelper.GetClip(actionButtons);Rect bounds=clip!=null?clip.Bounds:new Rect(0,0,actionButtons.ActualWidth,actionButtons.ActualHeight);
            foreach(StackPanel item in actionButtons.Children.Cast<StackPanel>().Take(expanded||editing?actionNames.Count:4)){var button=item.Children.OfType<Button>().FirstOrDefault();if(button==null)continue;Rect rect=button.TransformToAncestor(actionButtons).TransformBounds(new Rect(0,0,button.ActualWidth,button.ActualHeight));if(rect.Top<bounds.Top||rect.Bottom>bounds.Bottom||rect.Left<bounds.Left||rect.Right>bounds.Right)return false;}return true;
        }
        void ExpandActions()
        {
            SetExpanded(!expanded);
        }
        internal void SetExpanded(bool value) {expanded=value;PresentActions(true);}
        internal object AudioSnapshot() {return new {slider=volume.Kind,buttons=soundGlyphs.Select(g=>g.Kind).ToArray()};}
        void PresentActions(bool animate)
        {
            int token=++actionMotion;bool show=expanded||editing;double from=actionArea.Height,target=show?162:62;actionArea.BeginAnimation(HeightProperty,null);actionArea.Height=target;
            if(animate&&Motion.Enabled&&IsVisible)actionArea.BeginAnimation(HeightProperty,Motion.PanelTween(from,target,330));
            foreach(var name in actionNames){name.BeginAnimation(OpacityProperty,null);if(show){name.Visibility=Visibility.Visible;Motion.Fade(name,animate?0:1,1,220);}else if(animate&&Motion.Enabled&&name.IsVisible){var fade=Motion.Tween(name.Opacity,0,150);fade.Completed+=(s,e)=> {if(token==actionMotion)name.Visibility=Visibility.Collapsed;};name.Opacity=0;name.BeginAnimation(OpacityProperty,fade);}else {name.Visibility=Visibility.Collapsed;name.Opacity=0;}}
        }
        void ToggleTheme(){InvokeToggle("theme");}
        async void InvokeToggle(string key)
        {
            if(pendingToggles.Contains(key))return;pendingToggles.Add(key);if(key=="theme")themeBusy=true;Refresh(shell.Power,shell.Brightness,shell.Audio.Value,shell.Audio.Muted,shell.Audio.Available,shell.Brightness>=0);
            try {
                if(key=="mute"){bool before=shell.Audio.Muted;shell.Mute();shell.Audio.Read();if(shell.Audio.Muted==before)throw new InvalidOperationException("Windows 음소거 상태를 변경하지 못했습니다.");}
                else if(key=="theme"){var before=WindowsTheme.Read(true);if(!before.Available)throw new InvalidOperationException("Windows 색 모드를 읽지 못했습니다.");await Task.Run(()=>WindowsTheme.SetDark(!before.Dark));WindowsTheme.Invalidate();}
                else if(key=="efficiency")shell.Toggles.ToggleEfficiency();
                else if(key=="awake")shell.Toggles.SetAwake(!shell.Toggles.Awake);
                else if(key=="animations")Preferences.Set(p=>p.Animations=!p.Animations);
                else if(key=="startup")shell.Toggles.ToggleStartup();
                else if(key=="wifi"||key=="bluetooth"){if(!await shell.Services.ToggleRadio(key))throw new InvalidOperationException("Windows가 이 무선 장치의 제어를 허용하지 않았습니다.");await shell.Services.Refresh();}
                notice.Visibility=Visibility.Collapsed;
            }catch(Exception error){Feedback(error.Message);}
            finally {pendingToggles.Remove(key);if(key=="theme")themeBusy=false;Refresh(shell.Power,shell.Brightness,shell.Audio.Value,shell.Audio.Muted,shell.Audio.Available,shell.Brightness>=0);}
        }
        internal void Feedback(string text) {notice.Text=text;notice.Visibility=Visibility.Visible;}
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
        static Border SliderRow(PillSlider slider,UIElement action)
        {
            var grid=new Grid {Margin=new Thickness(12,9,12,9)};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(52)});
            slider.Height=44;slider.Margin=new Thickness(0,0,8,0);grid.Children.Add(slider);Grid.SetColumn(action,1);grid.Children.Add(action);return Card(grid,34,new Thickness(0,0,0,10));
        }
        Border WideTile(string icon,string label,string page,Thickness margin)
        {
            var row=new StackPanel {Orientation=Orientation.Horizontal};row.Children.Add(new Glyph(icon,22) {Margin=new Thickness(0,0,9,0)});row.Children.Add(Paint.TextBlock(label,11,Paint.Text));
            var button=Paint.Button(row,()=>OpenPage(page),label);button.Background=Brushes.Transparent;button.Height=59;button.Padding=new Thickness(9,7,9,7);return Card(button,30,margin);
        }
        void OpenPage(string page)
        {
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
            var samsung=shell.SamsungSettings();if(samsung!=null&&samsung.Icon!=null&&samsungIcon==null){samsungIcon=samsung.Icon;samsungSettings.Content=AppIcons.Visual(samsungIcon,24);}
            samsungSettings.Visibility=samsung!=null?Visibility.Visible:Visibility.Collapsed;
            bool devices=shell.SamsungDevices()!=null;devicesTile.Visibility=devices?Visibility.Visible:Visibility.Collapsed;shortcuts.Columns=devices?2:1;if(!devices)windowsTile.Margin=new Thickness(0);
            wifiTile.Visibility=shell.Services.WifiPresent==false?Visibility.Collapsed:Visibility.Visible;bluetoothTile.Visibility=shell.Services.BluetoothPresent==false?Visibility.Collapsed:Visibility.Visible;
            var availableConnections=connections.Children.OfType<FrameworkElement>().Where(e=>e.Visibility==Visibility.Visible).ToArray();sectionFrames["connections"].Visibility=availableConnections.Length==0?Visibility.Collapsed:Visibility.Visible;
            connections.ColumnDefinitions[1].Width=availableConnections.Length>1?new GridLength(1,GridUnitType.Star):new GridLength(0);for(int i=0;i<availableConnections.Length;i++)PlacePair(availableConnections[i],i);
            if(availableConnections.Length==1)availableConnections[0].Margin=new Thickness(0);
            sectionFrames["brightness"].Visibility=brightAvailable?Visibility.Visible:Visibility.Collapsed;
            double powerSlot=PowerDisplay.Slot(state,Preferences.Current.DesktopPowerWatts);footerPower.Visibility=powerSlot>0?Visibility.Visible:Visibility.Collapsed;battery.Width=powerSlot==78?70:62;footerPower.ToolTip=state.Known?"배터리 및 전원":"직접 입력한 PC 전력값 · 실시간 측정 아님";
            time.Text=DateTime.Now.ToString("H:mm:ss");date.Text=DateTime.Now.ToString("M월 d일 dddd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));battery.Update(state);
            brightness.Value=Math.Max(0,bright);brightness.IsEnabled=brightAvailable;brightness.ToolTip=brightAvailable?"밝기":"연결된 디스플레이는 Windows 밝기 조절을 지원하지 않습니다";
            string soundKind=SoundIcon.For(sound,muted,audioAvailable);volume.Kind=soundKind;volume.Value=muted?0:sound;volume.IsEnabled=audioAvailable;volume.InvalidateVisual();
            foreach(var glyph in soundGlyphs)if(glyph.Kind!=soundKind){glyph.Kind=soundKind;glyph.InvalidateVisual();Motion.Fade(glyph,.45,1,150);}Motion.Background(muteCircle,soundKind=="mute"?QuickPanel.Inactive:Paint.ControlWhiteColor);
            var muteGlyph=(Glyph)mute.Content;muteGlyph.Color=soundKind=="mute"?Paint.ControlWhite:Paint.Ink;muteGlyph.InvalidateVisual();
            var theme=WindowsTheme.Read();themeToggle.IsEnabled=theme.Available&&!themeBusy;themeToggle.ToolTip=theme.Available?(theme.Dark?"다크 모드 켜짐 · 라이트 모드로 전환":"다크 모드 꺼짐 · 다크 모드로 전환"):"Windows 색 모드를 읽지 못했습니다";AutomationProperties.SetItemStatus(themeToggle,theme.Dark?"켜짐":"꺼짐");
            if(darkMode!=theme.Dark){darkMode=theme.Dark;themeGlyph.Kind=theme.Dark?"moon":"brightness";themeGlyph.InvalidateVisual();Motion.Background(themeCircle,theme.Dark?Paint.ControlWhiteColor:QuickPanel.Inactive);Motion.Fade(themeGlyph,.35,1,180);}
            themeGlyph.Color=theme.Dark?Paint.Ink:Paint.ControlWhite;themeGlyph.InvalidateVisual();
            bool wifi=shell.Services.WifiOn??shell.Wifi!=null,bluetooth=shell.Services.BluetoothOn??false;
            Motion.Background(wifiCircle,wifi?Paint.ControlWhiteColor:QuickPanel.Inactive);Motion.Background(bluetoothCircle,bluetooth?Paint.ControlWhiteColor:QuickPanel.Inactive);
            wifiGlyph.Color=wifi?Paint.Ink:Paint.ControlWhite;bluetoothGlyph.Color=bluetooth?Paint.Ink:Paint.ControlWhite;wifiGlyph.InvalidateVisual();bluetoothGlyph.InvalidateVisual();
            wifiGlyph.Opacity=wifi?1:.5;bluetoothGlyph.Opacity=bluetooth?1:.5;wifiName.Text=shell.Wifi??(wifi?"연결 안 됨":"꺼짐");bluetoothState.Text=shell.Services.BluetoothOn.HasValue?(bluetooth?"켜짐":"꺼짐"):"연결 설정";
            var efficiency=shell.Toggles.Power();
            foreach(var pair in toggleButtons){string key=pair.Key;bool value=key=="mute"?muted:key=="theme"?theme.Dark:key=="efficiency"?efficiency.Selected==QuickActions.EfficientMode:key=="awake"?shell.Toggles.Awake:key=="wifi"?wifi:key=="bluetooth"?bluetooth:key=="animations"?Preferences.Current.Animations:shell.Toggles.Startup;
                bool available=!pendingToggles.Contains(key)&&(key=="mute"?audioAvailable:key=="theme"?theme.Available&&!themeBusy:key=="efficiency"?efficiency.Available:key=="wifi"?shell.Services.WifiPresent!=false:key=="bluetooth"?shell.Services.BluetoothPresent!=false:true);pair.Value.RefreshState(value,available);}
            var media=shell.Services.Media;string mediaKey=media.Title+"|"+media.Artist;if(currentMedia!=mediaKey){currentMedia=mediaKey;Motion.Fade(mediaTitle,.4,1,200);Motion.Fade(artist,.4,1,200);}mediaTitle.Text=media.Title;artist.Text=media.Artist;if(artworkBrush.ImageSource!=media.Artwork){artworkBrush.ImageSource=media.Artwork;Motion.Fade(artwork,.15,1,220);}play.IsEnabled=media.Available&&media.CanPlay;next.IsEnabled=media.CanNext;previous.IsEnabled=media.CanPrevious;
            playGlyph.Kind=media.Playing?"pause":"play";playGlyph.InvalidateVisual();
        }
    }
}
