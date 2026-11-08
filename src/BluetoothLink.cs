using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace OneTaskBar
{
    // Connects or drops an already paired Bluetooth device the way Windows' own device list does: the device's
    // audio and input services are switched on or off. Pairing is never touched.
    internal static class BluetoothLink
    {
        [StructLayout(LayoutKind.Sequential)]struct FindRadio {internal uint Size;}
        [StructLayout(LayoutKind.Sequential)]struct Time {internal ushort A,B,C,D,E,F,G,H;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct Info
        {
            internal uint Size;internal ulong Address;internal uint ClassOfDevice;
            [MarshalAs(UnmanagedType.Bool)]internal bool Connected;[MarshalAs(UnmanagedType.Bool)]internal bool Remembered;[MarshalAs(UnmanagedType.Bool)]internal bool Authenticated;
            internal Time LastSeen,LastUsed;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=248)]internal string Name;
        }
        [DllImport("bthprops.cpl")]static extern IntPtr BluetoothFindFirstRadio(ref FindRadio parameters,out IntPtr radio);
        [DllImport("bthprops.cpl")]static extern bool BluetoothFindNextRadio(IntPtr find,out IntPtr radio);
        [DllImport("bthprops.cpl")]static extern bool BluetoothFindRadioClose(IntPtr find);
        [DllImport("bthprops.cpl")]static extern uint BluetoothGetDeviceInfo(IntPtr radio,ref Info info);
        [DllImport("bthprops.cpl")]static extern uint BluetoothSetServiceState(IntPtr radio,ref Info info,ref Guid service,uint flags);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
        static readonly Guid[] Services=
        {
            new Guid("0000110b-0000-1000-8000-00805f9b34fb"), // A2DP sink: headphones, speakers
            new Guid("0000111e-0000-1000-8000-00805f9b34fb"), // hands-free
            new Guid("00001108-0000-1000-8000-00805f9b34fb"), // headset
            new Guid("00001124-0000-1000-8000-00805f9b34fb")  // keyboards, mice, controllers
        };

        // Returns the text shown under the list. Runs the slow part off the interface thread.
        internal static async Task<string> Set(DeviceInformation device,bool connect)
        {
            ulong address;
            var classic=await SystemServices.Wait(BluetoothDevice.FromIdAsync(device.Id));
            if(classic==null)return "이 기기는 여기서 연결을 바꿀 수 없습니다. Windows 설정을 이용해 주세요.";
            address=classic.BluetoothAddress;
            classic.Dispose();
            bool done=await Task.Run(()=>Apply(address,connect));
            return done?(connect?"연결했습니다":"연결을 끊었습니다"):(connect?"연결하지 못했습니다. 기기가 켜져 있고 가까이 있는지 확인해 주세요.":"연결을 끊지 못했습니다.");
        }

        static bool Apply(ulong address,bool connect)
        {
            var parameters=new FindRadio {Size=(uint)Marshal.SizeOf(typeof(FindRadio))};
            IntPtr radio;
            var find=BluetoothFindFirstRadio(ref parameters,out radio);
            if(find==IntPtr.Zero)return false;
            bool any=false;
            try
            {
                do
                {
                    var info=new Info {Size=(uint)Marshal.SizeOf(typeof(Info)),Address=address,Name=""};
                    if(BluetoothGetDeviceInfo(radio,ref info)==0)
                    {
                        // Switching a service off ends the link; switching it on again lets the device connect when it next can.
                        foreach(var service in Services)
                        {
                            var id=service;
                            if(connect){BluetoothSetServiceState(radio,ref info,ref id,0);System.Threading.Thread.Sleep(120);any|=BluetoothSetServiceState(radio,ref info,ref id,1)==0;}
                            else{any|=BluetoothSetServiceState(radio,ref info,ref id,0)==0;}
                        }
                        if(!connect)
                        {
                            System.Threading.Thread.Sleep(700);
                            foreach(var service in Services){var id=service;BluetoothSetServiceState(radio,ref info,ref id,1);}
                        }
                    }
                    CloseHandle(radio);
                }
                while(BluetoothFindNextRadio(find,out radio));
            }
            finally {BluetoothFindRadioClose(find);}
            return any;
        }
    }
}
