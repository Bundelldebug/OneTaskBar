using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace OneTaskBar
{
    internal sealed class SamsungModePicker : StackPanel
    {
        readonly Dictionary<int,Button> buttons=new Dictionary<int,Button>();
        readonly TextBlock fan=Paint.TextBlock("",11,Paint.Muted);
        string signature;
        internal IEnumerable<Button> Buttons {get{return buttons.Values;}}
        internal string FanText {get{return fan.Visibility==Visibility.Visible?fan.Text:"";}}
        internal SamsungModePicker(IEnumerable<SamsungMode> modes,Action<int> change)
        {
            var grid=new UniformGrid {Columns=4};var ids=modes.Select(m=>m.Id).ToArray();
            foreach(var mode in new[]{new SamsungMode {Id=0,Name="조용함"},new SamsungMode {Id=1,Name="저소음"},new SamsungMode {Id=2,Name="최적화"},new SamsungMode {Id=3,Name="고성능"}}){
                int id=mode.Id;var cell=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center};var button=Paint.Button(new Glyph(id==0?"moon":id==1?"volume-low":id==2?"leaf":"bolt",23),()=>change(id),mode.Name);button.Tag=id;button.Width=button.Height=43;button.Padding=new Thickness(0);button.Resources["ButtonRadius"]=new CornerRadius(22);buttons[id]=button;cell.Children.Add(button);var label=Paint.TextBlock(mode.Name,11,Paint.Text);label.HorizontalAlignment=HorizontalAlignment.Center;label.Margin=new Thickness(0,7,0,0);cell.Children.Add(label);if(!ids.Contains(id))cell.Visibility=Visibility.Collapsed;grid.Children.Add(cell);
            }
            Children.Add(new Border {CornerRadius=new CornerRadius(32),Background=QuickPanel.Card,Padding=new Thickness(8,13,8,12),Child=grid});fan.Margin=new Thickness(5,9,5,0);Children.Add(fan);
        }
        internal void Refresh(SamsungPerformanceState state,bool changing,SamsungDirectAccess access)
        {
            string next=state.Selected+"|"+state.MaximumFan+"|"+state.Stale+"|"+changing+"|"+access+"|"+String.Join(",",state.Modes.Select(m=>m.Id));if(next==signature)return;signature=next;
            foreach(var pair in buttons){bool selected=pair.Key==state.Selected;var button=pair.Value;var mode=state.Modes.FirstOrDefault(m=>m.Id==pair.Key);button.IsEnabled=!changing&&mode!=null&&access==SamsungDirectAccess.Ready;button.Background=selected?Paint.ControlWhite:Paint.Brush("#30000000");var glyph=(Glyph)button.Content;glyph.Color=selected?Paint.OnLight:Paint.Text;glyph.InvalidateVisual();button.ToolTip=mode==null?"이 기기에서 지원하지 않는 모드":access==SamsungDirectAccess.Ready?mode.Name:SamsungPerformance.AccessMessage;System.Windows.Automation.AutomationProperties.SetItemStatus(button,selected?"선택됨":"선택 안 됨");}
            fan.Visibility=state.Selected==3?Visibility.Visible:Visibility.Collapsed;fan.Text="최대 팬 모드 · "+(state.Stale?"상태 확인 중":!state.MaximumFan.HasValue?"정보 없음":state.MaximumFan.Value?"켜짐":"꺼짐");
        }
    }
}
