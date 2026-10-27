using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class DockStatusItems
    {
        internal static readonly string[][] Choices={new[]{"bluetooth","bluetooth","블루투스"},new[]{"buds","earbuds","Galaxy Buds"},new[]{"calendar","calendar","달력"},new[]{"capture","screenshot","화면 캡처"},new[]{"manager","task-manager","작업 관리자"},new[]{"settings","home","One TaskBar 설정"}};
        internal static string[] Clean(string[] items){return (items??new string[0]).Where(k=>Choices.Any(c=>c[0]==k)).Distinct().Take(4).ToArray();}
        internal static int Count {get{return Clean(Preferences.Current.DockStatusItems).Length;}}
        internal static void Invoke(string key,ShellController shell,GlassWindow anchor)
        {
            switch(key){case "bluetooth":shell.ShowDetail("bluetooth");break;case "buds":shell.OpenQuickDevices(true);break;case "calendar":shell.ToggleCalendar(anchor);break;case "capture":shell.OpenScreenCapture();break;case "manager":shell.OpenTaskManager();break;case "settings":shell.ShowWelcome();break;}
        }
    }
    internal sealed partial class DockHost
    {
        readonly StackPanel extraStatus=new StackPanel {VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        string extraStatusKey;
        ImageSource statusBudsIcon;
        bool statusBudsRequested;
        async void LoadStatusBuds()
        {
            statusBudsRequested=true;
            try{var symbol=await BackgroundWork.Run(BudsDeviceData.ShortcutIcon);if(!closed){statusBudsIcon=symbol;UpdateStatusSymbols();}}
            catch(Exception error){AppLog.Once("status-buds",error.GetType().Name);}
        }
        void UpdateStatusSymbols()
        {
            foreach(var button in extraStatus.Children.OfType<Button>())
            {
                var glyph=button.Content as Glyph;
                if(glyph==null)continue;
                glyph.Update(glyph.Kind,Glass95.DockForeground);
                if((string)button.Tag=="buds")glyph.SetDeviceIcon(statusBudsIcon);
            }
        }
        internal Glyph StatusShortcut(string key){var button=extraStatus.Children.OfType<Button>().FirstOrDefault(b=>(string)b.Tag==key);return button==null?null:button.Content as Glyph;}
        void RebuildStatusItems()
        {
            var keys=DockStatusItems.Clean(Preferences.Current.DockStatusItems);string signature=String.Join("|",keys);bool vertical=DockGeometry.Vertical(Edge);extraStatus.Orientation=vertical?Orientation.Vertical:Orientation.Horizontal;
            if(extraStatusKey!=signature){extraStatusKey=signature;extraStatus.Children.Clear();foreach(string key in keys){var choice=DockStatusItems.Choices.First(c=>c[0]==key);var glyph=new Glyph(choice[1],17){Color=Glass95.DockForeground};var button=Paint.Button(glyph,()=>DockStatusItems.Invoke(key,controller,Status),choice[2]);button.Tag=key;button.Width=24;button.Height=24;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;extraStatus.Children.Add(button);}}
            UpdateStatusSymbols();
            if(keys.Contains("buds")&&!statusBudsRequested)LoadStatusBuds();
            var grid=(Grid)Status.Surface.Child;if(vertical){grid.RowDefinitions[3].Height=new GridLength(keys.Length*24);extraStatus.Width=38;extraStatus.Height=keys.Length*24;}else grid.ColumnDefinitions[3].Width=new GridLength(keys.Length*24);
            extraStatus.Visibility=keys.Length==0?Visibility.Collapsed:Visibility.Visible;
        }
    }
    internal sealed partial class WelcomeWindow
    {
        void StatusItemsSetting(StackPanel rows)
        {
            var group=new StackPanel();group.Children.Add(Paint.TextBlock("시계 옆에 최대 4개를 추가합니다. 선택한 순서로 표시됩니다.",12,AppSettingsPalette.Muted));
            var switches=new Dictionary<string,ToggleSwitch>();foreach(var choice in DockStatusItems.Choices){string key=choice[0];switches[key]=Row(group,choice[2],"",DockStatusItems.Clean(Preferences.Current.DockStatusItems).Contains(key),value=>{var keys=DockStatusItems.Clean(Preferences.Current.DockStatusItems).Where(k=>k!=key).ToList();if(value){if(keys.Count==4)keys.RemoveAt(0);keys.Add(key);}Preferences.Set(p=>p.DockStatusItems=keys.ToArray());foreach(var item in switches)item.Value.Refresh(keys.Contains(item.Key));});}
            rows.Children.Add(new Expander {Header="상태 영역 바로가기",Content=group,Margin=new Thickness(15,12,15,12),Foreground=AppSettingsPalette.Text});
        }
    }
}
