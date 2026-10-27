using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static partial class Native
    {
        sealed class RegionState {internal int Width,Height,Radius;internal bool Soft;internal RectI? InputClip;internal LiveBackdrop Backdrop;}
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window,RegionState> regions=new System.Runtime.CompilerServices.ConditionalWeakTable<Window,RegionState>();
        internal delegate bool EnumProc(IntPtr window, IntPtr state);
        [StructLayout(LayoutKind.Sequential)] internal struct RectI { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct PointI { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct Margins { public int Left, Right, Top, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct PowerStatus { public byte AC, Flags, Percent, Reserved; public uint Life, FullLife; }
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr state);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out RectI rect);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")]static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
        [DllImport("user32.dll")]static extern int GetWindowRgn(IntPtr window,IntPtr region);
        [DllImport("gdi32.dll")]static extern bool PtInRegion(IntPtr region,int x,int y);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attr, ref int value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
        internal static bool ClearPreviewBackground(Window window)
        {
            var handle=new WindowInteropHelper(window).Handle;var source=HwndSource.FromHwnd(handle);
            if(source!=null)source.CompositionTarget.BackgroundColor=Colors.Transparent;
            var margins=new Margins {Left=-1,Right=-1,Top=-1,Bottom=-1};
            return DwmExtendFrameIntoClientArea(handle,ref margins)>=0;
        }
        [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr window, int attr, out int value, int size);
        [DllImport("kernel32.dll")] internal static extern bool GetSystemPowerStatus(out PowerStatus status);
        [DllImport("user32.dll")] internal static extern IntPtr RegisterPowerSettingNotification(IntPtr window, ref Guid setting, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnregisterPowerSettingNotification(IntPtr handle);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr window, int id, uint mods, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [StructLayout(LayoutKind.Sequential)] struct AccentPolicy {public int State,Flags;public uint Color;public int Animation;}
        [StructLayout(LayoutKind.Sequential)] struct CompositionData {public int Attribute;public IntPtr Data;public int Size;}
        [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr window,ref CompositionData data);

        internal static bool Blur(Window window, double radius, bool dark)
        {
            var handle = new WindowInteropHelper(window).Handle;
            var source = HwndSource.FromHwnd(handle);
            if (source != null) source.CompositionTarget.BackgroundColor = Colors.Transparent;
            int mode = dark ? 1 : 0, backdrop=1, corners=1;
            DwmSetWindowAttribute(handle, 20, ref mode, 4);
            DwmSetWindowAttribute(handle, 33, ref corners, 4);
            DwmSetWindowAttribute(handle,38,ref backdrop,4);
            if(SystemParameters.HighContrast||!WindowsTheme.Read().Transparency){Round(window,radius);return false;}
            bool success=false;
            try {var state=regions.GetValue(window,w=>new RegionState());state.Backdrop=new LiveBackdrop(window,radius);success=true;
                window.Closed+=(s,e)=> {if(state.Backdrop!=null){state.Backdrop.Dispose();state.Backdrop=null;}};
            }catch(Exception e){AppLog.Once("clipped-backdrop",e.ToString());}
            Round(window, radius);
            return success;
        }
        internal static void Round(Window window, double radius)
        {
            var handle = new WindowInteropHelper(window).Handle;
            RectI rect;
            if (!GetWindowRect(handle, out rect)) return;
            double scale = Math.Max(96, GetDpiForWindow(handle)) / 96.0;
            int width=rect.Right-rect.Left+1,height=rect.Bottom-rect.Top+1,curve=(int)Math.Round(radius*2*scale);
            var state=regions.GetValue(window,w=>new RegionState());
            if(state.Backdrop!=null)state.Backdrop.Resize();
            // A GDI region has binary pixel coverage and cuts off the layered window's antialiasing.
            // Capsules and panels already have per-pixel WPF alpha and a matching composition clip.
            var glass=window as GlassWindow;
            if(glass!=null&&glass.BackdropClip.HasValue){var bounds=InputBounds(glass.BackdropClip.Value,scale);
                if(!state.InputClip.HasValue||!state.InputClip.Value.Equals(bounds)){if(ClipWindow(handle,bounds))state.InputClip=bounds;}state.Soft=false;return;}
            if(state.InputClip.HasValue){SetWindowRgn(handle,IntPtr.Zero,true);state.InputClip=null;state.Width=0;}
            if(glass!=null&&glass.SoftCorners){if(!state.Soft){SetWindowRgn(handle,IntPtr.Zero,true);state.Soft=true;}state.Width=width;state.Height=height;state.Radius=curve;return;}
            // SetWindowRgn emits position notifications itself. Do not feed those back into another SetWindowRgn.
            if(!state.Soft&&state.Width==width&&state.Height==height&&state.Radius==curve)return;
            state.Soft=false;
            state.Width=width;state.Height=height;state.Radius=curve;
            var region=CreateRoundRectRgn(0,0,width,height,curve,curve);
            if(SetWindowRgn(handle,region,true)==0){DeleteObject(region);state.Width=0;}
        }
        internal static RectI InputBounds(Rect clip,double scale){return new RectI {Left=Math.Max(0,(int)Math.Floor(clip.Left*scale)-1),Top=Math.Max(0,(int)Math.Floor(clip.Top*scale)-1),Right=(int)Math.Ceiling(clip.Right*scale)+1,Bottom=(int)Math.Ceiling(clip.Bottom*scale)+1};}
        // WPF/composition presents the changed surface itself. A synchronous GDI
        // repaint here duplicates that work on every media morph frame.
        internal static bool ClipWindow(IntPtr window,RectI bounds){var region=CreateRectRgn(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom);if(SetWindowRgn(window,region,false)!=0)return true;DeleteObject(region);return false;}
        internal static void EnableBlur(Window window,bool enabled){RegionState state;if(regions.TryGetValue(window,out state)&&state.Backdrop!=null)state.Backdrop.Enable(enabled);}
        internal static bool RealtimeBackdrop(Window window){RegionState state;return regions.TryGetValue(window,out state)&&state.Backdrop!=null&&state.Backdrop.Realtime;}
        internal static int RefractedRegions(Window window){RegionState state;return regions.TryGetValue(window,out state)&&state.Backdrop!=null?state.Backdrop.RefractedRegions:0;}
        internal static int RefractionLayers(Window window){RegionState state;return regions.TryGetValue(window,out state)&&state.Backdrop!=null?state.Backdrop.RefractionLayers:0;}
        internal static void BlurOpacity(Window window,double value){RegionState state;if(regions.TryGetValue(window,out state)&&state.Backdrop!=null)state.Backdrop.SetOpacity(value);}
        internal static double BlurOpacity(Window window){RegionState state;return regions.TryGetValue(window,out state)&&state.Backdrop!=null?state.Backdrop.Opacity:0;}
        internal static void RefreshOptics(Window window){RegionState state;if(regions.TryGetValue(window,out state)&&state.Backdrop!=null)state.Backdrop.RefreshOptics();}
        internal static void EnsureDockVisible(Window window,bool raise=false)
        {
            // Explorer can hide or minimize an HWND without changing WPF Visibility.
            // Restore only a dock that its controller already intends to display.
            var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
            if(!window.IsVisible)window.Show();
            ShellWindowPolicy.Apply(window);
            if(!IsWindowVisible(handle)||IsIconic(handle))ShowWindow(handle,4);
            if(raise){var owner=GetWindow(handle,4);if(owner!=IntPtr.Zero)SetWindowPos(owner,new IntPtr(-1),0,0,0,0,0x53);}
            if(raise||(GetWindowLong(handle,-20)&8)==0)SetWindowPos(handle,new IntPtr(-1),0,0,0,0,0x53);
            RegionState state;if(regions.TryGetValue(window,out state)&&state.Backdrop!=null)state.Backdrop.EnsureDockVisible();
        }
        internal static double PrepareMonitor(Window window,System.Drawing.Rectangle screen)
        {
            var handle=new WindowInteropHelper(window).EnsureHandle();var monitor=MonitorFromPoint(new PointI {X=screen.Left+screen.Width/2,Y=screen.Top+screen.Height/2},2);
            if(MonitorFromWindow(handle,2)!=monitor)return SeedMonitor(window,screen);
            return Math.Max(96,GetDpiForWindow(handle))/96.0;
        }
        internal static double SeedMonitor(Window window,System.Drawing.Rectangle screen)
        {
            var handle=new WindowInteropHelper(window).EnsureHandle();double width=window.Width,height=window.Height;
            try{
                // Read destination DPI with the HWND wholly inside that monitor.
                // A wide HWND can otherwise retain the old/third monitor's scale.
                SetWindowPos(handle,IntPtr.Zero,screen.Left+screen.Width/2,screen.Top+screen.Height/2,1,1,0x4|0x8|0x10);
                return Math.Max(96,GetDpiForWindow(handle))/96.0;
            }finally{if(!window.Width.Equals(width))window.Width=width;if(!window.Height.Equals(height))window.Height=height;}
        }
        internal static RectI PlacementBounds(System.Drawing.Rectangle screen,double scale,double x,double y,double width,double height)
        {
            int left=screen.Left+(int)Math.Round(x*scale),top=screen.Top+(int)Math.Round(y*scale);return new RectI {Left=left,Top=top,Right=left+(int)Math.Round(width*scale),Bottom=top+(int)Math.Round(height*scale)};
        }
        internal static void Place(Window window, System.Drawing.Rectangle screen, double x, double y, double w, double h)
        {
            var handle = new WindowInteropHelper(window).EnsureHandle();
            double scale=PrepareMonitor(window,screen);var bounds=PlacementBounds(screen,scale,x,y,w,h);
            SetWindowPos(handle,new IntPtr(-1),bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top,0x10);
        }
        internal static object RegionInfo(Window window)
        {
            var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return null;RectI rect;GetWindowRect(handle,out rect);var r=CreateRectRgn(0,0,0,0);
            try{int type=GetWindowRgn(handle,r),style=GetWindowLong(handle,-20);var owner=GetWindow(handle,4);int ownerStyle=owner!=IntPtr.Zero?GetWindowLong(owner,-20):0;return new {title=window.Title,visible=window.IsVisible,left=rect.Left,top=rect.Top,width=rect.Right-rect.Left,height=rect.Bottom-rect.Top,rounded=type!=0&&!PtInRegion(r,0,0)&&PtInRegion(r,(rect.Right-rect.Left)/2,(rect.Bottom-rect.Top)/2),excludedFromAltTab=(style&0x80)!=0&&(style&0x40000)==0,backdropExcludedFromAltTab=(ownerStyle&0x80)!=0&&(ownerStyle&0x40000)==0};}finally{DeleteObject(r);}
        }
        internal static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute=true }); }
            catch (Exception e) { AppLog.Write("open", e.Message); }
        }
        internal static void RestoreTaskbars()
        {
            EnumWindows((h, s) => { var n = new StringBuilder(100); GetClassName(h,n,100);
                if(n.ToString()=="Shell_TrayWnd" || n.ToString()=="Shell_SecondaryTrayWnd") {TaskbarMask.Restore(h);ShowWindow(h,5);}
                if(n.ToString()=="TopLevelWindowForOverflowXamlIsland"||n.ToString()=="NotifyIconOverflowWindow")TrayIconSession.Restore(h);
                return true; }, IntPtr.Zero);
        }
        internal static void HideTaskbars()
        {
            if(!TaskbarMask.Active)return;
            EnumWindows((h, s) => { var n = new StringBuilder(100); GetClassName(h,n,100);
                if(n.ToString()=="Shell_TrayWnd" || n.ToString()=="Shell_SecondaryTrayWnd") TaskbarMask.Hide(h);
                return true; }, IntPtr.Zero);
        }

        [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IImageFactory { [PreserveSig] int GetImage(SizeI size, uint flags, out IntPtr bitmap); }
        [StructLayout(LayoutKind.Sequential)] struct SizeI { public int W,H; }
        [DllImport("shell32.dll", CharSet=CharSet.Unicode, PreserveSig=true)]
        static extern int SHCreateItemFromParsingName(string path, IntPtr ctx, ref Guid id, [MarshalAs(UnmanagedType.Interface)] out IImageFactory result);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHCreateItemInKnownFolder(ref Guid folder,uint flags,string item,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IImageFactory result);
        internal static ImageSource AppIcon(string target)
        {
            IImageFactory factory=null;
            try {
                var id = new Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B");
                int created;
                if(target.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase)){var folder=new Guid("1E87508D-89C2-42F0-8A7E-645A0F50CA58");created=SHCreateItemInKnownFolder(ref folder,0,target.Substring(17),ref id,out factory);}
                else created=SHCreateItemFromParsingName(target,IntPtr.Zero,ref id,out factory);
                if(created!=0)return null;
                IntPtr bitmap;
                if(factory.GetImage(new SizeI {W=256,H=256},0x104,out bitmap)!=0||bitmap==IntPtr.Zero)return null;
                try { var image=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze(); return image; } finally { DeleteObject(bitmap); }
            } catch { return null; }
            finally { if(factory!=null) Marshal.ReleaseComObject(factory); }
        }

        [DllImport("wlanapi.dll")] static extern uint WlanOpenHandle(uint version,IntPtr reserved,out uint agreed,out IntPtr client);
        [DllImport("wlanapi.dll")] static extern uint WlanEnumInterfaces(IntPtr client,IntPtr reserved,out IntPtr list);
        [DllImport("wlanapi.dll")] static extern uint WlanQueryInterface(IntPtr client,ref Guid id,uint opcode,IntPtr reserved,out uint size,out IntPtr data,out uint type);
        [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr memory);
        [DllImport("wlanapi.dll")] static extern uint WlanCloseHandle(IntPtr client,IntPtr reserved);
        internal static string WifiName(){return ReadWifi().Name;}
        internal static WifiLink ReadWifi()
        {
            IntPtr client=IntPtr.Zero,list=IntPtr.Zero;
            try {
                uint agreed; if(WlanOpenHandle(2,IntPtr.Zero,out agreed,out client)!=0) return new WifiLink();
                if(WlanEnumInterfaces(client,IntPtr.Zero,out list)!=0) return new WifiLink();
                int count=Marshal.ReadInt32(list);
                for(int i=0;i<count;i++) {
                    var guid=(Guid)Marshal.PtrToStructure(IntPtr.Add(list,8+i*532),typeof(Guid)); uint size,type; IntPtr data;
                    if(WlanQueryInterface(client,ref guid,7,IntPtr.Zero,out size,out data,out type)!=0) continue;
                    try {
                        if(size<556 || Marshal.ReadInt32(data)!=1) continue;
                        byte[] attributes=new byte[Math.Min(604,(int)size)];Marshal.Copy(data,attributes,0,attributes.Length);var link=WifiLink.Parse(attributes);try{foreach(var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()){Guid adapterId;if(Guid.TryParse(adapter.Id,out adapterId)&&adapterId==guid){var stats=adapter.GetIPStatistics();link.Sent=stats.BytesSent;link.Received=stats.BytesReceived;break;}}}catch{}return link;
                    } finally { WlanFreeMemory(data); }
                }
            } catch { }
            finally { if(list!=IntPtr.Zero) WlanFreeMemory(list); if(client!=IntPtr.Zero) WlanCloseHandle(client,IntPtr.Zero); }
            return new WifiLink();
        }
    }

    internal sealed class PowerState
    {
        public bool Known, Connected, Charging;
        public int Level=-1;
        public static PowerState From(bool ok, byte ac, byte flags, byte percent)
        {
            bool battery=ok && flags!=255 && (flags&128)==0;
            return new PowerState { Known=battery, Connected=ok && ac==1,
                Charging=battery && ac==1 && (flags&8)!=0, Level=battery && percent<=100 ? percent : -1 };
        }
        public static PowerState Read() { Native.PowerStatus s; bool ok=Native.GetSystemPowerStatus(out s); return From(ok,s.AC,s.Flags,s.Percent); }
    }

    // The method order is the native COM vtable order; scalar values are normalized by Windows.
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator {
        [PreserveSig] int EnumAudioEndpoints(int flow,uint mask,out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);
        int GetDevice(string id,out IMMDevice device); int RegisterEndpointNotificationCallback(IntPtr callback); int UnregisterEndpointNotificationCallback(IntPtr callback);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice {
        [PreserveSig] int Activate(ref Guid id,uint context,IntPtr param,[MarshalAs(UnmanagedType.IUnknown)] out object result);
        [PreserveSig] int OpenPropertyStore(uint mode,out IntPtr props);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out uint state);
    }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class DeviceEnumerator { }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr callback); [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback); int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float level,ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level,ref Guid context);
        int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel,float level,ref Guid context); int SetChannelVolumeLevelScalar(uint channel,float level,ref Guid context);
        int GetChannelVolumeLevel(uint channel,out float level); int GetChannelVolumeLevelScalar(uint channel,out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute,ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        int GetVolumeStepInfo(out uint step,out uint count); int VolumeStepUp(ref Guid context); int VolumeStepDown(ref Guid context);
        int QueryHardwareSupport(out uint mask); int GetVolumeRange(out float min,out float max,out float increment);
    }
    [ComVisible(true),Guid("657804FA-D6AD-4496-8A60-352752AF4F89"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioVolumeCallback {[PreserveSig]int OnNotify(IntPtr data);}
    [ComVisible(true),ClassInterface(ClassInterfaceType.None)]
    public sealed class AudioVolumeCallback : IAudioVolumeCallback
    {
        readonly Action changed;internal AudioVolumeCallback(Action changed){this.changed=changed;}
        public int OnNotify(IntPtr data){try{changed();}catch{}return 0;}
    }
    internal sealed class AudioService : IDisposable
    {
        IAudioEndpointVolume endpoint;
        readonly AudioVolumeCallback callback;IntPtr callbackPointer;
        internal Action Changed;
        public AudioService(){callback=new AudioVolumeCallback(()=>{var changed=Changed;if(changed!=null)changed();});}
        internal bool NotificationsRegistered {get{return callbackPointer!=IntPtr.Zero;}}
        internal double ExactValue {get{Ensure();float value;Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out value));return (double)value*100;}}
        string deviceId;
        DateTime nextDeviceCheck;
        public bool Available; public double Value; public bool Muted;
        void Ensure()
        {
            if(endpoint!=null && DateTime.UtcNow<nextDeviceCheck) return;
            IMMDeviceEnumerator enumerator=null; IMMDevice device=null;
            try {
                enumerator=(IMMDeviceEnumerator)new DeviceEnumerator();
                if(enumerator.GetDefaultAudioEndpoint(0,1,out device)!=0) throw new InvalidOperationException("No audio output");
                string id; device.GetId(out id);
                if(id!=deviceId || endpoint==null) {
                    Dispose(); object result; var iid=typeof(IAudioEndpointVolume).GUID;
                    Marshal.ThrowExceptionForHR(device.Activate(ref iid,23,IntPtr.Zero,out result));
                    endpoint=(IAudioEndpointVolume)result; deviceId=id;
                    try{callbackPointer=Marshal.GetComInterfaceForObject(callback,typeof(IAudioVolumeCallback));Marshal.ThrowExceptionForHR(endpoint.RegisterControlChangeNotify(callbackPointer));}
                    catch{if(callbackPointer!=IntPtr.Zero){Marshal.Release(callbackPointer);callbackPointer=IntPtr.Zero;}}
                }
                nextDeviceCheck=DateTime.UtcNow.AddSeconds(3);
            } finally { if(device!=null) Marshal.ReleaseComObject(device); if(enumerator!=null) Marshal.ReleaseComObject(enumerator); }
        }
        public void Read()
        {
            try { Ensure(); float value; bool muted;
                Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out value));
                Marshal.ThrowExceptionForHR(endpoint.GetMute(out muted)); Value=Math.Round(value*100); Muted=muted; Available=true;
            } catch { Available=false; Dispose(); }
        }
        public void Set(double value) { Ensure(); var ctx=Guid.Empty; Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar((float)(Math.Max(0,Math.Min(100,value))/100),ref ctx)); }
        public void Mute(bool muted) { Ensure(); var ctx=Guid.Empty; Marshal.ThrowExceptionForHR(endpoint.SetMute(muted,ref ctx)); }
        public void Dispose() { if(endpoint!=null) {if(callbackPointer!=IntPtr.Zero){try{endpoint.UnregisterControlChangeNotify(callbackPointer);}catch{}Marshal.Release(callbackPointer);callbackPointer=IntPtr.Zero;}Marshal.ReleaseComObject(endpoint); endpoint=null; } }
    }
    internal static class BrightnessService
    {
        public static int Read()
        {
            try { using(var query=new ManagementObjectSearcher("root\\WMI","SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE"))
                using(var results=query.Get()) foreach(ManagementObject item in results) using(item) return Convert.ToInt32(item["CurrentBrightness"]); }
            catch(Exception e) { AppLog.Once("brightness-read",e.Message); }
            return -1;
        }
        public static bool Set(int level)
        {
            bool any=false;
            using(var query=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=TRUE"))
            using(var results=query.Get()) foreach(ManagementObject item in results) using(item) { item.InvokeMethod("WmiSetBrightness",new object[] { (uint)0,(byte)Math.Max(0,Math.Min(100,level)) }); any=true; }
            return any;
        }
    }

    internal sealed class VolumeKeys : IDisposable
    {
        delegate IntPtr Hook(int code,IntPtr message,IntPtr info);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int type,Hook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr info);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string module);
        readonly Hook callback; IntPtr handle;
        public bool Enabled; public Func<int,bool> OnKey;internal Func<bool> InterceptStart,InterceptAltTab,InterceptTaskView;internal Action OnStart,OnNativeStart,OnTaskView;internal readonly WindowSwitchGesture AltTabGesture=new WindowSwitchGesture();
        readonly TaskViewGesture taskView=new TaskViewGesture();
        readonly StartKeyGesture start=new StartKeyGesture();
        int bufferedWin,nativeWins;
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        public VolumeKeys() { callback=Handle; handle=SetWindowsHookEx(13,callback,GetModuleHandle(null),0); }
        IntPtr Handle(int code,IntPtr message,IntPtr info)
        {
            int key=code>=0 ? Marshal.ReadInt32(info) : 0;
            if(code>=0&&Marshal.ReadInt64(info,16)==(long)SystemPanels.InputMarker)return CallNextHookEx(handle,code,message,info);
            if(code>=0) {
                bool down=message==new IntPtr(0x100)||message==new IntPtr(0x104),up=message==new IntPtr(0x101)||message==new IntPtr(0x105);
                if(down||up)try{uint flags=(uint)Marshal.ReadInt32(info,8);bool alt=(flags&0x20)!=0||(GetAsyncKeyState(0x12)&0x8000)!=0,ctrl=(GetAsyncKeyState(0x11)&0x8000)!=0,shift=(GetAsyncKeyState(0x10)&0x8000)!=0,win=(GetAsyncKeyState(0x5b)&0x8000)!=0||(GetAsyncKeyState(0x5c)&0x8000)!=0;if(AltTabGesture.Observe(key,down,Enabled&&InterceptAltTab!=null&&InterceptAltTab(),alt,ctrl,shift,win,(flags&0x10)!=0))return new IntPtr(1);}catch(Exception error){AppLog.Once("switcher-key",error.GetType().Name);}
                if(down||up){bool modifiers=(GetAsyncKeyState(0x10)&0x8000)!=0||(GetAsyncKeyState(0x11)&0x8000)!=0||(GetAsyncKeyState(0x12)&0x8000)!=0;
                    bool interceptStart=Enabled&&InterceptStart!=null&&InterceptStart(),interceptTask=Enabled&&InterceptTaskView!=null&&InterceptTaskView();
                    bool open;if(taskView.Observe(key,down,bufferedWin,interceptTask,modifiers,(Marshal.ReadInt32(info,8)&0x10)!=0,out open)){if(open){start.Observe(bufferedWin,false,false,false);bufferedWin=0;if(OnTaskView!=null)OnTaskView();}return new IntPtr(1);}
                    bool intercept=interceptStart||interceptTask,invoke=start.Observe(key,down,interceptStart,modifiers),win=key==0x5b||key==0x5c;
                    int winBit=key==0x5b?1:key==0x5c?2:0;
                    // Keep a solo Win press entirely out of Explorer. Cancelling its action
                    // at key-up is too late: shell/customizer dimming has already started.
                    if(win&&down&&bufferedWin==key)return new IntPtr(1);
                    if(win&&down&&bufferedWin==0&&(nativeWins&winBit)==0&&intercept&&!modifiers){bufferedWin=key;return new IntPtr(1);}
                    if(win&&up&&bufferedWin==key){bufferedWin=0;if(invoke&&OnStart!=null)OnStart();else if(!interceptStart&&OnNativeStart!=null)OnNativeStart();return new IntPtr(1);}
                    if(down&&bufferedWin!=0){int held=bufferedWin;bufferedWin=0;if(SystemPanels.ForwardStartChord((ushort)held,(ushort)key,(ushort)Marshal.ReadInt32(info,4),(uint)Marshal.ReadInt32(info,8))){nativeWins|=held==0x5b?1:2;nativeWins|=winBit;return new IntPtr(1);}}
                    if(win){if(down)nativeWins|=winBit;else nativeWins&=~winBit;}
                }
            }
            if(code>=0 && Enabled && key>=0xAD && key<=0xAF && OnKey!=null) {
                if(message==new IntPtr(0x100) || message==new IntPtr(0x104)) { if(OnKey(key)) return new IntPtr(1); }
                else if(message==new IntPtr(0x101) || message==new IntPtr(0x105)) return new IntPtr(1);
            }
            return CallNextHookEx(handle,code,message,info);
        }
        public void Dispose() { if(handle!=IntPtr.Zero) { UnhookWindowsHookEx(handle); handle=IntPtr.Zero; } }
    }
    internal sealed class StartKeyGesture
    {
        int held;bool solo;
        internal bool Observe(int key,bool down,bool enabled,bool modifiers)
        {
            if(key==0x5b||key==0x5c){
                if(down){if(held==0){held=key;solo=enabled&&!modifiers;}else if(held!=key)solo=false;return false;}
                bool invoke=held==key&&solo&&enabled&&!modifiers;held=0;solo=false;return invoke;
            }
            if(down)solo=false;return false;
        }
    }
}
