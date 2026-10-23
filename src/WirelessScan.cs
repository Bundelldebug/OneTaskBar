using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Foundation;
namespace OneTaskBar
{
    internal sealed class WirelessSnapshot
    {
        internal List<WifiNetwork> Networks=new List<WifiNetwork>();
        internal List<DeviceInformation> Devices=new List<DeviceInformation>();
        internal string Notice;
    }
    internal static class WirelessScan
    {
        internal static Task<WirelessSnapshot> Read(string page,CancellationToken token)
        {
            return page=="wifi"?Task.Run(()=>Native.ScanWifi(token),token):Bluetooth(token);
        }
        // A finite AssociationEndpoint watcher discovers nearby devices as well as
        // paired ones. It is always stopped when its owning panel is hidden.
        static async Task<WirelessSnapshot> Bluetooth(CancellationToken token)
        {
            string filter="("+BluetoothDevice.GetDeviceSelectorFromPairingState(true)+") OR ("+BluetoothDevice.GetDeviceSelectorFromPairingState(false)+") OR ("+BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)+") OR ("+BluetoothLEDevice.GetDeviceSelectorFromPairingState(false)+")";
            var watcher=DeviceInformation.CreateWatcher(filter,new[]{"System.Devices.Aep.IsConnected"},DeviceInformationKind.AssociationEndpoint);
            var found=new Dictionary<string,DeviceInformation>(StringComparer.Ordinal);var gate=new object();bool accepting=true;
            var ended=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TypedEventHandler<DeviceWatcher,DeviceInformation> added=(s,d)=>{lock(gate)if(accepting)found[d.Id]=d;};
            TypedEventHandler<DeviceWatcher,DeviceInformationUpdate> updated=(s,d)=>{lock(gate){DeviceInformation previous;if(accepting&&found.TryGetValue(d.Id,out previous))previous.Update(d);}};
            TypedEventHandler<DeviceWatcher,DeviceInformationUpdate> removed=(s,d)=>{lock(gate)if(accepting)found.Remove(d.Id);};
            TypedEventHandler<DeviceWatcher,object> completed=(s,e)=>{};
            TypedEventHandler<DeviceWatcher,object> stopped=(s,e)=>ended.TrySetResult(s.Status!=DeviceWatcherStatus.Aborted);
            watcher.Added+=added;watcher.Updated+=updated;watcher.Removed+=removed;watcher.EnumerationCompleted+=completed;watcher.Stopped+=stopped;
            try{
                token.ThrowIfCancellationRequested();watcher.Start();await Task.WhenAny(Task.Delay(4500,token),ended.Task);token.ThrowIfCancellationRequested();
                if(ended.Task.IsCompleted&&!await ended.Task)throw new InvalidOperationException("블루투스 검색을 시작하지 못했습니다. 장치 상태를 확인해 주세요.");
                lock(gate)return new WirelessSnapshot {Devices=found.Values.Where(d=>!String.IsNullOrWhiteSpace(d.Name)).OrderByDescending(d=>d.Pairing.IsPaired).ThenBy(d=>d.Name,StringComparer.CurrentCultureIgnoreCase).ToList()};
            }finally{
                lock(gate)accepting=false;
                if(watcher.Status==DeviceWatcherStatus.Started||watcher.Status==DeviceWatcherStatus.EnumerationCompleted)watcher.Stop();
                watcher.Added-=added;watcher.Updated-=updated;watcher.Removed-=removed;watcher.EnumerationCompleted-=completed;watcher.Stopped-=stopped;
            }
        }
    }
    internal static partial class Native
    {
        [StructLayout(LayoutKind.Sequential)]struct WlanNotice {internal uint Source,Code;internal Guid Adapter;internal uint Size;internal IntPtr Data;}
        delegate void WlanNoticeCallback(ref WlanNotice notice,IntPtr context);
        [DllImport("wlanapi.dll")]static extern uint WlanRegisterNotification(IntPtr client,uint source,[MarshalAs(UnmanagedType.Bool)]bool ignoreDuplicates,WlanNoticeCallback callback,IntPtr context,IntPtr reserved,out uint previous);
        internal static WirelessSnapshot ScanWifi(CancellationToken token)
        {
            IntPtr client=IntPtr.Zero,interfaces=IntPtr.Zero;WlanNoticeCallback callback=null;bool registered=false;
            using(var completed=new ManualResetEventSlim(false)){
                var gate=new object();var results=new Dictionary<Guid,bool?>();
                try{
                    token.ThrowIfCancellationRequested();uint version;uint error=WlanOpenHandle(2,IntPtr.Zero,out version,out client);if(error!=0)throw new System.ComponentModel.Win32Exception((int)error);
                    error=WlanEnumInterfaces(client,IntPtr.Zero,out interfaces);if(error!=0)throw new System.ComponentModel.Win32Exception((int)error);
                    var adapters=new List<Guid>();for(int i=0;i<Marshal.ReadInt32(interfaces);i++)adapters.Add((Guid)Marshal.PtrToStructure(IntPtr.Add(interfaces,8+i*532),typeof(Guid)));
                    if(adapters.Count==0)return new WirelessSnapshot {Notice="Wi-Fi 어댑터를 찾지 못했습니다."};foreach(var id in adapters)results[id]=null;
                    callback=(ref WlanNotice notice,IntPtr context)=>{if((notice.Source&8)==0||notice.Code!=7&&notice.Code!=8)return;lock(gate){if(!results.ContainsKey(notice.Adapter))return;results[notice.Adapter]=notice.Code==7;if(results.Values.All(v=>v.HasValue))completed.Set();}};
                    uint previous;error=WlanRegisterNotification(client,8,false,callback,IntPtr.Zero,IntPtr.Zero,out previous);if(error!=0)throw new System.ComponentModel.Win32Exception((int)error);registered=true;
                    foreach(var adapter in adapters){var id=adapter;error=WlanScan(client,ref id,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);if(error!=0)lock(gate)results[id]=false;}
                    lock(gate)if(results.Values.All(v=>v.HasValue))completed.Set();bool signaled=completed.Wait(4500,token);token.ThrowIfCancellationRequested();
                    bool success;lock(gate)success=signaled&&results.Values.All(v=>v==true);
                    return new WirelessSnapshot {Networks=ReadWifiNetworks(client,adapters),Notice=success?null:"재검색을 완료하지 못해 Windows의 마지막 네트워크 목록을 표시합니다."};
                }finally{
                    if(registered){uint previous;WlanRegisterNotification(client,0,false,null,IntPtr.Zero,IntPtr.Zero,out previous);}if(interfaces!=IntPtr.Zero)WlanFreeMemory(interfaces);if(client!=IntPtr.Zero)WlanCloseHandle(client,IntPtr.Zero);GC.KeepAlive(callback);
                }
            }
        }
    }
}
