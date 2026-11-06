using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // Widgets belong to a physical monitor, not to a Windows display number. "\\.\DISPLAY1" can name a different
    // screen after a monitor is unplugged, swapped or made primary; the interface path of the monitor does not.
    internal static class MonitorIdentity
    {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct DisplayDevice
        {
            public int Size;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;
            public int Flags;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool EnumDisplayDevices(string device,uint index,ref DisplayDevice value,uint flags);
        const string Prefix="id:";
        static readonly Dictionary<string,string> cache=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        // The display numbers can be reassigned when the monitors change, so the answers are read again then.
        internal static void Refresh(){cache.Clear();}
        internal static string Of(Forms.Screen screen)
        {
            string device=screen.DeviceName,result;
            if(cache.TryGetValue(device,out result))return result;
            result=Prefix+device;
            try
            {
                var value=new DisplayDevice {Size=Marshal.SizeOf(typeof(DisplayDevice))};
                // Flag 1 asks for the device interface path, which includes the connector, so two identical monitors differ.
                if(EnumDisplayDevices(device,0,ref value,1)&&!String.IsNullOrWhiteSpace(value.Id))result=Prefix+value.Id.Trim();
            }
            catch(Exception error)
            {
                AppLog.Once("monitor-identity",error.GetType().Name);
            }
            cache[device]=result;
            return result;
        }
        // The connected screen a saved value names, or null when that monitor is not connected now. A value saved
        // before identities existed is a display name and is matched as one.
        internal static Forms.Screen Find(string saved)
        {
            if(String.IsNullOrEmpty(saved))return Forms.Screen.PrimaryScreen;
            var screens=Forms.Screen.AllScreens;
            if(saved.StartsWith(Prefix,StringComparison.Ordinal))
                return screens.FirstOrDefault(s=>String.Equals(Of(s),saved,StringComparison.OrdinalIgnoreCase));
            return screens.FirstOrDefault(s=>String.Equals(s.DeviceName,saved,StringComparison.OrdinalIgnoreCase));
        }
    }
}
