using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal sealed class WelcomeWindow : GlassWindow
    {
        readonly ShellController shell;
        readonly ToggleSwitch desktop,startup;
        readonly TextBlock status=Paint.TextBlock("변경한 설정은 바로 적용되고 다음 실행에도 유지됩니다",11,Paint.Muted);
        internal readonly BatteryPill PreviewBattery=new BatteryPill {Width=56,Height=24,ExpandedWidth=90};
        internal WelcomeWindow(ShellController controller)
        {
            shell=controller;Title=Program.AppName;ShowInTaskbar=false;Topmost=false;Width=700;Height=650;WindowStartupLocation=WindowStartupLocation.CenterScreen;Dark=false;Surface.Background=LightPanel.Glass;Surface.BorderBrush=Brushes.Transparent;Surface.BorderThickness=new Thickness(0);
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
            var desktopRows=new StackPanel();desktop=Row(desktopRows,"Windows 작업표시줄 대체","기본 작업표시줄과 전환합니다",shell.Desktop,v=> {shell.SetDesktop(v);Show();Activate();});
            startup=Row(desktopRows,"로그인 시 자동 시작","Windows 로그인 후 One TaskBar을 실행합니다",StartupPreference.Enabled,v=> {try{StartupPreference.Set(v);status.Text=v?"로그인 자동 시작을 켰습니다":"로그인 자동 시작을 껐습니다";}catch(Exception e){status.Text="자동 시작 설정을 저장하지 못했습니다: "+e.Message;}});
            Group(groups,"데스크톱",desktopRows);
            var panels=new StackPanel();
            ChoiceRow(panels,"알림센터","시계와 알림 버튼에서 열 화면",Preferences.Current.NativeNotifications,v=>Preferences.Set(p=>p.NativeNotifications=v));
            var notificationLink=Paint.Action("Windows 알림 연결 · 권한 관리",()=>shell.ShowNotificationConnection());notificationLink.HorizontalContentAlignment=HorizontalAlignment.Left;notificationLink.Margin=new Thickness(12,5,12,12);panels.Children.Add(notificationLink);
            ChoiceRow(panels,"빠른 설정","Wi-Fi·소리·배터리 버튼에서 열 화면",Preferences.Current.NativeQuickSettings,v=>Preferences.Set(p=>p.NativeQuickSettings=v));
            Row(panels,"슬라이더 스크롤 조절","밝기·음량·재생 위치 위에서 휠이나 트랙패드 스크롤로 값을 바꿉니다. 기본은 꺼짐입니다",Preferences.Current.QuickSliderScroll,v=>Preferences.Set(p=>p.QuickSliderScroll=v));
            Group(groups,"패널 선택",panels);
            var launcher=new StackPanel();
            ChoiceRow(launcher,"독의 시작 버튼","버튼 모양과 눌렀을 때 열 화면",Preferences.Current.NativeStartButton,v=>Preferences.Set(p=>p.NativeStartButton=v),"앱 목록","Windows 시작");
            ChoiceRow(launcher,"키보드 시작 키","Windows 키만 눌렀을 때 열 화면",Preferences.Current.NativeStartKey,v=>Preferences.Set(p=>p.NativeStartKey=v),"앱 화면","Windows 시작");
            ChoiceRow(launcher,"검색 버튼","독과 앱 화면의 검색 버튼에서 열 화면",Preferences.Current.NativeSearch,v=>Preferences.Set(p=>p.NativeSearch=v),"런처 검색","Windows 검색");
            Group(groups,"시작과 검색",launcher);
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
            var scroller=new SmoothScrollViewer {Content=groups,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};scroller.Resources[SystemParameters.VerticalScrollBarWidthKey]=5.0;root.Children.Add(scroller);Surface.Child=root;LightPanel.Apply(root);
            MouseLeftButtonDown+=(s,e)=> {if(e.OriginalSource==header||e.OriginalSource==name)DragMove();};Closing+=(s,e)=> {if(!shell.IsDisposing&&!Application.Current.Dispatcher.HasShutdownStarted){e.Cancel=true;Hide();}};
        }
        static void Group(StackPanel target,string label,UIElement content)
        {
            var title=Paint.TextBlock(label,12,Paint.Muted);title.Margin=new Thickness(13,4,0,8);target.Children.Add(title);
            target.Children.Add(new Border {Background=Paint.Brush("#18FFFFFF"),CornerRadius=new CornerRadius(22),Padding=new Thickness(3),Child=content,Margin=new Thickness(0,0,0,17)});
        }
        static void ChoiceRow(StackPanel parent,string title,string description,bool native,Action<bool> changed,string customLabel="One TaskBar",string windowsLabel="Windows 기본")
        {
            var row=new Grid {Margin=new Thickness(15,11,15,11),MinHeight=44};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(218)});
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};labels.Children.Add(Paint.TextBlock(title,14,Paint.Text));var detail=Paint.TextBlock(description,11,Paint.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(0,3,0,0);labels.Children.Add(detail);row.Children.Add(labels);
            var options=new System.Windows.Controls.Primitives.UniformGrid {Columns=2};Button custom=null,windows=null;
            Action refresh=()=> {custom.Background=!native?Paint.Brush("#BFFFFFFF"):Brushes.Transparent;windows.Background=native?Paint.Brush("#BFFFFFFF"):Brushes.Transparent;};
            custom=Paint.Action(customLabel,()=> {if(!native)return;native=false;refresh();changed(false);});windows=Paint.Action(windowsLabel,()=> {if(native)return;native=true;refresh();changed(true);});
            foreach(var button in new[]{custom,windows}){button.Height=34;button.Padding=new Thickness(6,5,6,5);button.Margin=new Thickness(1);button.FontSize=12;options.Children.Add(button);}refresh();
            var frame=new Border {CornerRadius=new CornerRadius(20),Padding=new Thickness(2),Background=Paint.Brush("#18000000"),Child=options,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(frame,1);row.Children.Add(frame);parent.Children.Add(row);
        }
        internal void Feedback(string message) {status.Text=message;}
        static ToggleSwitch Row(StackPanel parent,string title,string description,bool value,Action<bool> changed)
        {
            var grid=new Grid {Margin=new Thickness(15,10,15,10),MinHeight=38};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(58)});
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,0)};labels.Children.Add(Paint.TextBlock(title,14,Paint.Text));var detail=Paint.TextBlock(description,11,Paint.Muted);detail.Margin=new Thickness(0,3,0,0);detail.TextWrapping=TextWrapping.Wrap;labels.Children.Add(detail);grid.Children.Add(labels);
            var toggle=new ToggleSwitch(title,value,changed) {HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(toggle,1);grid.Children.Add(toggle);parent.Children.Add(grid);return toggle;
        }
        internal void UpdateMode(){desktop.Refresh(shell.Desktop);startup.Refresh(StartupPreference.Enabled);}
    }
}
