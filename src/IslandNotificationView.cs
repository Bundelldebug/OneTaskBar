using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    // Reuses the notification already accepted by the normal popup gate.
    // No history reads, extra listener or background polling belong here.
    internal sealed class IslandNotificationView : Grid
    {
        readonly Image icon=new Image {Width=36,Height=36,Stretch=Stretch.Uniform,Margin=new Thickness(0,2,12,0)};
        readonly TextBlock app=Paint.TextBlock("",11,Paint.ControlWhite);
        readonly TextBlock title=Paint.TextBlock("",14,Paint.ControlWhite);
        readonly TextBlock text=Paint.TextBlock("",12,Paint.ControlWhite);
        internal readonly Button OpenButton;
        internal NotificationEntry Entry {get;private set;}
        internal Action<NotificationEntry> Open;

        internal IslandNotificationView(Action dismiss)
        {
            RowDefinitions.Add(new RowDefinition {Height=new GridLength(28)});
            RowDefinitions.Add(new RowDefinition());
            var fold=Paint.Button(new Glyph("chevron",14)
            {
                Color=Paint.ControlWhite,RenderTransform=new RotateTransform(-90),
                RenderTransformOrigin=new Point(.5,.5)
            },dismiss,"알림 접기");
            fold.Background=Brushes.Transparent;
            fold.Width=28;fold.Height=22;
            fold.Padding=new Thickness(0);
            fold.Focusable=false;
            fold.HorizontalAlignment=HorizontalAlignment.Right;
            fold.Margin=new Thickness(0,3,12,0);
            Children.Add(fold);
            var card=new Grid {Margin=new Thickness(16,12,16,12)};
            card.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            card.ColumnDefinitions.Add(new ColumnDefinition());
            icon.VerticalAlignment=VerticalAlignment.Center;
            card.Children.Add(icon);
            var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            app.Opacity=.8;
            app.TextTrimming=title.TextTrimming=TextTrimming.CharacterEllipsis;
            title.FontWeight=FontWeights.SemiBold;
            title.Margin=new Thickness(0,3,0,3);
            text.Opacity=.9;text.TextWrapping=TextWrapping.Wrap;text.MaxHeight=36;
            words.Children.Add(app);words.Children.Add(title);words.Children.Add(text);
            Grid.SetColumn(words,1);card.Children.Add(words);
            OpenButton=Paint.Button(card,()=>{var entry=Entry;if(entry!=null&&Open!=null)Open(entry);},"알림 열기");
            OpenButton.Padding=new Thickness(0);
            OpenButton.Focusable=false;
            OpenButton.Background=Brushes.Transparent;
            OpenButton.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            OpenButton.VerticalContentAlignment=VerticalAlignment.Stretch;
            var surface=new Border
            {
                Background=Brushes.Transparent,Child=OpenButton
            };
            Grid.SetRow(surface,1);Children.Add(surface);
        }

        internal void Present(NotificationEntry entry)
        {
            Entry=entry;
            icon.Source=entry==null?null:entry.Icon;
            icon.Visibility=icon.Source==null?Visibility.Collapsed:Visibility.Visible;
            app.Text=entry==null?"":entry.App??"";
            title.Text=entry==null?"":entry.Title??"알림";
            text.Text=entry==null?"":entry.Body??"";
            OpenButton.ToolTip=entry==null?null:(entry.Title+"\n"+entry.Body).Trim();
        }
    }
}
