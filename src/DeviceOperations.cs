using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace OneTaskBar
{
    internal sealed class WifiNetwork {internal string Name,Profile;internal int Signal,Authentication;internal bool Secure,Connected;internal Guid Adapter;internal byte[] Ssid;}
    internal sealed class OutputDevice {internal string Id,Name;internal bool Current;}
    internal sealed class PowerPlan {internal Guid Id;internal string Name;internal bool Current;}
    internal sealed class PerformanceMode {public bool Available;public Guid Selected,Effective;public string Name;}
    internal static partial class Native
    {
        [DllImport("wlanapi.dll")]static extern uint WlanGetAvailableNetworkList(IntPtr client,ref Guid id,uint flags,IntPtr reserved,out IntPtr list);
        [DllImport("wlanapi.dll")]static extern uint WlanScan(IntPtr client,ref Guid id,IntPtr ssid,IntPtr ie,IntPtr reserved);
        [DllImport("wlanapi.dll",CharSet=CharSet.Unicode)]static extern uint WlanSetProfile(IntPtr client,ref Guid id,uint flags,string xml,string security,bool overwrite,IntPtr reserved,out uint reason);
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct WifiParameters {public uint Mode;[MarshalAs(UnmanagedType.LPWStr)]public string Profile;public IntPtr Ssid,BssList;public uint Bss,Flags;}
        [DllImport("wlanapi.dll")]static extern uint WlanConnect(IntPtr client,ref Guid id,ref WifiParameters parameters,IntPtr reserved);
        [DllImport("wlanapi.dll")]static extern uint WlanDisconnect(IntPtr client,ref Guid id,IntPtr reserved);
        [DllImport("powrprof.dll")]static extern uint PowerEnumerate(IntPtr root,IntPtr scheme,IntPtr subgroup,uint access,uint index,out Guid buffer,ref uint size);
        [DllImport("powrprof.dll")]static extern uint PowerGetActiveScheme(IntPtr root,out IntPtr scheme);
        [DllImport("powrprof.dll")]static extern uint PowerReadFriendlyName(IntPtr root,ref Guid scheme,IntPtr subgroup,IntPtr setting,byte[] buffer,ref uint size);
        [DllImport("powrprof.dll")]static extern uint PowerSetActiveScheme(IntPtr root,ref Guid scheme);
        [DllImport("powrprof.dll")]static extern uint PowerGetActualOverlayScheme(out Guid mode);
        [DllImport("powrprof.dll")]static extern uint PowerGetEffectiveOverlayScheme(out Guid mode);
        [DllImport("powrprof.dll")]static extern uint PowerSetActiveOverlayScheme(Guid mode);
        [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr pointer);
        [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDeviceCollection {void GetCount(out uint count);void Item(uint index,out IMMDevice device);}
        [ComImport,Guid("F8679F50-850A-41CF-9C72-430F290290C8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IPolicyConfig {
            void Reserved0();void Reserved1();void Reserved2();void Reserved3();void Reserved4();void Reserved5();void Reserved6();void Reserved7();void Reserved8();void Reserved9();
            void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)]string id,int role);
        }
        internal static List<WifiNetwork> WifiNetworks()
        {
            var result=new List<WifiNetwork>();IntPtr client=IntPtr.Zero,interfaces=IntPtr.Zero;
            try {uint agreed;uint error=WlanOpenHandle(2,IntPtr.Zero,out agreed,out client);if(error!=0)throw new System.ComponentModel.Win32Exception((int)error);
                if(WlanEnumInterfaces(client,IntPtr.Zero,out interfaces)!=0)return result;
                for(int a=0;a<Marshal.ReadInt32(interfaces);a++) {var id=(Guid)Marshal.PtrToStructure(IntPtr.Add(interfaces,8+a*532),typeof(Guid));WlanScan(client,ref id,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);IntPtr list;
                    error=WlanGetAvailableNetworkList(client,ref id,0,IntPtr.Zero,out list);if(error!=0)throw new System.ComponentModel.Win32Exception((int)error);
                    try {for(int n=0;n<Marshal.ReadInt32(list);n++){var p=IntPtr.Add(list,8+n*628);int length=Math.Min(32,Math.Max(0,Marshal.ReadInt32(p,512)));var bytes=new byte[length];Marshal.Copy(IntPtr.Add(p,516),bytes,0,length);if(length==0)continue;
                        result.Add(new WifiNetwork {Name=Encoding.UTF8.GetString(bytes),Profile=Marshal.PtrToStringUni(p),Adapter=id,Ssid=bytes,Signal=Marshal.ReadInt32(p,604),Secure=Marshal.ReadInt32(p,608)!=0,Authentication=Marshal.ReadInt32(p,612),Connected=(Marshal.ReadInt32(p,620)&1)!=0});}}
                    finally{WlanFreeMemory(list);}
                }
            }finally{if(interfaces!=IntPtr.Zero)WlanFreeMemory(interfaces);if(client!=IntPtr.Zero)WlanCloseHandle(client,IntPtr.Zero);}
            return result.GroupBy(n=>n.Name).Select(g=>g.OrderByDescending(n=>n.Connected).ThenByDescending(n=>!String.IsNullOrEmpty(n.Profile)).First()).OrderByDescending(n=>n.Connected).ThenByDescending(n=>n.Signal).ToList();
        }
        internal static void ConnectWifi(WifiNetwork network,string password)
        {
            IntPtr client=IntPtr.Zero;try{uint agreed;uint code=WlanOpenHandle(2,IntPtr.Zero,out agreed,out client);if(code!=0)throw new System.ComponentModel.Win32Exception((int)code);var id=network.Adapter;
                if(network.Connected){code=WlanDisconnect(client,ref id,IntPtr.Zero);if(code!=0)throw new System.ComponentModel.Win32Exception((int)code);return;}
                string profile=network.Profile;
                if(String.IsNullOrEmpty(profile)){
                    if(network.Secure&&network.Authentication!=7&&network.Authentication!=9)throw new InvalidOperationException("이 네트워크는 Windows에 저장된 연결 프로필이 필요합니다.");
                    string auth=network.Secure?(network.Authentication==9?"WPA3SAE":"WPA2PSK"):"open",encryption=network.Secure?"AES":"none";profile=network.Name;
                    string key=network.Secure?"<sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>"+SecurityElement.Escape(password)+"</keyMaterial></sharedKey>":"";
                    string xml="<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>"+SecurityElement.Escape(profile)+"</name><SSIDConfig><SSID><hex>"+String.Join("",network.Ssid.Select(b=>b.ToString("X2")))+"</hex><name>"+SecurityElement.Escape(network.Name)+"</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>auto</connectionMode><MSM><security><authEncryption><authentication>"+auth+"</authentication><encryption>"+encryption+"</encryption><useOneX>false</useOneX></authEncryption>"+key+"</security></MSM></WLANProfile>";
                    uint reason;code=WlanSetProfile(client,ref id,2,xml,null,true,IntPtr.Zero,out reason);if(code!=0)throw new System.ComponentModel.Win32Exception((int)code);
                }
                var parameters=new WifiParameters {Profile=profile,Mode=0,Bss=3};code=WlanConnect(client,ref id,ref parameters,IntPtr.Zero);if(code!=0)throw new System.ComponentModel.Win32Exception((int)code);
            }finally{if(client!=IntPtr.Zero)WlanCloseHandle(client,IntPtr.Zero);}
        }
        internal static List<OutputDevice> AudioOutputs()
        {
            var result=new List<OutputDevice>();IMMDeviceEnumerator enumerator=null;IDeviceCollection collection=null;IntPtr pointer=IntPtr.Zero;
            try{enumerator=(IMMDeviceEnumerator)new DeviceEnumerator();Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0,1,out pointer));collection=(IDeviceCollection)Marshal.GetObjectForIUnknown(pointer);
                IMMDevice active;string current=null;if(enumerator.GetDefaultAudioEndpoint(0,1,out active)==0){active.GetId(out current);Marshal.ReleaseComObject(active);}
                uint count;collection.GetCount(out count);for(uint i=0;i<count;i++){IMMDevice device;collection.Item(i,out device);IntPtr properties=IntPtr.Zero;IPropertyStore store=null;
                    try{string id;device.GetId(out id);device.OpenPropertyStore(0,out properties);store=(IPropertyStore)Marshal.GetObjectForIUnknown(properties);var key=new PropertyKey {Format=new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),Id=14};Variant value;store.GetValue(ref key,out value);string name;
                        try{name=value.Type==31?Marshal.PtrToStringUni(value.Pointer):"오디오 출력";}finally{PropVariantClear(ref value);}result.Add(new OutputDevice {Id=id,Name=name,Current=id==current});
                    }finally{if(store!=null)Marshal.ReleaseComObject(store);if(properties!=IntPtr.Zero)Marshal.Release(properties);Marshal.ReleaseComObject(device);}}
            }finally{if(collection!=null)Marshal.ReleaseComObject(collection);if(pointer!=IntPtr.Zero)Marshal.Release(pointer);if(enumerator!=null)Marshal.ReleaseComObject(enumerator);}return result;
        }
        internal static void SetAudioOutput(string id)
        {
            object client=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")));
            try{var policy=(IPolicyConfig)client;for(int role=0;role<3;role++)policy.SetDefaultEndpoint(id,role);}finally{Marshal.FinalReleaseComObject(client);}
        }
        internal static List<PowerPlan> PowerPlans()
        {
            var result=new List<PowerPlan>();IntPtr active=IntPtr.Zero;Guid current=Guid.Empty;if(PowerGetActiveScheme(IntPtr.Zero,out active)==0){current=(Guid)Marshal.PtrToStructure(active,typeof(Guid));LocalFree(active);}
            for(uint index=0;index<64;index++){uint size=16;Guid id;if(PowerEnumerate(IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,16,index,out id,ref size)!=0)break;
                var buffer=new byte[1024];size=(uint)buffer.Length;PowerReadFriendlyName(IntPtr.Zero,ref id,IntPtr.Zero,IntPtr.Zero,buffer,ref size);result.Add(new PowerPlan {Id=id,Name=Encoding.Unicode.GetString(buffer,0,(int)Math.Min(size,1024)).TrimEnd('\0'),Current=id==current});}return result;
        }
        internal static void SetPowerPlan(Guid id) {uint result=PowerSetActiveScheme(IntPtr.Zero,ref id);if(result!=0)throw new System.ComponentModel.Win32Exception((int)result);}
        internal static List<PowerPlan> PerformanceModes()
        {
            return new List<PowerPlan>{new PowerPlan {Id=new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a"),Name="최고 전력 효율"},new PowerPlan {Id=Guid.Empty,Name="균형 조정"},new PowerPlan {Id=new Guid("ded574b5-45a0-4f42-8737-46345c09c238"),Name="최고 성능"}};
        }
        internal static PerformanceMode ReadPerformanceMode()
        {
            var state=new PerformanceMode();try {Guid selected,effective;uint actual=PowerGetActualOverlayScheme(out selected),current=PowerGetEffectiveOverlayScheme(out effective);if(actual!=0&&current!=0)return state;
                state.Available=true;state.Selected=actual==0?selected:effective;state.Effective=current==0?effective:state.Selected;var mode=PerformanceModes().FirstOrDefault(m=>m.Id==state.Selected);state.Name=mode!=null?mode.Name:"Windows 사용자 지정";
            }catch(EntryPointNotFoundException){}return state;
        }
        internal static void SetPerformanceMode(Guid id)
        {
            if(!PerformanceModes().Any(m=>m.Id==id))throw new ArgumentException("지원하지 않는 성능 모드입니다");
            uint result=PowerSetActiveOverlayScheme(id);if(result!=0)throw new System.ComponentModel.Win32Exception((int)result);
        }
    }
    internal static class BluetoothOperations
    {
        internal static async Task<List<DeviceInformation>> Read()
        {
            var paired=await SystemServices.Wait(DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true)));
            var unpaired=await SystemServices.Wait(DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(false)));
            return paired.Concat(unpaired).Where(d=>!String.IsNullOrWhiteSpace(d.Name)).GroupBy(d=>d.Id).Select(g=>g.First()).ToList();
        }
        internal static async Task<string> Pair(DeviceInformation device)
        {
            if(device.Pairing.IsPaired){var unpaired=await SystemServices.Wait(device.Pairing.UnpairAsync());return unpaired.Status.ToString();}
            var paired=await SystemServices.Wait(device.Pairing.PairAsync());return paired.Status.ToString();
        }
    }
}
