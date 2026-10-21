using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace OneTaskBar
{
    internal sealed class DisturbState
    {
        internal bool Available,Enabled,Automatic;internal string Selected,Active,Off,Error;
    }
    // Shell COM declarations based on Rafael Rivera's MIT-licensed QuietHours IDL.
    // https://gist.github.com/riverar/085d98ffb1343e92225a10817109b2e3
    internal sealed class DoNotDisturb : IDisposable
    {
        [ComImport,Guid("F53321FA-34F8-4B7F-B9A3-361877CB94CF")]class QuietHoursSettings {}
        [ComImport,Guid("6BFF4732-81EC-4FFB-AE67-B6C1BC29631F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IQuietHoursSettings
        {
            void GetSelected([MarshalAs(UnmanagedType.LPWStr)]out string profile);
            void SetSelected([MarshalAs(UnmanagedType.LPWStr)]string profile);
            void GetProfile([MarshalAs(UnmanagedType.LPWStr)]string profile,out IntPtr result);
            void GetAllProfileData(out uint count,IntPtr data);
            void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string profile,[MarshalAs(UnmanagedType.LPWStr)]out string name);
            void GetMoments(out IQuietMomentsManager manager);
            void GetOff([MarshalAs(UnmanagedType.LPWStr)]out string profile);
            void GetMomentProfile([MarshalAs(UnmanagedType.LPWStr)]out string profile);
            void SetMomentProfile([MarshalAs(UnmanagedType.LPWStr)]string profile);
            void GetActive([MarshalAs(UnmanagedType.LPWStr)]out string profile);
        }
        [ComImport,Guid("B0217783-87B7-422C-B902-5C148C14F150"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IQuietMomentsManager
        {
            void GetAll(out uint count,out IntPtr modes);
            void GetMoment(uint id,out IntPtr moment);
            void TurnOffCurrent();
        }
        readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);volatile DisturbState state=new DisturbState();DateTime expires;bool disposed;int reading;string enabledProfile="Microsoft.QuietHoursProfile.PriorityOnly";
        internal DisturbState State {get {if(!disposed&&DateTime.UtcNow>=expires)Poll();return state;}}
        internal async Task<DisturbState> RefreshAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);try {var next=await Task.Run(()=>ReadNative(null)).ConfigureAwait(false);if(!disposed){state=next;expires=DateTime.UtcNow.AddSeconds(next.Available?1:10);}return next;}finally{gate.Release();}
        }
        async void Poll(){if(Interlocked.CompareExchange(ref reading,1,0)!=0)return;try{await RefreshAsync();}catch(Exception error){AppLog.Once("dnd-read",error.Message);}finally{Interlocked.Exchange(ref reading,0);}}
        internal async Task Toggle()
        {
            await gate.WaitAsync().ConfigureAwait(false);try {var next=await Task.Run(()=>{var before=ReadNative(null);if(before.Available&&before.Enabled)enabledProfile=before.Active;return ReadNative(true,enabledProfile);}).ConfigureAwait(false);if(!disposed){state=next;expires=DateTime.UtcNow.AddSeconds(1);}if(!next.Available)throw new InvalidOperationException(next.Error??"Windows 방해 금지 제어를 사용할 수 없습니다.");}finally{gate.Release();}
        }
        internal async Task Restore(string selected)
        {
            await gate.WaitAsync().ConfigureAwait(false);try {await Task.Run(()=>{IQuietHoursSettings settings=null;try{settings=(IQuietHoursSettings)new QuietHoursSettings();settings.SetSelected(selected);}finally{if(settings!=null)Marshal.ReleaseComObject(settings);}}).ConfigureAwait(false);}finally{gate.Release();}await RefreshAsync();
        }
        static bool Off(string profile,string off){return String.Equals(profile,off,StringComparison.OrdinalIgnoreCase)||profile!=null&&profile.EndsWith(".Unrestricted",StringComparison.OrdinalIgnoreCase);}
        static DisturbState Read(IQuietHoursSettings settings)
        {
            string selected,active,off;settings.GetSelected(out selected);settings.GetOff(out off);settings.GetActive(out active);if(String.IsNullOrEmpty(active)||String.IsNullOrEmpty(off))throw new InvalidOperationException("Windows 방해 금지 상태를 읽지 못했습니다.");return new DisturbState {Available=true,Selected=selected,Active=active,Off=off,Enabled=!Off(active,off),Automatic=!Off(active,off)&&Off(selected,off)};
        }
        static DisturbState ReadNative(bool? toggle,string enabledProfile="Microsoft.QuietHoursProfile.PriorityOnly")
        {
            IQuietHoursSettings settings=null;try {
                settings=(IQuietHoursSettings)new QuietHoursSettings();var before=Read(settings);if(!toggle.HasValue)return before;bool enabled=!before.Enabled;settings.SetSelected(enabled?enabledProfile:before.Off);
                if(!enabled){var current=Read(settings);if(current.Enabled){IQuietMomentsManager moments=null;try{settings.GetMoments(out moments);moments.TurnOffCurrent();}finally{if(moments!=null)Marshal.ReleaseComObject(moments);}}}
                var after=Read(settings);if(after.Enabled!=enabled)throw new InvalidOperationException("Windows 자동 규칙 또는 정책이 방해 금지 상태를 유지하고 있습니다.");return after;
            }catch(Exception error){AppLog.Once("dnd-control",error.Message);return new DisturbState {Error=error.Message};}finally{if(settings!=null)Marshal.ReleaseComObject(settings);}
        }
        public void Dispose(){disposed=true;}
    }
}
