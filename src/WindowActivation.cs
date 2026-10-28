using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace OneTaskBar
{
    internal static partial class WindowActivation
    {
        [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
        [DllImport("user32.dll",SetLastError=true)]static extern bool AttachThreadInput(uint first,uint second,bool attach);
        [DllImport("user32.dll")]static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
        [DllImport("user32.dll")]static extern IntPtr SetActiveWindow(IntPtr window);
        [DllImport("user32.dll")]static extern IntPtr GetLastActivePopup(IntPtr window);
        [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")]static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")]static extern bool GetLastInputInfo(ref InputInfo info);
        [StructLayout(LayoutKind.Sequential)]struct InputInfo {internal uint Size,Time;}
        [DllImport("user32.dll")]static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
        static int links,sourceJoinError,targetJoinError,inputJoinError;
        internal static int AttachedQueues {get{return Volatile.Read(ref links);}}
        static bool Responsive(IntPtr window,uint message=0,uint timeout=60){IntPtr result;return SendMessageTimeout(ResponseTarget(window),message,IntPtr.Zero,IntPtr.Zero,3,timeout,out result)!=IntPtr.Zero;}
        static bool ForegroundIs(IntPtr window)
        {
            var foreground=Native.GetForegroundWindow();return foreground==window||foreground!=IntPtr.Zero&&Native.GetAncestor(foreground,2)==Native.GetAncestor(window,2);
        }
        static uint LastInput(){var info=new InputInfo {Size=(uint)Marshal.SizeOf(typeof(InputInfo))};return GetLastInputInfo(ref info)?info.Time:0;}
        static bool OwnedBy(IntPtr window,IntPtr owner){for(int i=0;i<16&&window!=IntPtr.Zero;i++){window=GetWindow(window,4);if(window==owner)return true;}return false;}
        internal static IntPtr Target(IntPtr window)
        {
            if(!Native.IsWindow(window))return IntPtr.Zero;var root=Native.GetAncestor(window,2);if(root!=IntPtr.Zero)window=root;
            if(!IsWindowEnabled(window)){var popup=GetLastActivePopup(window);if(popup!=IntPtr.Zero&&Native.IsWindowVisible(popup)&&IsWindowEnabled(popup)&&OwnedBy(popup,window))return popup;
                // GA_ROOTOWNER follows GetParent semantics, which omit owners of
                // overlapped WinUI/WPF windows. Walk the explicit owner relationship.
                IntPtr modal=IntPtr.Zero;Native.EnumWindows((candidate,state)=>{if(Native.IsWindowVisible(candidate)&&IsWindowEnabled(candidate)&&OwnedBy(candidate,window)){modal=candidate;return false;}return true;},IntPtr.Zero);if(modal!=IntPtr.Zero)return modal;
            }return window;
        }
        internal static async void Activate(IntPtr window)
        {
            window=Target(window);if(window==IntPtr.Zero)return;var source=Native.GetForegroundWindow();uint input=LastInput();bool confirmed=TryActivate(window);
            // WinUI activation can complete after the foreground request returns.
            // Recheck once after Alt-up dispatch, without overriding subsequent user input.
            await Task.Delay(60);if(!Native.IsWindow(window)||LastInput()!=input)return;var current=Native.GetForegroundWindow();
            if(current!=source&&!ForegroundIs(window))return;
            confirmed=TryActivate(window)||confirmed;
            if(!confirmed){RecordActivationFailure(source,window);AppLog.Once("switcher-focus","The requested window did not confirm foreground activation.");}
        }
        internal static void CancelMenu(IntPtr window)
        {
            if(window!=IntPtr.Zero&&Native.IsWindow(window)&&Responsive(window))Responsive(window,0x1F,40);
        }
        // Only called for an explicit committed switch. Do not borrow input queues for
        // discovery/hover, attach to an unresponsive application, or retain an attachment.
        internal static bool TryActivate(IntPtr window)
        {
            sourceJoinError=targetJoinError=inputJoinError=0;
            window=Target(window);if(window==IntPtr.Zero)return false;
            var original=Native.GetForegroundWindow();if(original==IntPtr.Zero)return false;
            if(!Responsive(window)||original!=window&&!Responsive(original))return false;
            if(Native.IsIconic(window))ShowWindowAsync(window,9);
            BringWindowToTop(window);Native.SetForegroundWindow(window);
            if(!ForegroundIs(window))ActivateHostedContent(window);
            if(ForegroundIs(window)){Native.SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x13);BringWindowToTop(window);Responsive(window);if(ForegroundIs(window)){RestoreContentFocus(window);return true;}}
            var foreground=Native.GetForegroundWindow();if(foreground==IntPtr.Zero||foreground!=original&&!ForegroundIs(window))return false;
            if(!Responsive(foreground)||!Responsive(window))return false;
            // Alt release can still be closing the previous app's menu. Finish that
            // work before joining queues, with a timeout and without generating keys.
            if(!Responsive(foreground,0x1F,40)||Native.GetForegroundWindow()!=foreground)return false;
            uint pid;uint current=GetCurrentThreadId(),source=Native.GetWindowThreadProcessId(ResponseTarget(foreground),out pid),input=ForegroundInputThread(foreground),target=Native.GetWindowThreadProcessId(window,out pid);
            if(source==0||target==0)return false;bool joinedSource=false,joinedTarget=false,joinedInput=false;
            try{
                if(source!=current){joinedSource=AttachThreadInput(current,source,true);if(joinedSource)Interlocked.Increment(ref links);else sourceJoinError=Marshal.GetLastWin32Error();}
                if(input!=0&&input!=current&&input!=source){joinedInput=AttachThreadInput(current,input,true);if(joinedInput)Interlocked.Increment(ref links);else inputJoinError=Marshal.GetLastWin32Error();}
                // A hosted/AppContainer target may reject queue attachment. The
                // foreground-source link can still authorize SetForegroundWindow;
                // a denied optional link must not abort the activation request.
                if(target!=current&&target!=source&&target!=input){joinedTarget=AttachThreadInput(current,target,true);if(joinedTarget)Interlocked.Increment(ref links);else targetJoinError=Marshal.GetLastWin32Error();}
                if(!Native.IsWindow(window))return false;
                // Keep the normal/maximized bounds and topmost policy. Only minimized
                // windows are restored; no always-on-top toggle or synthetic Alt key.
                BringWindowToTop(window);if(target==current||joinedTarget||target==source&&joinedSource)SetActiveWindow(window);Native.SetForegroundWindow(window);if(!ForegroundIs(window))ActivateHostedContent(window);Native.SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x13);if(ForegroundIs(window)){RestoreContentFocus(window,joinedSource?source:0,joinedTarget?target:0,joinedInput?input:0);return true;}return false;
            }finally{
                if(joinedTarget){AttachThreadInput(current,target,false);Interlocked.Decrement(ref links);}
                if(joinedInput){AttachThreadInput(current,input,false);Interlocked.Decrement(ref links);}
                if(joinedSource){AttachThreadInput(current,source,false);Interlocked.Decrement(ref links);}
            }
        }
    }
}
