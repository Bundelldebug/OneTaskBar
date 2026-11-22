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
        readonly MediaPlayerView media;
        readonly Border mediaCard;
        internal bool MediaVisible {get{return mediaCard.Visibility==Visibility.Visible;}}
        readonly TextBlock clock=Paint.TextBlock("",28,Paint.Text),today=Paint.TextBlock("",12,Paint.Muted),count=Paint.TextBlock("알림",17,Paint.Text);
        readonly Button access,clear,disturb,quickSettings;readonly Glyph disturbGlyph=new Glyph("dnd",20);
        TextBlock emptyMessage,emptyStatus;
        readonly Border dndNote=new Border {CornerRadius=new CornerRadius(13),Padding=new Thickness(11,4,11,4),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,9),Visibility=Visibility.Collapsed};
        Button noticeSettings;
        FrameworkElement[] lateParts;internal override FrameworkElement[] LateParts {get{return lateParts;}}
        // The Dex center: one line of time and date, the do-not-disturb note, the cards, and settings and clear at the foot.
        void DexLayout(Grid root,Grid header,StackPanel heading,Grid labelRow,DockPanel notifications)
        {
            Radius=Dex.PanelRadius;Surface.CornerRadius=new CornerRadius(Radius);
            root.Margin=new Thickness(14,14,14,14);root.RowDefinitions[0].Height=new GridLength(34);
            header.Margin=new Thickness(6,0,6,6);heading.Orientation=Orientation.Horizontal;
            clock.FontSize=18;today.FontSize=12;today.VerticalAlignment=VerticalAlignment.Bottom;today.Margin=new Thickness(7,0,0,2);
            disturb.Visibility=Visibility.Collapsed;labelRow.Visibility=Visibility.Collapsed;quickSettings.Visibility=Visibility.Collapsed;
            labelRow.Children.Remove(clear);
            var note=new StackPanel {Orientation=Orientation.Horizontal};note.Children.Add(new Glyph("dnd",12){Color=Paint.ControlWhite,Margin=new Thickness(0,0,5,0),VerticalAlignment=VerticalAlignment.Center});note.Children.Add(Paint.TextBlock("방해 금지 모드가 켜져 있습니다.",11,Paint.ControlWhite));
            dndNote.Child=note;dndNote.Background=Dex.NoticeDndPill;DockPanel.SetDock(dndNote,Dock.Top);notifications.Children.Insert(0,dndNote);
            var foot=new Grid {Margin=new Thickness(2,10,2,0)};
            noticeSettings=Paint.Action("알림 설정",()=>OpenNoticeSettings());noticeSettings.HorizontalAlignment=HorizontalAlignment.Left;noticeSettings.Padding=new Thickness(14,6,14,6);noticeSettings.Background=Dex.NoticeFootButton;noticeSettings.Resources["ButtonRadius"]=new CornerRadius(16);((TextBlock)noticeSettings.Content).Foreground=Paint.ControlWhite;((TextBlock)noticeSettings.Content).FontSize=11.5;foot.Children.Add(noticeSettings);
            ((TextBlock)clear.Content).Text="지우기";((TextBlock)clear.Content).FontSize=11.5;clear.Padding=new Thickness(14,6,14,6);clear.Background=Dex.NoticeFootButton;clear.Resources["ButtonRadius"]=new CornerRadius(16);foot.Children.Add(clear);
            DockPanel.SetDock(foot,Dock.Bottom);notifications.Children.Insert(1,foot);
            lateParts=new FrameworkElement[]{header,dndNote,foot};
        }
        async void OpenNoticeSettings(){if(await DismissAsync())Native.Open("ms-settings:notifications");}
        internal TextBlock ClockLabel {get{return clock;}}
        internal TextBlock DateLabel {get{return today;}}
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
            access=Paint.Action("Windows 알림 연결",()=>RequestAccess());access.HorizontalAlignment=HorizontalAlignment.Stretch;access.Height=40;access.Margin=new Thickness(0,8,0,0);DockPanel.SetDock(access,Dock.Bottom);notifications.Children.Add(access);quickSettings=Paint.Action("빠른 설정  ›",shell.QuickFromNotifications);quickSettings.Height=36;quickSettings.Margin=new Thickness(0,8,0,0);DockPanel.SetDock(quickSettings,Dock.Bottom);notifications.Children.Add(quickSettings);
            var feed=new StackPanel();media=new MediaPlayerView(shell.Media,shell.SeekMedia,()=>shell.ShowDetail("sound"),condensed:true,openApp:shell.OpenMediaApplication,dense:true,sourceApp:id=>MediaApplication.Resolve(id,shell.Running.Concat(shell.Apps)));media.SeekBar.WaveInterval=.033;mediaCard=new Border {Child=media,CornerRadius=new CornerRadius(28),Margin=new Thickness(0,0,0,12),Visibility=Visibility.Collapsed};media.PaletteChanged=palette=>mediaCard.Background=media.MediaBackground;mediaCard.Height=130;media.FitWidget(344,130);mediaCard.SizeChanged+=(s,e)=>media.FitWidget(mediaCard.ActualWidth,130);feed.Children.Add(mediaCard);feed.Children.Add(cards);
            notifications.Children.Add(new SmoothScrollViewer {Content=feed,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.VerticalFirst});Grid.SetRow(notifications,1);root.Children.Add(notifications);Surface.Child=root;StableSurface=true;TransitionContent=root;
            if(Dex.Enabled)DexLayout(root,header,heading,labelRow,notifications);
            RefreshChrome();
            Deactivated+=(s,e)=> {if(!Program.Inspectable&&IsVisible&&!IsMouseOver)Hide();};
        }
        internal void RefreshChrome()
        {
            clock.Foreground=count.Foreground=Paint.Text;
            today.Foreground=Paint.Muted;
            if(emptyMessage!=null)emptyMessage.Foreground=Paint.Text;
            if(emptyStatus!=null)emptyStatus.Foreground=Paint.Muted;
            foreach(var button in new[]{clear,access,quickSettings,noticeSettings})
            {
                if(button==null)continue;
                var text=button.Content as TextBlock;
                if(text!=null)text.Foreground=Dex.Enabled&&(button==clear||button==noticeSettings)?Paint.ControlWhite:Paint.Text;
                button.Background=Dex.Enabled&&(button==clear||button==noticeSettings)?Dex.NoticeFootButton:QuickPanel.Card;
            }
            if(Dex.Enabled)dndNote.Visibility=shell.Toggles.Disturb.State.Enabled?Visibility.Visible:Visibility.Collapsed;
            if(disturb!=null&&!disturbBusy)
            {
                bool enabled=shell.Toggles.Disturb.State.Enabled;
                disturb.Background=enabled?Paint.ControlWhite:QuickPanel.Card;
                disturbGlyph.Color=enabled?Paint.OnLight:Paint.Text;
                disturbGlyph.InvalidateVisual();
            }
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
            RefreshChrome();
            var playing=shell.Services.Media??new MediaState();bool showMedia=!Dex.Enabled&&playing.Active;mediaCard.Visibility=showMedia?Visibility.Visible:Visibility.Collapsed;if(showMedia){media.Update(playing,shell.Services.OutputName);mediaCard.Background=media.MediaBackground;}
            var now=DateTime.Now;long second=now.Ticks/TimeSpan.TicksPerSecond;if(second!=clockSecond){clockSecond=second;clock.Text=Dex.Enabled?now.ToString("h:mm",System.Globalization.CultureInfo.InvariantCulture):now.ToString("H:mm:ss");}if(now.Date!=renderedDate){renderedDate=now.Date;today.Text=Loc.Date(now,"M월 d일 dddd");}
            var dnd=shell.Toggles.Disturb.State;disturb.IsEnabled=dnd.Available&&!disturbBusy;System.Windows.Automation.AutomationProperties.SetItemStatus(disturb,dnd.Enabled?"켜짐":"꺼짐");
            var service=shell.Notifications;access.Visibility=service.Allowed?Visibility.Collapsed:Visibility.Visible;count.Text=service.Allowed&&service.Items.Count>0?"알림  "+service.Items.Count:"알림";clear.IsEnabled=service.Allowed&&service.Items.Count>0;clear.Opacity=clear.IsEnabled?1:.4;
            var accessText=access.Content as TextBlock;if(accessText!=null)accessText.Text="Windows 알림 연결";
            if(!shell.Demo&&signature!=null&&Object.ReferenceEquals(renderedItems,service.Items)&&renderedAllowed==service.Allowed)return;renderedItems=service.Items;renderedAllowed=service.Allowed;
            string current=service.Allowed+"|"+String.Join("|",service.Items.Select(a=>NotificationStack.ItemKey(a)+":"+a.App+":"+a.Title+":"+a.Body+":"+a.Created.UtcDateTime.Ticks+":"+(a.Icon!=null?a.Icon.GetHashCode():0)+":"+(a.Artwork!=null?a.Artwork.GetHashCode():0)));if(current==signature)return;signature=current;
            var items=NotificationStack.Group(service.Allowed?service.Items:new List<NotificationEntry>());var keys=new HashSet<string>(items.Select(g=>g.Key),StringComparer.OrdinalIgnoreCase);foreach(string key in retained.Keys.Where(k=>!keys.Contains(k)).ToArray()){var removed=retained[key];retained.Remove(key);RemoveCard(removed);}
            foreach(var empty in cards.Children.OfType<Border>().Where(b=>b.Tag as string=="empty").ToArray())cards.Children.Remove(empty);
            emptyMessage=emptyStatus=null;
            if(!service.Allowed||service.Items.Count==0)
            {
                var content=new StackPanel {Margin=new Thickness(20,32,20,34)};
                emptyMessage=Paint.TextBlock(service.Allowed?"알림이 없습니다.":"Windows 알림 연결",15,Paint.Text);
                emptyMessage.FontWeight=FontWeights.SemiBold;
                emptyMessage.TextAlignment=TextAlignment.Center;
                content.Children.Add(emptyMessage);
                if(!service.Allowed)
                {
                    emptyStatus=Paint.TextBlock(service.Status,12,Paint.Muted);
                    emptyStatus.TextAlignment=TextAlignment.Center;
                    emptyStatus.TextWrapping=TextWrapping.Wrap;
                    emptyStatus.Margin=new Thickness(0,9,0,0);
                    content.Children.Add(emptyStatus);
                }
                cards.Children.Add(new Border {Tag="empty",Background=Brushes.Transparent,Margin=new Thickness(0,4,0,6),Child=content});
                return;
            }

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
