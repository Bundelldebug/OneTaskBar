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
        readonly Dictionary<string,NotificationStackView> retained=new Dictionary<string,NotificationStackView>(StringComparer.OrdinalIgnoreCase);string signature;bool opening,disturbBusy;
        List<NotificationEntry> renderedItems;bool renderedAllowed;long clockSecond=-1;DateTime renderedDate;
        internal int CardCount {get{return retained.Count;}}
        internal IEnumerable<NotificationStackView> Stacks {get{return retained.Values;}}
        internal NotificationWindow(ShellController controller)
        {
            shell=controller;Title="One TaskBar · 알림";Width=394;Height=584;Radius=30;Dark=true;Surface.CornerRadius=new CornerRadius(30);Surface.Background=QuickPanel.Glass;Surface.BorderBrush=Paint.Brush("#48FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var root=new Grid {Margin=new Thickness(20,22,20,16)};root.RowDefinitions.Add(new RowDefinition {Height=new GridLength(79)});root.RowDefinitions.Add(new RowDefinition());
            var header=new Grid {Margin=new Thickness(5,0,3,13)};var heading=new StackPanel();clock.FontWeight=FontWeights.SemiBold;heading.Children.Add(clock);heading.Children.Add(today);var calendar=Paint.Button(heading,()=>shell.ToggleCalendar(this),"달력 열기");calendar.Background=Brushes.Transparent;calendar.Padding=new Thickness(0);calendar.HorizontalAlignment=HorizontalAlignment.Left;header.Children.Add(calendar);
            disturb=Paint.Button(disturbGlyph,()=>ToggleDisturb(),"방해 금지");disturb.Width=disturb.Height=42;disturb.Padding=new Thickness(0);disturb.Resources["ButtonRadius"]=new CornerRadius(21);disturb.HorizontalAlignment=HorizontalAlignment.Right;disturb.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(disturb);root.Children.Add(header);
            var notifications=new DockPanel();var labelRow=new Grid {Height=39,Margin=new Thickness(6,0,3,7)};count.FontWeight=FontWeights.SemiBold;count.VerticalAlignment=VerticalAlignment.Center;labelRow.Children.Add(count);
            clear=Paint.Action("모두 지우기",()=> {shell.Notifications.Clear();RefreshNotifications();});clear.HorizontalAlignment=HorizontalAlignment.Right;clear.Padding=new Thickness(12,7,12,7);clear.FontSize=11;labelRow.Children.Add(clear);DockPanel.SetDock(labelRow,Dock.Top);notifications.Children.Add(labelRow);
            access=Paint.Action("Windows 알림 연결",()=>RequestAccess());access.HorizontalAlignment=HorizontalAlignment.Stretch;access.Height=40;access.Margin=new Thickness(0,8,0,0);DockPanel.SetDock(access,Dock.Bottom);notifications.Children.Add(access);var quickSettings=Paint.Action("빠른 설정  ›",shell.QuickFromNotifications);quickSettings.Height=36;quickSettings.Margin=new Thickness(0,8,0,0);DockPanel.SetDock(quickSettings,Dock.Bottom);notifications.Children.Add(quickSettings);
            notifications.Children.Add(new SmoothScrollViewer {Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.VerticalFirst});Grid.SetRow(notifications,1);root.Children.Add(notifications);Surface.Child=root;StableSurface=true;TransitionContent=root;
            Deactivated+=(s,e)=> {if(!Program.Inspectable&&IsVisible&&!IsMouseOver)Hide();};
        }
        async void ToggleDisturb()
        {
            if(disturbBusy)return;disturbBusy=true;disturb.IsEnabled=false;try{await shell.Toggles.Disturb.Toggle();}catch(Exception error){disturb.ToolTip=error.Message;}finally{disturbBusy=false;Refresh();}
        }
        void RequestAccess(){shell.ShowNotificationConnection();}
        internal async void RefreshNotifications() {if(!shell.Demo)shell.Notifications.Invalidate();await shell.Notifications.Refresh();signature=null;if(IsVisible)Refresh();}
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
            var now=DateTime.Now;long second=now.Ticks/TimeSpan.TicksPerSecond;if(second!=clockSecond){clockSecond=second;clock.Text=now.ToString("H:mm:ss");}if(now.Date!=renderedDate){renderedDate=now.Date;today.Text=now.ToString("M월 d일 dddd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));}
            var dnd=shell.Toggles.Disturb.State;disturb.IsEnabled=dnd.Available&&!disturbBusy;disturb.Background=dnd.Enabled?Paint.ControlWhite:QuickPanel.Card;disturbGlyph.Color=dnd.Enabled?Paint.Brush("#FF26312D"):Paint.Text;disturbGlyph.InvalidateVisual();System.Windows.Automation.AutomationProperties.SetItemStatus(disturb,dnd.Enabled?"켜짐":"꺼짐");
            var service=shell.Notifications;access.Visibility=service.Allowed?Visibility.Collapsed:Visibility.Visible;count.Text=service.Allowed&&service.Items.Count>0?"알림  "+service.Items.Count:"알림";clear.IsEnabled=service.Allowed&&service.Items.Count>0;clear.Opacity=clear.IsEnabled?1:.4;
            var accessText=access.Content as TextBlock;if(accessText!=null)accessText.Text="Windows 알림 연결";
            if(!shell.Demo&&signature!=null&&Object.ReferenceEquals(renderedItems,service.Items)&&renderedAllowed==service.Allowed)return;renderedItems=service.Items;renderedAllowed=service.Allowed;
            string current=service.Allowed+"|"+String.Join("|",service.Items.Select(a=>NotificationStack.ItemKey(a)+":"+a.App+":"+a.Title+":"+a.Body+":"+a.Created.UtcDateTime.Ticks+":"+(a.Icon!=null?a.Icon.GetHashCode():0)+":"+(a.Artwork!=null?a.Artwork.GetHashCode():0)));if(current==signature)return;signature=current;
            var items=NotificationStack.Group(service.Allowed?service.Items:new List<NotificationEntry>());var keys=new HashSet<string>(items.Select(g=>g.Key),StringComparer.OrdinalIgnoreCase);foreach(string key in retained.Keys.Where(k=>!keys.Contains(k)).ToArray()){var removed=retained[key];retained.Remove(key);RemoveCard(removed);}
            foreach(var empty in cards.Children.OfType<Border>().Where(b=>b.Tag as string=="empty").ToArray())cards.Children.Remove(empty);
            if(!service.Allowed||service.Items.Count==0){var content=new StackPanel {Margin=new Thickness(20,32,20,34)};var empty=Paint.TextBlock(service.Allowed?"알림이 없습니다.":"Windows 알림 연결",15,Paint.Text);empty.FontWeight=FontWeights.SemiBold;empty.TextAlignment=TextAlignment.Center;content.Children.Add(empty);if(!service.Allowed){var status=Paint.TextBlock(service.Status,12,Paint.Muted);status.TextAlignment=TextAlignment.Center;status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,9,0,0);content.Children.Add(status);}cards.Children.Add(new Border {Tag="empty",Background=Brushes.Transparent,Margin=new Thickness(0,4,0,6),Child=content});return;}

            int index=0;foreach(var group in items){NotificationStackView card;bool added=!retained.TryGetValue(group.Key,out card);
                if(added){card=new NotificationStackView(group,OpenNotification,item=>{service.Dismiss(item.Id);RefreshNotifications();},stack=>{foreach(var item in stack.Items)service.Dismiss(item.Id);RefreshNotifications();});retained[group.Key]=card;}else card.Update(group);
                int old=cards.Children.IndexOf(card);if(old!=index){if(old>=0)cards.Children.Remove(card);cards.Children.Insert(Math.Min(index,cards.Children.Count),card);}index++;
                if(added&&IsVisible&&Motion.Enabled){Motion.Fade(card,0,1,180);var shift=new TranslateTransform(0,10);card.RenderTransform=shift;shift.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(10,0,240));}
            }
        }
        void RemoveCard(FrameworkElement card)
        {
            if(!IsVisible||!Motion.Enabled){cards.Children.Remove(card);return;}card.IsHitTestVisible=false;double height=card.ActualHeight;card.Height=0;card.ClipToBounds=true;card.BeginAnimation(HeightProperty,Motion.Tween(height,0,180));var fade=Motion.Tween(card.Opacity,0,150);card.Opacity=0;fade.Completed+=(s,e)=>cards.Children.Remove(card);card.BeginAnimation(OpacityProperty,fade);
        }
    }
}
