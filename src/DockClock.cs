using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class DockClock
    {
        internal readonly TextBlock View=new TextBlock {TextAlignment=TextAlignment.Center,Foreground=Glass95.DockForeground,VerticalAlignment=VerticalAlignment.Center};
        readonly Run time=new Run(),date=new Run();
        readonly LineBreak line=new LineBreak();
        int mode=-1;bool vertical;
        internal static int Mode {get{return Math.Max(0,Math.Min(2,Preferences.Current.DockClockMode));}}
        internal static double Width {get{return Mode==1?82:Mode==2?76:94;}}
        internal static double SideHeight {get{return Mode==1?28:Mode==2?20:40;}}
        internal void Update(DateTime value,int edge)
        {
            bool side=DockGeometry.Vertical(edge);int next=Mode;
            if(mode!=next||vertical!=side)
            {
                mode=next;vertical=side;View.Inlines.Clear();
                time.FontWeight=FontWeights.SemiBold;time.FontSize=side?9:12.5;
                date.FontWeight=FontWeights.Normal;date.FontSize=side?8:10.5;
                if(next!=2)View.Inlines.Add(time);
                if(next==0)View.Inlines.Add(line);
                if(next!=1)View.Inlines.Add(date);
            }
            var culture=CultureInfo.GetCultureInfo("ko-KR");
            string nextTime=value.ToString(side?Preferences.Current.Seconds?"tt\nh:mm:ss":"tt\nh:mm":Preferences.Current.Seconds?"tt h:mm:ss":"tt h:mm",culture);
            string nextDate=value.ToString(side?"MM-dd":"yyyy-MM-dd",culture);
            if(time.Text!=nextTime)time.Text=nextTime;
            if(date.Text!=nextDate)date.Text=nextDate;
        }
    }
    internal sealed partial class WelcomeWindow
    {
        void ClockSetting(StackPanel rows)
        {
            var group=new StackPanel {Margin=new Thickness(15,14,15,14)};
            group.Children.Add(Paint.TextBlock("시계 표시",15,AppSettingsPalette.Text));
            var choices=new ComboBox {ItemsSource=new[]{new UIVersionChoice {Label="시간과 날짜"},new UIVersionChoice {Label="시간만"},new UIVersionChoice {Label="날짜만"}},DisplayMemberPath="Label",SelectedIndex=DockClock.Mode,MinHeight=38,Margin=new Thickness(0,9,0,0)};
            SettingsPickerStyle.Apply(choices);
            System.Windows.Automation.AutomationProperties.SetName(choices,"작업표시줄 시계 표시");
            choices.SelectionChanged+=(s,e)=>{int value=choices.SelectedIndex;if(value>=0&&value!=Preferences.Current.DockClockMode)Preferences.Set(p=>p.DockClockMode=value);};
            group.Children.Add(choices);
            var help=Paint.TextBlock("전체 시간과 날짜는 마우스를 올리거나 알림창에서 볼 수 있습니다",12,AppSettingsPalette.Muted);
            help.TextWrapping=TextWrapping.Wrap;help.Margin=new Thickness(0,8,0,0);group.Children.Add(help);rows.Children.Add(group);
        }
    }
}
