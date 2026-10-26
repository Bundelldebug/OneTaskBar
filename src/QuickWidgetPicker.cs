using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace OneTaskBar
{
    internal sealed class QuickWidgetPicker : DockPanel
    {
        readonly UniformGrid items=new UniformGrid {Columns=4};readonly List<KeyValuePair<string,Button>> buttons=new List<KeyValuePair<string,Button>>();
        internal QuickWidgetPicker(IEnumerable<QuickWidgetSlot> available,Func<string,string> title,Action<string> choose)
        {
            Width=320;LastChildFill=true;var heading=Paint.TextBlock("항목 추가",17,Paint.Text);heading.FontWeight=FontWeights.SemiBold;heading.Margin=new Thickness(8,5,8,12);DockPanel.SetDock(heading,Dock.Top);Children.Add(heading);var search=new TextBox {FontFamily=SystemFonts.MessageFontFamily,FontSize=13,Foreground=Paint.OnLight,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0,7,0,7),ToolTip="항목 검색"};System.Windows.Automation.AutomationProperties.SetName(search,"추가할 항목 검색");var searchBody=new Grid {Margin=new Thickness(11,0,11,0)};searchBody.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(25)});searchBody.ColumnDefinitions.Add(new ColumnDefinition());searchBody.Children.Add(new Glyph("search",17){Color=Paint.OnLight,VerticalAlignment=VerticalAlignment.Center});Grid.SetColumn(search,1);searchBody.Children.Add(search);var placeholder=Paint.TextBlock("항목 검색",13,Paint.Brush("#FF586960"));placeholder.IsHitTestVisible=false;placeholder.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(placeholder,1);searchBody.Children.Add(placeholder);search.TextChanged+=(s,e)=>placeholder.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;var field=new Border {Height=38,CornerRadius=new CornerRadius(19),Background=Paint.ControlWhite,Child=searchBody,Margin=new Thickness(6,0,6,12)};DockPanel.SetDock(field,Dock.Top);Children.Add(field);
            foreach(var slot in available){string key=slot.Key,label=title(key);var spec=QuickActionCatalog.Specs.FirstOrDefault(s=>s[0]==key);string kind=spec!=null?spec[1]:key=="brightness"?"brightness":key=="volume"?"volume":key=="media"?"play":"apps";var body=new StackPanel {VerticalAlignment=VerticalAlignment.Center};body.Children.Add(new Border {Width=43,Height=43,CornerRadius=new CornerRadius(21.5),Background=Paint.ControlWhite,Child=new Glyph(kind,24){Color=Paint.OnLight,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center},HorizontalAlignment=HorizontalAlignment.Center});var name=Paint.TextBlock(label,11.5,Paint.Text);name.TextWrapping=TextWrapping.Wrap;name.TextAlignment=TextAlignment.Center;name.Height=32;name.Margin=new Thickness(1,6,1,0);body.Children.Add(name);var button=Paint.Button(body,()=>choose(key),label+" 추가");button.Tag=key;button.Padding=new Thickness(3,7,3,3);button.Margin=new Thickness(1);button.Height=96;button.Background=Brushes.Transparent;items.Children.Add(button);buttons.Add(new KeyValuePair<string,Button>(label,button));}
            var empty=Paint.TextBlock("추가할 수 있는 항목이 없습니다",13,Paint.Muted);empty.TextWrapping=TextWrapping.Wrap;empty.Margin=new Thickness(8);if(buttons.Count==0)Children.Add(empty);else Children.Add(new SmoothScrollViewer {Content=items,MaxHeight=360,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});search.TextChanged+=(s,e)=>{string query=search.Text.Trim();foreach(var item in buttons)item.Value.Visibility=item.Key.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0?Visibility.Visible:Visibility.Collapsed;};
        }
    }
}
