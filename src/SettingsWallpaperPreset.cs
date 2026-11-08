using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    internal sealed partial class WelcomeWindow
    {
        // Saves one desktop wallpaper and one lock screen picture as a pair and applies them together.
        void WallpaperPresetSetting(StackPanel rows)
        {
            var box=new StackPanel {Margin=new Thickness(15,14,15,14)};
            var title=Paint.TextBlock("배경 프리셋",15,AppSettingsPalette.Text);title.FontWeight=FontWeights.SemiBold;box.Children.Add(title);
            var detail=Paint.TextBlock("바탕화면과 잠금화면 사진을 한 쌍으로 저장해 두었다가 한 번에 적용합니다",13,AppSettingsPalette.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(0,4,0,8);box.Children.Add(detail);
            var desktopName=Paint.TextBlock("",13,AppSettingsPalette.Text);var lockName=Paint.TextBlock("",13,AppSettingsPalette.Text);
            desktopName.TextTrimming=lockName.TextTrimming=TextTrimming.CharacterEllipsis;desktopName.Margin=new Thickness(0,0,0,3);
            Action show=()=>
            {
                desktopName.Text="바탕화면 · "+(String.IsNullOrEmpty(Preferences.Current.PresetDesktopWallpaper)?"선택 안 함":Path.GetFileName(Preferences.Current.PresetDesktopWallpaper));
                lockName.Text="잠금화면 · "+(String.IsNullOrEmpty(Preferences.Current.PresetLockScreen)?"선택 안 함":Path.GetFileName(Preferences.Current.PresetLockScreen));
            };
            show();box.Children.Add(desktopName);box.Children.Add(lockName);
            Func<string,string> pick=heading=>
            {
                var dialog=new Microsoft.Win32.OpenFileDialog {Title=heading,Filter="이미지|*.png;*.jpg;*.jpeg;*.bmp",CheckFileExists=true,Multiselect=false};
                return dialog.ShowDialog(this)==true?dialog.FileName:null;
            };
            var buttons=new WrapPanel {Margin=new Thickness(-3,10,0,0)};
            Action<string,Action> add=(label,click)=>{var button=Paint.Action(label,click);button.Margin=new Thickness(3);buttons.Children.Add(button);};
            add("바탕화면 선택",()=>{string file=pick("바탕화면 사진");if(file==null)return;Preferences.Set(p=>p.PresetDesktopWallpaper=file);show();});
            add("현재 바탕화면 가져오기",()=>{string file=WallpaperPreset.CurrentDesktop();if(!WallpaperPreset.Usable(file)){Feedback("현재 바탕화면 파일을 찾지 못했습니다");return;}Preferences.Set(p=>p.PresetDesktopWallpaper=file);show();});
            add("잠금화면 선택",()=>{string file=pick("잠금화면 사진");if(file==null)return;Preferences.Set(p=>p.PresetLockScreen=file);show();});
            add("프리셋 적용",async()=>{Feedback(await WallpaperPreset.Apply());});
            add("프리셋 지우기",()=>{Preferences.Set(p=>{p.PresetDesktopWallpaper=null;p.PresetLockScreen=null;});show();});
            box.Children.Add(buttons);
            rows.Children.Add(box);
        }
    }
}
