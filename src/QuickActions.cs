using System;
using System.Linq;
using System.Runtime.InteropServices;
namespace OneTaskBar
{
    internal sealed class QuickActions : IDisposable
    {
        internal readonly DoNotDisturb Disturb=new DoNotDisturb();
        internal static readonly Guid EfficientMode=new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
        [DllImport("kernel32.dll")]static extern uint SetThreadExecutionState(uint flags);
        internal bool Awake {get;private set;}uint previousExecution;
        Guid? previousMode;PerformanceMode power=new PerformanceMode();DateTime powerUntil,startupUntil;bool startup;
        internal PerformanceMode Power(bool force=false)
        {
            if(force||DateTime.UtcNow>=powerUntil){try{power=Native.ReadPerformanceMode();}catch{power=new PerformanceMode();}powerUntil=DateTime.UtcNow.AddSeconds(1);}return power;
        }
        internal bool Startup {get{if(DateTime.UtcNow>=startupUntil){startup=StartupPreference.Enabled;startupUntil=DateTime.UtcNow.AddSeconds(1);}return startup;}}
        internal void ToggleStartup(){bool target=!Startup;StartupPreference.Set(target);startupUntil=DateTime.MinValue;if(Startup!=target)throw new InvalidOperationException("자동 실행을 적용하지 못했습니다.");}
        // The request belongs to this UI thread; exit/crash releases it automatically.
        // https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate
        internal void SetAwake(bool value)
        {
            if(value==Awake)return;uint result=SetThreadExecutionState(value?0x80000003:previousExecution|0x80000000);if(result==0)throw new InvalidOperationException("화면 켜짐 유지 상태를 변경하지 못했습니다.");if(value)previousExecution=result;Awake=value;
        }
        internal void ToggleEfficiency()
        {
            var before=Power(true);if(!before.Available)throw new InvalidOperationException("이 PC는 Windows 성능 모드 제어를 지원하지 않습니다.");bool enabled=before.Selected==EfficientMode;
            if(!enabled)previousMode=before.Selected;Guid target=enabled?(previousMode??Guid.Empty):EfficientMode;
            if(!Native.PerformanceModes().Any(m=>m.Id==target))target=Guid.Empty;Native.SetPerformanceMode(target);var after=Power(true);if(!after.Available||after.Selected!=target)throw new InvalidOperationException("Windows가 전력 절약 모드를 적용하지 못했습니다.");if(enabled)previousMode=null;
        }
        public void Dispose(){Disturb.Dispose();if(Awake)try{SetAwake(false);}catch(Exception e){AppLog.Write("awake-release",e.Message);}}
    }
}
