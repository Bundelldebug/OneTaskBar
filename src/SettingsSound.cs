using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    internal sealed partial class WelcomeWindow
    {
        void NotificationSoundSetting(StackPanel parent)
        {
            var toggle=Row(parent,"사용자 알림음","새 알림 팝업에 선택한 소리를 재생합니다. Windows 알림음은 Windows 설정에서 끌 수 있습니다",Preferences.Current.NotificationSoundEnabled,v=>Preferences.Set(p=>p.NotificationSoundEnabled=v));
            var label=Paint.TextBlock(String.IsNullOrEmpty(Preferences.Current.NotificationSoundFile)?"선택한 파일 없음":Path.GetFileName(Preferences.Current.NotificationSoundFile),12,Paint.Muted);
            label.Margin=new Thickness(15,3,15,7);label.TextTrimming=TextTrimming.CharacterEllipsis;parent.Children.Add(label);
            var actions=new WrapPanel {Margin=new Thickness(12,0,12,12)};
            actions.Children.Add(Paint.Action("알림음 선택",()=>{
                var picker=new Microsoft.Win32.OpenFileDialog {Title="알림음 선택",Filter="소리 파일|*.wav;*.mp3;*.wma;*.m4a;*.aac",CheckFileExists=true,Multiselect=false};
                if(picker.ShowDialog(this)!=true)return;
                Preferences.Set(p=>{p.NotificationSoundFile=picker.FileName;p.NotificationSoundEnabled=true;});
                label.Text=Path.GetFileName(picker.FileName);toggle.Refresh(true);
            }));
            actions.Children.Add(Paint.Action("미리 듣기",()=>shell.NotificationSound.Play(Preferences.Current.NotificationSoundFile,message=>status.Text=message)));
            actions.Children.Add(Paint.Action("파일 지우기",()=>{
                Preferences.Set(p=>{p.NotificationSoundFile=null;p.NotificationSoundEnabled=false;});
                label.Text="선택한 파일 없음";toggle.Refresh(false);
            }));
            parent.Children.Add(actions);
        }
    }
}
