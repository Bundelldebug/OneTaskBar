using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Interop;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

namespace OneTaskBar
{
    internal sealed class IslandSafeArea : IDisposable
    {
        internal const double Height=32;
        const uint Callback=0x8058;
        const string LeaseFile="island-safe-area.json";
        static IslandSafeArea current;
        sealed class Lease
        {
            public Lease(){}
            public int Owner;
            public long Started,Handle;
        }
        readonly HwndSource source;
        readonly Forms.Screen screen;
        readonly double scale;
        readonly Lease lease;
        bool registered,busy,disposed,positioned;
        internal Native.RectI Bounds {get;private set;}
        internal bool Active {get{return registered;}}
        internal string Monitor {get{return screen.DeviceName;}}
        internal double Scale {get{return scale;}}
        internal static int ReservedPixels(Drawing.Rectangle monitor)
        {
            return current!=null&&current.registered&&current.screen.Bounds==monitor?current.Bounds.Bottom-current.Bounds.Top:0;
        }
        internal static int BottomOffsetPixels(Drawing.Rectangle monitor)
        {
            return current!=null&&current.registered&&current.screen.Bounds==monitor?current.Bounds.Bottom-monitor.Top:0;
        }
        internal IslandSafeArea(Forms.Screen monitor,double dpi)
        {
            Recover();
            screen=monitor;scale=dpi;
            source=new HwndSource(new HwndSourceParameters("OneTaskBar.IslandSafeArea")
            {
                WindowStyle=unchecked((int)0x80000000),ExtendedWindowStyle=0x80|0x08000000,
                PositionX=screen.Bounds.Left,PositionY=screen.Bounds.Top,Width=1,Height=1
            });
            source.AddHook(Message);
            using(var process=Process.GetCurrentProcess())lease=new Lease {Owner=process.Id,Started=process.StartTime.ToUniversalTime().Ticks,Handle=source.Handle.ToInt64()};
        }
        Native.AppBarData Data()
        {
            return new Native.AppBarData {Size=(uint)Marshal.SizeOf(typeof(Native.AppBarData)),Window=source.Handle,Edge=1,Callback=Callback};
        }
        IntPtr Message(IntPtr window,int message,IntPtr wparam,IntPtr lparam,ref bool handled)
        {
            if(message==Callback&&wparam.ToInt32()==1)Update();
            return IntPtr.Zero;
        }
        internal void SetActive(bool value)
        {
            if(disposed)return;
            if(!value){Remove();return;}
            if(registered)return;
            if(!registered)
            {
                AppLog.Json(LeaseFile,lease);
                var data=Data();
                registered=Native.SHAppBarMessage(0,ref data)!=UIntPtr.Zero;
                if(!registered){ClearLease();throw new InvalidOperationException("Windows did not reserve the island safety area");}
                current=this;
                positioned=false;
            }
            Update();
        }
        void Update()
        {
            if(disposed||busy||!registered)return;
            busy=true;
            try
            {
                var monitor=screen.Bounds;
                var data=Data();
                data.Rect=new Native.RectI {Left=monitor.Left,Top=monitor.Top,Right=monitor.Right,Bottom=monitor.Bottom};
                Native.SHAppBarMessage(2,ref data);
                data.Rect.Bottom=data.Rect.Top+(int)Math.Ceiling(Height*scale);
                if(positioned&&data.Rect.Equals(Bounds))return;
                Native.SHAppBarMessage(3,ref data);
                Bounds=data.Rect;positioned=true;
            }
            finally{busy=false;}
        }
        internal Drawing.Rectangle PlacementArea()
        {
            var work=Native.WorkArea(screen);
            return Drawing.Rectangle.FromLTRB(Bounds.Left,Bounds.Top,Bounds.Right,work.Bottom);
        }
        void Remove()
        {
            if(!registered)return;
            registered=false;positioned=false;
            if(current==this)current=null;
            var data=Data();Native.SHAppBarMessage(1,ref data);
            ClearLease();
        }
        void ClearLease()
        {
            try
            {
                string file=Path.Combine(AppLog.Data,LeaseFile);
                if(!File.Exists(file))return;
                var saved=new JavaScriptSerializer().Deserialize<Lease>(File.ReadAllText(file));
                if(saved!=null&&saved.Owner==lease.Owner&&saved.Started==lease.Started&&saved.Handle==lease.Handle)File.Delete(file);
            }
            catch(Exception error){AppLog.Once("island-safety-lease",error.GetType().Name);}
        }
        internal static void Recover(string directory=null)
        {
            string file=Path.Combine(directory??AppLog.Data,LeaseFile);
            try
            {
                if(!File.Exists(file)||new FileInfo(file).Length>4096)return;
                var saved=new JavaScriptSerializer().Deserialize<Lease>(File.ReadAllText(file));
                if(saved==null||saved.Owner<=0||saved.Handle==0)return;
                try{using(var process=Process.GetProcessById(saved.Owner))if(process.StartTime.ToUniversalTime().Ticks==saved.Started&&!process.HasExited)return;}catch(ArgumentException){}
                var window=new IntPtr(saved.Handle);
                if(Native.IsWindow(window))return;
                var data=new Native.AppBarData {Size=(uint)Marshal.SizeOf(typeof(Native.AppBarData)),Window=window};
                Native.SHAppBarMessage(1,ref data);
                File.Delete(file);
            }
            catch(Exception error){AppLog.Once("island-safety-recovery",error.GetType().Name);}
        }
        public void Dispose()
        {
            if(disposed)return;
            Remove();disposed=true;source.RemoveHook(Message);source.Dispose();
        }
    }
}
