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
        // The white pill the Dex panels float over their content.
        static readonly Brush PillInk=Paint.Brush("#FF20262C");
        internal static Border CreatePill(TextBox query,Action clear,string text,Brush hintColor,bool showIcon=true)
        {
            var frame=Create(query,clear,text,hintColor,showIcon);
            frame.Height=38;frame.CornerRadius=new CornerRadius(19);
            frame.Background=Dex.LightSheet?Paint.Brush("#FAFFFFFF"):Paint.Brush("#F0272E36");
            frame.BorderBrush=Dex.LightSheet?Paint.Brush("#18000000"):Paint.Brush("#28FFFFFF");
            frame.BorderThickness=new Thickness(.7);
            frame.Effect=Dex.SoftShadow;
            return frame;
        }
        internal static Border Create(TextBox query,Action clear,string text=null,Brush hintColor=null,bool showIcon=true)
        {
            bool pill=text!=null;
            Brush ink=pill?(Dex.LightSheet?PillInk:Paint.ControlWhite):Paint.Text;
            query.FontSize=14;query.Padding=new Thickness(showIcon?6:2,pill?7:9,6,pill?7:9);query.VerticalContentAlignment=VerticalAlignment.Center;query.Foreground=ink;
            var field=new Grid {Margin=new Thickness(showIcon?14:16,0,10,0)};field.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(showIcon?26:0)});field.ColumnDefinitions.Add(new ColumnDefinition());field.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(30)});
            if(showIcon)field.Children.Add(new Glyph("search",18){Color=ink});var input=new Grid();input.Children.Add(query);var hint=Paint.TextBlock(text??Hint,pill?13:14,hintColor??(Glass95.Enabled?Glass95.Muted:LightPanel.Muted));hint.VerticalAlignment=VerticalAlignment.Center;hint.Margin=new Thickness(showIcon?6:2,0,0,0);hint.IsHitTestVisible=false;input.Children.Add(hint);Grid.SetColumn(input,1);field.Children.Add(input);
            var reset=Paint.Button(new Glyph("close",15){Color=ink},clear,"검색어 지우기");reset.Width=reset.Height=26;reset.Padding=new Thickness(0);reset.Background=pill?Dex.SheetChip:Brushes.Transparent;reset.Resources["ButtonRadius"]=new CornerRadius(13);Grid.SetColumn(reset,2);field.Children.Add(reset);
            Action refresh=()=>{hint.Visibility=query.Text.Length==0?Visibility.Visible:Visibility.Collapsed;reset.Visibility=query.Text.Length==0?Visibility.Collapsed:Visibility.Visible;};query.TextChanged+=(s,e)=>refresh();refresh();
            var frame=new Border {Height=Height,Background=PanelMaterial.Field,BorderBrush=PanelMaterial.FieldStroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(22),Child=field};if(!pill){query.GotKeyboardFocus+=(s,e)=>frame.BorderBrush=PanelMaterial.Focus;query.LostKeyboardFocus+=(s,e)=>frame.BorderBrush=PanelMaterial.FieldStroke;}return frame;
        }
    }
}
