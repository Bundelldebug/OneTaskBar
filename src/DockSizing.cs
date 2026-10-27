using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class DockSizing
    {
        internal static int Clamp(int percent){return Math.Max(60,Math.Min(140,percent));}
        internal static double Zoom {get{return Clamp(Preferences.Current.DockScalePercent)/100.0;}}
        internal static double Reserved {get{return 68*Zoom;}}
        internal static double PopupInset {get{return Reserved+8;}}
        internal static Rect DisplayRect(Rect value,double zoom)
        {
            return new Rect(value.X*zoom,value.Y*zoom,value.Width*zoom,value.Height*zoom);
        }
    }
    internal sealed partial class WelcomeWindow
    {
        void DockScaleSetting(StackPanel rows)
        {
            var group=new StackPanel {Margin=new Thickness(15,14,15,14)};
            var heading=new DockPanel();
            var number=Paint.TextBlock(DockSizing.Clamp(Preferences.Current.DockScalePercent)+"%",14,AppSettingsPalette.Muted);
            number.HorizontalAlignment=HorizontalAlignment.Right;
            DockPanel.SetDock(number,Dock.Right);heading.Children.Add(number);
            heading.Children.Add(Paint.TextBlock("작업표시줄 크기",15,AppSettingsPalette.Text));
            group.Children.Add(heading);
            var slider=new Slider {Minimum=60,Maximum=140,TickFrequency=5,IsSnapToTickEnabled=true,Value=DockSizing.Clamp(Preferences.Current.DockScalePercent),Margin=new Thickness(0,14,0,4)};
            System.Windows.Automation.AutomationProperties.SetName(slider,"작업표시줄 크기 백분율");
            Action commit=()=>
            {
                int value=DockSizing.Clamp((int)Math.Round(slider.Value));
                if(value!=Preferences.Current.DockScalePercent)Preferences.Set(p=>p.DockScalePercent=value);
            };
            slider.ValueChanged+=(s,e)=>number.Text=((int)Math.Round(slider.Value))+"%";
            slider.PreviewMouseLeftButtonUp+=(s,e)=>commit();
            slider.LostMouseCapture+=(s,e)=>commit();
            slider.KeyUp+=(s,e)=>commit();
            group.Children.Add(slider);rows.Children.Add(group);
        }
    }
}
