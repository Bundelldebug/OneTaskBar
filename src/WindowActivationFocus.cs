using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace OneTaskBar
{
    internal static partial class WindowActivation
    {
        [StructLayout(LayoutKind.Sequential)]struct GuiInfo
        {
            internal uint Size,Flags;
            internal IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;
            internal Native.RectI CaretRect;
        }
        [DllImport("user32.dll")]static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
        [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr parent,Native.EnumProc callback,IntPtr state);
        [DllImport("user32.dll")]static extern bool IsChild(IntPtr parent,IntPtr child);
        [DllImport("user32.dll")]static extern IntPtr SetFocus(IntPtr window);
        static bool Within(IntPtr root,IntPtr child){return child!=IntPtr.Zero&&(child==root||IsChild(root,child));}
        internal static uint ForegroundInputThread(IntPtr root)
        {
            uint pid;var info=new GuiInfo {Size=(uint)Marshal.SizeOf(typeof(GuiInfo))};
            if(root==Native.GetForegroundWindow()&&GetGUIThreadInfo(0,ref info)&&(info.Active==root||Within(root,info.Focus)))
            {
                uint input=Native.GetWindowThreadProcessId(info.Focus,out pid);if(input!=0)return input;
            }
            return Native.GetWindowThreadProcessId(root,out pid);
        }
        internal static IntPtr ContentTarget(IntPtr root)
        {
            uint pid;uint thread=Native.GetWindowThreadProcessId(root,out pid);
            var info=new GuiInfo {Size=(uint)Marshal.SizeOf(typeof(GuiInfo))};
            if(GetGUIThreadInfo(thread,ref info)&&info.Focus!=root&&Within(root,info.Focus)&&IsWindowEnabled(info.Focus))return info.Focus;
            IntPtr content=IntPtr.Zero;
            var name=new StringBuilder(128);
            EnumChildWindows(root,(child,state)=>{
                if(!Native.IsWindowVisible(child)||!IsWindowEnabled(child))return true;
                name.Clear();Native.GetClassName(child,name,name.Capacity);
                if(name.ToString()!="Windows.UI.Core.CoreWindow")return true;
                content=child;return false;
            },IntPtr.Zero);
            return content==IntPtr.Zero?root:content;
        }
        static void ActivateHostedContent(IntPtr root)
        {
            var content=ContentTarget(root);
            if(content!=root&&HostedCore(WindowClass(content),WindowClass(root)))Native.SetForegroundWindow(content);
        }
        static void RestoreContentFocus(IntPtr root,uint attachedSource=0,uint attachedTarget=0,uint attachedInput=0)
        {
            if(!ForegroundIs(root))return;
            var info=new GuiInfo {Size=(uint)Marshal.SizeOf(typeof(GuiInfo))};
            // Do not disturb an edit control that already has real keyboard focus.
            if(GetGUIThreadInfo(0,ref info)&&info.Focus!=root&&Within(root,info.Focus))return;
            var content=ContentTarget(root);
            if(!Native.IsWindow(content)||!Responsive(content,0,40))return;
            uint pid;uint current=GetCurrentThreadId(),thread=Native.GetWindowThreadProcessId(content,out pid);
            bool joined=false;
            try
            {
                if(thread!=current&&thread!=attachedSource&&thread!=attachedTarget&&thread!=attachedInput){joined=AttachThreadInput(current,thread,true);if(!joined)return;Interlocked.Increment(ref links);}
                if(ForegroundIs(root))SetFocus(content);
            }
            finally{if(joined){AttachThreadInput(current,thread,false);Interlocked.Decrement(ref links);}}
        }
    }
}
