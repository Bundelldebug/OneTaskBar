using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal sealed class WelcomeWindow : GlassWindow
    {
        readonly ShellController shell;
        bool releasing;
        FrameworkElement legacyBudsOption,independentBudsHint;
        internal void ReleaseIdle(){releasing=true;Close();}
        readonly ToggleSwitch desktop,startup,autoHide;
        readonly TextBlock status=Paint.TextBlock("변경한 설정은 바로 적용되고 다음 실행에도 유지됩니다",11,Paint.Muted);
        internal readonly BatteryPill PreviewBattery=new BatteryPill {Width=56,Height=24,ExpandedWidth=90};
        internal WelcomeWindow(ShellController controller)
        {
            shell=controller;Title=Program.AppName;ShowInTaskbar=false;Topmost=false;Width=760;Height=720;WindowStartupLocation=WindowStartupLocation.CenterScreen;Dark=false;OpaqueSurface=true;SetMaterial(AppSettingsPalette.Surface,AppSettingsPalette.Surface);Surface.BorderBrush=AppSettingsPalette.Stroke;Surface.BorderThickness=new Thickness(1);
            Icon=new BitmapImage(new Uri(Path.Combine(AppLog.Root,"assets","OneTaskBar.ico")));
            var root=new DockPanel {Margin=new Thickness(28,24,28,20)};
            var header=new Grid {Height=60,Margin=new Thickness(0,0,0,18)};
            var brand=new StackPanel {Orientation=Orientation.Horizontal};brand.Children.Add(new Image {Source=new BitmapImage(new Uri(Path.Combine(AppLog.Root,"assets","OneTaskBar.png"))),Width=48,Height=48,Margin=new Thickness(0,0,14,0)});
            var title=new StackPanel {VerticalAlignment=VerticalAlignment.Center};var name=Paint.TextBlock(Program.AppName,24,Paint.Text);name.FontWeight=FontWeights.SemiBold;title.Children.Add(name);title.Children.Add(Paint.TextBlock("화면과 동작",12,Paint.Muted));brand.Children.Add(title);header.Children.Add(brand);
            var close=Paint.Button(new Glyph("close",19),()=>Hide(),"설정 닫기");close.Width=close.Height=34;close.Padding=new Thickness(0);close.HorizontalAlignment=HorizontalAlignment.Right;close.VerticalAlignment=VerticalAlignment.Top;header.Children.Add(close);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var footer=new StackPanel {Margin=new Thickness(2,15,2,0)};status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,0,0,12);footer.Children.Add(status);
            var actions=new DockPanel();var quit=Paint.Action("종료 · Windows 작업표시줄 복원",()=>Application.Current.Shutdown());quit.Background=Paint.Brush("#30213328");DockPanel.SetDock(quit,Dock.Right);actions.Children.Add(quit);
            var quick=Paint.Action("빠른 설정",()=>shell.ToggleQuick(null));quick.HorizontalAlignment=HorizontalAlignment.Left;actions.Children.Add(quick);footer.Children.Add(actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var groups=new StackPanel();
            var uiRows=new StackPanel();var versions=new UniformGrid {Columns=2,Margin=new Thickness(12,8,12,8)};var legacy=Paint.Action("8.5",()=>{});var modern=Paint.Action("9.0",()=>{});
            Action showVersion=()=>{bool current=Preferences.Current.UIVersion>=90;legacy.Background=current?AppSettingsPalette.AccentLight:AppSettingsPalette.Accent;modern.Background=current?AppSettingsPalette.Accent:AppSettingsPalette.AccentLight;legacy.Foreground=current?AppSettingsPalette.Text:Paint.ControlWhite;modern.Foreground=current?Paint.ControlWhite:AppSettingsPalette.Text;((TextBlock)legacy.Content).Foreground=legacy.Foreground;((TextBlock)modern.Content).Foreground=modern.Foreground;if(legacyBudsOption!=null)legacyBudsOption.Visibility=current?Visibility.Collapsed:Visibility.Visible;if(independentBudsHint!=null)independentBudsHint.Visibility=current?Visibility.Visible:Visibility.Collapsed;};
            legacy.Click+=(s,e)=>{Preferences.Set(p=>p.UIVersion=85);showVersion();};modern.Click+=(s,e)=>{Preferences.Set(p=>p.UIVersion=90);showVersion();};legacy.Margin=new Thickness(0,0,5,0);modern.Margin=new Thickness(5,0,0,0);legacy.Height=modern.Height=44;System.Windows.Automation.AutomationProperties.SetName(legacy,"UI 버전 8.5");System.Windows.Automation.AutomationProperties.SetName(modern,"UI 버전 9.0");versions.Children.Add(legacy);versions.Children.Add(modern);uiRows.Children.Add(versions);var uiHint=Paint.TextBlock("8.5 배치와 9.0 개별 메뉴 배치는 따로 저장됩니다",12.5,AppSettingsPalette.Muted);uiHint.Margin=new Thickness(14,0,14,14);uiHint.TextWrapping=TextWrapping.Wrap;uiRows.Children.Add(uiHint);Group(groups,"UI 버전",uiRows);showVersion();
            var desktopRows=new StackPanel();desktop=Row(desktopRows,"Windows 작업표시줄 대체","기본 작업표시줄과 전환합니다",shell.Desktop,v=> {shell.SetDesktop(v);Show();Activate();});
            autoHide=Row(desktopRows,"작업표시줄 자동 숨기기","Windows 설정과 함께 적용됩니다. 화면 아래쪽에 커서를 대면 다시 표시합니다",shell.Demo?shell.DockAutoHide:Native.TaskbarAutoHide(),v=>shell.SetAutoHide(v));
            Row(desktopRows,"앱 드래그로 분할·팝업 열기","독의 앱을 화면 가장자리로 끌면 분할, 중앙으로 끌면 팝업으로 엽니다. 독 안에서는 순서를 바꿉니다",Preferences.Current.DockWindowDrop,v=>Preferences.Set(p=>p.DockWindowDrop=v));
            Row(desktopRows,"작업표시줄 단색 배경","하단 작업표시줄을 블러 없는 회색 단색으로 표시합니다",Preferences.Current.DockSolidBackground,v=>Preferences.Set(p=>p.DockSolidBackground=v));
            startup=Row(desktopRows,"로그인 시 자동 시작","Windows 로그인 후 One TaskBar을 실행합니다",StartupPreference.Enabled,v=> {try{StartupPreference.Set(v);status.Text=v?"로그인 자동 시작을 켰습니다":"로그인 자동 시작을 껐습니다";}catch(Exception e){status.Text="자동 시작 설정을 저장하지 못했습니다: "+e.Message;}});
            Group(groups,"데스크톱",desktopRows);
            var panels=new StackPanel();
            ChoiceRow(panels,"알림센터","시계와 알림 버튼에서 열 화면",Preferences.Current.NativeNotifications,v=>Preferences.Set(p=>p.NativeNotifications=v));
            Row(panels,"One Taskbar 알림 팝업","Windows 알림을 둥근 블러 카드로 표시합니다. 방해 금지와 앱별 배너 설정을 따릅니다",Preferences.Current.NotificationPopups,v=>Preferences.Set(p=>p.NotificationPopups=v));
            Row(panels,"Galaxy Buds 연결 팝업","버즈 연결 정보를 오른쪽 아래에 표시합니다. 등록된 기기의 상태 변경만 감지합니다",Preferences.Current.BudsConnectionPopups,v=>Preferences.Set(p=>p.BudsConnectionPopups=v));
            var notificationLink=Paint.Action("Windows 알림 연결 · 권한 관리",()=>shell.ShowNotificationConnection());notificationLink.HorizontalContentAlignment=HorizontalAlignment.Left;notificationLink.Margin=new Thickness(12,5,12,12);panels.Children.Add(notificationLink);
            ChoiceRow(panels,"빠른 설정","Wi-Fi·소리·배터리 버튼에서 열 화면",Preferences.Current.NativeQuickSettings,v=>Preferences.Set(p=>p.NativeQuickSettings=v));
            var legacyBuds=Row(panels,"8.5에서 주변 기기 연결 대신 Galaxy Buds 열기","Galaxy Buds 앱 설치가 필요합니다",Preferences.Current.QuickGalaxyBuds,v=>Preferences.Set(p=>p.QuickGalaxyBuds=v));legacyBudsOption=(FrameworkElement)legacyBuds.Parent;var budsHint=Paint.TextBlock("9.0에서는 주변 기기 연결과 Galaxy Buds를 각각 배치할 수 있습니다. 빠른 설정의 배치 편집 → 항목 추가에서 선택하세요.",12.5,AppSettingsPalette.Muted);budsHint.TextWrapping=TextWrapping.Wrap;budsHint.Margin=new Thickness(15,12,15,12);independentBudsHint=budsHint;panels.Children.Add(budsHint);showVersion();
            Row(panels,"슬라이더 스크롤 조절","밝기·음량·재생 위치 위에서 휠이나 트랙패드 스크롤로 값을 바꿉니다. 기본은 꺼짐입니다",Preferences.Current.QuickSliderScroll,v=>Preferences.Set(p=>p.QuickSliderScroll=v));
            Group(groups,"패널 선택",panels);
            var launcher=new StackPanel();
            ChoiceRow(launcher,"독의 시작 버튼","버튼 모양과 눌렀을 때 열 화면",Preferences.Current.NativeStartButton,v=>Preferences.Set(p=>p.NativeStartButton=v),"One Taskbar","Windows 시작");
            ChoiceRow(launcher,"키보드 시작 키","Windows 키만 눌렀을 때 열 화면",Preferences.Current.NativeStartKey,v=>Preferences.Set(p=>p.NativeStartKey=v),"One Taskbar","Windows 시작");
            ChoiceRow(launcher,"검색 버튼","독과 앱 화면의 검색 버튼에서 열 화면",Preferences.Current.NativeSearch,v=>Preferences.Set(p=>p.NativeSearch=v),"런처 검색","Windows 검색");
            Group(groups,"시작과 검색",launcher);
            var nowBar=new StackPanel();ChoiceRow(nowBar,"미디어 플레이어 펼침 스타일","큰 커버는 앨범 이미지를 위로 펼쳐 표시합니다",Preferences.Current.NowBarLargeCover,v=>Preferences.Set(p=>p.NowBarLargeCover=v),"기본","큰 커버");Row(nowBar,"YouTube 고화질 커버",(Preferences.Current.YouTubeArtworkTrack!=null?"현재 영상만 허용됨 · ":"")+"삼성 브라우저의 재생 제목과 일치하는 기록에서 영상 ID만 읽어 YouTube 이미지 서버에서 커버를 가져옵니다",YouTubeArtwork.Enabled,v=>Preferences.Set(p=>{p.YouTubeHighResArtwork=v;p.YouTubeArtworkTrack=null;}));Group(groups,"미디어 플레이어",nowBar);
            var safety=new StackPanel();Row(safety,"긴급 종료 단축키","Ctrl + Shift + Tab + 1 · One TaskBar 종료 및 Windows 작업표시줄 복원",Preferences.Current.EmergencyExit,v=>Preferences.Set(p=>p.EmergencyExit=v));Group(groups,"복구",safety);
            var hardware=new StackPanel();
            var powerRow=new Grid {Margin=new Thickness(15,11,15,11)};powerRow.ColumnDefinitions.Add(new ColumnDefinition());powerRow.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(105)});
            var powerLabels=new StackPanel();powerLabels.Children.Add(Paint.TextBlock("배터리 없는 PC 전력 표시",14,Paint.Text));var powerHelp=Paint.TextBlock("0: 숨김 · 1~9999 W: 직접 입력한 값 (실시간 측정 아님)",11,Paint.Muted);powerHelp.TextWrapping=TextWrapping.Wrap;powerHelp.Margin=new Thickness(0,4,12,0);powerLabels.Children.Add(powerHelp);powerRow.Children.Add(powerLabels);
            var watts=new TextBox {Text=PowerDisplay.Clamp(Preferences.Current.DesktopPowerWatts).ToString(),MaxLength=4,FontFamily=SystemFonts.MessageFontFamily,FontSize=15,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(10,7,10,7),BorderThickness=new Thickness(0),Background=Paint.Brush("#40FFFFFF"),Foreground=Paint.Ink};
            watts.PreviewTextInput+=(s,e)=>e.Handled=e.Text.Any(c=>c<'0'||c>'9');
            DataObject.AddPastingHandler(watts,(s,e)=> {string value=e.DataObject.GetData(typeof(string)) as string;int number;if(String.IsNullOrEmpty(value)||value.Length>4||!Int32.TryParse(value,out number)||number<0||number>9999)e.CancelCommand();});
            watts.TextChanged+=(s,e)=> {int number;if(Int32.TryParse(watts.Text,out number)&&number>=0&&number<=9999&&Preferences.Current.DesktopPowerWatts!=number)Preferences.Set(p=>p.DesktopPowerWatts=number);};watts.LostKeyboardFocus+=(s,e)=>watts.Text=PowerDisplay.Clamp(Preferences.Current.DesktopPowerWatts).ToString();Grid.SetColumn(watts,1);powerRow.Children.Add(watts);hardware.Children.Add(powerRow);Group(groups,"PC 호환성",hardware);
            var appearance=new StackPanel();Row(appearance,"동적 블러","배경의 창과 화면 색을 캡슐에 반영합니다",Preferences.Current.Blur,v=>Preferences.Set(p=>p.Blur=v));
            Row(appearance,"호버 확대","마우스를 올린 앱 아이콘과 시작 버튼을 확대합니다",Preferences.Current.Hover,v=>Preferences.Set(p=>p.Hover=v));
            Row(appearance,"실행 중인 앱 표시","아이콘 아래에 실행 상태를 표시합니다",Preferences.Current.RunningDots,v=>Preferences.Set(p=>p.RunningDots=v));
            Row(appearance,"시계 초 표시","상태 캡슐 시계에 초를 표시합니다",Preferences.Current.Seconds,v=>Preferences.Set(p=>p.Seconds=v));Group(groups,"화면",appearance);
            var motion=new StackPanel();Row(motion,"부드러운 전환","패널 열기와 닫기 애니메이션을 사용합니다",Preferences.Current.Animations,v=>Preferences.Set(p=>p.Animations=v));
            Row(motion,"충전 애니메이션","연결할 때 배터리 아이콘이 커졌다가 돌아옵니다",Preferences.Current.ChargeAnimation,v=>Preferences.Set(p=>p.ChargeAnimation=v));
            var preview=new Grid {Margin=new Thickness(15,5,15,12),Height=44};PreviewBattery.State=controller.Power;PreviewBattery.VerticalAlignment=VerticalAlignment.Center;PreviewBattery.HorizontalAlignment=HorizontalAlignment.Left;PreviewBattery.Margin=new Thickness(34,0,0,0);preview.Children.Add(PreviewBattery);
            var tryCharge=Paint.Action("충전 효과 미리 보기",()=>shell.PreviewCharge());tryCharge.HorizontalAlignment=HorizontalAlignment.Right;preview.Children.Add(tryCharge);motion.Children.Add(preview);Group(groups,"움직임",motion);
            var sounds=new StackPanel();var soundToggle=Row(sounds,"충전 연결음","충전기를 새로 연결하면 선택한 소리 파일을 재생합니다",Preferences.Current.ChargeSoundEnabled,v=>Preferences.Set(p=>p.ChargeSoundEnabled=v));
            var soundName=Paint.TextBlock(String.IsNullOrEmpty(Preferences.Current.ChargeSoundFile)?"선택한 파일 없음":Path.GetFileName(Preferences.Current.ChargeSoundFile),12,Paint.Muted);soundName.Margin=new Thickness(15,3,15,7);soundName.TextTrimming=TextTrimming.CharacterEllipsis;sounds.Children.Add(soundName);
            var soundActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(12,0,12,12)};
            soundActions.Children.Add(Paint.Action("소리 파일 선택",()=> {var picker=new Microsoft.Win32.OpenFileDialog {Title="충전 연결음 선택",Filter="소리 파일|*.wav;*.mp3;*.wma;*.m4a;*.aac",CheckFileExists=true,Multiselect=false};if(picker.ShowDialog(this)!=true)return;Preferences.Set(p=> {p.ChargeSoundFile=picker.FileName;p.ChargeSoundEnabled=true;});soundName.Text=Path.GetFileName(picker.FileName);soundToggle.Refresh(true);status.Text="충전 연결음을 저장했습니다";}));
            soundActions.Children.Add(Paint.Action("미리 듣기",()=>shell.ConnectionSound.Play(Preferences.Current.ChargeSoundFile,message=>status.Text=message)));
            soundActions.Children.Add(Paint.Action("파일 지우기",()=> {Preferences.Set(p=> {p.ChargeSoundFile=null;p.ChargeSoundEnabled=false;});soundName.Text="선택한 파일 없음";soundToggle.Refresh(false);}));sounds.Children.Add(soundActions);Group(groups,"충전 소리",sounds);
            var scroller=new SmoothScrollViewer {Content=groups,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,6,0)};scroller.Resources[SystemParameters.VerticalScrollBarWidthKey]=6.0;root.Children.Add(scroller);Surface.Child=root;AppSettingsPalette.Apply(root);
            MouseLeftButtonDown+=(s,e)=> {if(e.OriginalSource==header||e.OriginalSource==name)DragMove();};Closing+=(s,e)=> {if(!releasing&&!shell.IsDisposing&&!Application.Current.Dispatcher.HasShutdownStarted){e.Cancel=true;Hide();}};
        }
        static void Group(StackPanel target,string label,UIElement content)
        {
            var title=Paint.TextBlock(label,16,AppSettingsPalette.Text);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(4,6,0,10);target.Children.Add(title);
            target.Children.Add(new Border {Background=Brushes.White,BorderBrush=AppSettingsPalette.Stroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Padding=new Thickness(3),Child=content,Margin=new Thickness(0,0,0,21)});
        }
        static void ChoiceRow(StackPanel parent,string title,string description,bool native,Action<bool> changed,string customLabel="One TaskBar",string windowsLabel="Windows 기본")
        {
            var row=new Grid {Margin=new Thickness(15,13,15,13),MinHeight=54};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(218)});
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,16,0)};var heading=Paint.TextBlock(title,15,AppSettingsPalette.Text);heading.FontWeight=FontWeights.SemiBold;heading.TextWrapping=TextWrapping.Wrap;labels.Children.Add(heading);var detail=Paint.TextBlock(description,12.5,AppSettingsPalette.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.LineHeight=18;detail.Margin=new Thickness(0,5,0,0);labels.Children.Add(detail);row.Children.Add(labels);
            var options=new System.Windows.Controls.Primitives.UniformGrid {Columns=2};Button custom=null,windows=null;
            Action refresh=()=> {custom.Background=!native?AppSettingsPalette.AccentLight:Brushes.Transparent;windows.Background=native?AppSettingsPalette.AccentLight:Brushes.Transparent;};
            custom=Paint.Action(customLabel,()=> {if(!native)return;native=false;refresh();changed(false);});windows=Paint.Action(windowsLabel,()=> {if(native)return;native=true;refresh();changed(true);});
            foreach(var button in new[]{custom,windows}){button.Height=40;button.Padding=new Thickness(6,5,6,5);button.Margin=new Thickness(1);((TextBlock)button.Content).FontSize=13;options.Children.Add(button);}refresh();
            var frame=new Border {CornerRadius=new CornerRadius(22),Padding=new Thickness(2),Background=Paint.Brush("#FFF0F3F7"),Child=options,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(frame,1);row.Children.Add(frame);parent.Children.Add(row);
        }
        internal void Feedback(string message) {status.Text=message;}
        static ToggleSwitch Row(StackPanel parent,string title,string description,bool value,Action<bool> changed)
        {
            var grid=new Grid {Margin=new Thickness(15,13,15,13),MinHeight=52};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(58)});
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,18,0)};var heading=Paint.TextBlock(title,15,AppSettingsPalette.Text);heading.FontWeight=FontWeights.SemiBold;heading.TextWrapping=TextWrapping.Wrap;labels.Children.Add(heading);var detail=Paint.TextBlock(description,12.5,AppSettingsPalette.Muted);detail.Margin=new Thickness(0,5,0,0);detail.LineHeight=18;detail.TextWrapping=TextWrapping.Wrap;labels.Children.Add(detail);grid.Children.Add(labels);
            var toggle=new ToggleSwitch(title,value,changed) {HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(toggle,1);grid.Children.Add(toggle);parent.Children.Add(grid);return toggle;
        }
        internal void UpdateAutoHide(bool value){autoHide.Refresh(value);}
        internal void UpdateMode(){desktop.Refresh(shell.Desktop);startup.Refresh(StartupPreference.Enabled);UpdateAutoHide(shell.Demo?shell.DockAutoHide:Native.TaskbarAutoHide());}
    }
    internal static class AppSettingsPalette
    {
        internal static readonly Brush Surface=Paint.Brush("#FFF3F5F8"),Stroke=Paint.Brush("#FFD9E0E8"),Text=Paint.Brush("#FF202B3B"),Muted=Paint.Brush("#FF536174"),Accent=Paint.Brush("#FF245BC2"),AccentLight=Paint.Brush("#FFE8EFFB");
        internal static void Apply(DependencyObject root){var text=root as TextBlock;if(text!=null){if(text.Foreground==Paint.Text||text.Foreground==Paint.Ink)text.Foreground=Text;else if(text.Foreground==Paint.Muted||text.Foreground==LightPanel.Muted)text.Foreground=Muted;if(text.FontSize<12.5)text.FontSize=12.5;}var control=root as Control;if(control!=null&&(control.Foreground==Paint.Text||control.Foreground==Paint.Ink))control.Foreground=Text;var glyph=root as Glyph;if(glyph!=null&&(glyph.Color==Paint.Text||glyph.Color==Paint.Ink))glyph.Color=Text;foreach(object child in LogicalTreeHelper.GetChildren(root)){var node=child as DependencyObject;if(node!=null)Apply(node);}}
    }
}
