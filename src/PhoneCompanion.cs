using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    internal sealed class PhoneSnapshot
    {
        internal string Name="휴대폰 연결",Status="휴대폰과 연결 앱에서 기기를 연결해 주세요.",Battery;
        internal bool Available;internal bool? Connected,Bluetooth;
        internal DateTime Updated;internal BitmapSource Image;
        internal readonly Dictionary<string,string> Actions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    }
    // Reads only the header and generic shortcuts in Phone Link's native Start card.
    // No messages, contacts, recent calls, credentials, network requests or idle watcher.
    internal static class PhoneCompanion
    {
        static string cachedPath;static DateTime cachedStamp;static PhoneSnapshot cached;
        internal static int Reads,Decodes;
        static IDictionary<string,object> Map(object value){return value as IDictionary<string,object>;}
        static object Get(object value,string key){var map=Map(value);object result;return map!=null&&map.TryGetValue(key,out result)?result:null;}
        static string Text(object value,string key){return Get(value,key) as string??"";}
        static IEnumerable<object> Many(object value){return value is IEnumerable&&!(value is string)&&!(value is IDictionary<string,object>)?((IEnumerable)value).Cast<object>():Enumerable.Empty<object>();}
        static IEnumerable<object> Nodes(object node,int depth=0)
        {
            if(depth>9||Map(node)==null)yield break;yield return node;foreach(string field in new[]{"items","columns","images"})foreach(var child in Many(Get(node,field)).Take(40))foreach(var value in Nodes(child,depth+1))yield return value;
        }
        static string Clean(string value,int length=120){return new string((value??"").Where(c=>!Char.IsControl(c)).Take(length).ToArray()).Trim();}
        internal static string SafeAction(string value)
        {
            Uri uri;if(String.IsNullOrEmpty(value)||value.Length>4096||!Uri.TryCreate(value,UriKind.Absolute,out uri)||!uri.Scheme.Equals("ms-phone",StringComparison.OrdinalIgnoreCase))return null;
            int colon=value.IndexOf(':');string route=value.Substring(colon+1).Split('?')[0].Trim('/').ToLowerInvariant();if(!new[]{"","messages","calling","photos","filetransfer","remoting"}.Contains(route))return null;
            var query=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);int question=value.IndexOf('?');if(question>=0)foreach(string pair in value.Substring(question+1).Split('&')){var parts=pair.Split(new[]{'='},2);if(parts.Length==2)query[Uri.UnescapeDataString(parts[0])]=Uri.UnescapeDataString(parts[1]);}
            string package;if(route=="remoting"&&(!query.TryGetValue("packagename",out package)||package!="yourphone.phonescreen"))return null;
            // Carry only the selected device identity. Call numbers, arbitrary app launches
            // and other arguments from a recent-activity card cannot become shortcuts.
            string device;Guid id;string result="ms-phone:"+route+"?ref=OneTaskBar";if(query.TryGetValue("deviceid",out device)&&Guid.TryParse(device,out id))result+="&deviceid="+id.ToString("D");if(route=="remoting")result+="&packagename=yourphone.phonescreen";return result;
        }
        internal static PhoneSnapshot Parse(string json,DateTime stamp)
        {
            var empty=new PhoneSnapshot();if(String.IsNullOrEmpty(json)||json.Length>1048576)return empty;
            try {
                var root=new JavaScriptSerializer {MaxJsonLength=1048576,RecursionLimit=32}.DeserializeObject(json);var body=Many(Get(root,"body")).Take(12).ToList();int nameIndex=body.FindIndex(n=>Text(n,"type")=="TextBlock"&&!String.IsNullOrWhiteSpace(Text(n,"text")));if(nameIndex<0)return empty;
                var result=new PhoneSnapshot {Name=Clean(Text(body[nameIndex],"text"),80),Available=true,Updated=stamp};
                var statusRow=body.Skip(nameIndex+1).FirstOrDefault(n=>Text(n,"type")=="ColumnSet");var status=statusRow==null?new List<object>():Nodes(statusRow).ToList();
                var images=status.Where(n=>Text(n,"type")=="Image").ToList();string connection=images.Select(n=>Clean(Text(n,"altText"))).FirstOrDefault(t=>t.Length>0);result.Status=String.IsNullOrEmpty(connection)?"Phone Link에서 받은 연결 정보":connection;
                string lower=result.Status.ToLowerInvariant();if(lower.Contains("disconnected")||lower.Contains("not connected")||lower.Contains("연결되지")||lower.Contains("연결 안")||lower.Contains("연결 끊"))result.Connected=false;else if(lower.Contains("connected")||lower.Contains("연결됨"))result.Connected=true;
                string bluetooth=images.Select(n=>Text(n,"altText")).FirstOrDefault(t=>t.IndexOf("bluetooth",StringComparison.OrdinalIgnoreCase)>=0);if(bluetooth!=null){string text=bluetooth.ToLowerInvariant();if(text.Contains("꺼져")||text.Contains("off"))result.Bluetooth=false;else if(text.Contains("켜져")||text.Contains("on"))result.Bluetooth=true;}
                foreach(var node in status.Where(n=>Text(n,"type")=="TextBlock")){var percent=Regex.Match(Text(node,"text"),@"^\s*(100|[0-9]{1,2})\s*%\s*$");if(percent.Success){result.Battery=percent.Groups[1].Value+"%";break;}}
                string picture=body.Take(nameIndex).SelectMany(n=>Nodes(n)).Where(n=>Text(n,"type")=="Image").Select(n=>Text(n,"url")).FirstOrDefault(u=>u.StartsWith("data:image/",StringComparison.OrdinalIgnoreCase));
                if(picture!=null&&picture.Length<=524288){int start=picture.IndexOf(";base64,",StringComparison.OrdinalIgnoreCase);if(start>0)try{var bytes=Convert.FromBase64String(picture.Substring(start+8));using(var stream=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=160;image.StreamSource=stream;image.EndInit();image.Freeze();result.Image=image;Decodes++;}}catch{}}
                foreach(var node in body.SelectMany(n=>Nodes(n)).Take(300)){
                    var action=Get(node,"selectAction");string safe=SafeAction(Text(action,"url"));if(safe==null)continue;string route=safe.Substring("ms-phone:".Length).Split('?')[0];if(route==""||route=="remoting")continue;if(!result.Actions.ContainsKey(route))result.Actions[route]=safe;
                }
                foreach(var action in Many(Get(root,"companionActions")).Take(12)){string safe=SafeAction(Text(action,"url"));if(safe==null)continue;string route=safe.Substring("ms-phone:".Length).Split('?')[0];if(route=="")result.Actions["open"]=safe;else if(route=="remoting")result.Actions["screen"]=safe;}
                return result;
            }catch(ArgumentException){return empty;}catch(InvalidOperationException){return empty;}catch(FormatException){return empty;}
        }
        internal static PhoneSnapshot Read(bool force=false)
        {
            string relative=Path.Combine("Packages","Microsoft.YourPhone_8wekyb3d8bbwe","LocalState","StartMenu","StartMenuCompanion.json");
            foreach(string local in new[]{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","Local")}.Distinct(StringComparer.OrdinalIgnoreCase))try {
                var value=ReadFile(Path.Combine(local,relative),force);if(value.Available)return value;
            }catch(IOException){}catch(UnauthorizedAccessException){}
            return new PhoneSnapshot();
        }
        internal static PhoneSnapshot ReadFile(string path,bool force=false)
        {
            var file=new FileInfo(path);if(!file.Exists||file.Length>1048576)return new PhoneSnapshot();DateTime stamp=file.LastWriteTimeUtc;if(!force&&cached!=null&&cachedPath==path&&cachedStamp==stamp)return cached;
            string json;using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))json=reader.ReadToEnd();Reads++;var value=Parse(json,stamp);if(value.Available){cached=value;cachedPath=path;cachedStamp=stamp;}return value;
        }
    }
    internal sealed class PhonePane : Border
    {
        readonly TextBlock name=Paint.TextBlock("휴대폰 연결",18,Paint.Text),status=Paint.TextBlock("연결 정보를 확인하는 중입니다.",12,Paint.Muted),updated=Paint.TextBlock("",10,Paint.Muted),battery=Paint.TextBlock("",12,Paint.Text);
        readonly Border portrait=new Border {Height=108,Margin=new Thickness(0,18,0,12)};readonly Glyph connection=new Glyph("network",14),bluetooth=new Glyph("bluetooth",15),batteryIcon=new Glyph("battery",17);
        readonly Dictionary<string,Button> actions=new Dictionary<string,Button>();readonly Action<string> open;readonly Button screen,primary;PhoneSnapshot snapshot;
        internal PhonePane(Action<string> launch,Action refresh)
        {
            open=launch;CornerRadius=new CornerRadius(27);Background=QuickPanel.Card;Padding=new Thickness(16,15,16,15);var root=new DockPanel();
            var heading=new Grid {Height=30};var label=Paint.TextBlock("Windows와 연결",14,Paint.Text);label.FontWeight=FontWeights.SemiBold;heading.Children.Add(label);var reload=Paint.Button(new Glyph("rotation",15),refresh,"연결 정보 새로고침");reload.Width=reload.Height=29;reload.Background=Brushes.Transparent;reload.Padding=new Thickness(6);reload.HorizontalAlignment=HorizontalAlignment.Right;heading.Children.Add(reload);DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
            var footer=new StackPanel {Margin=new Thickness(0,10,0,0)};updated.TextWrapping=TextWrapping.Wrap;updated.Margin=new Thickness(5,0,0,8);footer.Children.Add(updated);primary=Paint.Action("휴대폰과 연결 열기  ›",()=>Launch("open"));primary.HorizontalContentAlignment=HorizontalAlignment.Left;primary.FontSize=12;primary.Height=38;primary.Background=Paint.Brush("#18FFFFFF");footer.Children.Add(primary);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var body=new StackPanel();portrait.Child=new Glyph("phone",76);body.Children.Add(portrait);name.FontWeight=FontWeights.SemiBold;name.TextAlignment=TextAlignment.Center;name.TextWrapping=TextWrapping.Wrap;name.MaxHeight=62;body.Children.Add(name);status.TextAlignment=TextAlignment.Center;status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,9,0,8);body.Children.Add(status);
            var state=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};connection.Margin=new Thickness(0,0,12,0);state.Children.Add(connection);bluetooth.Margin=new Thickness(0,0,12,0);state.Children.Add(bluetooth);batteryIcon.Margin=new Thickness(0,0,5,0);state.Children.Add(batteryIcon);state.Children.Add(battery);body.Children.Add(state);
            var shortcuts=new StackPanel {Margin=new Thickness(0,22,0,0)};foreach(var item in new[]{new[]{"messages","message","메시지"},new[]{"calling","call","통화"},new[]{"photos","photo","사진"},new[]{"filetransfer","send","파일 보내기"}}){string key=item[0];var row=new StackPanel {Orientation=Orientation.Horizontal};row.Children.Add(new Glyph(item[1],20){Margin=new Thickness(0,0,14,0)});row.Children.Add(Paint.TextBlock(item[2],13,Paint.Text));var button=Paint.Button(row,()=>Launch(key),item[2]+" · 휴대폰과 연결에서 열기");button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Background=Brushes.Transparent;button.Height=43;button.Margin=new Thickness(0,0,0,3);shortcuts.Children.Add(button);actions[key]=button;}body.Children.Add(shortcuts);
            screen=Paint.Action("휴대폰 화면  ›",()=>Launch("screen"));screen.Height=35;screen.FontSize=12;screen.HorizontalContentAlignment=HorizontalAlignment.Left;screen.Background=Brushes.Transparent;body.Children.Add(screen);root.Children.Add(new SmoothScrollViewer {Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Child=root;Update(new PhoneSnapshot());
        }
        internal bool HasPhone {get{return snapshot!=null&&snapshot.Available;}}
        internal int EnabledActions {get{return actions.Values.Count(b=>b.IsEnabled);}}
        internal Button ActionButton(string key){Button button;return actions.TryGetValue(key,out button)?button:null;}
        internal void Update(PhoneSnapshot value)
        {
            snapshot=value;name.Text=value.Name;status.Text=value.Status;
            if(value.Image!=null){var image=new Image {Source=value.Image,Width=46,Height=100,Stretch=Stretch.UniformToFill,Clip=new RectangleGeometry(new Rect(0,0,46,100),6,6)};portrait.Child=new Border {Width=52,Height=108,CornerRadius=new CornerRadius(9),Background=Paint.Brush("#FF101713"),BorderBrush=Paint.Brush("#65FFFFFF"),BorderThickness=new Thickness(1.5),Child=image,HorizontalAlignment=HorizontalAlignment.Center};}else portrait.Child=new Glyph("phone",76);
            connection.Visibility=value.Available?Visibility.Visible:Visibility.Collapsed;connection.Color=value.Connected==true?Paint.Brush("#FF9BE4C1"):Paint.Muted;connection.ToolTip=value.Status;bluetooth.Visibility=value.Bluetooth.HasValue?Visibility.Visible:Visibility.Collapsed;bluetooth.Opacity=value.Bluetooth==true?1:.4;bluetooth.ToolTip=value.Bluetooth==true?"휴대폰 Bluetooth 켜짐":"휴대폰 Bluetooth 꺼짐";
            battery.Text=value.Battery??"";batteryIcon.Visibility=String.IsNullOrEmpty(value.Battery)?Visibility.Collapsed:Visibility.Visible;foreach(var pair in actions)pair.Value.IsEnabled=value.Actions.ContainsKey(pair.Key);screen.Visibility=value.Actions.ContainsKey("screen")?Visibility.Visible:Visibility.Collapsed;
            updated.Text=value.Available?"최근 수신 · "+value.Updated.ToLocalTime().ToString("M/d H:mm")+((DateTime.UtcNow-value.Updated).TotalMinutes>3?"\n휴대폰과 연결에서 갱신할 수 있습니다.":""):"연결 후 기기 상태와 바로가기가 표시됩니다.";primary.Content=Paint.TextBlock(value.Available?"휴대폰과 연결 열기  ›":"휴대폰 연결하기  ›",12,Paint.Text);
        }
        void Launch(string key){string uri;if(snapshot!=null&&snapshot.Actions.TryGetValue(key,out uri))open(uri);else if(key=="open")open("ms-settings:mobile-devices-addphone-direct");}
    }
}
