using System;
using System.Windows.Media;

namespace OneTaskBar
{
    // Decorative work that a modest PC should not do: pointer-following glass light, the liquid edge,
    // and the blurred backdrop copy during a media flight. The wave and title scroll always run every frame. It turns on by itself for
    // a PC with a weak graphics tier, four or fewer logical processors or 6 GB or less of memory, and can be
    // forced on in the settings. Layout, content and the main motion of every panel stay the same.
    internal static class Performance
    {
        static bool? weak;
        internal static bool Light {get{return Preferences.Current.LightEffects||Weak;}}
        internal static bool Weak
        {
            get
            {
                if(weak.HasValue)return weak.Value;
                bool result=false;
                try
                {
                    result=RenderCapability.Tier>>16<2||Environment.ProcessorCount<=4||Memory()<=6.2;
                }
                catch(Exception error){AppLog.Once("performance-probe",error.GetType().Name);}
                weak=result;
                return result;
            }
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct MemoryStatus{public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPageFile,AvailablePageFile,TotalVirtual,AvailableVirtual,ExtendedVirtual;}
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        static double Memory()
        {
            var status=new MemoryStatus {Length=(uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MemoryStatus))};
            return GlobalMemoryStatusEx(ref status)?status.TotalPhysical/1073741824.0:64;
        }
    }
}
