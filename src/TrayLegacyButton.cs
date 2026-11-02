using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using Accessibility;

namespace OneTaskBar
{
    // Windows 10's toolbar exposes MSAA child buttons. Use public accessibility
    // actions, without reading shell memory or replacing its notification callback.
    internal sealed class TrayLegacyButton
    {
        readonly IntPtr window;
        readonly int slot;
        internal readonly string InitialName;

        TrayLegacyButton(IntPtr window, int slot, string name)
        {
            this.window = window;
            this.slot = slot;
            InitialName = name;
        }

        [DllImport("oleacc.dll")]
        static extern int AccessibleObjectFromWindow(IntPtr window, uint objectId, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IAccessible accessible);
        [DllImport("oleacc.dll")]
        static extern int AccessibleChildren(IAccessible accessible, int start, int count,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] children, out int obtained);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);

        static IAccessible Open(IntPtr window)
        {
            if (!Native.IsWindow(window)) return null;
            var iid = typeof(IAccessible).GUID;
            IAccessible accessible;
            return AccessibleObjectFromWindow(window, unchecked((uint)-4), ref iid, out accessible) >= 0 ? accessible : null;
        }

        static void Release(object accessible)
        {
            if (accessible != null && Marshal.IsComObject(accessible)) Marshal.ReleaseComObject(accessible);
        }

        static bool Snapshot(IAccessible accessible, int child, out string name, out Rect bounds)
        {
            name = null;
            bounds = Rect.Empty;
            if(accessible==null)return false;
            int role=Convert.ToInt32(accessible.get_accRole(child),CultureInfo.InvariantCulture);
            if(role!=0x0c&&role!=0x2b&&role!=0x2c&&role!=0x38&&role!=0x39&&role!=0x3e)return false;
            int state = Convert.ToInt32(accessible.get_accState(child), CultureInfo.InvariantCulture);
            if ((state & (0x8000 | 0x10000)) != 0) return false;
            name = accessible.get_accName(child);
            if (String.IsNullOrWhiteSpace(name) || String.IsNullOrEmpty(accessible.get_accDefaultAction(child))) return false;
            int x, y, width, height;
            accessible.accLocation(out x, out y, out width, out height, child);
            if (width < 1 || height < 1 || width > 512 || height > 512) return false;
            bounds = new Rect(x, y, width, height);
            return true;
        }

        bool Access(Func<IAccessible,int,bool> read)
        {
            IAccessible accessible = null;
            object[] children = null;
            try
            {
                accessible = Open(window);
                if(accessible==null)return false;
                if(slot<0)return read(accessible,0);
                children=new object[1];
                int obtained;
                if(AccessibleChildren(accessible,slot,1,children,out obtained)<0||obtained!=1)return false;
                if(children[0] is int)return read(accessible,(int)children[0]);
                var target=children[0] as IAccessible;
                return target!=null&&read(target,0);
            }
            finally
            {
                if(children!=null)Release(children[0]);
                Release(accessible);
            }
        }

        internal bool Read(out string name, out Rect bounds)
        {
            string currentName=null;
            Rect currentBounds=Rect.Empty;
            bool found=Access((accessible,child)=>Snapshot(accessible,child,out currentName,out currentBounds));
            name=currentName;
            bounds=currentBounds;
            return found;
        }

        internal bool Invoke(string expectedName, bool focus)
        {
            return Access((accessible,child)=>
            {
                string name;
                Rect bounds;
                if (!Snapshot(accessible, child, out name, out bounds) || name != expectedName) return false;
                if (focus) accessible.accSelect(1, child);
                else accessible.accDoDefaultAction(child);
                return true;
            });
        }

        internal static List<TrayLegacyButton> ReadToolbar(IntPtr toolbar,List<object> facts=null)
        {
            var result = new List<TrayLegacyButton>();
            IAccessible accessible = null;
            object[] children = null;
            try
            {
                accessible = Open(toolbar);
                if (accessible == null) return result;
                children = new object[Math.Min(128, Math.Max(0, accessible.accChildCount))];
                int obtained;
                if (children.Length == 0 || AccessibleChildren(accessible, 0, children.Length, children, out obtained) < 0)
                    return result;
                for (int i = 0; i < obtained; i++)
                {
                    var target=children[i] as IAccessible;
                    int child=children[i] is int?(int)children[i]:0;
                    if(target==null&&!(children[i] is int))continue;
                    if(facts!=null)facts.Add(new {slot=i,type=children[i].GetType().FullName,
                        role=(target??accessible).get_accRole(child),state=(target??accessible).get_accState(child),
                        name=(target??accessible).get_accName(child),action=(target??accessible).get_accDefaultAction(child)});
                    string name;
                    Rect bounds;
                    if (Snapshot(target??accessible, child, out name, out bounds))
                        result.Add(new TrayLegacyButton(toolbar, i, name));
                }
            }
            finally
            {
                if (children != null) foreach (var value in children) Release(value);
                Release(accessible);
            }
            return result;
        }

        internal static List<TrayLegacyButton> ReadOverflow(IntPtr host)
        {
            var toolbar = FindWindowEx(host, IntPtr.Zero, "ToolbarWindow32", null);
            return toolbar == IntPtr.Zero ? new List<TrayLegacyButton>() : ReadToolbar(toolbar);
        }

        internal static bool InvokeChevron(IntPtr taskbar)
        {
            var notify = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (notify == IntPtr.Zero) return false;
            var button = FindWindowEx(notify, IntPtr.Zero, "Button", null);
            if (button == IntPtr.Zero || FindWindowEx(notify, button, "Button", null) != IntPtr.Zero) return false;
            var action = new TrayLegacyButton(button, -1, null);
            string name;
            Rect bounds;
            return action.Read(out name, out bounds) && action.Invoke(name, false);
        }
    }
}
