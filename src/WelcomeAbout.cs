using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    internal sealed partial class WelcomeWindow
    {
        ReleaseInfo foundRelease;
        bool updateBusy;

        // Settings page with the installed version, project facts and the GitHub release updater.
        StackPanel AboutSection()
        {
            var about=new StackPanel();
            AboutLine(about,"현재 버전","One TaskBar "+SoftwareUpdate.CurrentText+(Program.Inspectable?" (개발 빌드)":""));
            AboutLine(about,"개발","OneTaskBar contributors · MIT 라이선스");
            AboutLine(about,"저장소","github.com/"+SoftwareUpdate.Repository);
            AboutLine(about,"설치 위치",AppLog.Root);
            var message=Paint.TextBlock("업데이트는 직접 확인할 때만 GitHub에 접속합니다. 자동으로 확인하거나 정보를 보내지 않습니다.",12,Paint.Muted);
            message.TextWrapping=TextWrapping.Wrap;
            message.Margin=new Thickness(15,10,15,4);
            about.Children.Add(message);
            var result=Paint.TextBlock("",13,AppSettingsPalette.Text);
            result.TextWrapping=TextWrapping.Wrap;
            result.Margin=new Thickness(15,4,15,6);
            about.Children.Add(result);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(12,2,12,12)};
            Button check=null,install=null;
            install=Paint.Action("업데이트 설치",()=>InstallUpdate(result,check,install));
            install.Visibility=Visibility.Collapsed;
            check=Paint.Action("업데이트 확인",()=>CheckUpdate(result,check,install));
            actions.Children.Add(check);
            actions.Children.Add(install);
            actions.Children.Add(Paint.Action("릴리스 페이지",()=>Native.Open("https://github.com/"+SoftwareUpdate.Repository+"/releases")));
            about.Children.Add(actions);
            return about;
        }

        static void AboutLine(StackPanel parent,string label,string value)
        {
            var row=new Grid {Margin=new Thickness(15,11,15,11)};
            row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(120)});
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(Paint.TextBlock(label,14,AppSettingsPalette.Muted));
            var text=Paint.TextBlock(value,14,AppSettingsPalette.Text);
            text.TextWrapping=TextWrapping.Wrap;
            Grid.SetColumn(text,1);
            row.Children.Add(text);
            parent.Children.Add(row);
        }

        async void CheckUpdate(TextBlock result,Button check,Button install)
        {
            if(updateBusy)return;
            updateBusy=true;
            check.IsEnabled=false;
            install.Visibility=Visibility.Collapsed;
            result.Text="GitHub 릴리스를 확인하는 중…";
            try
            {
                var release=await Task.Run(()=>SoftwareUpdate.Latest());
                if(release==null)result.Text="릴리스 정보를 읽지 못했습니다.";
                else if(SoftwareUpdate.IsNewer(release))
                {
                    foundRelease=release;
                    result.Text="새 버전 "+release.Version.Major+"."+release.Version.Minor+"."+release.Version.Build+"을 설치할 수 있습니다.";
                    install.Visibility=Visibility.Visible;
                }
                else result.Text="최신 버전을 사용 중입니다.";
            }
            catch(Exception error)
            {
                AppLog.Once("software-update-check",error.GetType().Name);
                result.Text="업데이트를 확인하지 못했습니다. 인터넷 연결을 확인해 주세요.";
            }
            finally
            {
                updateBusy=false;
                check.IsEnabled=true;
            }
        }

        async void InstallUpdate(TextBlock result,Button check,Button install)
        {
            var release=foundRelease;
            if(updateBusy||release==null)return;
            updateBusy=true;
            check.IsEnabled=false;
            install.IsEnabled=false;
            try
            {
                var path=await Task.Run(()=>SoftwareUpdate.Download(release,percent=>Dispatcher.BeginInvoke(new Action(()=>result.Text="내려받는 중… "+percent+"%"))));
                result.Text="설치를 시작합니다. 앱이 잠시 종료된 뒤 새 버전으로 다시 시작됩니다.";
                SoftwareUpdate.Install(path);
            }
            catch(Exception error)
            {
                AppLog.Once("software-update-install",error.GetType().Name);
                result.Text=error is InvalidOperationException?error.Message:"업데이트를 내려받지 못했습니다.";
                install.IsEnabled=true;
            }
            finally
            {
                updateBusy=false;
                check.IsEnabled=true;
            }
        }
    }
}
