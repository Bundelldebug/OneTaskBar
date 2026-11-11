using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace OneTaskBar
{
    internal sealed class FullscreenCandidate
    {
        internal IntPtr Window;
        internal Native.RectI Bounds,Client;
        internal Rectangle Monitor;
        internal bool Maximized,Framed;
        internal bool Captioned;
        internal string Kind="";
        internal int ExStyle;
    }
    internal static class FullscreenVisibility
    {
        static readonly uint ownProcess = CurrentProcessId();
        static readonly object mediaGate = new object();
        static readonly HashSet<IntPtr> mediaSurfaces = new HashSet<IntPtr>();

        internal static void TrackMediaSurface(IntPtr window, bool track)
        {
            if (window == IntPtr.Zero)
                return;
            lock (mediaGate)
            {
                if (track)
                    mediaSurfaces.Add(window);
                else
                    mediaSurfaces.Remove(window);
            }
        }

        static bool MediaSurface(IntPtr window)
        {
            lock (mediaGate)
                return mediaSurfaces.Contains(window);
        }

        static uint CurrentProcessId()
        {
            using (var process = Process.GetCurrentProcess()) return (uint)process.Id;
        }

        [DllImport("user32.dll", CharSet=CharSet.Unicode)]static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr window,out Native.RectI rect);
        [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr window,ref Native.PointI point);
        internal static bool PassiveSurface(int extended,bool foreground,string kind,bool titled)
        {
            if(kind=="Progman"||kind=="WorkerW"||kind=="DesktopWindowXamlSource"||kind.StartsWith("Shell_",StringComparison.Ordinal)||kind=="tooltips_class32")return true;
            if(foreground)return false;
            bool app=(extended&0x40000)!=0;
            return !app&&((extended&(0x08000000|0x20))!=0||!titled&&(extended&0x80)!=0);
        }
        // Screenshot and screen-recording tools put a borderless, topmost, screen-sized selection layer over the
        // monitor. That layer is not an app, and counting it as fullscreen hid the dock exactly when the capture was
        // taken. Names are matched without the extension; the answer is kept per process.
        static readonly string[] captureTools={"sharex","snippingtool","screenclippinghost","screensketch","greenshot","lightshot","flameshot","snagit32","snagiteditor","gyazo","gyazowin","picpick","screenpresso","faststone capture","fscapture","aiselect","smartselect"};
        static readonly Dictionary<uint,bool> captureByProcess=new Dictionary<uint,bool>();
        internal static bool IsCaptureTool(string processName)
        {
            if(String.IsNullOrEmpty(processName))return false;
            foreach(string tool in captureTools)if(String.Equals(processName,tool,StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }
        static bool CaptureToolProcess(uint pid)
        {
            bool known;
            if(captureByProcess.TryGetValue(pid,out known))return known;
            try{using(var process=Process.GetProcessById((int)pid))known=IsCaptureTool(process.ProcessName);}
            catch(Exception){known=false;}
            if(captureByProcess.Count>256)captureByProcess.Clear();
            captureByProcess[pid]=known;
            return known;
        }
        // The capture layer the owner is working in right now, if any: it is not a fullscreen app, but the bar must not
        // float over it.
        internal static IntPtr CaptureLayer {get;private set;}
        internal static System.Drawing.Rectangle CaptureLayerMonitor {get;private set;}
        internal static List<FullscreenCandidate> Capture()
        {
            var result = new List<FullscreenCandidate>();
            var className = new StringBuilder(100);
            IntPtr layer = IntPtr.Zero;
            var layerMonitor = Rectangle.Empty;
            IntPtr foreground = Native.GetForegroundWindow();
            IntPtr frontRoot = Native.GetAncestor(foreground, 3);
            uint frontPid;
            Native.GetWindowThreadProcessId(foreground, out frontPid);
            Native.EnumWindows((window, state) =>
            {
                uint pid;
                Native.GetWindowThreadProcessId(window, out pid);
                bool media = pid == ownProcess && MediaSurface(window);
                // Ordinary shell surfaces must not hide the dock. The explicitly
                // registered fullscreen player is the one owned exception.
                if (pid == ownProcess && !media)
                    return true;
                var candidate = Inspect(window, media, window == frontRoot, className);
                // Only a window that would count as fullscreen is worth looking up.
                if (candidate != null && !media && (ClientCovers(candidate.Client, candidate.Monitor) || Native.CoversMonitor(candidate.Bounds, candidate.Monitor, candidate.Maximized, candidate.Framed)) && CaptureToolProcess(pid))
                {
                    if(pid==frontPid&&layer==IntPtr.Zero){layer=window;layerMonitor=candidate.Monitor;}
                    return true;
                }
                if (candidate != null)
                    result.Add(candidate);
                return true;
            }, IntPtr.Zero);
            CaptureLayer=layer;CaptureLayerMonitor=layerMonitor;
            return result;
        }
        internal static FullscreenCandidate Inspect(IntPtr window)
        {
            bool foreground = Native.GetAncestor(Native.GetForegroundWindow(), 3) == window;
            return Inspect(window, MediaSurface(window), foreground, new StringBuilder(100));
        }
        static FullscreenCandidate Inspect(IntPtr window, bool media, bool foreground, StringBuilder className)
        {
            if (!Native.IsWindowVisible(window) || Native.IsIconic(window)) return null;
            int cloaked;
            if (Native.DwmGetWindowAttribute(window, 14, out cloaked, 4) >= 0 && cloaked != 0) return null;
            int extended = Native.GetWindowLong(window, -20);
            bool checkTitle = !media && !foreground && (extended & 0x80) != 0 && (extended & (0x40000 | 0x08000000 | 0x20)) == 0;
            bool titled = !checkTitle || GetWindowTextLength(window) > 0;
            className.Length = 0;
            Native.GetClassName(window, className, className.Capacity);
            string kind = className.ToString();
            if (!media && PassiveSurface(extended, foreground, kind, titled)) return null;
            Native.RectI bounds,client;if(!Native.GetWindowRect(window,out bounds)||!GetClientRect(window,out client)||bounds.Right<=bounds.Left||bounds.Bottom<=bounds.Top)return null;
            var first=new Native.PointI {X=client.Left,Y=client.Top};var last=new Native.PointI {X=client.Right,Y=client.Bottom};if(!ClientToScreen(window,ref first)||!ClientToScreen(window,ref last))return null;
            int style = Native.GetWindowLong(window, -16);
            var candidate = new FullscreenCandidate
            {
                Window = window,
                Bounds = bounds,
                Client = new Native.RectI { Left = first.X, Top = first.Y, Right = last.X, Bottom = last.Y },
                Monitor = System.Windows.Forms.Screen.FromHandle(window).Bounds,
                Maximized = Native.IsZoomed(window),
                Framed = (style & 0xC40000) != 0,
                Captioned = (style & 0xC00000) != 0,
                Kind = kind,
                ExStyle = extended
            };
            if ((extended & 0x80) != 0 && !ClientCovers(candidate.Client, candidate.Monitor)
                && !Native.CoversMonitor(candidate.Bounds, candidate.Monitor, candidate.Maximized, candidate.Framed))
                return null;
            return candidate;
        }
        static bool ClientCovers(Native.RectI rect,Rectangle screen){return rect.Left<=screen.Left+1&&rect.Top<=screen.Top+1&&rect.Right>=screen.Right-1&&rect.Bottom>=screen.Bottom-1;}
        internal static bool IsFullScreen(Rectangle screen,IEnumerable<FullscreenCandidate> windows)
        {
            return Cover(screen,windows)!=null;
        }
        // Browsers and video players: fullscreen windows that can share the screen with a black band at the top.
        // Games are left alone, since the band would cover part of their picture.
        internal static bool BandFriendly(FullscreenCandidate window)
        {
            string kind=window==null?"":window.Kind??"";
            return BrowserWindow(kind)||kind=="ApplicationFrameWindow"||
                kind.StartsWith("PotPlayer",StringComparison.OrdinalIgnoreCase)||kind.StartsWith("mpv",StringComparison.OrdinalIgnoreCase)||kind.StartsWith("VLC",StringComparison.OrdinalIgnoreCase);
        }
        static bool BrowserWindow(string kind)
        {
            return kind.StartsWith("Chrome_WidgetWin",StringComparison.Ordinal)||kind=="MozillaWindowClass";
        }

        static bool Covers(FullscreenCandidate window,Rectangle screen)
        {
            if(window.Maximized&&Preferences.Current.MaximizedBehindDock)
            {
                // A browser can enter fullscreen from a maximized window without clearing WS_MAXIMIZE.
                // Its native caption is removed and its client matches the monitor, including all four edges.
                return !window.Captioned&&BrowserWindow(window.Kind??"")&&window.Monitor==screen
                    && Math.Abs(window.Client.Left-screen.Left)<=1&&Math.Abs(window.Client.Top-screen.Top)<=1
                    && Math.Abs(window.Client.Right-screen.Right)<=1&&Math.Abs(window.Client.Bottom-screen.Bottom)<=1;
            }
            return ClientCovers(window.Client,screen)||Native.CoversMonitor(window.Bounds,screen,window.Maximized,window.Framed);
        }

        // Like Cover, but unframed tool windows (other overlays and floating widgets) are not the app on screen.
        internal static FullscreenCandidate BandCover(Rectangle screen,IEnumerable<FullscreenCandidate> windows)
        {
            return Cover(screen, windows, true);
        }
        internal static FullscreenCandidate Cover(Rectangle screen, IEnumerable<FullscreenCandidate> windows)
        {
            return Cover(screen, windows, false);
        }
        static bool IsToolOverlay(FullscreenCandidate window)
        {
            return !window.Framed && !window.Maximized && (window.ExStyle & 0x80) != 0;
        }
        static FullscreenCandidate Cover(Rectangle screen, IEnumerable<FullscreenCandidate> windows, bool skipTools)
        {
            // The window being used decides first. After Alt-Tab back to a borderless game, another app's overlay or a
            // window that has not finished leaving can still sit above it in stacking order, which left the dock up.
            IntPtr front=Native.GetAncestor(Native.GetForegroundWindow(),3);
            if(front!=IntPtr.Zero)
            {
                foreach(var window in windows)
                {
                    if(window.Window!=front||window.Monitor!=screen||skipTools&&IsToolOverlay(window))continue;
                    if(Covers(window,screen))return window;
                    break;
                }
            }
            // EnumWindows is in stacking order. An inactive fullscreen window on a
            // second monitor still hides that monitor's dock; a normal app above it does not.
            foreach(var window in windows)
            {
                if (skipTools && IsToolOverlay(window)) continue;
                var bounds=window.Bounds;
                if(bounds.Right<=screen.Left||bounds.Left>=screen.Right||bounds.Bottom<=screen.Top||bounds.Top>=screen.Bottom)continue;
                if(Covers(window,screen))return window;
                if(window.Monitor==screen)return null;
            }
            return null;
        }
    }
}
