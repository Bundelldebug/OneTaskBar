using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
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
        internal string PageKey {get{return page;}}
        readonly StackPanel body=new StackPanel();readonly TextBlock status=Paint.TextBlock("",12,Paint.Muted);
        readonly DispatcherTimer ticker=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};
        readonly ScanIndicator scanIndicator=new ScanIndicator();readonly Button refreshButton;
        readonly Func<CancellationToken,Task<WirelessSnapshot>> scanLoader;
        readonly Dictionary<int,Button> samsungButtons=new Dictionary<int,Button>();readonly Dictionary<Guid,Button> windowsButtons=new Dictionary<Guid,Button>();
        readonly TextBlock samsungStatus=Paint.TextBlock("",11,Paint.Muted);SamsungModePicker samsungPicker;bool checkingSamsung;
        CancellationTokenSource scan;int scanVersion;internal int ScanRequests {get;private set;}
        internal bool Scanning {get{return scan!=null;}}
        internal ScanIndicator ScanGlyph {get{return scanIndicator;}}
        internal string StatusText {get{return status.Text;}}
        internal int BodyRows {get{return body.Children.Count;}}
        bool closed,busy,changingMode,changingSamsung;Guid? selectedMode;int samsungMode=-1;
        int deviceActions;
        internal override bool CanRelease {get{return deviceActions==0&&!busy&&!changingMode&&!changingSamsung&&!checkingSamsung;}}
        internal bool TimerActive {get{return ticker.IsEnabled;}}
        internal DetailWindow(ShellController controller,string kind,Func<CancellationToken,Task<WirelessSnapshot>> loader=null)
        {
            shell=controller;page=kind;scanLoader=loader;Title="One TaskBar · "+PageName(kind);Width=380;Height=540;Radius=30;Dark=true;Surface.CornerRadius=new CornerRadius(30);Surface.Background=QuickPanel.Glass;Surface.BorderBrush=Paint.Brush("#48FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var root=new DockPanel {Margin=new Thickness(20)};
            var header=new Grid {Height=44,Margin=new Thickness(0,0,0,12)};
            var back=Paint.Button(new Glyph("back",18),()=> {Hide();shell.ToggleQuick(null);},"빠른 설정으로 돌아가기");back.Width=back.Height=34;back.VerticalAlignment=VerticalAlignment.Center;back.Background=Brushes.Transparent;back.Padding=new Thickness(0);back.HorizontalAlignment=HorizontalAlignment.Left;header.Children.Add(back);
            var title=Paint.TextBlock(PageName(kind),21,Paint.Text);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(40,0,30,0);header.Children.Add(title);
            refreshButton=Paint.Button(scanIndicator,()=>{Reload();if(page=="power")CheckSamsungAccess(true);},"다시 검색");refreshButton.Width=refreshButton.Height=34;refreshButton.VerticalAlignment=VerticalAlignment.Center;refreshButton.Background=Brushes.Transparent;refreshButton.Padding=new Thickness(0);refreshButton.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(refreshButton);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(5,10,5,0);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
            root.Children.Add(new SmoothScrollViewer {Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Surface.Child=root;
            Closed+=(s,e)=> {closed=true;ticker.Stop();CancelScan();};Deactivated+=(s,e)=> {if(deviceActions==0&&!Program.Inspectable&&IsVisible&&!IsMouseOver)Hide();};if(!Wireless)Reload();
            ticker.Tick+=(s,e)=> {if(page=="focus")FocusStatus();else if(page=="power"&&!busy&&!changingMode&&!changingSamsung)RefreshPowerSelection();};
            IsVisibleChanged+=(s,e)=> {if(IsVisible&&(page=="focus"||page=="power"))ticker.Start();else ticker.Stop();if(Wireless){if(IsVisible)Reload();else CancelScan();}else scanIndicator.SetScanning(busy&&IsVisible);};
        }
        bool Wireless {get{return page=="wifi"||page=="bluetooth";}}
        static string PageName(string kind) {return kind=="wifi"?"Wi-Fi":kind=="bluetooth"?"블루투스":kind=="sound"?"소리 및 출력":kind=="power"?"배터리 및 전원":kind=="focus"?"집중 타이머":"디스플레이";}
        static Border Card(UIElement child) {return new Border {CornerRadius=new CornerRadius(22),Background=QuickPanel.Card,Padding=new Thickness(13),Margin=new Thickness(0,5,0,5),Child=child};}
        void Text(string text) {var label=Paint.TextBlock(text,12,Paint.Muted);label.TextWrapping=TextWrapping.Wrap;label.Margin=new Thickness(5,10,5,10);body.Children.Add(label);}
        void Action(string text,Action action) {var button=Paint.Action(text,action);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,5,0,5);button.Padding=new Thickness(13,12,13,12);button.Background=QuickPanel.Card;body.Children.Add(button);}
        async void Reload()
        {
            if(Wireless){ReloadWireless();return;}
            if(busy||closed||changingMode||changingSamsung)return;busy=true;body.Children.Clear();samsungButtons.Clear();windowsButtons.Clear();status.Text="불러오는 중…";
            refreshButton.IsEnabled=false;scanIndicator.SetScanning(IsVisible);
            try {
                if(page=="sound") {
                    var slider=new PillSlider("volume") {Value=shell.Audio.Muted?0:shell.Audio.Value,Light=true,DarkTrack=true};slider.Changed+=v=>shell.Volume(v);body.Children.Add(Card(slider));Action(shell.Audio.Muted?"음소거 해제":"음소거",()=> {shell.Mute();Reload();});
                    var outputs=await Task.Run(()=>Native.AudioOutputs());if(closed)return;if(outputs.Any(o=>o.Current&&BudsInfo.IsBuds(o.Name))){var symbol=await BackgroundWork.Run(BudsDeviceData.ShortcutIcon);if(closed)return;slider.SetDeviceIcon(symbol);}
                    foreach(var output in outputs){var d=output;Action((d.Current?"✓  ":"")+d.Name,async()=> {try{await Task.Run(()=>Native.SetAudioOutput(d.Id));shell.Audio.Dispose();shell.Audio.Read();Reload();}catch(Exception e){status.Text=e.Message;}});}
                    status.Text="출력 장치를 선택하면 Windows의 기본 소리 출력도 함께 바뀝니다.";
                } else if(page=="power") {
                    if(PowerDisplay.Slot(shell.Power,Preferences.Current.DesktopPowerWatts)>0)body.Children.Add(Card(new BatteryPill {Width=140,Height=40,State=shell.Power}));
                    Text(shell.Power.Known?(shell.Power.Connected?"전원에 연결되어 있습니다":"배터리를 사용하고 있습니다"):"배터리가 없는 PC · Windows 전원 관리를 사용합니다");
                    if(!shell.Power.Known&&Preferences.Current.DesktopPowerWatts>0)Text("표시된 W 값은 사용자가 직접 입력한 값이며 실시간 소비전력 측정값이 아닙니다.");
                    var samsung=shell.SamsungSettings();var vendor=SamsungPerformance.Read(samsung!=null);samsungMode=vendor.Selected;
                    if(vendor.Available){Text("삼성 성능 모드");samsungPicker=new SamsungModePicker(vendor.Modes,ChangeSamsungMode);foreach(var button in samsungPicker.Buttons)samsungButtons[(int)button.Tag]=button;body.Children.Add(samsungPicker);samsungStatus.TextWrapping=TextWrapping.Wrap;samsungStatus.Margin=new Thickness(5,9,5,5);body.Children.Add(samsungStatus);RefreshSamsungSelection(vendor);Action("삼성 설정에서 성능 모드 열기  ›",()=>SamsungPerformance.Open());CheckSamsungAccess();}
                    var mode=Native.ReadPerformanceMode();selectedMode=mode.Available?(Guid?)mode.Selected:null;Text("Windows 성능 모드");
                    if(mode.Available){foreach(var option in Native.PerformanceModes()){var choice=option;var button=Paint.Action((choice.Id==mode.Selected?"✓  ":"")+choice.Name,()=>ChangeMode(choice.Id));button.Tag=choice.Name;windowsButtons[choice.Id]=button;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(13,11,13,11);button.Margin=new Thickness(0,3,0,3);button.BorderBrush=Brushes.Transparent;button.BorderThickness=new Thickness(0);button.Background=choice.Id==mode.Selected?Paint.ControlWhite:QuickPanel.Card;((TextBlock)button.Content).Foreground=choice.Id==mode.Selected?Paint.OnLight:Paint.Text;body.Children.Add(button);}}
                    else Text("이 Windows 환경은 성능 모드 변경을 지원하지 않습니다.");
                    Text("전원 관리 옵션");
                    var plans=await Task.Run(()=>Native.PowerPlans());if(closed)return;foreach(var plan in plans){var p=plan;Action((p.Current?"✓  ":"")+p.Name,()=> {try{Native.SetPowerPlan(p.Id);Reload();}catch(Exception e){status.Text=e.Message;}});}status.Text="Windows의 실제 전원 관리 옵션을 표시합니다.";
                } else if(page=="focus") {
                    Text("집중할 시간을 정해 보세요. 타이머가 끝나면 알려 드립니다.");foreach(int value in new[]{15,30,60}){int minutes=value;Action(minutes+"분 시작",()=> {shell.FocusUntil=DateTime.UtcNow.AddMinutes(minutes);FocusStatus();});}
                    Action("타이머 종료",()=> {shell.FocusUntil=DateTime.MinValue;FocusStatus();});FocusStatus();
                } else {
                    foreach(var monitor in Forms.Screen.AllScreens)Text((monitor.Primary?"주 디스플레이":"디스플레이")+"\n"+monitor.Bounds.Width+" × "+monitor.Bounds.Height);
                    var slider=new PillSlider("brightness") {Value=Math.Max(0,shell.Brightness),IsEnabled=shell.Brightness>=0,Light=true,DarkTrack=true};slider.Changed+=v=>shell.Bright(v);body.Children.Add(Card(slider));
                    status.Text=shell.Brightness>=0?"노트북의 실제 디스플레이 밝기를 조절합니다.":"이 디스플레이는 Windows 밝기 조절을 지원하지 않습니다.";
                }
            }catch(Exception e){status.Text=e.Message;AppLog.Write("details-"+page,e.Message);}finally{busy=false;refreshButton.IsEnabled=true;scanIndicator.SetScanning(false);}
        }
        internal void Rescan(){Reload();}
        void CancelScan()
        {
            ++scanVersion;var pending=scan;scan=null;if(pending!=null)pending.Cancel();busy=false;scanIndicator.SetScanning(false);refreshButton.IsEnabled=true;
        }
        async void ReloadWireless()
        {
            if(busy||closed||!IsVisible)return;busy=true;int revision=++scanVersion;var source=new CancellationTokenSource();scan=source;ScanRequests++;
            status.Text=page=="wifi"?"Wi-Fi 네트워크를 다시 검색하는 중…":"주변 블루투스 기기를 다시 검색하는 중…";refreshButton.IsEnabled=false;scanIndicator.SetScanning(true);
            if(body.Children.Count==0)RadioAction();
            try{
                var snapshot=await (scanLoader!=null?scanLoader(source.Token):WirelessScan.Read(page,source.Token));
                if(closed||revision!=scanVersion||source.IsCancellationRequested||!IsVisible)return;
                body.Children.Clear();RadioAction();
                if(page=="wifi"){
                    foreach(var network in snapshot.Networks){var item=network;body.Children.Add(WifiNetworkView.Create(item,()=>SelectNetwork(item)));}
                    status.Text=snapshot.Notice??(snapshot.Networks.Count==0?"사용 가능한 네트워크가 없습니다. Wi-Fi 상태를 확인해 주세요.":"저장된 네트워크는 바로 연결합니다. 새 네트워크는 비밀번호를 입력해 연결할 수 있습니다.");
                }else{
                    var peers=BluetoothDeviceView.Rows(snapshot.Devices);foreach(bool registered in new[]{true,false}){var rows=peers.Where(p=>p.Paired==registered).ToList();if(rows.Count==0)continue;Text(registered?"등록된 기기":"새 기기");foreach(var peer in rows){var item=peer;Button button=null;button=BluetoothDeviceView.Create(item,()=>SelectBluetooth(item,button));if(item.Paired)button.PreviewMouseRightButtonUp+=(s,e)=>{e.Handled=true;SelectBluetooth(item,button);};body.Children.Add(button);}}
                    status.Text=snapshot.Notice??(snapshot.Devices.Count==0?"주변 기기를 찾지 못했습니다. 기기의 페어링 모드를 켜고 다시 검색해 주세요.":"새 기기를 눌러 페어링합니다. 등록된 기기는 메뉴에서 연결 관리 또는 제거를 선택할 수 있습니다.");
                }
            }catch(OperationCanceledException){}catch(Exception error){if(!closed&&revision==scanVersion)status.Text="재검색하지 못했습니다: "+error.Message;}
            finally{if(revision==scanVersion){scan=null;busy=false;refreshButton.IsEnabled=true;scanIndicator.SetScanning(false);}source.Dispose();}
        }
        void RadioAction()
        {
            if(page=="wifi"&&shell.Apps.Any(a=>(a.AppID??"").StartsWith("Microsoft.YourPhone_",StringComparison.OrdinalIgnoreCase))){
                var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(42)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(new Glyph("phone",28));var text=new StackPanel();text.Children.Add(Paint.TextBlock("인스턴트 핫스팟",14,Paint.Text));text.Children.Add(Paint.TextBlock("Windows에서 기기 선택 · 연결",11,Paint.Muted));Grid.SetColumn(text,1);row.Children.Add(text);var button=Paint.Button(row,OpenInstantHotspot,"Windows 인스턴트 핫스팟 연결 화면 열기");button.Background=QuickPanel.Card;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(13,12,13,12);button.Margin=new Thickness(0,4,0,7);body.Children.Add(button);
            }
            Action((page=="wifi"?"Wi-Fi":"블루투스")+((page=="wifi"?shell.Services.WifiOn:shell.Services.BluetoothOn)==true?" 끄기":" 켜기"),()=>ToggleRadio(page));
        }
        async void SelectBluetooth(BluetoothPeer peer,Button button)
        {
            if(peer.Device==null||closed)return;
            if(peer.Paired){var menu=new ContextMenu {PlacementTarget=button};menu.Opened+=(s,e)=>deviceActions++;menu.Closed+=(s,e)=>deviceActions=Math.Max(0,deviceActions-1);var manage=new MenuItem {Header="Windows에서 연결 관리"};manage.Click+=async(s,e)=>{if(await DismissAsync())Native.Open("ms-settings:bluetooth");};menu.Items.Add(manage);var remove=new MenuItem {Header="등록된 기기 제거…"};remove.Click+=async(s,e)=>{deviceActions++;try{if(MessageBox.Show(this,peer.Name+" 기기의 등록을 해제할까요? 다시 사용하려면 페어링해야 합니다.","기기 제거",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;button.IsEnabled=false;status.Text=await BluetoothOperations.Pair(peer.Device);if(!closed&&IsVisible)Reload();}catch(Exception error){if(!closed)status.Text=error.Message;}finally{deviceActions=Math.Max(0,deviceActions-1);button.IsEnabled=true;}};menu.Items.Add(remove);menu.IsOpen=true;return;}
            deviceActions++;button.IsEnabled=false;try{status.Text="페어링 중…";status.Text=await BluetoothOperations.Pair(peer.Device);if(!closed&&IsVisible)Reload();}catch(Exception error){if(!closed)status.Text=error.Message;}finally{deviceActions--;button.IsEnabled=true;}
        }
        async void OpenInstantHotspot(){if(await DismissAsync())Native.Open("ms-availablenetworks:");}
        async void ToggleRadio(string kind)
        {
            try {if(!await shell.Services.ToggleRadio(kind)){status.Text="Windows가 무선 장치 제어를 허용하지 않았습니다.";return;}await shell.Services.Refresh();if(!closed)Reload();}
            catch(Exception error){if(!closed)status.Text=error.Message;}
        }
        void FocusStatus() {status.Text=shell.FocusUntil>DateTime.UtcNow?"남은 시간  "+(shell.FocusUntil-DateTime.UtcNow).ToString(@"mm\:ss"):"진행 중인 타이머가 없습니다";}
        async void CheckSamsungAccess(bool force=false)
        {
            if(closed||checkingSamsung||samsungButtons.Count==0||shell.Demo)return;checkingSamsung=true;
            try{await BackgroundWork.Run(()=>SamsungPerformance.Probe(force));if(!closed)RefreshSamsungSelection(SamsungPerformance.Read(shell.SamsungSettings()!=null));}
            finally{checkingSamsung=false;}
        }
        void RefreshSamsungSelection(SamsungPerformanceState state)
        {
            samsungMode=state.Selected;samsungStatus.Text=state.Stale?state.Error:SamsungPerformance.AccessMessage;
            if(samsungPicker!=null)samsungPicker.Refresh(state,changingSamsung,SamsungPerformance.Access);
        }
        void RefreshPowerSelection()
        {
            if(samsungButtons.Count>0)RefreshSamsungSelection(SamsungPerformance.Read(shell.SamsungSettings()!=null));var state=Native.ReadPerformanceMode();selectedMode=state.Available?(Guid?)state.Selected:null;
            foreach(var pair in windowsButtons){bool selected=state.Available&&pair.Key==state.Selected;pair.Value.IsEnabled=state.Available&&!changingMode;pair.Value.Background=selected?Paint.ControlWhite:QuickPanel.Card;var text=(TextBlock)pair.Value.Content;text.Text=(selected?"✓  ":"")+(string)pair.Value.Tag;text.Foreground=selected?Paint.OnLight:Paint.Text;}
        }
        async void ChangeSamsungMode(int value)
        {
            if(changingSamsung||closed)return;changingSamsung=true;RefreshPowerSelection();status.Text="삼성 성능 모드를 적용하는 중…";
            try{var state=await BackgroundWork.Run(()=>SamsungPerformance.Set(shell.SamsungSettings(),value));if(!closed){samsungMode=state.Selected;status.Text="삼성 성능 모드를 적용했습니다.";}}
            catch(Exception error){if(!closed)status.Text=error.Message;}finally{changingSamsung=false;if(!closed)RefreshPowerSelection();}
        }
        async void ChangeMode(Guid mode)
        {
            if(changingMode||closed)return;changingMode=true;status.Text="성능 모드를 적용하는 중…";
            try {await Task.Run(()=>Native.SetPerformanceMode(mode));if(!closed){var current=Native.ReadPerformanceMode();if(!current.Available||current.Selected!=mode)throw new InvalidOperationException("Windows가 선택한 모드를 적용하지 못했습니다. 현재 전원 관리 옵션을 확인해 주세요.");status.Text="Windows 성능 모드를 적용했습니다.";}}
            catch(Exception e){if(!closed)status.Text="성능 모드를 변경하지 못했습니다: "+e.Message;}
            finally {changingMode=false;if(!closed)RefreshPowerSelection();}
        }
        void SelectNetwork(WifiNetwork network)
        {
            if(network.Connected||!network.Secure||!String.IsNullOrEmpty(network.Profile)){Connect(network,null);return;}
            var content=new StackPanel();var label=Paint.TextBlock(network.Name+" 비밀번호",13,Paint.Text);content.Children.Add(label);var password=new PasswordBox {FontFamily=SystemFonts.MessageFontFamily,FontSize=15,Padding=new Thickness(8),Margin=new Thickness(0,10,0,10)};content.Children.Add(password);
            content.Children.Add(Paint.Action("연결",()=> {Connect(network,password.Password);password.Clear();}));body.Children.Insert(1,Card(content));password.Focus();
        }
        async void Connect(WifiNetwork network,string password)
        {
            try {status.Text=network.Connected?"연결 해제 중…":"연결 중…";await Task.Run(()=>Native.ConnectWifi(network,password));await Task.Delay(1500);if(!closed)Reload();}
            catch(Exception e){status.Text=e.Message;}
        }
    }
}
