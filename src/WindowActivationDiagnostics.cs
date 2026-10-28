using System;
using System.Text;

namespace OneTaskBar
{
    internal static partial class WindowActivation
    {
        internal static bool HostedCore(string childClass,string frameClass)
        {
            return childClass=="Windows.UI.Core.CoreWindow"&&frameClass=="ApplicationFrameWindow";
        }
        static string WindowClass(IntPtr window)
        {
            var name=new StringBuilder(128);
            Native.GetClassName(window,name,name.Capacity);
            return name.ToString();
        }
        internal static IntPtr ResponseTarget(IntPtr window)
        {
            var root=Native.GetAncestor(window,2);
            // Hosted UWP content may reject/time out WM_NULL while its real frame
            // is responsive. This is not evidence that the application is hung.
            return root!=IntPtr.Zero&&root!=window&&HostedCore(WindowClass(window),WindowClass(root))?root:window;
        }
        static DateTime activationFailureAt;
        static object ActivationWindowInfo(IntPtr window)
        {
            uint process;
            uint thread=Native.GetWindowThreadProcessId(window,out process);
            return new {handle=window.ToInt64(),windowClass=WindowClass(window),process=process,thread=thread,root=Native.GetAncestor(window,2).ToInt64(),responseTarget=ResponseTarget(window).ToInt64()};
        }
        static void RecordActivationFailure(IntPtr source,IntPtr target)
        {
            var now=DateTime.UtcNow;
            if(now-activationFailureAt<TimeSpan.FromSeconds(1))return;
            activationFailureAt=now;
            // No window titles, document names, typed input or screenshots.
            AppLog.Json("window-activation-last.json",new {source=ActivationWindowInfo(source),target=ActivationWindowInfo(target),foreground=ActivationWindowInfo(Native.GetForegroundWindow()),inputQueues=AttachedQueues,sourceJoinError=sourceJoinError,inputJoinError=inputJoinError,targetJoinError=targetJoinError});
        }
    }
}
