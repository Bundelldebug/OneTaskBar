using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // The Fluid UI page of the settings. It is only built while the Fluid UI is the chosen interface, so the other
    // versions never list options that do nothing for them.
    internal sealed partial class WelcomeWindow
    {
        StackPanel FluidSection()
        {
            var rows=new StackPanel();
            SubHeading(rows,"뒤쪽 글로우");
            var note=Paint.TextBlock("유리 뒤에서 번지는 빛의 색입니다. 패널, 카드 테두리, Dynamic Island, 전체화면 미디어에 함께 적용됩니다.",13,AppSettingsPalette.Muted);
            note.TextWrapping=TextWrapping.Wrap;note.LineHeight=19;note.Margin=new Thickness(17,6,17,6);rows.Children.Add(note);
            var chips=new WrapPanel {Margin=new Thickness(15,4,15,8)};
            var refreshers=new List<Action>();
            for(int i=0;i<Fluid.GlowNames.Length;i++)
            {
                int choice=i;
                var dot=new Border {Width=16,Height=16,CornerRadius=new CornerRadius(8),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};
                var label=Paint.TextBlock(Fluid.GlowNames[i],13.5,AppSettingsPalette.Text);label.VerticalAlignment=VerticalAlignment.Center;
                var content=new StackPanel {Orientation=Orientation.Horizontal};content.Children.Add(dot);content.Children.Add(label);
                var chip=new Border {CornerRadius=new CornerRadius(19),Padding=new Thickness(12,7,14,7),BorderThickness=new Thickness(1.2),Child=content};
                var button=Paint.Button(chip,()=>ChooseGlow(choice),"뒤쪽 글로우 "+Fluid.GlowNames[i]);
                button.Padding=new Thickness(0);button.Background=Brushes.Transparent;button.Margin=new Thickness(0,0,8,8);
                refreshers.Add(()=>
                {
                    bool selected=Fluid.GlowChoice==choice;
                    chip.Background=selected?AppSettingsPalette.AccentLight:AppSettingsPalette.Card;
                    chip.BorderBrush=selected?AppSettingsPalette.Accent:AppSettingsPalette.Stroke;
                    dot.Background=GlowDot(choice);
                    dot.BorderBrush=choice==Fluid.GlowNone?AppSettingsPalette.Muted:Brushes.Transparent;
                    dot.BorderThickness=new Thickness(choice==Fluid.GlowNone?1.2:0);
                });
                chips.Children.Add(button);
            }
            glowRefresh=()=>{foreach(var refresh in refreshers)refresh();};
            glowRefresh();
            rows.Children.Add(chips);
            SubHeading(rows,"알림");
            Row(rows,"알림 글로우를 앱 아이콘 색으로","알림 팝업과 Dynamic Island 알림이 앱 아이콘의 주요 색으로 빛납니다. 끄면 위에서 고른 색을 씁니다. 글로우가 없음이면 적용되지 않습니다",Preferences.Current.FluidIconGlow,v=>Preferences.Set(p=>p.FluidIconGlow=v));
            return rows;
        }
        Action glowRefresh;
        static Brush GlowDot(int choice)
        {
            if(choice==Fluid.GlowNone)return Brushes.Transparent;
            if(choice==Fluid.GlowCustom)
            {
                Color custom;
                return new SolidColorBrush(DockAppearance.TryColor(Preferences.Current.FluidGlowColor,out custom)?Color.FromRgb(custom.R,custom.G,custom.B):Color.FromRgb(0xFF,0x8A,0x3D));
            }
            int saved=Preferences.Current.FluidGlow;
            var colors=new[]{Color.FromRgb(0xFF,0x8A,0x3D),Color.FromRgb(0xFF,0x5C,0x9E),Color.FromRgb(0x9A,0x6B,0xFF),Color.FromRgb(0x3D,0x9B,0xFF),Color.FromRgb(0x3D,0xD6,0x8C)};
            return new SolidColorBrush(colors[Math.Max(0,Math.Min(colors.Length-1,choice))]);
        }
        void ChooseGlow(int choice)
        {
            if(choice==Fluid.GlowCustom)
            {
                using(var picker=new Forms.ColorDialog())
                {
                    Color current;
                    DockAppearance.TryColor(Preferences.Current.FluidGlowColor,out current);
                    picker.Color=System.Drawing.Color.FromArgb(current.R,current.G,current.B);picker.FullOpen=true;
                    if(picker.ShowDialog()!=Forms.DialogResult.OK)return;
                    string value="#FF"+picker.Color.R.ToString("X2")+picker.Color.G.ToString("X2")+picker.Color.B.ToString("X2");
                    Preferences.Set(p=>{p.FluidGlow=Fluid.GlowCustom;p.FluidGlowColor=value;});
                }
            }
            else
            {
                if(Preferences.Current.FluidGlow==choice)return;
                Preferences.Set(p=>p.FluidGlow=choice);
            }
            if(glowRefresh!=null)glowRefresh();
            VersionPreview.InvalidateVisual();
        }
    }
}
