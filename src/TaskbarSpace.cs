using System;
using System.Runtime.InteropServices;

namespace OneTaskBar
{
    // Explorer stays alive for the tray, but its work-area reservation is leased away.
    // The durable desktop lease restores the user's state on normal exit or guard recovery.
    internal static class TaskbarSpace
    {
        static volatile DesktopLease owner;
        internal static DesktopLease Owner {get{return owner;}}
        internal static bool Owned {get{return owner!=null;}}
        internal static uint Physical(uint logical,bool temporarilyShown){return temporarilyShown?logical&~1U:logical|1U;}
        internal static bool AutoHide {get{return owner!=null&&(owner.TaskbarState&1)!=0;}}
        internal static void Acquire(DesktopLease lease)
        {
            lock(TaskbarMask.Gate){lease.RestoreTaskbarState=true;lease.Save();owner=lease;TaskbarMask.SavedAutoHide=(lease.TaskbarState&1)!=0;Write(Physical(lease.TaskbarState,false));}
        }
        internal static void Set(bool value)
        {
            var lease=owner;if(lease==null)return;lease.TaskbarState=value?lease.TaskbarState|1U:lease.TaskbarState&~1U;
            TaskbarMask.SavedAutoHide=value;if(TaskbarMask.TransientState)lease.TemporaryTaskbarState=lease.TaskbarState;lease.Save();Write(Physical(lease.TaskbarState,TaskbarMask.TransientState));
        }
        internal static void Maintain()
        {
            lock(TaskbarMask.Gate){var lease=owner;if(lease==null||!TaskbarMask.Active||TaskbarMask.TransientState)return;
                var data=new Native.AppBarData {Size=(uint)Marshal.SizeOf(typeof(Native.AppBarData))};uint state=(uint)Native.SHAppBarMessage(4,ref data).ToUInt64();
                if((state&1)==0){lease.TaskbarState=state;TaskbarMask.SavedAutoHide=false;lease.Save();Write(Physical(state,false));}
            }
        }
        internal static void Forget(string session){var lease=owner;if(lease!=null&&lease.Session==session)owner=null;}
        static void Write(uint state){var data=new Native.AppBarData {Size=(uint)Marshal.SizeOf(typeof(Native.AppBarData)),Window=Native.FindWindow("Shell_TrayWnd",null),Param=new IntPtr(state)};if(data.Window!=IntPtr.Zero)Native.SHAppBarMessage(10,ref data);}
    }
}
