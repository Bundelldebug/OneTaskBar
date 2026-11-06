using System;
using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    // The hidden-icons panel can be made smaller or larger; everything in it scales together.
    internal static class TrayPanelSizing
    {
        internal static int Clamp(int percent){return Math.Max(50,Math.Min(130,percent));}
        internal static double Scale {get{return Clamp(Preferences.Current.TrayPanelPercent)/100.0;}}
    }
    internal sealed partial class WelcomeWindow
    {
        void TrayPanelSetting(StackPanel rows)
        {
            var group=new StackPanel {Margin=new Thickness(15,14,15,14)};
            var heading=new DockPanel();
            var number=Paint.TextBlock(TrayPanelSizing.Clamp(Preferences.Current.TrayPanelPercent)+"%",14,AppSettingsPalette.Muted);
            number.HorizontalAlignment=HorizontalAlignment.Right;
            DockPanel.SetDock(number,Dock.Right);heading.Children.Add(number);
            heading.Children.Add(Paint.TextBlock("숨겨진 아이콘 패널 크기",15,AppSettingsPalette.Text));
            group.Children.Add(heading);
            var slider=new Slider {Minimum=50,Maximum=130,TickFrequency=5,IsSnapToTickEnabled=true,Value=TrayPanelSizing.Clamp(Preferences.Current.TrayPanelPercent),Margin=new Thickness(0,14,0,4)};
            System.Windows.Automation.AutomationProperties.SetName(slider,"숨겨진 아이콘 패널 크기 백분율");
            Action commit=()=>
            {
                int value=TrayPanelSizing.Clamp((int)Math.Round(slider.Value));
                if(value!=Preferences.Current.TrayPanelPercent)Preferences.Set(p=>p.TrayPanelPercent=value);
            };
            slider.ValueChanged+=(s,e)=>number.Text=((int)Math.Round(slider.Value))+"%";
            slider.PreviewMouseLeftButtonUp+=(s,e)=>commit();
            slider.LostMouseCapture+=(s,e)=>commit();
            slider.KeyUp+=(s,e)=>commit();
            group.Children.Add(SettingsSliderStyle.WithSymbol(slider,"desktops"));rows.Children.Add(group);
        }
    }
}
