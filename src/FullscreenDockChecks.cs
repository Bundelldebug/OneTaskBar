using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace OneTaskBar
{
    internal static class FullscreenDockChecks
    {
        internal static async Task Run(DockHost dock)
        {
            Require(FullscreenVisibility.IsCaptureTool("ShareX")&&FullscreenVisibility.IsCaptureTool("SnippingTool")&&!FullscreenVisibility.IsCaptureTool("chrome")&&!FullscreenVisibility.IsCaptureTool(null),"capture tools' selection layers are not fullscreen apps");
            {
                var screen=dock.Screen.Bounds;var box=new Native.RectI {Left=screen.Left,Top=screen.Top,Right=screen.Right,Bottom=screen.Bottom};
                var office=new FullscreenCandidate {Window=new IntPtr(1),Bounds=box,Client=box,Monitor=screen,Maximized=true,Framed=false};
                bool previous=Preferences.Current.MaximizedBehindDock;
                try
                {
                    Preferences.Current.MaximizedBehindDock=true;
                    Require(!FullscreenVisibility.IsFullScreen(screen,new[]{office}),"a maximized window with its own title bar is not fullscreen when maximizing to the taskbar is on");
                    Preferences.Current.MaximizedBehindDock=false;
                    Require(FullscreenVisibility.IsFullScreen(screen,new[]{office}),"the same window still counts as fullscreen when that option is off");
                    Preferences.Current.MaximizedBehindDock=true;
                    office.Maximized=false;
                    Require(FullscreenVisibility.IsFullScreen(screen,new[]{office}),"a window that is not maximized and covers the monitor is fullscreen");
                }
                finally{Preferences.Current.MaximizedBehindDock=previous;}
            }
            var media = new FullscreenMedia(command => { }, position => Task.FromResult(true), () => { }, () => { });
            try
            {
                media.Present(dock.Screen, dock.Scale);
                await Task.Delay(40);
                var handle = new WindowInteropHelper(media).Handle;
                var ownWindow = FullscreenVisibility.Inspect(handle);
                Require(ownWindow != null && FullscreenVisibility.IsFullScreen(dock.Screen.Bounds, new[] { ownWindow }),
                    "fullscreen media covers its monitor");
                var captured = FullscreenVisibility.Capture();
                Require(captured.Any(candidate => candidate.Window == handle)
                    && FullscreenVisibility.IsFullScreen(dock.Screen.Bounds, captured),
                    "the app's fullscreen media participates in dock hiding");

                // The edge event can arrive before the next shell visibility tick.
                dock.UpdateVisibility(false, true);
                dock.CompleteAutoHide(false);
                await Task.Delay(240);
                dock.Reveal();
                await Task.Delay(260);
                Require(!dock.DockVisible && !dock.EdgeVisible && !dock.AutoHideAnimating,
                    "entering the edge cannot reveal a dock over newly fullscreen media");

                media.HideImmediately();
                Require(!FullscreenVisibility.Capture().Any(candidate => candidate.Window == handle),
                    "hidden fullscreen media no longer suppresses the dock");
                dock.UpdateVisibility(false, true);
                dock.Reveal();
                await Task.Delay(260);
                Require(dock.DockVisible && dock.Apps.IsVisible,
                    "normal edge reveal resumes after fullscreen media closes");
            }
            finally
            {
                media.Close();
                dock.UpdateVisibility(false, false);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
