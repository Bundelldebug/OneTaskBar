using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed partial class WelcomeWindow
    {
        void MaterialSetting(StackPanel parent,bool dock)
        {
            var data=Preferences.Current;
            string name=dock?"독 배경":"시작 패널 배경";
            var controls=new StackPanel {IsEnabled=dock?data.DockMaterialCustom:data.StartMaterialCustom};
            var toggle=Row(parent,name+" 미세 조정",dock?"기본 재질의 색상·불투명도·블러를 조절합니다":"시작 메뉴의 색상·불투명도·블러를 조절합니다",
                controls.IsEnabled,value=>{
                    controls.IsEnabled=value;
                    Preferences.SetAppearance(p=>{if(dock)p.DockMaterialCustom=value;else p.StartMaterialCustom=value;});
                });
            var actions=new WrapPanel {Margin=new Thickness(15,8,15,3)};
            var color=SettingsChip.Action("색상 선택",()=>{
                using(var picker=new Forms.ColorDialog()){
                    System.Windows.Media.Color selected;
                    DockAppearance.TryColor(dock?data.DockMaterialColor:data.StartMaterialColor,out selected);
                    picker.Color=System.Drawing.Color.FromArgb(selected.R,selected.G,selected.B);picker.FullOpen=true;
                    if(picker.ShowDialog()!=Forms.DialogResult.OK)return;
                    string value="#FF"+picker.Color.R.ToString("X2")+picker.Color.G.ToString("X2")+picker.Color.B.ToString("X2");
                    Preferences.SetAppearance(p=>{if(dock)p.DockMaterialColor=value;else p.StartMaterialColor=value;});
                }
            });
            actions.Children.Add(color);controls.Children.Add(actions);
            var opacity=MaterialSlider(controls,name+" 불투명도",dock?data.DockMaterialOpacity:data.StartMaterialOpacity,100,"%",value=>
                Preferences.SetAppearance(p=>{if(dock)p.DockMaterialOpacity=value;else p.StartMaterialOpacity=value;}));
            var blur=MaterialSlider(controls,name+" 블러 강도",dock?data.DockMaterialBlur:data.StartMaterialBlur,40,"",value=>
                Preferences.SetAppearance(p=>{if(dock)p.DockMaterialBlur=value;else p.StartMaterialBlur=value;}));
            parent.Children.Add(controls);
            var reset=SettingsChip.Action("기본 재질로 되돌리기",()=>{
                Preferences.SetAppearance(p=>{
                    if(dock){p.DockMaterialCustom=false;p.DockMaterialColor="#FF52616D";p.DockMaterialOpacity=30;p.DockMaterialBlur=16;}
                    else{p.StartMaterialCustom=false;p.StartMaterialColor="#FF3D4854";p.StartMaterialOpacity=30;p.StartMaterialBlur=16;}
                });
                controls.IsEnabled=false;toggle.Refresh(false);opacity.Value=30;blur.Value=16;
            });
            reset.Margin=new Thickness(15,3,15,12);reset.HorizontalAlignment=HorizontalAlignment.Left;parent.Children.Add(reset);
        }
        Slider MaterialSlider(StackPanel parent,string name,int initial,int maximum,string unit,Action<int> commit)
        {
            var row=new StackPanel {Margin=new Thickness(15,8,15,10)};
            var heading=new DockPanel();
            var number=Paint.TextBlock(initial+unit,13,AppSettingsPalette.Muted);
            DockPanel.SetDock(number,Dock.Right);heading.Children.Add(number);
            heading.Children.Add(Paint.TextBlock(name,14,AppSettingsPalette.Text));row.Children.Add(heading);
            var slider=new Slider {Minimum=0,Maximum=maximum,Value=initial,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,12,0,2)};
            AutomationProperties.SetName(slider,name);
            int saved=initial;
            Action save=()=>{int value=(int)Math.Round(slider.Value);if(value==saved)return;saved=value;commit(value);};
            slider.ValueChanged+=(s,e)=>number.Text=((int)Math.Round(slider.Value))+unit;
            slider.PreviewMouseLeftButtonUp+=(s,e)=>save();slider.LostMouseCapture+=(s,e)=>save();slider.KeyUp+=(s,e)=>save();
            // Resetting values does not emit a preference write during teardown.
            slider.ValueChanged+=(s,e)=>{if(!parent.IsEnabled)saved=(int)Math.Round(slider.Value);};
            row.Children.Add(SettingsSliderStyle.WithSymbol(slider,SettingsSliderStyle.SymbolFor(name)));parent.Children.Add(row);return slider;
        }
    }
}
