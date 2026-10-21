using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed class DetailWindow : GlassWindow
    {
        readonly ShellController shell;readonly string page;
        readonly StackPanel body=new StackPanel();readonly TextBlock status=Paint.TextBlock("",12,Paint.Muted);
        readonly DispatcherTimer ticker=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};
        bool closed,busy,changingMode;Guid? selectedMode;
        internal DetailWindow(ShellController controller,string kind)
        {
            shell=controller;page=kind;Title="One TaskBar · "+PageName(kind);Width=380;Height=540;Radius=30;Dark=false;Surface.CornerRadius=new CornerRadius(30);Surface.Background=GrayPanel.Glass;Surface.BorderBrush=Paint.Brush("#48FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var root=new DockPanel {Margin=new Thickness(20)};
            var header=new Grid {Height=44,Margin=new Thickness(0,0,0,12)};
            var back=Paint.Action("‹",()=> {Hide();shell.ToggleQuick(null);});back.Width=34;back.Background=Brushes.Transparent;back.Padding=new Thickness(0);back.HorizontalAlignment=HorizontalAlignment.Left;header.Children.Add(back);
            var title=Paint.TextBlock(PageName(kind),21,Paint.Text);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(40,0,30,0);header.Children.Add(title);
            var refresh=Paint.Action("↻",()=>Reload());refresh.Width=30;refresh.Background=Brushes.Transparent;refresh.Padding=new Thickness(0);refresh.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(refresh);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(5,10,5,0);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
            root.Children.Add(new SmoothScrollViewer {Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Surface.Child=root;GrayPanel.Apply(root);
            Closed+=(s,e)=> {closed=true;ticker.Stop();};Deactivated+=(s,e)=> {if(!Program.Inspectable&&IsVisible&&!IsMouseOver)Hide();};Reload();
            ticker.Tick+=(s,e)=> {if(page=="focus")FocusStatus();else if(page=="power"&&!busy&&!changingMode){var mode=Native.ReadPerformanceMode();if(mode.Available&&selectedMode!=mode.Selected)Reload();}};ticker.Start();
        }
        static string PageName(string kind) {return kind=="wifi"?"Wi-Fi":kind=="bluetooth"?"블루투스":kind=="sound"?"소리 및 출력":kind=="power"?"배터리 및 전원":kind=="focus"?"집중 타이머":"디스플레이";}
        static Border Card(UIElement child) {return new Border {CornerRadius=new CornerRadius(22),Background=GrayPanel.Card,Padding=new Thickness(13),Margin=new Thickness(0,5,0,5),Child=child};}
        void Text(string text) {var label=Paint.TextBlock(text,12,Paint.Muted);label.TextWrapping=TextWrapping.Wrap;label.Margin=new Thickness(5,10,5,10);body.Children.Add(label);}
        void Action(string text,Action action) {var button=Paint.Action(text,action);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,5,0,5);button.Padding=new Thickness(13,12,13,12);body.Children.Add(button);}
        async void Reload()
        {
            if(busy||closed)return;busy=true;body.Children.Clear();status.Text="불러오는 중…";
            try {
                if(page=="wifi") {
                    Action((shell.Services.WifiOn??false)?"Wi-Fi 끄기":"Wi-Fi 켜기",()=>shell.Radio("wifi"));
                    var networks=await Task.Run(()=>Native.WifiNetworks());if(closed)return;
                    foreach(var network in networks){var label=network.Name+"\n"+(network.Connected?"연결됨  ·  ":"")+(network.Secure?"보안  ·  ":"")+"신호 "+network.Signal;
                        var item=Paint.Action(label,()=>SelectNetwork(network));item.HorizontalContentAlignment=HorizontalAlignment.Left;item.HorizontalAlignment=HorizontalAlignment.Stretch;item.Padding=new Thickness(13,11,13,11);item.Margin=new Thickness(0,4,0,4);body.Children.Add(item);}
                    status.Text=networks.Count==0?"사용 가능한 네트워크가 없습니다. Wi-Fi 상태를 확인하거나 새로 고침해 주세요.":"저장된 네트워크는 바로 연결합니다. 새 네트워크는 비밀번호를 입력해 연결할 수 있습니다.";
                } else if(page=="bluetooth") {
                    Action((shell.Services.BluetoothOn??false)?"블루투스 끄기":"블루투스 켜기",()=>shell.Radio("bluetooth"));
                    var devices=await BluetoothOperations.Read();if(closed)return;
                    foreach(var device in devices){var d=device;Action(d.Name+"  ·  "+(d.Pairing.IsPaired?"페어링됨 · 해제":"페어링"),async()=> {try{status.Text=await BluetoothOperations.Pair(d);}catch(Exception e){status.Text=e.Message;}});}
                    status.Text=devices.Count==0?"주변 기기를 검색하지 못했습니다. 기기의 페어링 모드를 켜고 새로 고침해 주세요.":"기기를 눌러 페어링하거나 해제할 수 있습니다. PIN 확인 창이 표시되면 두 기기의 번호를 확인해 주세요.";
                } else if(page=="sound") {
                    var slider=new PillSlider("volume") {Value=shell.Audio.Muted?0:shell.Audio.Value,Light=false};slider.Changed+=v=>shell.Volume(v);body.Children.Add(Card(slider));Action(shell.Audio.Muted?"음소거 해제":"음소거",()=> {shell.Mute();Reload();});
                    var outputs=await Task.Run(()=>Native.AudioOutputs());if(closed)return;
                    foreach(var output in outputs){var d=output;Action((d.Current?"✓  ":"")+d.Name,async()=> {try{await Task.Run(()=>Native.SetAudioOutput(d.Id));shell.Audio.Dispose();shell.Audio.Read();Reload();}catch(Exception e){status.Text=e.Message;}});}
                    status.Text="출력 장치를 선택하면 Windows의 기본 소리 출력도 함께 바뀝니다.";
                } else if(page=="power") {
                    if(PowerDisplay.Slot(shell.Power,Preferences.Current.DesktopPowerWatts)>0)body.Children.Add(Card(new BatteryPill {Width=140,Height=40,State=shell.Power}));
                    Text(shell.Power.Known?(shell.Power.Connected?"전원에 연결되어 있습니다":"배터리를 사용하고 있습니다"):"배터리가 없는 PC · Windows 전원 관리를 사용합니다");
                    if(!shell.Power.Known&&Preferences.Current.DesktopPowerWatts>0)Text("표시된 W 값은 사용자가 직접 입력한 값이며 실시간 소비전력 측정값이 아닙니다.");
                    var mode=Native.ReadPerformanceMode();selectedMode=mode.Available?(Guid?)mode.Selected:null;Text("Windows 성능 모드");
                    if(mode.Available){foreach(var option in Native.PerformanceModes()){var choice=option;var button=Paint.Action((choice.Id==mode.Selected?"✓  ":"")+choice.Name,()=>ChangeMode(choice.Id));button.HorizontalContentAlignment=HorizontalAlignment.Left;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(13,11,13,11);button.Margin=new Thickness(0,3,0,3);button.BorderBrush=Brushes.Transparent;button.BorderThickness=new Thickness(0);button.Background=Paint.Brush(choice.Id==mode.Selected?"#3026362D":"#18FFFFFF");body.Children.Add(button);}}
                    else Text("이 Windows 환경은 성능 모드 변경을 지원하지 않습니다.");
                    Text("전원 관리 옵션");
                    var plans=await Task.Run(()=>Native.PowerPlans());if(closed)return;foreach(var plan in plans){var p=plan;Action((p.Current?"✓  ":"")+p.Name,()=> {try{Native.SetPowerPlan(p.Id);Reload();}catch(Exception e){status.Text=e.Message;}});}status.Text="Windows의 실제 전원 관리 옵션을 표시합니다.";
                } else if(page=="focus") {
                    Text("집중할 시간을 정해 보세요. 타이머가 끝나면 알려 드립니다.");foreach(int value in new[]{15,30,60}){int minutes=value;Action(minutes+"분 시작",()=> {shell.FocusUntil=DateTime.UtcNow.AddMinutes(minutes);FocusStatus();});}
                    Action("타이머 종료",()=> {shell.FocusUntil=DateTime.MinValue;FocusStatus();});FocusStatus();
                } else {
                    foreach(var monitor in Forms.Screen.AllScreens)Text((monitor.Primary?"주 디스플레이":"디스플레이")+"\n"+monitor.Bounds.Width+" × "+monitor.Bounds.Height);
                    var slider=new PillSlider("brightness") {Value=Math.Max(0,shell.Brightness),IsEnabled=shell.Brightness>=0,Light=false};slider.Changed+=v=>shell.Bright(v);body.Children.Add(Card(slider));
                    status.Text=shell.Brightness>=0?"노트북의 실제 디스플레이 밝기를 조절합니다.":"이 디스플레이는 Windows 밝기 조절을 지원하지 않습니다.";
                }
            }catch(Exception e){status.Text=e.Message;AppLog.Write("details-"+page,e.Message);}finally{busy=false;GrayPanel.Apply(Surface);}
        }
        void FocusStatus() {status.Text=shell.FocusUntil>DateTime.UtcNow?"남은 시간  "+(shell.FocusUntil-DateTime.UtcNow).ToString(@"mm\:ss"):"진행 중인 타이머가 없습니다";}
        async void ChangeMode(Guid mode)
        {
            if(changingMode||closed)return;changingMode=true;status.Text="성능 모드를 적용하는 중…";
            try {await Task.Run(()=>Native.SetPerformanceMode(mode));if(!closed){var current=Native.ReadPerformanceMode();if(!current.Available||current.Selected!=mode)throw new InvalidOperationException("Windows가 선택한 모드를 적용하지 못했습니다. 현재 전원 관리 옵션을 확인해 주세요.");Reload();}}
            catch(Exception e){if(!closed)status.Text="성능 모드를 변경하지 못했습니다: "+e.Message;}
            finally {changingMode=false;}
        }
        void SelectNetwork(WifiNetwork network)
        {
            if(network.Connected||!network.Secure||!String.IsNullOrEmpty(network.Profile)){Connect(network,null);return;}
            var content=new StackPanel();var label=Paint.TextBlock(network.Name+" 비밀번호",13,Paint.Text);content.Children.Add(label);var password=new PasswordBox {FontFamily=SystemFonts.MessageFontFamily,FontSize=15,Padding=new Thickness(8),Margin=new Thickness(0,10,0,10)};content.Children.Add(password);
            content.Children.Add(Paint.Action("연결",()=> {Connect(network,password.Password);password.Clear();}));GrayPanel.Apply(content);body.Children.Insert(1,Card(content));password.Focus();
        }
        async void Connect(WifiNetwork network,string password)
        {
            try {status.Text=network.Connected?"연결 해제 중…":"연결 중…";await Task.Run(()=>Native.ConnectWifi(network,password));await Task.Delay(1500);if(!closed)Reload();}
            catch(Exception e){status.Text=e.Message;}
        }
    }
}
