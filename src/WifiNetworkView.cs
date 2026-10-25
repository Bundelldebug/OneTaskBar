using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace OneTaskBar
{
    internal static class WifiNetworkView
    {
        internal static Button Create(WifiNetwork network,Action select)
        {
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(49)});row.ColumnDefinitions.Add(new ColumnDefinition());
            var glyph=new Glyph("wifi",29) {Generation=network.Generation,Signal=network.Signal,Color=network.Connected?Paint.OnLight:Paint.Text};var badge=new Border {Width=39,Height=39,CornerRadius=new CornerRadius(20),Background=network.Connected?Paint.ControlWhite:Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Left,Child=glyph};row.Children.Add(badge);
            string state=(network.Connected?"연결됨 · ":"")+(network.Generation>=4?"Wi-Fi "+network.Generation:"Wi-Fi")+" · "+(network.Secure?"보안":"공개");var text=new StackPanel {VerticalAlignment=VerticalAlignment.Center};var title=Paint.TextBlock(network.Name,14,Paint.Text);title.FontWeight=network.Connected?FontWeights.SemiBold:FontWeights.Normal;title.TextTrimming=TextTrimming.CharacterEllipsis;text.Children.Add(title);var detail=Paint.TextBlock(state,11,Paint.Muted);detail.Margin=new Thickness(0,3,0,0);text.Children.Add(detail);Grid.SetColumn(text,1);row.Children.Add(text);
            var button=Paint.Button(row,select,network.Name+" · "+state+" · 신호 "+network.Signal+"%");button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(12,10,14,10);button.Margin=new Thickness(0,4,0,4);button.Background=QuickPanel.Card;System.Windows.Automation.AutomationProperties.SetItemStatus(button,state);return button;
        }
    }
}
