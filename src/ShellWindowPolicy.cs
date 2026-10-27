using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OneTaskBar
{
    // Shell surfaces must never become entries in Explorer's Task View.
    internal static class ShellWindowPolicy
    {
        const int ToolWindow=0x80,AppWindow=0x40000;
        static DispatcherTimer reviewLease;
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        [ComImport,Guid("56FDF342-FD6D-11D0-958A-006097C9A090"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ITaskbarList
        {
            void HrInit();
            void AddTab(IntPtr window);
            void DeleteTab(IntPtr window);
            void ActivateTab(IntPtr window);
            void SetActiveAlt(IntPtr window);
        }
        internal static int Style(int original,bool review)
        {
            return review?(original|AppWindow)&~ToolWindow:(original|ToolWindow)&~AppWindow;
        }
        internal static void Track(GlassWindow window)
        {
            window.Loaded+=(s,e)=>Apply(window,true);
            window.IsVisibleChanged+=(s,e)=>{if(window.IsVisible)Apply(window,true);};
            if(!Program.CaptureReview||reviewLease!=null)return;
            // Capture is temporary, including when a review is interrupted.
            reviewLease=new DispatcherTimer {Interval=TimeSpan.FromMinutes(2)};
            reviewLease.Tick+=(s,e)=>
            {
                reviewLease.Stop();
                Program.CaptureReview=false;
                foreach(Window open in Application.Current.Windows)
                {
                    var surface=open as GlassWindow;
                    if(surface!=null)Apply(surface,true);
                }
            };
            reviewLease.Start();
        }
        internal static void Apply(Window window,bool removeTab=false)
        {
            var handle=new WindowInteropHelper(window).Handle;
            if(handle==IntPtr.Zero)return;
            bool review=Program.CaptureReview;
            if(window.ShowInTaskbar!=review)window.ShowInTaskbar=review;
            int previous=Native.GetWindowLong(handle,-20),next=Style(previous,review);
            if(previous!=next)Native.SetWindowLong(handle,-20,next);
            if(review)return;
            if(previous==next&&!removeTab)return;
            int excluded=1;
            DwmSetWindowAttribute(handle,11,ref excluded,4);
            DwmSetWindowAttribute(handle,12,ref excluded,4);
            if(!removeTab)return;
            object instance=null;
            try
            {
                instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11D0-958A-006097C9A090")));
                var taskbar=(ITaskbarList)instance;
                taskbar.HrInit();
                taskbar.DeleteTab(handle);
            }
            catch(COMException) { }
            finally {if(instance!=null)Marshal.ReleaseComObject(instance);}
        }
    }
}
