using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.UI.Notifications.Management;
namespace OneTaskBar
{
    internal sealed class NotificationConnectWindow : GlassWindow
    {
        readonly ShellController shell;readonly TextBlock state=Paint.TextBlock("연결 상태 확인 중…",16,Paint.Text),detail=Paint.TextBlock("",12,Paint.Muted);readonly Button connect,privacy,disconnect;readonly ProgressBar progress=new ProgressBar {Height=3,IsIndeterminate=true,Foreground=Paint.ControlWhite,Background=Paint.Brush("#20FFFFFF"),Visibility=Visibility.Collapsed};
        readonly DispatcherTimer timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(500)};bool busy,initialized;IdentityResult identity;string lastError;
        internal NotificationConnectWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · Windows 알림 연결";Width=490;Height=414;Radius=28;Surface.CornerRadius=new CornerRadius(28);Surface.Background=QuickPanel.Glass;Surface.BorderThickness=new Thickness(.7);Dark=true;
            var root=new DockPanel {Margin=new Thickness(26)};var header=new StackPanel {Margin=new Thickness(0,0,0,20)};var title=Paint.TextBlock("Windows 알림 연결",23,Paint.Text);title.FontWeight=FontWeights.SemiBold;header.Children.Add(title);var intro=Paint.TextBlock("이 PC의 알림을 One TaskBar에서 확인하고 지웁니다.\n알림 내용은 이 PC에서만 처리됩니다.",12,Paint.Muted);intro.Margin=new Thickness(0,10,0,0);intro.TextWrapping=TextWrapping.Wrap;header.Children.Add(intro);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var footer=new StackPanel {Margin=new Thickness(0,17,0,0)};connect=Paint.Action("연결 시작",()=>Begin());connect.HorizontalContentAlignment=HorizontalAlignment.Center;connect.Height=42;connect.Background=Paint.ControlWhite;connect.Foreground=Paint.OnLight;var connectText=connect.Content as TextBlock;if(connectText!=null)connectText.Foreground=Paint.OnLight;footer.Children.Add(connect);
            var links=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,9,0,0)};privacy=Paint.Action("Windows 권한 설정",()=>OpenPrivacy());privacy.FontSize=11;privacy.Background=Brushes.Transparent;links.Children.Add(privacy);disconnect=Paint.Action("연동 끄기",()=>Disconnect());disconnect.FontSize=11;disconnect.Background=Brushes.Transparent;links.Children.Add(disconnect);var close=Paint.Action("닫기",()=>Hide());close.FontSize=11;close.Background=Brushes.Transparent;links.Children.Add(close);footer.Children.Add(links);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var status=new StackPanel();state.FontWeight=FontWeights.SemiBold;state.TextWrapping=TextWrapping.Wrap;status.Children.Add(state);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(0,10,0,16);status.Children.Add(detail);status.Children.Add(progress);root.Children.Add(new Border {Background=QuickPanel.Card,CornerRadius=new CornerRadius(22),Padding=new Thickness(20),Child=status});Surface.Child=root;StableSurface=true;TransitionContent=root;
            timer.Tick+=(s,e)=>{if(!busy)RefreshState();};IsVisibleChanged+=(s,e)=>{if(IsVisible){timer.Start();if(!initialized)Initialize();else RefreshState();}else timer.Stop();};Closed+=(s,e)=>timer.Stop();
        }
        async void Initialize(){initialized=true;identity=await Task.Run(()=>NotificationIdentity.Find());RefreshState();}
        void Caption(string title,string text){state.Text=title;detail.Text=text;}
        void ButtonText(string text){var label=connect.Content as TextBlock;if(label!=null){label.Text=text;label.Foreground=Paint.OnLight;}}
        internal void RefreshState()
        {
            var service=shell.Notifications;service.Check();connect.IsEnabled=identity!=null&&!service.RequestPending;disconnect.Visibility=service.Allowed?Visibility.Visible:Visibility.Collapsed;
            if(lastError!=null){Caption("알림을 연결하지 못했습니다",lastError);connect.IsEnabled=true;ButtonText("다시 연결");return;}
            if(service.Allowed){Caption("Windows 알림이 연결되었습니다",service.Items.Count+"개의 알림이 연결되어 있습니다. 알림을 누르면 보낸 앱으로 이동합니다.");ButtonText("알림 열기");return;}
            if(service.Packaged){Caption(service.RequestPending?"Windows 확인창을 기다리고 있습니다":service.Permission==UserNotificationListenerAccessStatus.Denied?"Windows에서 접근을 허용해 주세요":"알림 접근 권한이 필요합니다",service.RequestPending?"확인창이 보이지 않으면 아래 Windows 권한 설정에서 One TaskBar를 허용해 주세요.":service.Permission==UserNotificationListenerAccessStatus.Denied?"이전에 거부한 권한은 Windows 설정에서 직접 허용해야 합니다.":"아래 버튼을 누른 뒤 Windows 확인창에서 허용을 선택해 주세요.");ButtonText(service.Permission==UserNotificationListenerAccessStatus.Denied?"권한 설정 열기":"Windows 알림 접근 허용");return;}
            if(identity==null){Caption("연결 상태 확인 중…","Windows 앱 등록을 확인하고 있습니다.");return;}
            if(identity.Registered){Caption("앱 등록이 확인되었습니다","알림 권한을 연결하기 위해 One TaskBar를 한 번 다시 시작합니다.");ButtonText("연결 계속");return;}
            Caption("이 PC에 알림 연결 등록","이 빌드는 Windows 11용 로컬 테스트 등록을 사용합니다. 연결 중 관리자 승인창이 뜰 수 있으며 앱을 한 번 다시 시작합니다. 인증서 신뢰와 Windows 보안 설정은 변경하지 않습니다.");ButtonText("이 PC에 연결 (로컬 테스트)");
        }
        internal async void Begin()
        {
            if(busy)return;lastError=null;busy=true;connect.IsEnabled=false;progress.Visibility=Visibility.Visible;
            try {
                var service=shell.Notifications;service.Check();if(service.Allowed){Preferences.Set(p=>p.NativeNotifications=false);Hide();shell.ToggleNotifications(null);return;}
                Preferences.Set(p=>p.NotificationIntegration=true);
                if(!service.Packaged){Caption("알림 연결 등록 중…","Windows 앱 등록이 끝나면 연결을 계속합니다.");identity=await Task.Run(()=>NotificationIdentity.Find());if(!IsVisible)return;if(!identity.Registered){identity=await NotificationIdentity.Register(false);if(!IsVisible)return;if(identity.AdministratorRequired){Caption("Windows 관리자 승인을 기다립니다","현재 Windows 계정의 승인창에서 예를 눌러 주세요.");identity=await NotificationIdentity.Register(true);}}if(!identity.Registered)throw new InvalidOperationException(identity.Error??"앱 등록을 확인하지 못했습니다.");if(!IsVisible)return;Caption("연결을 계속하기 위해 다시 시작합니다","One TaskBar가 잠시 후 돌아옵니다.");NotificationIdentity.RestartWithIdentity(identity.AppId);return;}
                if(service.Permission==UserNotificationListenerAccessStatus.Denied){OpenPrivacy();return;}Caption("Windows 확인창에서 허용해 주세요","알림 접근 권한은 Windows가 직접 관리합니다.");Activate();await Task.Delay(150);if(!IsVisible)return;await service.Request();if(service.Allowed){Preferences.Set(p=>p.NativeNotifications=false);if(StartupPreference.Enabled)StartupPreference.Set(true);await service.Refresh();}
            }catch(Exception error){lastError=error.Message;Caption("알림을 연결하지 못했습니다",error.Message);AppLog.Write("notification-connect",error.ToString());}
            finally{busy=false;progress.Visibility=Visibility.Collapsed;if(initialized)RefreshState();else{connect.IsEnabled=true;ButtonText("다시 연결");}}
        }
        void Disconnect(){Preferences.Set(p=>p.NotificationIntegration=false);shell.Notifications.Items.Clear();shell.Notifications.Check();RefreshState();}
        static void OpenPrivacy(){Process.Start(new ProcessStartInfo("ms-settings:privacy-notifications"){UseShellExecute=true});}
    }
}
