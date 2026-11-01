using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OneTaskBar
{
    // Observe Explorer window creation without replacing its notification callback.
    // Existing hidden hosts are masked before the chevron is invoked.
    internal sealed class TrayOpenMask : IDisposable
    {
        readonly HashSet<IntPtr> windows = new HashSet<IntPtr>();
        readonly Callback callback;
        IntPtr hook;
        uint process;
        bool transferred;

        delegate void Callback(IntPtr hook, uint kind, IntPtr window, int objectId,
            int childId, uint thread, uint time);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module,
            Callback callback, uint process, uint thread, uint flags);

        [DllImport("user32.dll")]
        static extern bool UnhookWinEvent(IntPtr hook);

        internal TrayOpenMask()
        {
            callback = Observe;
            if (!TaskbarMask.Active) return;
            var tray = Native.FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero) return;
            Native.GetWindowThreadProcessId(tray, out process);
            hook = SetWinEventHook(0x8000, 0x8002, IntPtr.Zero, callback, process, 0, 2);
            Mask(Native.FindWindow("TopLevelWindowForOverflowXamlIsland", null));
            Mask(Native.FindWindow("NotifyIconOverflowWindow", null));
        }

        void Observe(IntPtr eventHook, uint kind, IntPtr window, int objectId,
            int childId, uint thread, uint time)
        {
            if (objectId != 0 || childId != 0 || window == IntPtr.Zero) return;
            var name = new StringBuilder(128);
            Native.GetClassName(window, name, name.Capacity);
            if (name.ToString() == "TopLevelWindowForOverflowXamlIsland" ||
                name.ToString() == "NotifyIconOverflowWindow") Mask(window);
        }

        void Mask(IntPtr window)
        {
            uint owner;
            if (window == IntPtr.Zero) return;
            Native.GetWindowThreadProcessId(window, out owner);
            if (owner == process && TaskbarMask.Hide(window, false)) windows.Add(window);
        }

        internal void Transfer() { transferred = true; }

        public void Dispose()
        {
            if (hook != IntPtr.Zero) UnhookWinEvent(hook);
            hook = IntPtr.Zero;
            if (!transferred)
                foreach (var window in windows)
                    if (TaskbarMask.Protected(window))
                    {
                        Native.ShowWindow(window, 0);
                        TaskbarMask.Restore(window);
                    }
            windows.Clear();
        }
    }
}
