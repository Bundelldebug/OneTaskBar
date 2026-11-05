using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    internal sealed partial class WelcomeWindow
    {
        void FullscreenTileSettings(StackPanel rows)
        {
            Row(rows,"접힌 화면 위젯","전체화면 미디어를 접으면 시간 아래에 위젯을 표시합니다",Preferences.Current.FullscreenWidgets,v=>Preferences.SetMediaAppearance(p=>p.FullscreenWidgets=v));
            foreach(var family in InfoTileCatalog.Families)TileSizePicker(rows,family);
            ActionPicker(rows,"첫 번째 빠른 실행 버튼",0);
            ActionPicker(rows,"두 번째 빠른 실행 버튼",1);
            ZonePicker(rows);
            WeatherPlaceRow(rows);
            var hint=Paint.TextBlock("접힌 화면의 위젯을 우클릭하거나 길게 누르면 그 자리에서 바로 편집됩니다. 끌어서 옮기고, 아래 막대로 크기를 바꾸거나 추가·제거할 수 있습니다. 왼쪽·오른쪽 아래 앱은 버튼을 눌러 선택하고 우클릭으로 바꿉니다.",12,Paint.Muted);
            hint.TextWrapping=TextWrapping.Wrap;
            hint.Margin=new Thickness(15,8,15,12);
            rows.Children.Add(hint);
        }
        static ComboBox Picker(StackPanel rows,string title)
        {
            var label=Paint.TextBlock(title,13,Paint.Text);
            label.Margin=new Thickness(15,10,15,4);
            rows.Children.Add(label);
            var picker=new ComboBox {MinHeight=38,Margin=new Thickness(15,0,15,4),DisplayMemberPath="Label"};
            SettingsPickerStyle.Apply(picker);
            rows.Children.Add(picker);
            return picker;
        }
        static void TileSizePicker(StackPanel rows,InfoTileFamily family)
        {
            var picker=Picker(rows,family.Title);
            picker.Items.Add(new PickerChoice {Label="표시 안 함",Value=""});
            picker.SelectedIndex=0;
            var saved=Preferences.Current.FullscreenTiles??new string[0];
            foreach(string span in family.Spans)
            {
                string token=family.Key+":"+span;
                picker.Items.Add(new PickerChoice {Label=InfoTileCatalog.SpanLabel(token),Value=token});
                if(saved.Contains(token))picker.SelectedIndex=picker.Items.Count-1;
            }
            picker.SelectionChanged+=(s,e)=>
            {
                var chosen=picker.SelectedItem as PickerChoice;
                if(chosen!=null)SetFullscreenTile(family.Key,(string)chosen.Value);
            };
        }
        internal static void SetFullscreenTile(string family,string token)
        {
            // The order set on the screen is kept: a changed size stays in place, a new tile goes last.
            var current=(Preferences.Current.FullscreenTiles??new string[0]).Where(InfoTileCatalog.Known).ToList();
            int at=current.FindIndex(k=>InfoTileCatalog.FamilyKey(k)==family);
            if(String.IsNullOrEmpty(token))
            {
                if(at>=0)current.RemoveAt(at);
            }
            else if(at>=0)current[at]=token;
            else current.Add(token);
            var ordered=current.ToArray();
            Preferences.SetMediaAppearance(p=>p.FullscreenTiles=ordered);
        }
        static void ActionPicker(StackPanel rows,string title,int slot)
        {
            var picker=Picker(rows,title);
            picker.Items.Add(new PickerChoice {Label="없음",Value=""});
            var saved=Preferences.Current.FullscreenActions??new string[0];
            string current=slot<saved.Length?saved[slot]:"";
            picker.SelectedIndex=0;
            foreach(var spec in QuickActionCatalog.Specs)
            {
                picker.Items.Add(new PickerChoice {Label=spec[2],Value=spec[0]});
                if(spec[0]==current)picker.SelectedIndex=picker.Items.Count-1;
            }
            picker.SelectionChanged+=(s,e)=>
            {
                var chosen=picker.SelectedItem as PickerChoice;
                if(chosen==null)return;
                var next=new string[2];
                var before=Preferences.Current.FullscreenActions??new string[0];
                for(int i=0;i<2;i++)next[i]=i<before.Length?before[i]:"";
                next[slot]=(string)chosen.Value;
                var value=next.Where(k=>!String.IsNullOrEmpty(k)).ToArray();
                Preferences.SetMediaAppearance(p=>p.FullscreenActions=value);
            };
        }
        static void ZonePicker(StackPanel rows)
        {
            var picker=Picker(rows,"듀얼 시계 도시");
            foreach(var zone in DualClockZones.All)
            {
                picker.Items.Add(new PickerChoice {Label=zone[1],Value=zone[0]});
                if(zone[0]==Preferences.Current.DualClockZone)picker.SelectedIndex=picker.Items.Count-1;
            }
            picker.SelectionChanged+=(s,e)=>
            {
                var chosen=picker.SelectedItem as PickerChoice;
                if(chosen!=null)Preferences.SetMediaAppearance(p=>p.DualClockZone=(string)chosen.Value);
            };
        }
        void WeatherPlaceRow(StackPanel rows)
        {
            var label=Paint.TextBlock("날씨 위치",13,Paint.Text);
            label.Margin=new Thickness(15,10,15,2);
            rows.Children.Add(label);
            Row(rows,"PC 위치 자동 사용","Windows 위치 서비스로 찾은 대략의 좌표로 날씨를 조회합니다. Windows 위치 권한이 켜져 있어야 합니다",Preferences.Current.WeatherAutoLocation,v=>{Preferences.SetMediaAppearance(p=>p.WeatherAutoLocation=v);WeatherService.Reset();});
            var note=Paint.TextBlock("입력한 지역 이름과 좌표가 날씨 조회를 위해 날씨 제공 서비스로 전송됩니다. 자동 위치를 끄고 비워 두면 전송하지 않습니다.",12,Paint.Muted);
            note.TextWrapping=TextWrapping.Wrap;
            note.Margin=new Thickness(15,0,15,6);
            rows.Children.Add(note);
            var line=new DockPanel {Margin=new Thickness(15,0,15,4)};
            var box=new TextBox {Text=Preferences.Current.WeatherPlace??"",Background=Paint.ControlWhite,Foreground=Paint.OnLight,BorderThickness=new Thickness(0),FontSize=14,Padding=new Thickness(12,8,12,8),MaxLength=80};
            System.Windows.Automation.AutomationProperties.SetName(box,"날씨 위치");
            var result=Paint.TextBlock("",12,Paint.Muted);
            result.Margin=new Thickness(15,0,15,8);
            Button apply=null;
            apply=Paint.Action("적용",async()=>
            {
                apply.IsEnabled=false;
                result.Text="위치를 찾는 중입니다";
                bool found=await WeatherService.SetPlace(box.Text);
                apply.IsEnabled=true;
                if(found)box.Text=Preferences.Current.WeatherPlace??"";
                result.Text=!found?"위치를 찾지 못했습니다":WeatherService.Configured?Preferences.Current.WeatherPlace+" 날씨를 표시합니다":"날씨를 사용하지 않습니다";
            });
            apply.Margin=new Thickness(8,0,0,0);
            DockPanel.SetDock(apply,Dock.Right);
            line.Children.Add(apply);
            line.Children.Add(box);
            rows.Children.Add(line);
            rows.Children.Add(result);
            var channelLabel=Paint.TextBlock("The Weather Channel API 키",13,Paint.Text);
            channelLabel.Margin=new Thickness(15,10,15,2);
            rows.Children.Add(channelLabel);
            var channelNote=Paint.TextBlock("The Weather Company에서 발급받은 키가 있어야 합니다. 키가 없거나 조회에 실패하면 Open-Meteo를 사용합니다. 키는 이 PC의 설정 파일에 그대로 저장됩니다.",12,Paint.Muted);
            channelNote.TextWrapping=TextWrapping.Wrap;
            channelNote.Margin=new Thickness(15,0,15,6);
            rows.Children.Add(channelNote);
            var channelLine=new DockPanel {Margin=new Thickness(15,0,15,12)};
            var channelKey=new PasswordBox {Password=Preferences.Current.WeatherChannelKey??"",Background=Paint.ControlWhite,Foreground=Paint.OnLight,BorderThickness=new Thickness(0),FontSize=14,Padding=new Thickness(12,8,12,8),MaxLength=128};
            System.Windows.Automation.AutomationProperties.SetName(channelKey,"The Weather Channel API 키");
            var channelSave=Paint.Action("저장",()=>
            {
                string value=channelKey.Password.Trim();
                Preferences.SetMediaAppearance(p=>p.WeatherChannelKey=value.Length==0?null:value);
                WeatherService.Reset();
            });
            channelSave.Margin=new Thickness(8,0,0,0);
            DockPanel.SetDock(channelSave,Dock.Right);
            channelLine.Children.Add(channelSave);
            channelLine.Children.Add(channelKey);
            rows.Children.Add(channelLine);
        }
    }
}
