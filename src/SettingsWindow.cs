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
    internal sealed partial class WelcomeWindow : GlassWindow
    {
        readonly ShellController shell;
        bool releasing;
        FrameworkElement legacyBudsOption,independentBudsHint;
        internal void ReleaseIdle(){releasing=true;Close();}
        readonly ToggleSwitch desktop,startup,autoHide;
        readonly TextBlock status=Paint.TextBlock("",11,Paint.Muted);
        internal readonly BatteryPill PreviewBattery=new BatteryPill {Width=56,Height=24,ExpandedWidth=90};
        internal WelcomeWindow(ShellController controller)
        {
            shell=controller;Title=Program.AppName;ShowInTaskbar=false;Topmost=false;Width=1000;Height=740;WindowStartupLocation=WindowStartupLocation.CenterScreen;Dark=false;OpaqueSurface=true;SetMaterial(AppSettingsPalette.Surface,AppSettingsPalette.Surface);Surface.BorderBrush=AppSettingsPalette.Stroke;Surface.BorderThickness=new Thickness(1);
            Icon=new BitmapImage(new Uri(Path.Combine(AppLog.Root,"assets","OneTaskBar.ico")));
            PreviewMouseLeftButtonDown+=DragSettingsHeader;
            var root=new DockPanel {Margin=new Thickness(28,24,28,20)};
            var header=SettingsHeader();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(5,0,5,10);
            status.SetBinding(VisibilityProperty,new System.Windows.Data.Binding("Text"){Source=status,Converter=new EmptySettingsText()});
            DockPanel.SetDock(status,Dock.Top);root.Children.Add(status);
            var groups=new StackPanel();
            Group(groups,"UI 버전",VersionSetting());
            var desktopRows=new StackPanel();SubHeading(desktopRows,"데스크톱 스타일");DesktopStyleSetting(desktopRows);SubHeading(desktopRows,"작업표시줄 동작");desktop=Row(desktopRows,"Windows 작업표시줄 대체","기본 작업표시줄과 전환합니다",shell.Desktop,v=> {shell.SetDesktop(v,true);Show();Activate();});
            autoHide=Row(desktopRows,"작업표시줄 자동 숨기기","선택한 화면 가장자리에 커서를 대면 다시 표시합니다",shell.Demo?shell.DockAutoHide:Native.TaskbarAutoHide(),v=>shell.SetAutoHide(v));
            Row(desktopRows,"최대화 창을 작업표시줄 뒤까지 확장","최대화한 앱이 화면 가장자리까지 채우고 작업표시줄이 그 위에 표시됩니다",Preferences.Current.MaximizedBehindDock,v=>Preferences.Set(p=>p.MaximizedBehindDock=v));
            Row(desktopRows,"앱 드래그로 분할·팝업 열기","독의 앱을 화면 가장자리로 끌면 분할, 중앙으로 끌면 팝업으로 엽니다. 독 안에서는 순서를 바꿉니다",Preferences.Current.DockWindowDrop,v=>Preferences.Set(p=>p.DockWindowDrop=v));
            Row(desktopRows,"작업표시줄 단색 배경","하단 작업표시줄을 블러 없는 회색 단색으로 표시합니다",Preferences.Current.DockSolidBackground,v=>Preferences.Set(p=>{p.DockSolidBackground=v;p.DockBackgroundKind=v?1:0;}));
            startup=Row(desktopRows,"로그인 시 자동 시작","현재 사용자 로그인 작업으로 지연 없이 실행합니다",StartupPreference.Enabled,v=> {try{StartupPreference.Set(v);status.Text=v?"로그인 자동 시작을 켰습니다":"로그인 자동 시작을 껐습니다";}catch(Exception e){status.Text="자동 시작 설정을 저장하지 못했습니다: "+e.Message;if(startup!=null)startup.Refresh(StartupPreference.Enabled);}});
            SubHeading(desktopRows,"작업표시줄 모양");PersonalizeDock(desktopRows);Group(groups,"데스크톱",desktopRows);
            Group(groups,"바탕화면 위젯",DesktopWidgetSettings(shell));
            var panels=new StackPanel();
            ChoiceRow(panels,"알림센터","시계와 알림 버튼에서 열 화면",Preferences.Current.NativeNotifications,v=>Preferences.Set(p=>p.NativeNotifications=v));
            Row(panels,"One Taskbar 알림 팝업","Windows 알림을 둥근 블러 카드로 표시합니다. 방해 금지와 앱별 배너 설정을 따릅니다",Preferences.Current.NotificationPopups,v=>Preferences.Set(p=>p.NotificationPopups=v));
            NotificationSoundSetting(panels);
            Row(panels,"Galaxy Buds 연결 팝업","버즈 연결 정보를 오른쪽 아래에 표시합니다. 등록된 기기의 상태 변경만 감지합니다",Preferences.Current.BudsConnectionPopups,v=>Preferences.Set(p=>p.BudsConnectionPopups=v));
            var notificationLink=Paint.Action("Windows 알림 연결 · 권한 관리",()=>shell.ShowNotificationConnection());notificationLink.HorizontalContentAlignment=HorizontalAlignment.Left;notificationLink.Margin=new Thickness(12,5,12,12);panels.Children.Add(notificationLink);
            ChoiceRow(panels,"빠른 설정","Wi-Fi·소리·배터리 버튼에서 열 화면",Preferences.Current.NativeQuickSettings,v=>Preferences.Set(p=>p.NativeQuickSettings=v));
            ChoiceRow(panels,"데스크톱 전환","독 버튼과 Win+Tab에서 열 화면",Preferences.Current.NativeTaskView,v=>Preferences.Set(p=>p.NativeTaskView=v));
            ChoiceRow(panels,"숨겨진 트레이 아이콘","상태 영역의 화살표에서 열 화면",Preferences.Current.NativeTrayOverflow,v=>Preferences.Set(p=>p.NativeTrayOverflow=v));
            var legacyBuds=Row(panels,"8.5에서 주변 기기 연결 대신 Galaxy Buds 열기","Galaxy Buds 앱 설치가 필요합니다",Preferences.Current.QuickGalaxyBuds,v=>Preferences.Set(p=>p.QuickGalaxyBuds=v));legacyBudsOption=(FrameworkElement)legacyBuds.Parent;var budsHint=Paint.TextBlock("9.0에서는 주변 기기 연결과 Galaxy Buds를 각각 배치할 수 있습니다. 빠른 설정의 배치 편집 → 항목 추가에서 선택하세요.",12.5,AppSettingsPalette.Muted);budsHint.TextWrapping=TextWrapping.Wrap;budsHint.Margin=new Thickness(15,12,15,12);independentBudsHint=budsHint;panels.Children.Add(budsHint);RefreshVersionOptions();
            Row(panels,"슬라이더 스크롤 조절","밝기·음량·재생 위치 위에서 휠이나 트랙패드 스크롤로 값을 바꿉니다. 기본은 꺼짐입니다",Preferences.Current.QuickSliderScroll,v=>Preferences.Set(p=>p.QuickSliderScroll=v));
            Group(groups,"패널 선택",panels);
            var launcher=new StackPanel();
            ChoiceRow(launcher,"독의 시작 버튼","버튼 모양과 눌렀을 때 열 화면",Preferences.Current.NativeStartButton,v=>Preferences.Set(p=>p.NativeStartButton=v),"One Taskbar","Windows 시작");
            ChoiceRow(launcher,"키보드 시작 키","Windows 키만 눌렀을 때 열 화면",Preferences.Current.NativeStartKey,v=>Preferences.Set(p=>p.NativeStartKey=v),"One Taskbar","Windows 시작");
            ChoiceRow(launcher,"검색 버튼","독과 앱 화면의 검색 버튼에서 열 화면",Preferences.Current.NativeSearch,v=>Preferences.Set(p=>p.NativeSearch=v),"런처 검색","Windows 검색");
            ChoiceRow(launcher,"Alt+Tab 창 전환","작업표시줄 대체 모드에서 사용할 창 전환 화면",Preferences.Current.NativeAltTab,v=>Preferences.Set(p=>p.NativeAltTab=v),"One Taskbar","Windows 기본");
            Row(launcher,"Windows 시작 메뉴 폴더로 정리","앱 화면에서 Windows 시작 메뉴의 폴더별로 앱을 모읍니다",Preferences.Current.GroupAppsByStartMenu,v=>
            {
                Preferences.Set(p=>p.GroupAppsByStartMenu=v);
                shell.AppScreen.Reconcile(shell.Apps);
                shell.AppScreen.ApplyWindowsFolders(shell.Apps,v);
                if(shell.Drawer!=null)shell.Drawer.RefreshApps();
            });
            Row(launcher,"앱 자동 폴더 정리","앱 화면에서 브라우저·게임·미디어처럼 비슷한 앱을 큰 폴더로 자동으로 묶습니다",Preferences.Current.AutoFolderApps,v=>
            {
                Preferences.Set(p=>p.AutoFolderApps=v);
                shell.AppScreen.Reconcile(shell.Apps);
                shell.AppScreen.ApplyAutoFolders(shell.Apps,v);
                if(shell.Drawer!=null)shell.Drawer.RefreshApps();
            });
            MaterialSetting(launcher,false);
            Group(groups,"시작과 검색",launcher);
            var hardware=new StackPanel();
            var island=new StackPanel();
            var nowBar=new StackPanel();SubHeading(nowBar,"작업표시줄 미디어 캡슐");
            Row(nowBar,"작업표시줄 미디어 캡슐 표시","작업표시줄 옆에 재생 중인 미디어를 표시합니다",
                Preferences.Current.ShowNowBar,v=>Preferences.Set(p=>p.ShowNowBar=v));
            ChoiceRow(nowBar,"미디어 플레이어 펼침 스타일","큰 커버는 앨범 이미지를 위로 펼쳐 표시합니다",
                Preferences.Current.NowBarLargeCover,v=>Preferences.Set(p=>p.NowBarLargeCover=v),"기본","큰 커버");
            Row(nowBar,"YouTube 고화질 커버",(Preferences.Current.YouTubeArtworkTrack!=null?"현재 영상만 허용됨 · ":"")+
                "삼성 브라우저의 재생 제목과 일치하는 기록에서 영상 ID만 읽어 YouTube 이미지 서버에서 커버를 가져옵니다",
                YouTubeArtwork.Enabled,v=>Preferences.Set(p=>{p.YouTubeHighResArtwork=v;p.YouTubeArtworkTrack=null;}));
            if(ReleaseFeatures.GlassUI)
            {
                Row(island,"다이나믹 아일랜드","미디어·새 알림과 충전·음량·밝기를 표시합니다. 위쪽 작업표시줄에서는 화면 아래에 표시합니다",
                    Preferences.Current.DynamicIsland,v=>Preferences.Set(p=>p.DynamicIsland=v));
                Row(nowBar,"미디어 앱 전환 애니메이션","재생 앱을 최소화하거나 복원할 때 미디어 캡슐로 연결합니다",
                    Preferences.Current.MediaWindowAnimation,v=>Preferences.Set(p=>p.MediaWindowAnimation=v));
                if(Preferences.Current.UIVersion>=95)Row(hardware,"가벼운 효과","포인터를 따라 움직이는 유리 빛, 리퀴드 글래스, 미디어 전환의 배경 흐림을 끄고 파형·제목 갱신을 줄입니다. 사양이 낮은 PC는 자동으로 켜집니다",
                    Preferences.Current.LightEffects,v=>Preferences.Set(p=>p.LightEffects=v));
                Row(hardware,"60Hz 모드","120Hz 화면에서도 애니메이션을 60프레임으로 제한해 발열과 전력 사용을 줄입니다. 앱을 다시 시작하면 적용됩니다",
                    Preferences.Current.Limit60Hz,v=>Preferences.Set(p=>p.Limit60Hz=v));
                Row(island,"상단 상태 표시줄","안드로이드처럼 화면 위쪽에 시계·알림(왼쪽), 재생 중인 미디어, 알림 앱 아이콘, 빠른 설정(오른쪽)을 배치합니다. 다이나믹 아일랜드와 같은 두께의 위쪽 공간을 씁니다",
                    Preferences.Current.TopStatusBar,v=>Preferences.Set(p=>p.TopStatusBar=v));
                Row(island,"전체화면 검은 띠","브라우저·동영상 플레이어가 전체화면일 때 위쪽 안전 영역을 검게 채우고, 포인터를 올리면 상태 표시줄을 보여 줍니다. 게임에는 적용하지 않습니다",
                    Preferences.Current.FullscreenBand,v=>Preferences.Set(p=>p.FullscreenBand=v));
                ChoiceRow(nowBar,"미디어 앱 전환 애니메이션 목적지","다이나믹 아일랜드가 켜져 있을 때 창이 들어가고 나오는 자리를 고릅니다. 꺼져 있으면 나우바로 갑니다",
                    Preferences.Current.MediaFlightToIsland,v=>Preferences.Set(p=>p.MediaFlightToIsland=v),"나우바","다이나믹 아일랜드");
                Row(nowBar,"측면 미디어 세로 캡슐","왼쪽·오른쪽 작업표시줄에서 커버와 재생 표시를 세로로 배치합니다",
                    Preferences.Current.NowBarSideCapsule,v=>Preferences.Set(p=>p.NowBarSideCapsule=v));
                SubHeading(nowBar,"전체화면 미디어");
                var fullscreen=Paint.Action("전체화면 미디어 열기",shell.OpenFullscreenMedia);
                fullscreen.Margin=new Thickness(15,8,15,12);
                nowBar.Children.Add(fullscreen);
                var clockSize=MaterialSlider(nowBar,"전체화면 미디어 시계 크기",Preferences.Current.FullscreenClockPercent,200,"%",value=>
                    Preferences.SetMediaAppearance(p=>p.FullscreenClockPercent=value));
                clockSize.Minimum=50;
                clockSize.TickFrequency=5;
                FullscreenTileSettings(nowBar);
            }
            Group(groups,"미디어 플레이어",nowBar);
            var safety=new StackPanel();Row(safety,"긴급 종료 단축키","Ctrl + Shift + Tab + 1 · One TaskBar 종료 및 Windows 작업표시줄 복원",Preferences.Current.EmergencyExit,v=>Preferences.Set(p=>p.EmergencyExit=v));Group(groups,"복구",safety);Group(groups,"소프트웨어 정보",AboutSection());
            ToggleSwitch adminToggle=null;
            adminToggle=Row(hardware,"관리자 권한 창 지원","관리자 권한으로 실행한 게임에서도 Alt+Tab 창 전환이 되고, 최소화된 관리자 권한 창을 독에서 눌러 다시 켤 수 있습니다. 켜거나 끌 때 Windows 확인이 한 번 나오고 앱이 다시 시작됩니다. One TaskBar 본체는 일반 권한으로 실행되므로 알림 연결도 그대로 쓸 수 있습니다",
                AdminMode.Enabled,async v=>{status.Text="Windows 확인창에서 허용해 주세요…";string message=await AdminMode.SetAsync(v);status.Text=message??"";if(message==null||message.IndexOf("다시 시작")<0)adminToggle.Refresh(AdminBridge.Registered||LogonStartup.IsAdministrator);});
            var powerRow=new Grid {Margin=new Thickness(15,11,15,11)};powerRow.ColumnDefinitions.Add(new ColumnDefinition());powerRow.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(105)});
            var powerLabels=new StackPanel();powerLabels.Children.Add(Paint.TextBlock("배터리 없는 PC 전력 표시",14,Paint.Text));var powerHelp=Paint.TextBlock("0: 숨김 · 1~9999 W: 직접 입력한 값 (실시간 측정 아님)",11,Paint.Muted);powerHelp.TextWrapping=TextWrapping.Wrap;powerHelp.Margin=new Thickness(0,4,12,0);powerLabels.Children.Add(powerHelp);powerRow.Children.Add(powerLabels);
            var watts=new TextBox {Text=PowerDisplay.Clamp(Preferences.Current.DesktopPowerWatts).ToString(),MaxLength=4,FontFamily=SystemFonts.MessageFontFamily,FontSize=15,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(10,7,10,7),BorderThickness=new Thickness(1),Background=AppSettingsPalette.Card,Foreground=AppSettingsPalette.Text,BorderBrush=AppSettingsPalette.Stroke};
            watts.PreviewTextInput+=(s,e)=>e.Handled=e.Text.Any(c=>c<'0'||c>'9');
            DataObject.AddPastingHandler(watts,(s,e)=> {string value=e.DataObject.GetData(typeof(string)) as string;int number;if(String.IsNullOrEmpty(value)||value.Length>4||!Int32.TryParse(value,out number)||number<0||number>9999)e.CancelCommand();});
            watts.TextChanged+=(s,e)=> {int number;if(Int32.TryParse(watts.Text,out number)&&number>=0&&number<=9999&&Preferences.Current.DesktopPowerWatts!=number)Preferences.Set(p=>p.DesktopPowerWatts=number);};watts.LostKeyboardFocus+=(s,e)=>watts.Text=PowerDisplay.Clamp(Preferences.Current.DesktopPowerWatts).ToString();Grid.SetColumn(watts,1);powerRow.Children.Add(watts);hardware.Children.Add(powerRow);Group(groups,"PC 호환성",hardware);
            var appearance=new StackPanel();ThemeSetting(appearance);Row(appearance,"동적 블러","배경의 창과 화면 색을 캡슐에 반영합니다",Preferences.Current.Blur,v=>Preferences.Set(p=>p.Blur=v));if(Preferences.Current.UIVersion>=95)Row(appearance,"리퀴드 글래스","실시간 블러에 채도를 더하고 틴트를 얇게 해 배경이 더 투명하게 비치는 재질입니다. UI 9.5와 동적 블러가 켜져 있어야 하며 기본은 꺼짐입니다",Preferences.Current.LiquidGlass,v=>Preferences.Set(p=>p.LiquidGlass=v));
            AppIconSetting(appearance);
            Row(appearance,"듀오 스타일 상태 아이콘","배터리 원 게이지 · Wi-Fi · 음량 네 점을 하나로 표시합니다",Preferences.Current.DuoStatusIcon,v=>Preferences.Set(p=>p.DuoStatusIcon=v));
            StatusItemsSetting(appearance);
            Row(appearance,"밝기·음량 수치 표시","조절바 끝에 현재 값을 표시합니다",Preferences.Current.SliderValueNumbers,v=>Preferences.Set(p=>p.SliderValueNumbers=v));
            Row(appearance,"호버 확대","마우스를 올린 앱 아이콘과 시작 버튼을 확대합니다",Preferences.Current.Hover,v=>Preferences.Set(p=>p.Hover=v));
            Row(appearance,"실행 중인 앱 표시","아이콘 아래에 실행 상태를 표시합니다",Preferences.Current.RunningDots,v=>Preferences.Set(p=>p.RunningDots=v));
            Row(appearance,"시계 초 표시","상태 캡슐 시계에 초를 표시합니다",Preferences.Current.Seconds,v=>Preferences.Set(p=>p.Seconds=v));SubHeading(appearance,"배경");WallpaperPresetSetting(appearance);Group(groups,"화면",appearance);
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
            if(island.Children.Count>0)Group(groups,"다이나믹 아일랜드와 상단바",island);
            if(Fluid.Available&&Preferences.Current.UIVersion>=Fluid.Version)Group(groups,"플루이드 UI",FluidSection());
            // Ordered by what people reach for: the bar itself, how it looks and moves, panels, launcher, media, then upkeep.
            var order=new[]{"데스크톱","플루이드 UI","다이나믹 아일랜드와 상단바","화면","움직임","패널 선택","시작과 검색","바탕화면 위젯","미디어 플레이어","충전 소리","PC 호환성","복구","소프트웨어 정보"};
            sections.Sort((a,b)=>Array.IndexOf(order,a.Name)-Array.IndexOf(order,b.Name));
            SettingsPages(root,groups);Surface.Child=root;AppSettingsPalette.Apply(root);
            Closing+=(s,e)=> {if(!releasing&&!shell.IsDisposing&&!Application.Current.Dispatcher.HasShutdownStarted){e.Cancel=true;Hide();}};
        }
        static void ChoiceRow(StackPanel parent,string title,string description,bool native,Action<bool> changed,string customLabel="One TaskBar",string windowsLabel="Windows 기본")
        {
            var row=new Grid {Margin=new Thickness(15,13,15,13),MinHeight=54};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(218)});
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,16,0)};var heading=Paint.TextBlock(title,15,AppSettingsPalette.Text);heading.FontWeight=FontWeights.SemiBold;heading.TextWrapping=TextWrapping.Wrap;labels.Children.Add(heading);var detail=Paint.TextBlock(description,13,AppSettingsPalette.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.LineHeight=19;detail.Margin=new Thickness(0,5,0,0);labels.Children.Add(detail);row.Children.Add(labels);
            var options=new System.Windows.Controls.Primitives.UniformGrid {Columns=2};Button custom=null,windows=null;
            Action refresh=()=> {custom.Background=!native?AppSettingsPalette.Accent:Brushes.Transparent;windows.Background=native?AppSettingsPalette.Accent:Brushes.Transparent;custom.Foreground=!native?Paint.ControlWhite:AppSettingsPalette.Text;windows.Foreground=native?Paint.ControlWhite:AppSettingsPalette.Text;((TextBlock)custom.Content).Foreground=custom.Foreground;((TextBlock)windows.Content).Foreground=windows.Foreground;};
            custom=Paint.Action(customLabel,()=> {if(!native)return;native=false;refresh();changed(false);});windows=Paint.Action(windowsLabel,()=> {if(native)return;native=true;refresh();changed(true);});
            foreach(var button in new[]{custom,windows}){button.Height=40;button.Padding=new Thickness(6,5,6,5);button.Margin=new Thickness(1);((TextBlock)button.Content).FontSize=13;options.Children.Add(button);}refresh();
            var frame=new Border {CornerRadius=new CornerRadius(22),Padding=new Thickness(2),Background=AppSettingsPalette.Card,Child=options,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(frame,1);row.Children.Add(frame);parent.Children.Add(row);
        }
        internal void Feedback(string message) {status.Text=message;}
        // A small label that starts a group of rows inside a long page.
        internal static void SubHeading(StackPanel parent,string text)
        {
            var label=Paint.TextBlock(text,13,AppSettingsPalette.Accent);
            label.FontWeight=FontWeights.SemiBold;
            label.Tag="sub";
            label.Margin=new Thickness(17,parent.Children.Count==0?14:18,17,2);
            parent.Children.Add(label);
        }
        static ToggleSwitch Row(StackPanel parent,string title,string description,bool value,Action<bool> changed)
        {
            var grid=new Grid {Margin=new Thickness(15,13,15,13),MinHeight=52};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(58)});
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,18,0)};var heading=Paint.TextBlock(title,15,AppSettingsPalette.Text);heading.FontWeight=FontWeights.SemiBold;heading.TextWrapping=TextWrapping.Wrap;labels.Children.Add(heading);var detail=Paint.TextBlock(description,13,AppSettingsPalette.Muted);detail.Margin=new Thickness(0,5,0,0);detail.LineHeight=19;detail.TextWrapping=TextWrapping.Wrap;labels.Children.Add(detail);grid.Children.Add(labels);
            var toggle=new ToggleSwitch(title,value,changed) {HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(toggle,1);grid.Children.Add(toggle);parent.Children.Add(grid);return toggle;
        }
        internal void UpdateAutoHide(bool value){autoHide.Refresh(value);}
        internal void UpdateMode(){desktop.Refresh(shell.Desktop);startup.Refresh(StartupPreference.Enabled);UpdateAutoHide(shell.Demo?shell.DockAutoHide:Native.TaskbarAutoHide());}
    }
    internal static class AppSettingsPalette
    {
        internal static bool Dark {get{return Glass95.Dark;}}
        internal static Brush Surface {get{return Paint.Brush(Dark?"#FF101114":"#FFF3F3F6");}}
        internal static Brush Card {get{return Paint.Brush(Dark?"#FF1C1D22":"#FFFFFFFF");}}
        internal static Brush Stroke {get{return Paint.Brush(Dark?"#FF36373D":"#FFE2E2E6");}}
        internal static Brush Text {get{return Paint.Brush(Dark?"#FFF6F6F8":"#FF161619");}}
        internal static Brush Muted {get{return Paint.Brush(Dark?"#FFC4C4CD":"#FF5A5A64");}}
        internal static Brush Accent {get{return Paint.Brush("#FF3478FD");}}
        internal static Brush AccentLight {get{return Paint.Brush(Dark?"#FF30333D":"#FFE9EDF5");}}
        internal static void Apply(DependencyObject root){var text=root as TextBlock;if(text!=null){if(text.Foreground==Paint.Text||text.Foreground==Paint.Ink)text.Foreground=Text;else if(text.Foreground==Paint.Muted||text.Foreground==LightPanel.Muted)text.Foreground=Muted;if(text.FontSize<13)text.FontSize=13;}var control=root as Control;if(control!=null&&(control.Foreground==Paint.Text||control.Foreground==Paint.Ink))control.Foreground=Text;var glyph=root as Glyph;if(glyph!=null&&(glyph.Color==Paint.Text||glyph.Color==Paint.Ink))glyph.Color=Text;foreach(object child in LogicalTreeHelper.GetChildren(root)){var node=child as DependencyObject;if(node!=null)Apply(node);}}
    }
}
