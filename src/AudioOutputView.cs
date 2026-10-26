using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace OneTaskBar
{
    internal sealed class AudioOutputView : StackPanel
    {
        readonly List<Button> choices=new List<Button>();bool switching;
        internal readonly PillSlider Volume;
        internal AudioOutputView(IEnumerable<OutputDevice> devices,MediaState media,double volume,bool muted,Func<string,Task> choose,Action<double> changeVolume,Action mute,Action play,Action openApp,Action<string> feedback,ImageSource budsSymbol=null)
        {
            if(media!=null&&media.Active){var header=new Grid {Margin=new Thickness(4,0,4,18)};header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(43)});header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(38)});if(media.Artwork!=null)header.Children.Add(new Border {Width=34,Height=34,CornerRadius=new CornerRadius(9),Background=new ImageBrush(media.Artwork){Stretch=Stretch.UniformToFill}});else header.Children.Add(new Glyph("volume",25));var text=new StackPanel {VerticalAlignment=VerticalAlignment.Center};var title=Paint.TextBlock(media.Title??"미디어",13,Paint.Text);title.FontWeight=FontWeights.SemiBold;title.TextTrimming=TextTrimming.CharacterEllipsis;text.Children.Add(title);var artist=Paint.TextBlock(media.Artist??"",11,Paint.Muted);artist.TextTrimming=TextTrimming.CharacterEllipsis;text.Children.Add(artist);var app=Paint.Button(text,openApp,"현재 재생 앱 열기");app.Background=Brushes.Transparent;app.Padding=new Thickness(0);app.HorizontalContentAlignment=HorizontalAlignment.Stretch;Grid.SetColumn(app,1);header.Children.Add(app);var playback=Paint.Button(new Glyph(media.Playing?"pause":"play",19),play,"재생 · 일시 정지");playback.Width=playback.Height=34;playback.Padding=new Thickness(0);playback.Resources["ButtonRadius"]=new CornerRadius(17);Grid.SetColumn(playback,2);header.Children.Add(playback);Children.Add(header);}
            foreach(var device in devices.OrderByDescending(d=>d.Current)){
                var row=new Grid {Margin=new Thickness(0,2,0,2),MinHeight=device.Current?82:56};row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(42)});row.ColumnDefinitions.Add(new ColumnDefinition());var icon=new Glyph(Kind(device.Name),23){VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};if(BudsInfo.IsBuds(device.Name))icon.SetDeviceIcon(budsSymbol);row.Children.Add(icon);
                var body=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,9,12,9)};var label=Paint.TextBlock(device.Name,13,device.Current?Paint.Text:Paint.Muted);label.FontWeight=device.Current?FontWeights.SemiBold:FontWeights.Normal;label.TextWrapping=TextWrapping.Wrap;label.MaxHeight=38;label.TextTrimming=TextTrimming.CharacterEllipsis;label.ToolTip=device.Name;body.Children.Add(label);Grid.SetColumn(body,1);row.Children.Add(body);
                if(device.Current){Volume=new PillSlider("volume") {Value=muted?0:volume,Height=24,ShowIcon=false,Light=true,DarkTrack=true,NoTrackBorder=true,Margin=new Thickness(0,9,34,1)};Volume.Changed+=changeVolume;body.Children.Add(Volume);var muteButton=Paint.Button(new Glyph(muted?"mute":"volume",17),mute,"음소거 켜기 · 끄기");muteButton.Width=muteButton.Height=30;muteButton.Padding=new Thickness(0);muteButton.HorizontalAlignment=HorizontalAlignment.Right;muteButton.VerticalAlignment=VerticalAlignment.Bottom;muteButton.Margin=new Thickness(0,0,2,7);muteButton.Background=Brushes.Transparent;Grid.SetColumn(muteButton,1);row.Children.Add(muteButton);Children.Add(new Border {CornerRadius=new CornerRadius(23),Background=QuickPanel.Card,Padding=new Thickness(5,0,0,0),Child=row});}
                else{string id=device.Id;var button=Paint.Button(row,async()=>{if(switching)return;switching=true;foreach(var item in choices)item.IsEnabled=false;feedback("출력 장치를 전환하는 중…");try{await choose(id);}catch(Exception error){feedback(error.Message);}finally{switching=false;foreach(var item in choices)item.IsEnabled=true;}},device.Name+" 출력으로 전환");button.Tag=id;button.Padding=new Thickness(5,0,0,0);button.Background=Brushes.Transparent;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;choices.Add(button);Children.Add(button);Children.Add(new Border {Height=.7,Margin=new Thickness(47,0,8,0),Background=Paint.Brush("#25FFFFFF")});}
            }
            if(!devices.Any())Children.Add(Paint.TextBlock("사용 가능한 출력 장치가 없습니다",13,Paint.Muted));
        }
        static string Kind(string name){if(BudsInfo.IsBuds(name)||System.Text.RegularExpressions.Regex.IsMatch(name??"","headphone|headset|헤드폰|헤드셋|이어폰",System.Text.RegularExpressions.RegexOptions.IgnoreCase))return "headphones";return System.Text.RegularExpressions.Regex.IsMatch(name??"","HDMI|Display|NVIDIA|AMD|디스플레이",System.Text.RegularExpressions.RegexOptions.IgnoreCase)?"computer":"volume";}
    }
}
