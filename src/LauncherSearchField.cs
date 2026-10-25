using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class LauncherSearchField
    {
        internal const double Height=44;
        internal const string Hint="앱, 설정 및 파일 검색";
        internal static Border Create(TextBox query,Action clear)
        {
            query.FontSize=14;query.Padding=new Thickness(6,9,6,9);query.VerticalContentAlignment=VerticalAlignment.Center;query.Foreground=Glass95.Enabled?Glass95.Text:Paint.Brush("#FF19221E");
            var field=new Grid {Margin=new Thickness(14,0,10,0)};field.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(26)});field.ColumnDefinitions.Add(new ColumnDefinition());field.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(30)});
            field.Children.Add(new Glyph("search",19){Color=query.Foreground});var input=new Grid();input.Children.Add(query);var hint=Paint.TextBlock(Hint,14,Glass95.Enabled?Glass95.Muted:LightPanel.Muted);hint.Margin=new Thickness(6,0,0,0);hint.IsHitTestVisible=false;input.Children.Add(hint);Grid.SetColumn(input,1);field.Children.Add(input);
            var reset=Paint.Button(new Glyph("close",16){Color=query.Foreground},clear,"검색어 지우기");reset.Width=reset.Height=28;reset.Padding=new Thickness(0);reset.Background=Brushes.Transparent;Grid.SetColumn(reset,2);field.Children.Add(reset);
            Action refresh=()=>{hint.Visibility=query.Text.Length==0?Visibility.Visible:Visibility.Collapsed;reset.Visibility=query.Text.Length==0?Visibility.Collapsed:Visibility.Visible;};query.TextChanged+=(s,e)=>refresh();refresh();
            return new Border {Height=Height,Background=Glass95.Enabled?Glass95.Card:Paint.ControlWhite,BorderBrush=Glass95.Enabled?Glass95.Rim:Brushes.Transparent,BorderThickness=new Thickness(Glass95.Enabled?.7:0),CornerRadius=new CornerRadius(22),Child=field};
        }
    }
}
