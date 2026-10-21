using System;
using System.Runtime.InteropServices;

namespace OneTaskBar
{
    // Recovery markers belong to Explorer's window, so the watchdog can restore opacity after a crash.
    internal static class TaskbarMask
    {
        internal static volatile bool Active;
        internal static readonly object Gate=new object();
        const string State="OneTaskBar.TaskbarMask.v1",Color="OneTaskBar.TaskbarMaskColor.v1";
        const int Marker=1<<16,WasLayered=1<<17,WasTransparent=1<<18,Layered=0x80000,Transparent=0x20;
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool SetProp(IntPtr h,string name,IntPtr data);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetProp(IntPtr h,string name);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr RemoveProp(IntPtr h,string name);
        [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr h,uint color,byte alpha,uint flags);
        [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr h,out uint color,out byte alpha,out uint flags);
        internal static bool Protected(IntPtr h)
        {
            uint color,flags;byte alpha;
            return h!=IntPtr.Zero&&(GetProp(h,State).ToInt64()&Marker)!=0&&(Native.GetWindowLong(h,-20)&Layered)!=0&&GetLayeredWindowAttributes(h,out color,out alpha,out flags)&&alpha==0&&(flags&2)!=0;
        }
        internal static bool Hide(IntPtr h)
        {
            lock(Gate){if(!Active)return false;bool hidden=HideCore(h);if(!hidden)Native.ShowWindow(h,0);return hidden;}
        }
        static bool HideCore(IntPtr h)
        {
            if(!Native.IsWindow(h))return false;
            if(!Protected(h)) {
                int style=Native.GetWindowLong(h,-20);
                if(GetProp(h,State)==IntPtr.Zero) {
                    uint color=0,flags=2;byte alpha=255;
                    if((style&Layered)!=0&&!GetLayeredWindowAttributes(h,out color,out alpha,out flags))return false;
                    long saved=Marker|((style&Layered)!=0?WasLayered:0)|((style&Transparent)!=0?WasTransparent:0)|((int)flags<<8)|alpha;
                    if(!SetProp(h,Color,new IntPtr((long)color)))return false;
                    if(!SetProp(h,State,new IntPtr(saved))){RemoveProp(h,Color);return false;}
                }
                Native.SetWindowLong(h,-20,style|Layered|Transparent);
                if(!SetLayeredWindowAttributes(h,0,0,2)||!Protected(h)){Restore(h);return false;}
            }
            // Keep Explorer's real tray host alive; alpha zero prevents any taskbar flash.
            if(!Native.IsWindowVisible(h))Native.ShowWindow(h,8);
            return true;
        }
        internal static bool Restore(IntPtr h)
        {lock(Gate)return RestoreCore(h);}
        static bool RestoreCore(IntPtr h)
        {
            long saved=GetProp(h,State).ToInt64();if((saved&Marker)==0)return true;
            int style=Native.GetWindowLong(h,-20)&~(Layered|Transparent);
            if((saved&WasLayered)!=0)style|=Layered;if((saved&WasTransparent)!=0)style|=Transparent;
            Native.SetWindowLong(h,-20,style);
            if((saved&WasLayered)!=0&&!SetLayeredWindowAttributes(h,(uint)GetProp(h,Color).ToInt64(),(byte)(saved&255),(uint)((saved>>8)&3)))return false;
            RemoveProp(h,State);RemoveProp(h,Color);return true;
        }
    }
}
