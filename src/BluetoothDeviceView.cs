using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.Devices.Enumeration;
namespace OneTaskBar
{
    internal sealed class BluetoothPeer
    {
        internal string Key,Name,Icon="bluetooth";internal bool Paired,Connected,ConnectionKnown;internal DeviceInformation Device;internal Guid? Container;
        internal string State {get{return Connected?"연결됨":Paired?(ConnectionKnown?"등록됨 · 연결 안 됨":"등록됨"):"새 기기 · 페어링 가능";}}
        internal static BluetoothPeer Read(DeviceInformation device){var row=From(device.Id,device.Name,device.Pairing.IsPaired,device.Properties);row.Device=device;return row;}
        internal static BluetoothPeer From(string key,string name,bool paired,IReadOnlyDictionary<string,object> properties){object connection;var row=new BluetoothPeer {Key=key,Name=name,Paired=paired,Container=ContainerId(properties),Connected=Boolean(properties,"System.Devices.Aep.IsConnected"),ConnectionKnown=properties.TryGetValue("System.Devices.Aep.IsConnected",out connection)&&connection is bool};int major=Number(properties,"System.Devices.Aep.Bluetooth.Cod.Major"),minor=Number(properties,"System.Devices.Aep.Bluetooth.Cod.Minor");row.Icon=major==1?"computer":major==2?"phone":major==4?(minor==1||minor==2||minor==6?"headphones":minor==4?"microphone":"volume"):major==5&&minor>=0?((minor&32)!=0?"mouse":(minor&16)!=0?"keyboard":"bluetooth"):"bluetooth";return row;}
        internal static Guid? ContainerId(IReadOnlyDictionary<string,object> properties){object value;Guid id;return properties.TryGetValue("System.Devices.Aep.ContainerId",out value)&&value!=null&&Guid.TryParse(value.ToString(),out id)&&id!=Guid.Empty?(Guid?)id:null;}
        static bool Boolean(IReadOnlyDictionary<string,object> properties,string name){object value;return properties.TryGetValue(name,out value)&&value is bool&&(bool)value;}
        static int Number(IReadOnlyDictionary<string,object> properties,string name){object value;try{return properties.TryGetValue(name,out value)&&value!=null?Convert.ToInt32(value):-1;}catch{return -1;}}
    }
    internal static class BluetoothDeviceView
    {
        internal static string ConnectedCaption(IEnumerable<BluetoothPeer> peers)
        {
            var connected=peers.Where(p=>p.Connected).GroupBy(p=>p.Container.HasValue?"id:"+p.Container.Value.ToString("N"):"name:"+(String.IsNullOrWhiteSpace(p.Name)?p.Key:BudsConnectionTracker.Normalize(p.Name)),StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderBy(p=>System.Text.RegularExpressions.Regex.IsMatch(p.Name??"",@"^LE[_ -]|[ _-]LE$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ThenBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).First()).ToList();
            return connected.Count==0?"연결 안 됨":connected.Count==1?(String.IsNullOrWhiteSpace(connected[0].Name)?"블루투스 기기":connected[0].Name):"기기 "+connected.Count+"개 연결됨";
        }
        internal static Button Create(BluetoothPeer peer,Action select)
        {
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(49)});row.ColumnDefinitions.Add(new ColumnDefinition());var glyph=new Glyph(peer.Icon,27){Color=peer.Connected?Paint.Ink:Paint.Text};row.Children.Add(new Border {Width=39,Height=39,CornerRadius=new CornerRadius(20),Background=peer.Connected?Paint.ControlWhite:Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Left,Child=glyph});
            var labels=new StackPanel {VerticalAlignment=VerticalAlignment.Center};var name=Paint.TextBlock(peer.Name,14,Paint.Text);name.FontWeight=peer.Connected?FontWeights.SemiBold:FontWeights.Normal;name.TextTrimming=TextTrimming.CharacterEllipsis;labels.Children.Add(name);var state=Paint.TextBlock(peer.State,11,Paint.Muted);state.Margin=new Thickness(0,3,0,0);labels.Children.Add(state);Grid.SetColumn(labels,1);row.Children.Add(labels);var button=Paint.Button(row,select,peer.Name+" · "+peer.State);button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(12,10,14,10);button.Margin=new Thickness(0,4,0,4);button.Background=QuickPanel.Card;System.Windows.Automation.AutomationProperties.SetItemStatus(button,peer.State);return button;
        }
        internal static List<BluetoothPeer> Rows(IEnumerable<DeviceInformation> devices){return devices.Select(BluetoothPeer.Read).OrderByDescending(p=>p.Paired).ThenByDescending(p=>p.Connected).ThenBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).ToList();}
    }
}
