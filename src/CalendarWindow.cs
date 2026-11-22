using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace OneTaskBar
{
    internal sealed class CalendarWindow : GlassWindow
    {
        internal const double DexWidth=400,DexHeight=284,DexPickerHeight=332;
        static readonly Brush sunday=Paint.Brush("#FFE5484D"),saturday=Paint.Brush("#FF3F7BE0");
        UniformGrid days=new UniformGrid {Columns=7,Rows=7};readonly TextBlock month=Paint.TextBlock("",19,Paint.Text);DateTime shown=DateTime.Today,selected=DateTime.Today;
        readonly bool dex=Dex.Enabled;
        Grid pages;FrameworkElement calendarView,pickerView;Button backKey,nextKey,titleButton,cancelKey,doneKey;Border footDivider;Path caret;bool picking;int pickYear,pickMonth;
        readonly TextBlock[] yearRow=new TextBlock[3],monthRow=new TextBlock[3];
        readonly List<Glyph> dexGlyphs=new List<Glyph>();
        readonly List<TextBlock> weekdayLabels=new List<TextBlock>();
        internal DateTime Month {get{return shown;}}
        internal int DayCells {get{return dex?days.Children.Count:days.Children.Count-7;}}
        internal bool Picking {get{return picking;}}
        internal void PickForFixture(bool on){Pick(on);}
        internal CalendarWindow()
        {
            Title="One TaskBar · 달력";Width=346;Height=370;Radius=30;Surface.CornerRadius=new CornerRadius(30);Surface.Background=QuickPanel.Glass;
            Deactivated+=(s,e)=>{if(!Program.Inspectable&&!IsMouseOver)Hide();};
            if(dex){BuildDex();return;}
            var root=new DockPanel {Margin=new Thickness(18)};var header=new Grid {Height=44,Margin=new Thickness(0,0,0,10)};month.FontWeight=FontWeights.SemiBold;header.Children.Add(month);
            var controls=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};foreach(int step in new[]{-1,1}){int delta=step;var button=Paint.Button(new Glyph(step<0?"back":"chevron",17),()=>MoveMonth(delta),step<0?"이전 달":"다음 달");button.Width=button.Height=32;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;controls.Children.Add(button);}header.Children.Add(controls);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var today=Paint.Action("오늘",()=>{shown=selected=DateTime.Today;Refresh();});today.Height=34;today.Margin=new Thickness(0,10,0,0);DockPanel.SetDock(today,Dock.Bottom);root.Children.Add(today);root.Children.Add(days);Surface.Child=root;Refresh();
        }
        Button Round(string glyph,Action press,string tip,double angle=0)
        {
            var icon=new Glyph(glyph,15){Color=Dex.SheetInk};
            dexGlyphs.Add(icon);
            if(angle!=0){icon.RenderTransformOrigin=new Point(.5,.5);icon.RenderTransform=new RotateTransform(angle);}
            var button=Paint.Button(icon,press,tip);
            button.Width=button.Height=32;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;button.Resources["ButtonRadius"]=new CornerRadius(16);
            return button;
        }
        internal void RefreshDexTheme()
        {
            if(!dex)return;
            var ink=Dex.SheetInk;var muted=Dex.SheetMuted;
            month.Foreground=ink;
            if(caret!=null)caret.Fill=ink;
            if(titleButton!=null)titleButton.Background=picking?Dex.SheetChip:Brushes.Transparent;
            foreach(var g in dexGlyphs){g.Color=ink;g.InvalidateVisual();}
            for(int i=0;i<weekdayLabels.Count;i++)weekdayLabels[i].Foreground=i==0?sunday:i==6?saturday:muted;
            for(int i=0;i<3;i++)
            {
                if(yearRow[i]!=null)yearRow[i].Foreground=i==1?ink:muted;
                if(monthRow[i]!=null)monthRow[i].Foreground=i==1?ink:muted;
            }
            if(cancelKey!=null&&cancelKey.Content is TextBlock)((TextBlock)cancelKey.Content).Foreground=ink;
            if(doneKey!=null&&doneKey.Content is TextBlock)((TextBlock)doneKey.Content).Foreground=ink;
            if(footDivider!=null)footDivider.Background=Dex.LightSheet?Paint.Brush("#1C000000"):Paint.Brush("#28FFFFFF");
            Fill(days);
        }
        void BuildDex()
        {
            Radius=Dex.PanelRadius;Surface.CornerRadius=new CornerRadius(Radius);
            var root=new DockPanel {Margin=new Thickness(22,12,22,12)};
            var header=new Grid {Height=36,Margin=new Thickness(0,0,0,6)};
            backKey=Round("back",()=>MoveMonth(-1),"이전 달");backKey.HorizontalAlignment=HorizontalAlignment.Left;header.Children.Add(backKey);
            nextKey=Round("chevron",()=>MoveMonth(1),"다음 달");nextKey.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(nextKey);
            month.FontSize=15;month.FontWeight=FontWeights.SemiBold;month.Foreground=Dex.SheetInk;month.VerticalAlignment=VerticalAlignment.Center;
            caret=new Path {Data=Geometry.Parse("M0,0 L8,0 4,5 Z"),Fill=Dex.SheetInk,Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new RotateTransform(0)};
            var words=new StackPanel {Orientation=Orientation.Horizontal};words.Children.Add(month);words.Children.Add(caret);
            titleButton=Paint.Button(words,()=>Pick(!picking),"연도와 월 고르기");titleButton.HorizontalAlignment=HorizontalAlignment.Center;titleButton.Padding=new Thickness(12,5,12,5);titleButton.Background=Brushes.Transparent;titleButton.Resources["ButtonRadius"]=new CornerRadius(12);header.Children.Add(titleButton);
            DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var body=new Grid();
            var view=new DockPanel();calendarView=view;
            var names=new UniformGrid {Columns=7,Height=22};DockPanel.SetDock(names,Dock.Top);
            string[] labels={"일","월","화","수","목","금","토"};
            for(int i=0;i<7;i++)
            {
                var label=Paint.TextBlock(labels[i],10.5,i==0?sunday:i==6?saturday:Dex.SheetMuted);label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;weekdayLabels.Add(label);names.Children.Add(label);
            }
            view.Children.Add(names);
            pages=new Grid {ClipToBounds=true};days=NewPage();pages.Children.Add(days);view.Children.Add(pages);body.Children.Add(view);
            pickerView=BuildPicker();pickerView.Visibility=Visibility.Collapsed;body.Children.Add(pickerView);
            root.Children.Add(body);Surface.Child=root;Refresh();
            IsVisibleChanged+=(s,e)=>{if(!IsVisible){shown=selected=DateTime.Today;if(picking)Pick(false,false);Refresh();}};
        }
        static UniformGrid NewPage(){return new UniformGrid {Columns=7,Rows=6};}
        FrameworkElement Wheel(TextBlock[] row,Action<int> move)
        {
            var stack=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Background=Brushes.Transparent};
            var up=Round("chevron",()=>move(-1),"이전",-90);up.HorizontalAlignment=HorizontalAlignment.Center;stack.Children.Add(up);
            for(int i=0;i<3;i++)
            {
                row[i]=Paint.TextBlock("",i==1?19:16,i==1?Dex.SheetInk:Dex.SheetMuted);row[i].HorizontalAlignment=HorizontalAlignment.Center;row[i].Margin=new Thickness(0,3,0,3);row[i].Opacity=i==1?1:.55;
                if(i==1)row[i].FontWeight=FontWeights.SemiBold;
                stack.Children.Add(row[i]);
            }
            var down=Round("chevron",()=>move(1),"다음",90);down.HorizontalAlignment=HorizontalAlignment.Center;stack.Children.Add(down);
            stack.MouseWheel+=(s,e)=>{move(e.Delta>0?-1:1);e.Handled=true;};
            return stack;
        }
        FrameworkElement BuildPicker()
        {
            var view=new DockPanel();
            var foot=new Grid {Height=38};foot.ColumnDefinitions.Add(new ColumnDefinition());foot.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1)});foot.ColumnDefinitions.Add(new ColumnDefinition());
            cancelKey=Paint.Action("취소",()=>Pick(false));cancelKey.Background=Brushes.Transparent;cancelKey.BorderThickness=new Thickness(0);cancelKey.HorizontalAlignment=HorizontalAlignment.Stretch;((TextBlock)cancelKey.Content).Foreground=Dex.SheetInk;((TextBlock)cancelKey.Content).FontWeight=FontWeights.SemiBold;foot.Children.Add(cancelKey);
            footDivider=new Border {Width=1,Height=16,VerticalAlignment=VerticalAlignment.Center,Background=Dex.LightSheet?Paint.Brush("#1C000000"):Paint.Brush("#28FFFFFF")};Grid.SetColumn(footDivider,1);foot.Children.Add(footDivider);
            doneKey=Paint.Action("완료",()=>{shown=new DateTime(pickYear,pickMonth,1);Pick(false);Refresh();});doneKey.Background=Brushes.Transparent;doneKey.BorderThickness=new Thickness(0);doneKey.HorizontalAlignment=HorizontalAlignment.Stretch;((TextBlock)doneKey.Content).Foreground=Dex.SheetInk;((TextBlock)doneKey.Content).FontWeight=FontWeights.SemiBold;Grid.SetColumn(doneKey,2);foot.Children.Add(doneKey);
            DockPanel.SetDock(foot,Dock.Bottom);view.Children.Add(foot);
            var wheels=new Grid();wheels.ColumnDefinitions.Add(new ColumnDefinition());wheels.ColumnDefinitions.Add(new ColumnDefinition());
            wheels.Children.Add(Wheel(yearRow,d=>{pickYear=Math.Max(1901,Math.Min(2099,pickYear+d));ShowPick();}));
            var months=Wheel(monthRow,d=>{pickMonth=(pickMonth+d+11)%12+1;ShowPick();});Grid.SetColumn(months,1);wheels.Children.Add(months);
            view.Children.Add(wheels);
            return view;
        }
        void ShowPick()
        {
            for(int i=0;i<3;i++)
            {
                yearRow[i].Text=(pickYear+i-1)+"년";
                monthRow[i].Text=((pickMonth+i+10)%12+1)+"월";
            }
        }
        int pickToken;
        // The month grid and year/month wheels crossfade smoothly inside the glass while its height settles.
        void Pick(bool on,bool animate=true)
        {
            if(picking==on)return;
            int token=++pickToken;
            picking=on;pickYear=shown.Year;pickMonth=shown.Month;ShowPick();
            backKey.Visibility=nextKey.Visibility=on?Visibility.Hidden:Visibility.Visible;
            if(titleButton!=null)titleButton.Background=on?Dex.SheetChip:Brushes.Transparent;
            ((RotateTransform)caret.RenderTransform).Angle=on?180:0;
            var show=on?pickerView:calendarView;var hide=on?calendarView:pickerView;
            show.BeginAnimation(OpacityProperty,null);hide.BeginAnimation(OpacityProperty,null);
            if(animate&&IsVisible&&Motion.Enabled)
            {
                show.Opacity=0;show.Visibility=Visibility.Visible;
                hide.Opacity=1;hide.Visibility=Visibility.Visible;
                PanelMorph.Resize(this,on?DexPickerHeight:DexHeight);
                var fadeIn=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(220)){EasingFunction=SpringEase.Smooth};
                var fadeOut=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(160)){EasingFunction=SpringEase.Smooth};
                fadeOut.Completed+=(s,e)=>{if(token==pickToken){hide.Visibility=Visibility.Collapsed;hide.BeginAnimation(OpacityProperty,null);hide.Opacity=1;}};
                show.BeginAnimation(OpacityProperty,fadeIn);
                hide.BeginAnimation(OpacityProperty,fadeOut);
                return;
            }
            show.Opacity=1;show.Visibility=Visibility.Visible;
            hide.Opacity=1;hide.Visibility=Visibility.Collapsed;
        }
        internal void MoveMonth(int delta)
        {
            shown=new DateTime(shown.Year,shown.Month,1).AddMonths(delta);
            if(!dex||!IsVisible||!Motion.Enabled||pages.ActualWidth<1){Refresh();return;}
            // The days slide sideways and the next month follows them in.
            month.Text=Loc.Date(shown,"yyyy년 M월");
            var old=days;double wide=pages.ActualWidth+12;
            days=NewPage();Fill(days);pages.Children.Add(days);
            var enter=new TranslateTransform(delta*wide,0);days.RenderTransform=enter;
            var leave=new TranslateTransform();old.RenderTransform=leave;
            enter.BeginAnimation(TranslateTransform.XProperty,Motion.PanelTween(delta*wide,0,280));
            var away=Motion.PanelTween(0,-delta*wide,280);
            away.Completed+=(s,e)=>pages.Children.Remove(old);
            leave.BeginAnimation(TranslateTransform.XProperty,away);
        }
        void Fill(UniformGrid grid)
        {
            grid.Children.Clear();
            var first=new DateTime(shown.Year,shown.Month,1);DateTime start=first.AddDays(-(int)first.DayOfWeek);
            var normalInk=Dex.SheetInk;
            for(int i=0;i<42;i++)
            {
                var date=start.AddDays(i);bool current=date.Month==shown.Month,today=date.Date==DateTime.Today;
                var label=Paint.TextBlock(date.Day.ToString(),13.5,today?Paint.ControlWhite:date.DayOfWeek==DayOfWeek.Sunday?sunday:date.DayOfWeek==DayOfWeek.Saturday?saturday:normalInk);
                label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;
                var cell=new Border {Width=26,Height=26,CornerRadius=new CornerRadius(13),Background=today?Glass95.Accent:Brushes.Transparent,Child=label,Opacity=current?1:.38,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
                grid.Children.Add(cell);
            }
        }
        void Refresh()
        {
            month.Text=Loc.Date(shown,"yyyy년 M월");
            if(dex){Fill(days);return;}
            days.Children.Clear();foreach(string name in new[]{"일","월","화","수","목","금","토"}){var label=Paint.TextBlock(name,12,Paint.Muted);label.HorizontalAlignment=HorizontalAlignment.Center;days.Children.Add(label);}
            var first=new DateTime(shown.Year,shown.Month,1);DateTime start=first.AddDays(-(int)first.DayOfWeek);for(int i=0;i<42;i++){
                var date=start.AddDays(i);bool current=date.Month==shown.Month,today=date.Date==DateTime.Today;
                var label=Paint.TextBlock(date.Day.ToString(),13,today?Paint.ControlWhite:date.DayOfWeek==DayOfWeek.Sunday?Paint.Brush("#FFE66B71"):Paint.Text);label.Opacity=current?1:.35;var button=Paint.Button(label,()=>{selected=date;shown=date;Refresh();},date.ToString("yyyy년 M월 d일 dddd",Loc.Culture));button.Margin=new Thickness(2);button.Padding=new Thickness(0);button.Resources["ButtonRadius"]=new CornerRadius(18);button.Background=today?Glass95.Accent:selected.Date==date.Date?QuickPanel.Card:Brushes.Transparent;button.BorderThickness=new Thickness(selected.Date==date.Date&&!today?.7:0);button.BorderBrush=Glass95.Enabled?Glass95.Rim:Paint.Muted;days.Children.Add(button);
            }
        }
    }
    internal sealed partial class ShellController
    {
        internal CalendarWindow Calendar;
        internal void ToggleCalendar(GlassWindow anchor)
        {
            if(Calendar!=null&&Calendar.IsVisible&&!Calendar.IsDismissing){Calendar.Hide();return;}if(Calendar==null)Calendar=new CalendarWindow();var screen=anchor==null?System.Windows.Forms.Screen.PrimaryScreen:System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(anchor).Handle);double scale=Native.PrepareMonitor(Calendar,screen.Bounds);var work=Native.WorkArea(screen);Native.RectI source;
            if(anchor==null||!Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(anchor).Handle,out source))source=work;
            bool dexPanel=Dex.Enabled;
            double width=Math.Min(dexPanel?CalendarWindow.DexWidth:346,(work.Right-work.Left)/scale-20),height=Math.Min(dexPanel?CalendarWindow.DexHeight:370,(work.Bottom-work.Top)/scale-20),x=source.Left/scale-width-12,y=source.Top/scale;
            if(x*scale<work.Left)x=source.Right/scale+12;x=Math.Max(work.Left/scale+8,Math.Min(work.Right/scale-width-8,x));y=Math.Max(work.Top/scale+8,Math.Min(work.Bottom/scale-height-8,y));bool underClock=anchor is TopStatusBar;Calendar.PopFromTop=underClock;if(underClock){x=screen.Bounds.Left/scale+8;y=screen.Bounds.Top/scale+IslandSafeArea.Height+4;}Calendar.ShowAt(screen.Bounds,x-screen.Bounds.Left/scale,y-screen.Bounds.Top/scale,width,height);Calendar.Pop();Calendar.Activate();
        }
        internal void QuickFromNotifications(){if(Center!=null)Center.HideImmediately();ToggleQuick(null,true);}
    }
}
