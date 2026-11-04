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
            return rows;
        }
    }
}
