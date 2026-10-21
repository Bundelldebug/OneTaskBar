using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class MenuTheme
    {
        static bool installed;
        static readonly ConditionalWeakTable<ContextMenu,LiveBackdrop> backdrops=new ConditionalWeakTable<ContextMenu,LiveBackdrop>();
        internal static bool HasBackdrop(ContextMenu menu) {LiveBackdrop blur;return backdrops.TryGetValue(menu,out blur);}
        static void Opened(object sender,RoutedEventArgs args)
        {
            var menu=sender as ContextMenu;if(menu==null)return;
            var window=menu.PlacementTarget==null?null:Window.GetWindow(menu.PlacementTarget);bool gray=window is QuickWindow||window is DetailWindow||window is NotificationWindow;
            menu.Background=Preferences.Current.Blur?(gray?GrayPanel.Glass:LightPanel.Glass):Paint.Brush(gray?"#FFBEC6C1":"#FFD4DDD6");
            menu.Foreground=Paint.Ink;if(gray)foreach(object entry in menu.Items){var item=entry as MenuItem;if(item!=null){item.Foreground=menu.Foreground;var icon=item.Icon as Glyph;if(icon!=null)icon.Color=menu.Foreground;}}
            LightPanel.Apply(menu);
            menu.Dispatcher.BeginInvoke(new Action(()=> {
                if(!menu.IsOpen||!Preferences.Current.Blur)return;
                try {LiveBackdrop previous;if(backdrops.TryGetValue(menu,out previous)){previous.Dispose();backdrops.Remove(menu);}var blur=new LiveBackdrop(menu,18);backdrops.Add(menu,blur);}
                catch(Exception e){menu.Background=Paint.Brush(gray?"#FFBEC6C1":"#FFD4DDD6");AppLog.Once("menu-blur",e.Message);}
            }),DispatcherPriority.Loaded);
        }
        static void Closed(object sender,RoutedEventArgs args) {var menu=sender as ContextMenu;LiveBackdrop blur;if(menu!=null&&backdrops.TryGetValue(menu,out blur)){blur.Dispose();backdrops.Remove(menu);}}
        internal static void Install()
        {
            if(installed||Application.Current==null)return;installed=true;
            var styles=(ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <Style TargetType='ContextMenu'>
  <Setter Property='FontFamily' Value='{DynamicResource {x:Static SystemFonts.MessageFontFamilyKey}}'/>
  <Setter Property='FontSize' Value='13'/><Setter Property='Foreground' Value='#FF19221E'/>
  <Setter Property='Background' Value='#8FD8E2DB'/><Setter Property='BorderBrush' Value='Transparent'/>
  <Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='6'/>
  <Setter Property='MinWidth' Value='214'/><Setter Property='MaxWidth' Value='400'/>
  <Setter Property='HasDropShadow' Value='False'/><Setter Property='OverridesDefaultStyle' Value='True'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ContextMenu'>
   <Border Margin='10' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='18' Padding='{TemplateBinding Padding}'>
    <Border.Effect><DropShadowEffect Color='#000000' BlurRadius='18' ShadowDepth='3' Opacity='0.24'/></Border.Effect>
    <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>
   </Border>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='MenuItem'>
  <Setter Property='OverridesDefaultStyle' Value='True'/><Setter Property='Foreground' Value='#FF19221E'/>
  <Setter Property='FontSize' Value='13'/><Setter Property='MinHeight' Value='40'/>
  <Setter Property='Margin' Value='1'/><Setter Property='Padding' Value='11,8,14,8'/>
  <Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='MenuItem'>
   <Border x:Name='tile' Background='Transparent' CornerRadius='12' Padding='{TemplateBinding Padding}'>
    <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='28'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
     <ContentPresenter ContentSource='Icon' Width='18' Height='18' HorizontalAlignment='Left' VerticalAlignment='Center'/>
     <ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='False' VerticalAlignment='Center'/>
    </Grid>
   </Border>
   <ControlTemplate.Triggers>
    <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='tile' Property='Background' Value='#22000000'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='tile' Property='Background' Value='#22000000'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='tile' Property='Opacity' Value='0.4'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='Separator'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Separator'><Border Height='1' Background='#27000000' Margin='13,6'/></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>");
            Application.Current.Resources.MergedDictionaries.Add(styles);
            EventManager.RegisterClassHandler(typeof(ContextMenu),ContextMenu.OpenedEvent,new RoutedEventHandler(Opened));
            EventManager.RegisterClassHandler(typeof(ContextMenu),ContextMenu.ClosedEvent,new RoutedEventHandler(Closed),true);
        }
    }
}
