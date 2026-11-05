using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace OneTaskBar
{
    // The settings sliders look like the volume and brightness bars: a rounded track with a rounded fill
    // that grows from the left. It is still an ordinary Slider, so keyboard, automation and snapping are
    // unchanged; only its template differs.
    internal static class SettingsSliderStyle
    {
        const double Height=40;
        static readonly Dictionary<string,ControlTemplate> templates=new Dictionary<string,ControlTemplate>();
        static string Hex(Brush brush)
        {
            var solid=brush as SolidColorBrush;
            return solid==null?"#FF3478FD":solid.Color.ToString();
        }
        static ControlTemplate Template(string track,string fill)
        {
            string key=track+fill;
            ControlTemplate cached;
            if(templates.TryGetValue(key,out cached))return cached;
            string xaml=
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Slider'>"+
                "<Grid Height='"+Height+"' Background='Transparent' SnapsToDevicePixels='True'>"+
                "<Border CornerRadius='20' Background='"+track+"'/>"+
                "<Track Name='PART_Track'>"+
                "<Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Focusable='False' IsTabStop='False'>"+
                "<RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border CornerRadius='20,0,0,20' Margin='0,0,-20,0' Background='"+fill+"'/></ControlTemplate></RepeatButton.Template>"+
                "</RepeatButton></Track.DecreaseRepeatButton>"+
                "<Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Focusable='False' IsTabStop='False'>"+
                "<RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template>"+
                "</RepeatButton></Track.IncreaseRepeatButton>"+
                "<Track.Thumb><Thumb Width='"+Height+"' Height='"+Height+"' Focusable='True'>"+
                "<Thumb.Template><ControlTemplate TargetType='Thumb'><Grid Background='Transparent'>"+
                "<Ellipse Fill='"+fill+"'/>"+
                "<Ellipse x:Name='focus' Margin='3' Stroke='White' StrokeThickness='2' Visibility='Collapsed'/>"+
                "</Grid><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='focus' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers>"+
                "</ControlTemplate></Thumb.Template></Thumb></Track.Thumb>"+
                "</Track></Grid></ControlTemplate>";
            var template=(ControlTemplate)XamlReader.Parse(xaml);
            templates[key]=template;
            return template;
        }
        internal static void Apply(Slider slider)
        {
            slider.Template=Template(Hex(AppSettingsPalette.AccentLight),Hex(AppSettingsPalette.Accent));
            slider.Height=Height;
            slider.IsMoveToPointEnabled=true;
            slider.FocusVisualStyle=null;
        }
        // The slider with a small symbol on the fill, as the volume bar has.
        internal static FrameworkElement WithSymbol(Slider slider,string symbol)
        {
            Apply(slider);
            var host=new Grid {Margin=slider.Margin};
            slider.Margin=new Thickness(0);
            host.Children.Add(slider);
            host.Children.Add(new Glyph(symbol,18){Color=Paint.ControlWhite,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(11,0,0,0),IsHitTestVisible=false});
            return host;
        }
        internal static string SymbolFor(string name)
        {
            name=name??"";
            if(name.Contains("불투명"))return "brightness";
            if(name.Contains("블러"))return "photo";
            if(name.Contains("시계"))return "focus";
            return "desktops";
        }
    }
}
