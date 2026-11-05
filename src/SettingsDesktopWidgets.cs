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
            var add=Paint.Action("위젯 추가 및 배치 편집",()=>shell.Widgets.OpenGallery());
            add.HorizontalAlignment=HorizontalAlignment.Left;
            add.Margin=new Thickness(0,12,0,8);
            rows.Children.Add(add);
            var sliders=new StackPanel {Visibility=WidgetMaterial.Glass?Visibility.Collapsed:Visibility.Visible};
            Row(rows,"글래스 재질","UI 9.5에서 위젯에 패널과 같은 글래스 재질을 씁니다. 끄면 아래 불투명도와 블러를 직접 정합니다",Preferences.Current.WidgetGlass,v=>
            {
                Preferences.SetWidgetMaterial(p=>p.WidgetGlass=v);
                sliders.Visibility=WidgetMaterial.Glass?Visibility.Collapsed:Visibility.Visible;
            });
            Row(rows,"바탕화면 우클릭 메뉴에 위젯 추가","Windows 11에서는 우클릭 후 ‘더 많은 옵션 표시’ 안에 나타납니다",Preferences.Current.DesktopMenuEntry,v=>Preferences.SetWidgetMaterial(p=>p.DesktopMenuEntry=v));
            var layout=Paint.Action("기본 배치로 바꾸기",()=>shell.Widgets.SeedDefault(true));
            layout.HorizontalAlignment=HorizontalAlignment.Left;
            layout.Margin=new Thickness(0,0,0,8);
            layout.ToolTip="지금 놓인 위젯을 모두 지우고 오른쪽 가장자리에 정돈된 기본 위젯을 놓습니다";
            rows.Children.Add(layout);
            rows.Children.Add(sliders);
            var opacity=MaterialSlider(sliders,"위젯 불투명도",Preferences.Current.WidgetOpacity,100,"%",value=>Preferences.SetWidgetMaterial(p=>p.WidgetOpacity=value));
            opacity.Minimum=10;
            opacity.TickFrequency=5;
            var blur=MaterialSlider(sliders,"위젯 블러",Preferences.Current.WidgetBlur,40,"",value=>Preferences.SetWidgetMaterial(p=>p.WidgetBlur=value));
            blur.TickFrequency=2;
            return rows;
        }
    }
}
