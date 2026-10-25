using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OneTaskBar
{
    // Only a captionless Samsung Browser window covering its monitor is eligible.
    // Native title bars and normal maximized windows are never restyled.
    internal sealed class BrowserFullscreen : IDisposable
    {
        IntPtr checkedWindow,adjusted;uint checkedPid;bool samsung;int savedBorder,savedCorner,savedStyle,appliedStyle;bool borderSet,cornerSet;
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int key,ref int value,int size);
        internal static bool Eligible(Native.RectI box,System.Drawing.Rectangle monitor,int style)
        {
            return (style&0xC00000)==0&&box.Left<=monitor.Left&&box.Top<=monitor.Top&&box.Right>=monitor.Right&&box.Bottom>=monitor.Bottom&&box.Left>=monitor.Left-24&&box.Top>=monitor.Top-24&&box.Right<=monitor.Right+24&&box.Bottom<=monitor.Bottom+24;
        }
        internal static bool SamsungPath(string path)
        {
            if(String.IsNullOrWhiteSpace(path))return false;string name=Path.GetFileNameWithoutExtension(path);return (name.Equals("samsunginternet",StringComparison.OrdinalIgnoreCase)||name.Equals("Internet",StringComparison.OrdinalIgnoreCase))&&path.IndexOf("\\Samsung\\",StringComparison.OrdinalIgnoreCase)>=0;
        }
        internal void Update(IntPtr foreground)
        {
            if(adjusted!=IntPtr.Zero&&(foreground!=adjusted||!Native.IsWindow(adjusted)||!Full(adjusted)))Restore();
            if(foreground==IntPtr.Zero||adjusted==foreground||!Native.IsWindowVisible(foreground)||Native.IsIconic(foreground))return;
            uint pid;Native.GetWindowThreadProcessId(foreground,out pid);if(checkedWindow!=foreground||checkedPid!=pid){checkedWindow=foreground;checkedPid=pid;string id;samsung=SamsungPath(Native.ProcessPath(pid,out id));}
            if(samsung&&Full(foreground))Apply(foreground);
        }
        static bool Full(IntPtr h){Native.RectI box;return Native.GetWindowRect(h,out box)&&Eligible(box,System.Windows.Forms.Screen.FromHandle(h).Bounds,Native.GetWindowLong(h,-16));}
        internal void ApplyForFixture(IntPtr h){if(!Program.Inspectable)throw new InvalidOperationException("Fullscreen fixture only");Apply(h);}
        void Apply(IntPtr h)
        {
            adjusted=h;int none=-2,square=1;
            // Some Windows builds expose these as write-only DWM attributes.
            if(Native.DwmGetWindowAttribute(h,34,out savedBorder,4)!=0)savedBorder=-1;if(Native.DwmGetWindowAttribute(h,33,out savedCorner,4)!=0)savedCorner=0;
            borderSet=DwmSetWindowAttribute(h,34,ref none,4)==0;cornerSet=DwmSetWindowAttribute(h,33,ref square,4)==0;
            savedStyle=Native.GetWindowLong(h,-16);appliedStyle=savedStyle&~0x40000;if(appliedStyle!=savedStyle){Native.SetWindowLong(h,-16,appliedStyle);Native.SetWindowPos(h,IntPtr.Zero,0,0,0,0,0x1|0x2|0x4|0x10|0x20);}
        }
        internal void Restore()
        {
            var h=adjusted;adjusted=IntPtr.Zero;if(h==IntPtr.Zero||!Native.IsWindow(h))return;int value;
            if(borderSet&&(Native.DwmGetWindowAttribute(h,34,out value,4)!=0||value==-2))DwmSetWindowAttribute(h,34,ref savedBorder,4);
            if(cornerSet&&(Native.DwmGetWindowAttribute(h,33,out value,4)!=0||value==1))DwmSetWindowAttribute(h,33,ref savedCorner,4);
            if(savedStyle!=appliedStyle&&Native.GetWindowLong(h,-16)==appliedStyle){Native.SetWindowLong(h,-16,savedStyle);Native.SetWindowPos(h,IntPtr.Zero,0,0,0,0,0x1|0x2|0x4|0x10|0x20);}
        }
        public void Dispose(){Restore();}
    }
}
