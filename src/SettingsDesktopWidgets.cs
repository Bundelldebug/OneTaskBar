using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    internal sealed partial class WelcomeWindow
    {
        StackPanel DesktopWidgetSettings(ShellController shell)
        {
            var rows=new StackPanel();
            Row(rows,"바탕화면 위젯 표시","시계, 배터리, 미디어와 빠른 설정 기능을 바탕화면에 배치합니다",Preferences.Current.DesktopWidgets,Preferences.SetDesktopWidgets);
            var add=Paint.Action("위젯 추가 및 배치 편집",shell.Widgets.OpenGallery);
            add.HorizontalAlignment=HorizontalAlignment.Left;
            add.Margin=new Thickness(0,12,0,8);
            rows.Children.Add(add);
            var opacity=MaterialSlider(rows,"위젯 불투명도",Preferences.Current.WidgetOpacity,100,"%",value=>Preferences.SetWidgetMaterial(p=>p.WidgetOpacity=value));
            opacity.Minimum=10;
            opacity.TickFrequency=5;
            var blur=MaterialSlider(rows,"위젯 블러",Preferences.Current.WidgetBlur,40,"",value=>Preferences.SetWidgetMaterial(p=>p.WidgetBlur=value));
            blur.TickFrequency=2;
            return rows;
        }
    }
}
