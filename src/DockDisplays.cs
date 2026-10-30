using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal static class DockDisplays
    {
        internal const string Primary="@primary";

        internal static string[] Resolve(string[] devices,string primary,string selection)
        {
            if(String.IsNullOrEmpty(selection))return devices;
            string selected=selection==Primary?primary:selection;
            if(!devices.Contains(selected,StringComparer.OrdinalIgnoreCase))selected=primary;
            if(!devices.Contains(selected,StringComparer.OrdinalIgnoreCase))selected=devices.FirstOrDefault();
            return selected==null?new string[0]:new[]{selected};
        }

        internal static Forms.Screen[] Selected()
        {
            var screens=Forms.Screen.AllScreens;
            var primary=screens.FirstOrDefault(screen=>screen.Primary)??screens.FirstOrDefault();
            var names=Resolve(screens.Select(screen=>screen.DeviceName).ToArray(),primary!=null?primary.DeviceName:null,Preferences.Current.DockMonitor);
            return screens.Where(screen=>names.Contains(screen.DeviceName,StringComparer.OrdinalIgnoreCase)).ToArray();
        }

        internal static void Check()
        {
            var names=new[]{"display-one","display-two","display-three"};
            if(Resolve(names,names[1],null).Length!=3)throw new InvalidOperationException("Existing display settings must keep all monitors");
            if(Resolve(names,names[1],Primary).Single()!=names[1])throw new InvalidOperationException("Primary display selection must follow Windows");
            if(Resolve(names,names[0],names[2]).Single()!=names[2])throw new InvalidOperationException("A selected secondary monitor must be retained");
            if(Resolve(new[]{names[2],names[0]},names[0],names[2]).Single()!=names[2])throw new InvalidOperationException("Display order must not change a saved selection");
            if(Resolve(new[]{names[0]},names[0],names[2]).Single()!=names[0])throw new InvalidOperationException("A disconnected monitor must fall back to the primary display");
            if(Resolve(names,names[1],"DISPLAY-THREE").Single()!= "DISPLAY-THREE")throw new InvalidOperationException("Monitor identities are case insensitive");
            if(Resolve(new string[0],null,Primary).Length!=0)throw new InvalidOperationException("Missing display data must remain bounded");
        }
    }

    internal sealed class DockDisplayChoice
    {
        public string Label {get;set;}
        public string Value {get;set;}
        public override string ToString(){return Label??"";}
    }

    internal sealed partial class WelcomeWindow
    {
        internal ComboBox DisplayPicker {get;private set;}
        void DockDisplaySetting(StackPanel rows)
        {
            var row=new StackPanel {Margin=new Thickness(15,14,15,14)};
            row.Children.Add(Paint.TextBlock("작업표시줄 표시 모니터",15,AppSettingsPalette.Text));
            var choices=new System.Collections.Generic.List<DockDisplayChoice>
            {
                new DockDisplayChoice {Label="모든 모니터",Value=""},
                new DockDisplayChoice {Label="주 모니터만",Value=DockDisplays.Primary}
            };
            int index=0;
            foreach(var screen in Forms.Screen.AllScreens)
            {
                ++index;
                choices.Add(new DockDisplayChoice {Value=screen.DeviceName,Label="모니터 "+index+(screen.Primary?" · 주 모니터":"")+" · "+screen.Bounds.Width+" × "+screen.Bounds.Height+" · "+screen.DeviceName});
            }
            string saved=Preferences.Current.DockMonitor??"";
            if(!choices.Any(choice=>String.Equals(choice.Value,saved,StringComparison.OrdinalIgnoreCase)))
                choices.Add(new DockDisplayChoice {Value=saved,Label="연결되지 않은 모니터 · "+saved});
            var picker=new ComboBox {Height=44,FontSize=14,Margin=new Thickness(0,10,0,0),DisplayMemberPath="Label",SelectedValuePath="Value",ItemsSource=choices};
            DisplayPicker=picker;
            SettingsPickerStyle.Apply(picker);
            System.Windows.Automation.AutomationProperties.SetName(picker,"작업표시줄 표시 모니터");
            picker.SelectedItem=choices.First(choice=>String.Equals(choice.Value,saved,StringComparison.OrdinalIgnoreCase));
            picker.SelectionChanged+=(s,e)=>
            {
                var choice=picker.SelectedItem as DockDisplayChoice;
                if(choice==null||String.Equals(Preferences.Current.DockMonitor??"",choice.Value,StringComparison.OrdinalIgnoreCase))return;
                Preferences.Set(p=>p.DockMonitor=String.IsNullOrEmpty(choice.Value)?null:choice.Value);
            };
            row.Children.Add(picker);
            var help=Paint.TextBlock("지정한 모니터가 연결되지 않으면 주 모니터에 표시합니다",11,AppSettingsPalette.Muted);
            help.Margin=new Thickness(0,7,0,0);
            row.Children.Add(help);
            rows.Children.Add(row);
        }
    }
}
