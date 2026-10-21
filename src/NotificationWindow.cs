using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    internal sealed class NotificationWindow : GlassWindow
    {
        readonly ShellController shell;
        readonly StackPanel cards=new StackPanel();
        readonly TextBlock clock=Paint.TextBlock("",28,Paint.Text),today=Paint.TextBlock("",12,Paint.Muted),count=Paint.TextBlock("알림",17,Paint.Text);
        readonly Button access,clear,disturb;readonly Glyph disturbGlyph=new Glyph("dnd",20);
        readonly Dictionary<string,Border> retained=new Dictionary<string,Border>();string signature;bool opening,disturbBusy;
        internal int CardCount {get{return retained.Count;}}
        internal NotificationWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · 알림";Width=394;Height=584;Radius=30;Dark=true;Surface.CornerRadius=new CornerRadius(30);Surface.Background=QuickPanel.Glass;Surface.BorderBrush=Paint.Brush("#48FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var root=new Grid {Margin=new Thickness(20,22,20,16)};root.RowDefinitions.Add(new RowDefinition {Height=new GridLength(79)});root.RowDefinitions.Add(new RowDefinition());
            var header=new Grid {Margin=new Thickness(5,0,3,13)};var heading=new StackPanel();clock.FontWeight=FontWeights.SemiBold;heading.Children.Add(clock);heading.Children.Add(today);header.Children.Add(heading);
            disturb=Paint.Button(disturbGlyph,()=>ToggleDisturb(),"방해 금지");disturb.Width=disturb.Height=42;disturb.Padding=new Thickness(0);disturb.Resources["ButtonRadius"]=new CornerRadius(21);disturb.HorizontalAlignment=HorizontalAlignment.Right;disturb.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(disturb);root.Children.Add(header);
            var notifications=new DockPanel();var labelRow=new Grid {Height=39,Margin=new Thickness(6,0,3,7)};count.FontWeight=FontWeights.SemiBold;count.VerticalAlignment=VerticalAlignment.Center;labelRow.Children.Add(count);
            clear=Paint.Action("모두 지우기",()=> {shell.Notifications.Clear();RefreshNotifications();});clear.HorizontalAlignment=HorizontalAlignment.Right;clear.Padding=new Thickness(12,7,12,7);clear.FontSize=11;labelRow.Children.Add(clear);DockPanel.SetDock(labelRow,Dock.Top);notifications.Children.Add(labelRow);
            access=Paint.Action("Windows 알림 연결",()=>RequestAccess());access.HorizontalAlignment=HorizontalAlignment.Stretch;access.Height=40;access.Margin=new Thickness(0,8,0,0);DockPanel.SetDock(access,Dock.Bottom);notifications.Children.Add(access);
            notifications.Children.Add(new SmoothScrollViewer {Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.VerticalFirst});Grid.SetRow(notifications,1);root.Children.Add(notifications);Surface.Child=root;StableSurface=true;TransitionContent=root;
            Deactivated+=(s,e)=> {if(!Program.Inspectable&&IsVisible&&!IsMouseOver)Hide();};
        }
        async void ToggleDisturb()
        {
            if(disturbBusy)return;disturbBusy=true;disturb.IsEnabled=false;try{await shell.Toggles.Disturb.Toggle();}catch(Exception error){disturb.ToolTip=error.Message;}finally{disturbBusy=false;Refresh();}
        }
        void RequestAccess(){shell.ShowNotificationConnection();}
        internal async void RefreshNotifications() {await shell.Notifications.Refresh();signature=null;if(IsVisible)Refresh();}
        async void OpenNotification(NotificationEntry item)
        {
            if(opening)return;opening=true;
            try {if(!await DismissAsync())return;var result=await Task.Run(()=>NotificationActivation.Open(item,false));
                if(result.Invoked&&result.Exact)shell.Notifications.Dismiss(item.Id);
                if(!result.Invoked)AppLog.Write("notification-open","Notification activation failed");
            }catch(Exception e){AppLog.Write("notification-open",e.Message);}finally{opening=false;RefreshNotifications();}
        }
        internal void Refresh()
        {
            clock.Text=DateTime.Now.ToString("H:mm:ss");today.Text=DateTime.Now.ToString("M월 d일 dddd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));
            var dnd=shell.Toggles.Disturb.State;disturb.IsEnabled=dnd.Available&&!disturbBusy;disturb.Background=dnd.Enabled?Paint.ControlWhite:QuickPanel.Card;disturbGlyph.Color=dnd.Enabled?Paint.Ink:Paint.ControlWhite;disturbGlyph.InvalidateVisual();System.Windows.Automation.AutomationProperties.SetItemStatus(disturb,dnd.Enabled?"켜짐":"꺼짐");
            var service=shell.Notifications;access.Visibility=service.Allowed?Visibility.Collapsed:Visibility.Visible;count.Text=service.Allowed&&service.Items.Count>0?"알림  "+service.Items.Count:"알림";clear.IsEnabled=service.Allowed&&service.Items.Count>0;clear.Opacity=clear.IsEnabled?1:.4;
            var accessText=access.Content as TextBlock;if(accessText!=null)accessText.Text="Windows 알림 연결";
            string current=service.Allowed+"|"+dnd.Enabled+"|"+String.Join("|",service.Items.Select(a=>a.Id+":"+a.Title+":"+a.Body+":"+(a.Icon!=null)));if(current==signature)return;signature=current;
            var items=service.Allowed?service.Items:new List<NotificationEntry>();var keys=new HashSet<string>(items.Select(Key));foreach(string key in retained.Keys.Where(k=>!keys.Contains(k)).ToArray()){var removed=retained[key];retained.Remove(key);RemoveCard(removed);}
            foreach(var empty in cards.Children.OfType<Border>().Where(b=>b.Tag as string=="empty").ToArray())cards.Children.Remove(empty);
            if(!service.Allowed||service.Items.Count==0){var content=new StackPanel {Margin=new Thickness(20,32,20,34)};var empty=Paint.TextBlock(service.Allowed?"알림이 없습니다.":"Windows 알림 연결",15,Paint.Text);empty.FontWeight=FontWeights.SemiBold;empty.TextAlignment=TextAlignment.Center;content.Children.Add(empty);if(!service.Allowed){var status=Paint.TextBlock(service.Status,12,Paint.Muted);status.TextAlignment=TextAlignment.Center;status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,9,0,0);content.Children.Add(status);}cards.Children.Add(new Border {Tag="empty",Background=Brushes.Transparent,Margin=new Thickness(0,4,0,6),Child=content});return;}

            int index=0;foreach(var item in service.Items){string key=Key(item),version=item.Title+"|"+item.Body+"|"+(item.Icon!=null);Border existing;if(retained.TryGetValue(key,out existing)){if((string)existing.Tag==version){int old=cards.Children.IndexOf(existing);if(old!=index){cards.Children.Remove(existing);cards.Children.Insert(Math.Min(index,cards.Children.Count),existing);}index++;continue;}retained.Remove(key);RemoveCard(existing);}
                var row=new StackPanel {Margin=new Thickness(0)};
                var appRow=new DockPanel();
                var app=Paint.TextBlock(item.App+"  ·  "+item.Created.LocalDateTime.ToString("H:mm"),10,Paint.Muted);app.TextTrimming=TextTrimming.CharacterEllipsis;appRow.Children.Add(app);row.Children.Add(appRow);
                var title=Paint.TextBlock(item.Title,14,Paint.Text);title.FontWeight=FontWeights.SemiBold;title.TextWrapping=TextWrapping.Wrap;title.MaxHeight=44;title.TextTrimming=TextTrimming.CharacterEllipsis;title.Margin=new Thickness(0,7,0,4);row.Children.Add(title);
                if(!String.IsNullOrEmpty(item.Body)){var body=Paint.TextBlock(item.Body,12,Paint.Text);body.TextWrapping=TextWrapping.Wrap;body.MaxHeight=90;body.TextTrimming=TextTrimming.CharacterEllipsis;row.Children.Add(body);}
                var content=new Grid();content.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});content.ColumnDefinitions.Add(new ColumnDefinition());
                FrameworkElement icon=item.Icon!=null?AppIcons.Visual(item.Icon,32):new Glyph("window",28){Color=Paint.Muted};icon.VerticalAlignment=VerticalAlignment.Top;icon.Margin=new Thickness(0,3,6,0);content.Children.Add(icon);Grid.SetColumn(row,1);content.Children.Add(row);
                bool exact=item.Payload!=null&&(item.Payload.Type=="protocol"||item.Activator!=Guid.Empty);var open=Paint.Button(content,()=>OpenNotification(item),item.App+(exact?" · 알림 내용 열기":" · 알림을 보낸 앱 열기"));open.HorizontalContentAlignment=HorizontalAlignment.Stretch;open.Background=Brushes.Transparent;open.Padding=new Thickness(13,13,35,14);
                var layout=new Grid();layout.Children.Add(open);var close=Paint.Button(new Glyph("close",12){Color=Paint.Muted},()=> {service.Dismiss(item.Id);RefreshNotifications();},"알림 지우기");close.Width=24;close.Height=24;close.Padding=new Thickness(5);close.Background=Brushes.Transparent;close.HorizontalAlignment=HorizontalAlignment.Right;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new Thickness(0,8,8,0);layout.Children.Add(close);
                var card=new Border {Tag=version,Background=QuickPanel.Card,CornerRadius=new CornerRadius(27),Margin=new Thickness(0,4,0,7),Child=layout};retained[key]=card;cards.Children.Insert(Math.Min(index,cards.Children.Count),card);index++;if(IsVisible&&Motion.Enabled){Motion.Fade(card,0,1,180);var shift=new TranslateTransform(0,10);card.RenderTransform=shift;shift.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(10,0,240));}
            }
        }
        static string Key(NotificationEntry item){return item.AppId+"|"+item.Id;}
        void RemoveCard(Border card)
        {
            if(!IsVisible||!Motion.Enabled){cards.Children.Remove(card);return;}card.IsHitTestVisible=false;double height=card.ActualHeight;card.Height=0;card.ClipToBounds=true;card.BeginAnimation(HeightProperty,Motion.Tween(height,0,180));var fade=Motion.Tween(card.Opacity,0,150);card.Opacity=0;fade.Completed+=(s,e)=>cards.Children.Remove(card);card.BeginAnimation(OpacityProperty,fade);
        }
    }
}
