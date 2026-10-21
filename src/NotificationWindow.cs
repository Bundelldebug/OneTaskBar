using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class NotificationWindow : GlassWindow
    {
        readonly ShellController shell;
        readonly StackPanel cards=new StackPanel();
        readonly TextBlock clock=Paint.TextBlock("",28,Paint.Text),today=Paint.TextBlock("",12,Paint.Muted),monthTitle=Paint.TextBlock("",17,Paint.Text);
        readonly UniformGrid days=new UniformGrid {Columns=7,Rows=7};
        readonly Button access;
        DateTime month=DateTime.Today;string signature;bool opening;
        internal NotificationWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · 알림 및 달력";Width=394;Height=704;Radius=30;Dark=true;Surface.CornerRadius=new CornerRadius(30);Surface.Background=GrayPanel.Glass;Surface.BorderBrush=Brushes.Transparent;Surface.BorderThickness=new Thickness(0);
            var root=new Grid {Margin=new Thickness(20,20,20,16)};root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
            var header=new StackPanel {Margin=new Thickness(5,0,0,17)};clock.FontWeight=FontWeights.SemiBold;header.Children.Add(clock);header.Children.Add(today);root.Children.Add(header);
            var calendar=new Border {CornerRadius=new CornerRadius(25),Background=GrayPanel.Card,Padding=new Thickness(10,12,10,10),Margin=new Thickness(0,0,0,16)};
            var calendarContent=new DockPanel();var monthRow=new Grid {Height=34,Margin=new Thickness(5,0,5,5)};monthRow.Children.Add(monthTitle);
            var previous=Paint.Action("‹",()=> {month=month.AddMonths(-1);Calendar();});previous.HorizontalAlignment=HorizontalAlignment.Right;previous.Margin=new Thickness(0,0,35,0);previous.Padding=new Thickness(9,0,9,0);previous.Background=Brushes.Transparent;monthRow.Children.Add(previous);
            var next=Paint.Action("›",()=> {month=month.AddMonths(1);Calendar();});next.HorizontalAlignment=HorizontalAlignment.Right;next.Padding=new Thickness(9,0,9,0);next.Background=Brushes.Transparent;monthRow.Children.Add(next);DockPanel.SetDock(monthRow,Dock.Top);calendarContent.Children.Add(monthRow);calendarContent.Children.Add(days);calendar.Child=calendarContent;Grid.SetRow(calendar,1);root.Children.Add(calendar);
            var notifications=new DockPanel();var labelRow=new Grid {Height=36};var label=Paint.TextBlock("알림",17,Paint.Text);label.FontWeight=FontWeights.SemiBold;labelRow.Children.Add(label);
            var clear=Paint.Action("모두 지우기",()=> {shell.Notifications.Clear();RefreshNotifications();});clear.HorizontalAlignment=HorizontalAlignment.Right;clear.Padding=new Thickness(10,4,10,4);clear.FontSize=11;labelRow.Children.Add(clear);DockPanel.SetDock(labelRow,Dock.Top);notifications.Children.Add(labelRow);
            access=Paint.Action("Windows 알림 연결",()=>RequestAccess());access.HorizontalAlignment=HorizontalAlignment.Stretch;access.Height=40;access.Margin=new Thickness(0,8,0,0);DockPanel.SetDock(access,Dock.Bottom);notifications.Children.Add(access);
            notifications.Children.Add(new SmoothScrollViewer {Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Grid.SetRow(notifications,2);root.Children.Add(notifications);Surface.Child=root;
            Deactivated+=(s,e)=> {if(!Program.Inspectable&&IsVisible&&!IsMouseOver)Hide();};Calendar();
        }
        void Calendar()
        {
            monthTitle.Text=month.ToString("yyyy년 M월");days.Children.Clear();
            foreach(var name in new[]{"일","월","화","수","목","금","토"}) {var day=Paint.TextBlock(name,11,Paint.Muted);day.HorizontalAlignment=HorizontalAlignment.Center;days.Children.Add(new Border {Height=26,Child=day});}
            var first=new DateTime(month.Year,month.Month,1);var start=first.AddDays(-(int)first.DayOfWeek);
            for(int n=0;n<42;n++){var date=start.AddDays(n);var text=Paint.TextBlock(date.Day.ToString(),12,date.Date==DateTime.Today?Paint.Ink:date.Month==month.Month?Paint.Text:Paint.Brush("#88DBE3DF"));text.HorizontalAlignment=HorizontalAlignment.Center;
                days.Children.Add(new Border {Width=31,Height=31,CornerRadius=new CornerRadius(16),Child=text,Background=date.Date==DateTime.Today?Paint.Brush("#FFDFE9E2"):Brushes.Transparent,Margin=new Thickness(0,2,0,2)});}
        }
        async void RequestAccess()
        {
            if(!shell.Notifications.Packaged){
                shell.ShowWindowsPanel(true,null);return;
            }
            await shell.Notifications.Request();signature=null;Refresh();
        }
        internal async void RefreshNotifications() {await shell.Notifications.Refresh();signature=null;if(IsVisible)Refresh();}
        async void OpenNotification(NotificationEntry item)
        {
            if(opening)return;opening=true;Hide();
            try {var result=await System.Threading.Tasks.Task.Run(()=>NotificationActivation.Open(item,false));
                if(result.Invoked&&result.Exact)shell.Notifications.Dismiss(item.Id);
                if(!result.Invoked)AppLog.Write("notification-open","Notification activation failed");
            }catch(Exception e){AppLog.Write("notification-open",e.Message);}finally{opening=false;RefreshNotifications();}
        }
        internal void Refresh()
        {
            clock.Text=DateTime.Now.ToString("H:mm:ss");today.Text=DateTime.Now.ToString("yyyy년 M월 d일 dddd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));
            var service=shell.Notifications;access.Visibility=service.Allowed?Visibility.Collapsed:Visibility.Visible;
            var accessText=access.Content as TextBlock;if(accessText!=null)accessText.Text=service.Packaged?"Windows 알림 접근 허용":"Windows 알림센터 열기";
            string current=service.Allowed+"|"+String.Join("|",service.Items.Select(a=>a.Id+":"+a.Title+":"+a.Body+":"+(a.Icon!=null)));if(current==signature)return;signature=current;cards.Children.Clear();
            if(!service.Allowed||service.Items.Count==0){var empty=Paint.TextBlock(service.Allowed?"새 알림이 없습니다":service.Status,12,Paint.Muted);empty.TextWrapping=TextWrapping.Wrap;empty.Margin=new Thickness(7,20,7,18);cards.Children.Add(empty);return;}
            foreach(var item in service.Items){var row=new StackPanel {Margin=new Thickness(0)};
                var appRow=new DockPanel();
                var app=Paint.TextBlock(item.App+"  ·  "+item.Created.LocalDateTime.ToString("H:mm"),10,Paint.Muted);app.TextTrimming=TextTrimming.CharacterEllipsis;appRow.Children.Add(app);row.Children.Add(appRow);
                var title=Paint.TextBlock(item.Title,13,Paint.Text);title.FontWeight=FontWeights.SemiBold;title.TextWrapping=TextWrapping.Wrap;title.Margin=new Thickness(0,8,0,3);row.Children.Add(title);
                if(!String.IsNullOrEmpty(item.Body)){var body=Paint.TextBlock(item.Body,12,Paint.Text);body.TextWrapping=TextWrapping.Wrap;body.MaxHeight=90;body.TextTrimming=TextTrimming.CharacterEllipsis;row.Children.Add(body);}
                var content=new Grid();content.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});content.ColumnDefinitions.Add(new ColumnDefinition());
                FrameworkElement icon=item.Icon!=null?AppIcons.Visual(item.Icon,30):new Glyph("window",28){Color=Paint.Muted};icon.VerticalAlignment=VerticalAlignment.Top;icon.Margin=new Thickness(0,3,6,0);content.Children.Add(icon);Grid.SetColumn(row,1);content.Children.Add(row);
                bool exact=item.Payload!=null&&(item.Payload.Type=="protocol"||item.Activator!=Guid.Empty);var open=Paint.Button(content,()=>OpenNotification(item),item.App+(exact?" · 알림 내용 열기":" · 알림을 보낸 앱 열기"));open.HorizontalContentAlignment=HorizontalAlignment.Stretch;open.Background=Brushes.Transparent;open.Padding=new Thickness(13,13,35,14);
                var layout=new Grid();layout.Children.Add(open);var close=Paint.Button(new Glyph("close",12){Color=Paint.Muted},()=> {service.Dismiss(item.Id);RefreshNotifications();},"알림 지우기");close.Width=24;close.Height=24;close.Padding=new Thickness(5);close.Background=Brushes.Transparent;close.HorizontalAlignment=HorizontalAlignment.Right;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new Thickness(0,8,8,0);layout.Children.Add(close);
                var card=new Border {Background=GrayPanel.Card,BorderBrush=Paint.Brush("#45FFFFFF"),BorderThickness=new Thickness(.6),CornerRadius=new CornerRadius(22),Margin=new Thickness(0,6,0,4),Child=layout};cards.Children.Add(card);
            }
        }
    }
}
