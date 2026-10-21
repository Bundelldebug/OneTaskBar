using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class MenuDismiss
    {
        delegate IntPtr MouseHook(int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int type,MouseHook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string module);
        sealed class Watcher : IDisposable {internal Func<Native.PointI,bool> Contains;internal Action Dismiss;public void Dispose(){watchers.Remove(this);StopIfUnused();}}
        static readonly List<Watcher> watchers=new List<Watcher>();
        internal static IDisposable Watch(Func<Native.PointI,bool> contains,Action dismiss){var watcher=new Watcher {Contains=contains,Dismiss=dismiss};watchers.Add(watcher);Start();return watcher;}
        static void Start(){if(hook==IntPtr.Zero)hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);}
        static void StopIfUnused(){if(menus.Count==0&&watchers.Count==0&&hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
        static readonly List<ContextMenu> menus=new List<ContextMenu>();static readonly MouseHook callback=Mouse;static IntPtr hook;
        internal static bool Active {get{return hook!=IntPtr.Zero;}}
        internal static void Open(ContextMenu menu){menu.StaysOpen=false;if(!menus.Contains(menu))menus.Add(menu);Start();if(hook==IntPtr.Zero)AppLog.Once("menu-outside","Windows mouse observer unavailable; using WPF menu dismissal.");}
        internal static void Close(ContextMenu menu){menus.Remove(menu);StopIfUnused();}
        static IntPtr Mouse(int code,IntPtr message,IntPtr data){int kind=message.ToInt32();if(code>=0&&(kind==0x201||kind==0x204||kind==0x207)){var point=(Native.PointI)Marshal.PtrToStructure(data,typeof(Native.PointI));var app=Application.Current;if(app!=null&&!app.Dispatcher.HasShutdownStarted)app.Dispatcher.BeginInvoke(new Action(()=>PointerPressed(point)),DispatcherPriority.Input);}return CallNextHookEx(hook,code,message,data);}
        internal static void PointerPressed(Native.PointI point)
        {
            foreach(var watcher in watchers.ToArray())if(!watcher.Contains(point))watcher.Dismiss();
            foreach(var menu in menus.ToArray()){var source=PresentationSource.FromVisual(menu) as HwndSource;Native.RectI bounds;if(menu.IsOpen&&source!=null&&Native.GetWindowRect(source.Handle,out bounds)&&point.X>=bounds.Left&&point.X<bounds.Right&&point.Y>=bounds.Top&&point.Y<bounds.Bottom)return;}
            foreach(var menu in menus.ToArray())if(menu.IsOpen){menu.IsOpen=false;MenuTheme.Release(menu);}
        }
    }
}
