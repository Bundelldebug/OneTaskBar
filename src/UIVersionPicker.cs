using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
namespace OneTaskBar
{
    internal sealed class UIVersionChoice
    {
        public string Label {get;set;}public int Value {get;set;}
        public override string ToString(){return Label??"";}
    }
    internal static class UIVersions
    {
        internal static UIVersionChoice[] Available {get{return ReleaseFeatures.GlassUI?new[]{new UIVersionChoice {Value=85,Label="8.5"},new UIVersionChoice {Value=90,Label="9.0"},new UIVersionChoice {Value=95,Label="9.5 베타"}}:new[]{new UIVersionChoice {Value=85,Label="8.5"},new UIVersionChoice {Value=90,Label="9.0"}};}}
    }
    internal sealed partial class WelcomeWindow
    {
        internal ComboBox VersionPicker {get;private set;}
        StackPanel VersionSetting()
        {
            var rows=new StackPanel {Margin=new Thickness(14,12,14,14)};var picker=new ComboBox {Height=44,FontSize=15,DisplayMemberPath="Label",SelectedValuePath="Value",ItemsSource=UIVersions.Available,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center};VersionPicker=picker;
            picker.Resources["PickerCard"]=AppSettingsPalette.Card;picker.Resources["PickerSurface"]=AppSettingsPalette.AccentLight;picker.Resources["PickerInk"]=AppSettingsPalette.Text;picker.Resources["PickerLine"]=AppSettingsPalette.Stroke;picker.Resources["PickerAccent"]=AppSettingsPalette.Accent;picker.Resources["PickerSelectedInk"]=Paint.ControlWhite;
            picker.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <Style TargetType='ComboBoxItem'><Setter Property='Foreground' Value='{DynamicResource PickerInk}'/><Setter Property='Padding' Value='12,10'/><Setter Property='Margin' Value='4,2'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='item' Background='Transparent' CornerRadius='10' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource PickerSurface}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource PickerAccent}'/><Setter Property='Foreground' Value='{DynamicResource PickerSelectedInk}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType='ComboBox'><Setter Property='Foreground' Value='{DynamicResource PickerInk}'/><Setter Property='Background' Value='{DynamicResource PickerSurface}'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><ToggleButton Foreground='{DynamicResource PickerInk}' Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='face' Background='{DynamicResource PickerSurface}' BorderBrush='{DynamicResource PickerLine}' BorderThickness='1' CornerRadius='14'><Grid Margin='13,0'><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='24'/></Grid.ColumnDefinitions><TextBlock Text='{Binding SelectedItem.Label, RelativeSource={RelativeSource AncestorType=ComboBox}}' Foreground='{DynamicResource PickerInk}' VerticalAlignment='Center'/><TextBlock Grid.Column='1' Text='⌄' FontSize='18' Foreground='{DynamicResource PickerInk}' HorizontalAlignment='Right' VerticalAlignment='Center'/></Grid></Border></ControlTemplate></ToggleButton.Template></ToggleButton><Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False'><Border Background='{DynamicResource PickerCard}' BorderBrush='{DynamicResource PickerLine}' BorderThickness='1' CornerRadius='14' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' Margin='0,5,0,0' Padding='2,5'><ScrollViewer MaxHeight='320' CanContentScroll='True'><StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>"));
            System.Windows.Automation.AutomationProperties.SetName(picker,"UI 버전 선택");picker.SelectedValue=Preferences.Current.UIVersion;
            Action<UIVersionChoice> apply=choice=>{if(choice==null||choice.Value==Preferences.Current.UIVersion)return;int version=choice.Value;Preferences.Set(p=>p.UIVersion=version);RefreshVersionOptions();if(shell.Welcome==this)Dispatcher.BeginInvoke(new Action(shell.ShowWelcome));};
            picker.SelectionChanged+=(s,e)=>{if(!picker.IsDropDownOpen&&e.AddedItems.Count==1)apply(e.AddedItems[0] as UIVersionChoice);};picker.DropDownClosed+=(s,e)=>apply(picker.SelectedItem as UIVersionChoice);rows.Children.Add(picker);return rows;
        }
        void RefreshVersionOptions(){bool modern=Preferences.Current.UIVersion>=90;if(legacyBudsOption!=null)legacyBudsOption.Visibility=modern?Visibility.Collapsed:Visibility.Visible;if(independentBudsHint!=null)independentBudsHint.Visibility=modern?Visibility.Visible:Visibility.Collapsed;}
    }
}
