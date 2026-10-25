using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class NotificationStack
    {
        internal string Key;
        internal List<NotificationEntry> Items;
        internal NotificationEntry Latest {get{return Items[0];}}
        internal static string AppKey(NotificationEntry entry)
        {
            if(!String.IsNullOrWhiteSpace(entry.AppId))return "id:"+entry.AppId.Trim();
            if(!String.IsNullOrWhiteSpace(entry.App))return "name:"+entry.App.Trim();
            return "unknown:"+entry.Id;
        }
        internal static string ItemKey(NotificationEntry entry){return AppKey(entry)+"|"+entry.Id;}
        internal static List<NotificationStack> Group(IEnumerable<NotificationEntry> entries)
        {
            return entries.Where(n=>n!=null).GroupBy(AppKey,StringComparer.OrdinalIgnoreCase)
                .Select(g=>new NotificationStack {Key=g.Key,Items=g.GroupBy(n=>n.Id).Select(n=>n.OrderByDescending(i=>i.Created).First()).OrderByDescending(n=>n.Created).ThenByDescending(n=>n.Id).ToList()})
                .OrderByDescending(g=>g.Latest.Created).ThenBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    // One capsule shows the latest item and its count. Expanding preserves the
    // original per-notification actions and runs only a short layout transition.
    internal sealed class NotificationStackView : Grid
    {
        readonly Border front=new GlassTileBorder();
        readonly StackPanel content=new StackPanel(),items=new StackPanel();
        readonly Grid header=new Grid();
        readonly TextBlock label=Paint.TextBlock("",11,Paint.Muted);
        readonly Glyph arrow=new Glyph("chevron",13);
        readonly Button expand;Button collapsedButton;
        readonly Dictionary<string,Border> retained=new Dictionary<string,Border>();
        readonly HashSet<string> fullText=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,Button> detailButtons=new Dictionary<string,Button>(StringComparer.OrdinalIgnoreCase);
        readonly Action<NotificationEntry> open,dismiss;
        readonly Action<NotificationStack> clear;
        NotificationStack stack;
        string signature;
        internal bool Expanded {get;private set;}
        internal int Count {get{return stack.Items.Count;}}
        internal string AppKey {get{return stack.Key;}}
        internal Button ExpandButton {get{return !Expanded&&Count>1?collapsedButton:expand;}}
        internal IEnumerable<Border> RetainedCards {get{return retained.Values;}}
        internal static ImageSource CardIcon(NotificationEntry entry){return BudsInfo.Toast(entry)==null?entry.Icon:null;}
        internal bool DetailExpanded(NotificationEntry entry){return fullText.Contains(NotificationStack.ItemKey(entry));}
        internal Button DetailButton(NotificationEntry entry){Button button;return detailButtons.TryGetValue(NotificationStack.ItemKey(entry),out button)?button:null;}
        internal void SetDetail(NotificationEntry entry,bool expanded){string key=NotificationStack.ItemKey(entry);if(expanded)fullText.Add(key);else fullText.Remove(key);Rebuild(true);}
        internal NotificationStackView(NotificationStack group,Action<NotificationEntry> onOpen,Action<NotificationEntry> onDismiss,Action<NotificationStack> onClear)
        {
            open=onOpen;dismiss=onDismiss;clear=onClear;Margin=new Thickness(0,4,0,7);
            front.CornerRadius=new CornerRadius(32);front.Background=QuickPanel.Card;Children.Add(front);front.Child=content;
            header.Margin=new Thickness(13,8,8,0);header.Height=28;
            var heading=new StackPanel {Orientation=Orientation.Horizontal};label.VerticalAlignment=VerticalAlignment.Center;heading.Children.Add(label);arrow.Margin=new Thickness(7,0,0,0);heading.Children.Add(arrow);
            expand=Paint.Button(heading,()=>SetExpanded(!Expanded),"앱 알림 펼치기 · 접기");expand.Background=Brushes.Transparent;expand.Padding=new Thickness(0);expand.HorizontalContentAlignment=HorizontalAlignment.Left;expand.HorizontalAlignment=HorizontalAlignment.Stretch;expand.Margin=new Thickness(0,0,30,0);header.Children.Add(expand);
            var close=Paint.Button(new Glyph("close",12){Color=Paint.Muted},()=>clear(stack),"이 앱의 알림 모두 지우기");close.Background=Brushes.Transparent;close.Width=close.Height=26;close.Padding=new Thickness(6);close.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(close);
            content.Children.Add(header);content.Children.Add(items);Update(group,false);
        }
        internal void Update(NotificationStack group,bool animate=true)
        {
            stack=group;fullText.RemoveWhere(k=>!group.Items.Any(n=>NotificationStack.ItemKey(n)==k));string next=String.Join("\n",group.Items.Select(n=>NotificationStack.ItemKey(n)+"|"+Version(n)));if(next==signature)return;signature=next;if(Count<2)Expanded=false;Rebuild(animate);
        }
        internal void SetExpanded(bool value)
        {
            bool next=value&&Count>1;if(next==Expanded)return;Expanded=next;Rebuild(true);
        }
        void Rebuild(bool animate)
        {
            double before=ActualHeight;BeginAnimation(HeightProperty,null);Height=Double.NaN;ClipToBounds=false;
            bool grouped=Count>1;header.Visibility=grouped&&Expanded?Visibility.Visible:Visibility.Collapsed;
            label.Text=stack.Latest.App+"  ·  "+Count+"개";expand.ToolTip=Expanded?"알림 접기":"알림 "+Count+"개 펼치기";
            System.Windows.Automation.AutomationProperties.SetName(expand,expand.ToolTip.ToString());System.Windows.Automation.AutomationProperties.SetItemStatus(expand,Expanded?"펼쳐짐":"접힘");
            arrow.RenderTransformOrigin=new Point(.5,.5);arrow.RenderTransform=new RotateTransform(Expanded?-90:90);
            var visible=(Expanded?stack.Items:stack.Items.Take(1)).ToList();var keys=new HashSet<string>(visible.Select(NotificationStack.ItemKey),StringComparer.OrdinalIgnoreCase);
            foreach(string key in retained.Keys.Where(k=>!keys.Contains(k)).ToArray()){items.Children.Remove(retained[key]);retained.Remove(key);detailButtons.Remove(key);}
            int index=0;foreach(var entry in visible){string key=NotificationStack.ItemKey(entry),version=CardVersion(entry,grouped);Border card;
                if(!retained.TryGetValue(key,out card)||(string)card.Tag!=version){if(card!=null)items.Children.Remove(card);card=Card(entry,grouped);retained[key]=card;}
                int old=items.Children.IndexOf(card);if(old!=index){if(old>=0)items.Children.Remove(card);items.Children.Insert(Math.Min(index,items.Children.Count),card);}index++;
            }
            if(!animate||!IsVisible||!Motion.Enabled||before<1||ActualWidth<1)return;
            Measure(new Size(ActualWidth,Double.PositiveInfinity));double target=DesiredSize.Height-Margin.Top-Margin.Bottom;
            if(target<1||Math.Abs(before-target)<.5)return;
            ClipToBounds=true;var transition=Motion.Tween(before,target,260);transition.Completed+=(s,e)=>{BeginAnimation(HeightProperty,null);Height=Double.NaN;ClipToBounds=false;};BeginAnimation(HeightProperty,transition);Motion.Fade(items,.65,1,210);
        }
        static string Version(NotificationEntry entry){return entry.App+"|"+entry.Title+"|"+entry.Body+"|"+entry.Created.UtcDateTime.Ticks+"|"+(entry.Icon!=null?entry.Icon.GetHashCode():0)+"|"+(entry.Artwork!=null?entry.Artwork.GetHashCode():0);}
        string CardVersion(NotificationEntry entry,bool grouped){return Version(entry)+"|"+grouped+"|"+(Expanded?"expanded":"capsule:"+Count)+"|"+DetailExpanded(entry);}
        Border Card(NotificationEntry entry,bool grouped)
        {
            if(!Expanded&&!DetailExpanded(entry))return Capsule(entry,grouped);
            var row=new StackPanel();
            var app=Paint.TextBlock(grouped?entry.Created.LocalDateTime.ToString("H:mm"):entry.App+"  ·  "+entry.Created.LocalDateTime.ToString("H:mm"),10,Paint.Muted);app.TextTrimming=TextTrimming.CharacterEllipsis;row.Children.Add(app);
            bool full=DetailExpanded(entry);var title=Paint.TextBlock(entry.Title,14,Paint.Text);title.FontWeight=FontWeights.SemiBold;title.TextWrapping=TextWrapping.Wrap;title.MaxHeight=full?Double.PositiveInfinity:44;title.TextTrimming=full?TextTrimming.None:TextTrimming.CharacterEllipsis;title.Margin=new Thickness(0,7,0,4);row.Children.Add(title);
            TextBlock body=null;if(!String.IsNullOrEmpty(entry.Body)){body=Paint.TextBlock(entry.Body,12,Paint.Text);body.TextWrapping=TextWrapping.Wrap;body.MaxHeight=full?Double.PositiveInfinity:90;body.TextTrimming=full?TextTrimming.None:TextTrimming.CharacterEllipsis;row.Children.Add(body);}
            if(entry.Artwork!=null)row.Children.Add(Picture(entry.Artwork,full?240:130));
            var source=CardIcon(entry);var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(source!=null?40:0)});layout.ColumnDefinitions.Add(new ColumnDefinition());
            if(source!=null){var icon=AppIcons.Visual(source,32);icon.VerticalAlignment=VerticalAlignment.Top;icon.Margin=new Thickness(0,3,6,0);layout.Children.Add(icon);}Grid.SetColumn(row,1);layout.Children.Add(row);
            var hit=Paint.Button(layout,()=>open(entry),entry.App+" · 원래 알림 동작 열기");hit.Tag=entry.Id;hit.HorizontalContentAlignment=HorizontalAlignment.Stretch;hit.Background=Brushes.Transparent;hit.Padding=new Thickness(13,grouped?8:13,35,14);
            var frame=new Grid();frame.Children.Add(hit);var close=Paint.Button(new Glyph("close",12){Color=Paint.Muted},()=>dismiss(entry),"알림 지우기");close.Width=close.Height=24;close.Padding=new Thickness(5);close.Background=Brushes.Transparent;close.HorizontalAlignment=HorizontalAlignment.Right;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new Thickness(0,5,8,0);frame.Children.Add(close);
            var more=DetailToggle(entry,title,body,false);more.VerticalAlignment=VerticalAlignment.Bottom;more.Margin=new Thickness(0,0,7,5);frame.Children.Add(more);hit.Padding=new Thickness(13,grouped?8:13,38,31);
            return new Border {Tag=CardVersion(entry,grouped),Child=frame,Background=Brushes.Transparent,CornerRadius=new CornerRadius(22)};
        }
        Border Capsule(NotificationEntry entry,bool grouped)
        {
            var source=CardIcon(entry);var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(source!=null?53:0)});layout.ColumnDefinitions.Add(new ColumnDefinition());layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(grouped?28:12)});
            if(source!=null){var icon=AppIcons.Visual(source,42);icon.VerticalAlignment=VerticalAlignment.Center;icon.HorizontalAlignment=HorizontalAlignment.Left;layout.Children.Add(icon);}
            var text=new StackPanel {VerticalAlignment=VerticalAlignment.Center};var heading=new DockPanel();var time=Paint.TextBlock(entry.Created.LocalDateTime.ToString("H:mm"),10,Paint.Muted);time.Margin=new Thickness(8,1,0,0);DockPanel.SetDock(time,Dock.Right);heading.Children.Add(time);var title=Paint.TextBlock(entry.Title,14,Paint.Text);title.FontWeight=FontWeights.SemiBold;title.TextTrimming=TextTrimming.CharacterEllipsis;heading.Children.Add(title);text.Children.Add(heading);
            TextBlock body=null;if(!String.IsNullOrWhiteSpace(entry.Body)){body=Paint.TextBlock(entry.Body,12,Paint.Text);body.TextWrapping=TextWrapping.Wrap;body.MaxHeight=38;body.TextTrimming=TextTrimming.CharacterEllipsis;body.Margin=new Thickness(0,4,0,0);text.Children.Add(body);}Grid.SetColumn(text,1);layout.Children.Add(text);
            if(grouped){var count=Paint.TextBlock(Count.ToString(),18,Paint.Muted);count.HorizontalAlignment=HorizontalAlignment.Right;count.Margin=new Thickness(5,0,0,0);Grid.SetColumn(count,2);layout.Children.Add(count);}
            var summary=new StackPanel();summary.Children.Add(layout);if(entry.Artwork!=null)summary.Children.Add(Picture(entry.Artwork,112));
            var hit=Paint.Button(summary,()=>{if(grouped)SetExpanded(true);else open(entry);},grouped?entry.App+" · 알림 "+Count+"개 펼치기":entry.App+" · 원래 알림 동작 열기");hit.Tag=entry.Id;hit.Background=Brushes.Transparent;hit.HorizontalContentAlignment=HorizontalAlignment.Stretch;hit.Padding=new Thickness(16,17,grouped?17:39,17);hit.MinHeight=80;
            var frame=new Grid();frame.Children.Add(hit);if(grouped){collapsedButton=hit;var menu=new ContextMenu();var clearAll=new MenuItem {Header="이 앱의 알림 모두 지우기"};clearAll.Click+=(s,e)=>clear(stack);menu.Items.Add(clearAll);hit.ContextMenu=menu;}
            else{var close=Paint.Button(new Glyph("close",11){Color=Paint.Muted},()=>dismiss(entry),"알림 지우기");close.Background=Brushes.Transparent;close.Width=close.Height=22;close.Padding=new Thickness(5);close.HorizontalAlignment=HorizontalAlignment.Right;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new Thickness(0,5,6,0);frame.Children.Add(close);var more=DetailToggle(entry,title,body,true);more.VerticalAlignment=VerticalAlignment.Top;more.Margin=new Thickness(0,31,6,0);frame.Children.Add(more);}
            return new Border {Tag=CardVersion(entry,grouped),Child=frame,Background=Brushes.Transparent,CornerRadius=new CornerRadius(32)};
        }
        static Image Picture(ImageSource source,double height){return new Image {Source=source,Height=height,Stretch=Stretch.Uniform,Margin=new Thickness(0,9,0,0)};}
        Button DetailToggle(NotificationEntry entry,TextBlock title,TextBlock body,bool singleLine)
        {
            bool full=DetailExpanded(entry);var glyph=new Glyph("chevron",13){RenderTransformOrigin=new Point(.5,.5),RenderTransform=new RotateTransform(full?-90:90)};
            var button=Paint.Button(glyph,()=>SetDetail(entry,!DetailExpanded(entry)),full?"알림 내용 접기":"알림 내용 모두 펼치기");button.Background=Brushes.Transparent;button.Width=button.Height=27;button.Padding=new Thickness(6);button.HorizontalAlignment=HorizontalAlignment.Right;
            System.Windows.Automation.AutomationProperties.SetName(button,full?"알림 내용 접기":"알림 내용 모두 펼치기");detailButtons[NotificationStack.ItemKey(entry)]=button;
            Action update=()=>button.Visibility=full||entry.Artwork!=null||Clipped(title,singleLine)||body!=null&&Clipped(body,false)?Visibility.Visible:Visibility.Collapsed;
            button.Loaded+=(s,e)=>update();title.SizeChanged+=(s,e)=>update();if(body!=null)body.SizeChanged+=(s,e)=>update();return button;
        }
        static bool Clipped(TextBlock text,bool singleLine)
        {
            if(String.IsNullOrEmpty(text.Text)||text.ActualWidth<1)return false;
            var formatted=new FormattedText(text.Text,System.Globalization.CultureInfo.CurrentUICulture,text.FlowDirection,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,text.Foreground,VisualTreeHelper.GetDpi(text).PixelsPerDip);
            if(singleLine)return formatted.WidthIncludingTrailingWhitespace>text.ActualWidth+.5;
            formatted.MaxTextWidth=Math.Max(1,text.ActualWidth);return formatted.Height>Math.Min(text.MaxHeight,text.ActualHeight)+.5;
        }
    }
}
