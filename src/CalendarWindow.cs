using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class CalendarWindow : GlassWindow
    {
        readonly UniformGrid days=new UniformGrid {Columns=7,Rows=7};readonly TextBlock month=Paint.TextBlock("",19,Paint.Text);DateTime shown=DateTime.Today,selected=DateTime.Today;
        internal DateTime Month {get{return shown;}}
        internal int DayCells {get{return days.Children.Count-7;}}
        internal CalendarWindow()
        {
            Title="One TaskBar · 달력";Width=346;Height=370;Radius=30;Surface.CornerRadius=new CornerRadius(30);Surface.Background=QuickPanel.Glass;
            var root=new DockPanel {Margin=new Thickness(18)};var header=new Grid {Height=44,Margin=new Thickness(0,0,0,10)};month.FontWeight=FontWeights.SemiBold;header.Children.Add(month);
            var controls=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};foreach(int step in new[]{-1,1}){int delta=step;var button=Paint.Button(new Glyph(step<0?"back":"chevron",17),()=>MoveMonth(delta),step<0?"이전 달":"다음 달");button.Width=button.Height=32;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;controls.Children.Add(button);}header.Children.Add(controls);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var today=Paint.Action("오늘",()=>{shown=selected=DateTime.Today;Refresh();});today.Height=34;today.Margin=new Thickness(0,10,0,0);DockPanel.SetDock(today,Dock.Bottom);root.Children.Add(today);root.Children.Add(days);Surface.Child=root;Refresh();Deactivated+=(s,e)=>{if(!Program.Inspectable&&!IsMouseOver)Hide();};
        }
        internal void MoveMonth(int delta){shown=shown.AddMonths(delta);Refresh();}
        void Refresh()
        {
            month.Text=shown.ToString("yyyy년 M월",CultureInfo.GetCultureInfo("ko-KR"));days.Children.Clear();foreach(string name in new[]{"일","월","화","수","목","금","토"}){var label=Paint.TextBlock(name,12,Paint.Muted);label.HorizontalAlignment=HorizontalAlignment.Center;days.Children.Add(label);}
            var first=new DateTime(shown.Year,shown.Month,1);DateTime start=first.AddDays(-(int)first.DayOfWeek);for(int i=0;i<42;i++){
                var date=start.AddDays(i);bool current=date.Month==shown.Month,today=date.Date==DateTime.Today;
                var label=Paint.TextBlock(date.Day.ToString(),13,today?Paint.ControlWhite:date.DayOfWeek==DayOfWeek.Sunday?Paint.Brush("#FFE66B71"):Paint.Text);label.Opacity=current?1:.35;var button=Paint.Button(label,()=>{selected=date;shown=date;Refresh();},date.ToString("yyyy년 M월 d일 dddd"));button.Margin=new Thickness(2);button.Padding=new Thickness(0);button.Resources["ButtonRadius"]=new CornerRadius(18);button.Background=today?Glass95.Accent:selected.Date==date.Date?QuickPanel.Card:Brushes.Transparent;button.BorderThickness=new Thickness(selected.Date==date.Date&&!today?.7:0);button.BorderBrush=Glass95.Enabled?Glass95.Rim:Paint.Muted;days.Children.Add(button);
            }
        }
    }
    internal sealed partial class ShellController
    {
        internal CalendarWindow Calendar;
        internal void ToggleCalendar(GlassWindow anchor)
        {
            if(Calendar!=null&&Calendar.IsVisible){Calendar.Hide();return;}if(Calendar==null)Calendar=new CalendarWindow();var screen=anchor==null?System.Windows.Forms.Screen.PrimaryScreen:System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(anchor).Handle);double scale=Native.PrepareMonitor(Calendar,screen.Bounds);var work=Native.WorkArea(screen);Native.RectI source;
            if(anchor==null||!Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(anchor).Handle,out source))source=work;
            double width=Math.Min(346,(work.Right-work.Left)/scale-20),height=Math.Min(370,(work.Bottom-work.Top)/scale-20),x=source.Left/scale-width-12,y=source.Top/scale;
            if(x*scale<work.Left)x=source.Right/scale+12;x=Math.Max(work.Left/scale+8,Math.Min(work.Right/scale-width-8,x));y=Math.Max(work.Top/scale+8,Math.Min(work.Bottom/scale-height-8,y));Calendar.ShowAt(screen.Bounds,x-screen.Bounds.Left/scale,y-screen.Bounds.Top/scale,width,height);Calendar.Pop();Calendar.Activate();
        }
        internal void QuickFromNotifications(){if(Center!=null)Center.HideImmediately();ToggleQuick(null,true);}
    }
}
