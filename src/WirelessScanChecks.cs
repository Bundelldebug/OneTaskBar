using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class WirelessScanChecks
    {
        static void Require(bool value,string name){if(!value)throw new InvalidOperationException("Wireless scan: "+name);}
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;DetailWindow panel=null;int result=1;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    Directory.CreateDirectory(AppLog.Data);Program.Inspectable=true;shell=new ShellController(true);var screen=System.Windows.Forms.Screen.PrimaryScreen;var tested=new List<string>();var preview=new StackPanel {Background=QuickPanel.Glass};int clicked=0;foreach(var network in new[]{new WifiNetwork {Name="연결된 Wi-Fi 7",Connected=true,Secure=true,Generation=7,Signal=96},new WifiNetwork {Name="보안 Wi-Fi 6",Secure=true,Generation=6,Signal=52},new WifiNetwork {Name="공개 Wi-Fi 5",Generation=5,Signal=16},new WifiNetwork {Name="버전 미확인",Signal=0}}){var button=WifiNetworkView.Create(network,()=>clicked++);var glyph=Find<Glyph>(button);Require(glyph.Generation==network.Generation&&glyph.Signal==network.Signal,"per-network generation and signal");button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));preview.Children.Add(button);}Require(clicked==4,"network selection dispatch");Program.Save(preview,340,284,Path.Combine(AppLog.Data,"wifi-network-states.png"));
                    var bluetoothPreview=new StackPanel {Background=QuickPanel.Glass};int deviceClicks=0;foreach(var peer in new[]{new BluetoothPeer {Key="headset",Name="연결된 헤드폰",Icon="headphones",Paired=true,Connected=true,ConnectionKnown=true},new BluetoothPeer {Key="phone",Name="등록된 휴대폰",Icon="phone",Paired=true,ConnectionKnown=true},new BluetoothPeer {Key="keyboard",Name="새 키보드",Icon="keyboard"},new BluetoothPeer {Key="unknown",Name="연결 상태 미확인",Paired=true}}){var button=BluetoothDeviceView.Create(peer,()=>deviceClicks++);Require(Find<Glyph>(button).Kind==peer.Icon,"Bluetooth device type icon");Require(System.Windows.Automation.AutomationProperties.GetItemStatus(button)==peer.State,"Bluetooth registered/connected status");button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));bluetoothPreview.Children.Add(button);}Require(deviceClicks==4,"Bluetooth row dispatch");Program.Save(bluetoothPreview,340,284,Path.Combine(AppLog.Data,"bluetooth-device-states.png"));
                    foreach(string page in new[]{"wifi","bluetooth"}){
                        var requests=new List<TaskCompletionSource<WirelessSnapshot>>();var tokens=new List<CancellationToken>();
                        Func<CancellationToken,Task<WirelessSnapshot>> loader=token=>{var ready=new TaskCompletionSource<WirelessSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);requests.Add(ready);tokens.Add(token);return ready.Task;};
                        panel=new DetailWindow(shell,page,loader);Require(requests.Count==0,"no hidden scan");panel.ShowAt(screen.Bounds,30,30,380,540);await Task.Delay(100);double angle=panel.ScanGlyph.Angle;await Task.Delay(120);Require(requests.Count==1&&panel.Scanning&&panel.ScanGlyph.Spinning&&Math.Abs(panel.ScanGlyph.Angle-angle)>1,"open triggers animated scan");panel.Rescan();panel.Rescan();Require(requests.Count==1,"coalesce refresh while busy");
                        Program.Save(panel.Surface,380,540,Path.Combine(AppLog.Data,page+"-scanning.png"));requests[0].SetResult(new WirelessSnapshot {Networks=new List<WifiNetwork>{new WifiNetwork {Name="네트워크 미리보기",Signal=86,Secure=true}}});await Task.Delay(80);Require(!panel.Scanning&&!panel.ScanGlyph.Spinning,"completion stops spinner");int rows=panel.BodyRows;
                        panel.Rescan();await Task.Delay(30);requests[1].SetException(new InvalidOperationException("검색 오류 예시"));await Task.Delay(60);Require(panel.BodyRows==rows&&panel.StatusText.Contains("검색 오류 예시")&&!panel.Scanning,"keep results and stop after failure");
                        panel.Rescan();await Task.Delay(30);panel.HideImmediately();Require(tokens[2].IsCancellationRequested&&!panel.Scanning&&!panel.ScanGlyph.Spinning,"hiding cancels scan");requests[2].SetResult(new WirelessSnapshot {Notice="stale result must not paint"});await Task.Delay(60);Require(!panel.StatusText.Contains("stale result"),"late result ignored");
                        panel.ShowAt(screen.Bounds,30,30,380,540);await Task.Delay(80);Require(requests.Count==4&&panel.Scanning,"reopen always rescans");panel.Close();Require(tokens[3].IsCancellationRequested,"closing cancels scan");requests[3].SetResult(new WirelessSnapshot());await Task.Delay(60);panel=null;tested.Add(page);
                    }
                    bool clean=LiveBackdrop.OrphanCount==0;Require(clean,"no orphan backdrop");AppLog.Json("wireless-scan-probe.json",new {passed=true,pages=tested.ToArray(),bluetoothTypeIconsAndRegisteredStates=true,scanOnEveryOpen=true,noHiddenScan=true,completionErrorAndHideStopSpinner=true,duplicateScanCoalesced=true,errorsPreserveRows=true,lateResultsIgnored=true,closeCancelsDiscovery=true,noOrphanBackdrop=clean,networkOrPairingChanged=false});result=0;
                }catch(Exception error){AppLog.Json("wireless-scan-probe.json",new {passed=false,error=error.ToString()});}finally{if(panel!=null)panel.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
        static T Find<T>(DependencyObject root)where T:DependencyObject{var content=root as ContentControl;if(content!=null&&content.Content is DependencyObject){var found=Find<T>((DependencyObject)content.Content);if(found!=null)return found;}if(root is T)return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=Find<T>(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}var border=root as Border;if(border!=null&&border.Child!=null)return Find<T>(border.Child);return null;}
        internal static int Live()
        {
            var results=new List<object>();bool passed=true;
            foreach(string page in new[]{"wifi","bluetooth"}){
                var timer=System.Diagnostics.Stopwatch.StartNew();try{using(var source=new CancellationTokenSource(7000)){var snapshot=WirelessScan.Read(page,source.Token).GetAwaiter().GetResult();results.Add(new {page=page,completed=true,rows=page=="wifi"?snapshot.Networks.Count:snapshot.Devices.Count,registered=page=="bluetooth"?snapshot.Devices.Count(d=>d.Pairing.IsPaired):0,notice=snapshot.Notice,milliseconds=timer.ElapsedMilliseconds});}}
                catch(Exception error){passed=false;results.Add(new {page=page,completed=false,error=error.GetType().Name,message=error.Message,milliseconds=timer.ElapsedMilliseconds});}
            }
            AppLog.Json("wireless-live-probe.json",new {passed=passed,results=results.ToArray(),connectionOrPairingChanged=false});return passed?0:1;
        }
    }
}
