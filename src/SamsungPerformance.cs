using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using Microsoft.Win32;
namespace OneTaskBar
{
    internal sealed class SamsungMode {internal int Id;internal string Name;}
    internal sealed class SamsungPerformanceState
    {
        internal bool Available;internal int Selected=-1;internal string Error;
        internal List<SamsungMode> Modes=new List<SamsungMode>();
    }
    internal static class SamsungPerformance
    {
        const string registry=@"SOFTWARE\Samsung\SamsungSettings\ModulePerformance";
        internal static int Current(){try{using(var key=Registry.LocalMachine.OpenSubKey(registry))return key!=null?Convert.ToInt32(key.GetValue("Value",-1)):-1;}catch{return -1;}}
        internal static SamsungPerformanceState Read(bool installed)
        {
            var state=new SamsungPerformanceState();if(!installed)return state;
            try{using(var key=Registry.LocalMachine.OpenSubKey(registry)){if(key==null||Convert.ToInt32(key.GetValue("Support",0))!=1)return state;return Parse(Convert.ToString(key.GetValue("ModeList","")),Convert.ToInt32(key.GetValue("Value",-1)));}}
            catch(Exception error){state.Error=error.GetType().Name;return state;}
        }
        internal static SamsungPerformanceState Parse(string modes,int selected)
        {
            var supported=new JavaScriptSerializer().Deserialize<string[]>(modes)??new string[0];var state=new SamsungPerformanceState {Selected=selected};
            foreach(var mode in new[]{new SamsungMode {Id=2,Name="최적화"},new SamsungMode {Id=1,Name="저소음"},new SamsungMode {Id=0,Name="조용함"},new SamsungMode {Id=3,Name="고성능"}})if(supported.Contains(mode.Id.ToString()))state.Modes.Add(mode);
            state.Available=state.Modes.Any(m=>m.Id==selected);return state;
        }
        internal static void Open(){Native.Open("samsungsettings15:-Go-PerformanceMode");}
        // Selection goes through the signed OEM app's existing controls. Never write
        // the mirrored registry value or send untrusted commands to the firmware service.
        internal static SamsungPerformanceState Set(AppEntry app,int value)
        {
            var before=Read(app!=null);if(!before.Available||!before.Modes.Any(m=>m.Id==value))throw new InvalidOperationException("이 기기에서 지원하지 않는 삼성 성능 모드입니다.");if(before.Selected==value)return before;
            Open();var watch=Stopwatch.StartNew();bool selected=false;
            while(watch.ElapsedMilliseconds<4500){
                if(Current()==value)return Read(true);
                if(!selected){IntPtr window=SettingsWindow();if(window!=IntPtr.Zero)selected=Select(window,value);}
                Thread.Sleep(220);
            }
            throw new InvalidOperationException("삼성 설정의 성능 모드 화면을 열었습니다. 이 버전은 자동 선택을 지원하지 않아 삼성 설정에서 직접 선택해 주세요.");
        }
        static IntPtr SettingsWindow()
        {
            IntPtr result=IntPtr.Zero;Native.EnumWindows((handle,state)=> {if(!Native.IsWindowVisible(handle))return true;uint id;Native.GetWindowThreadProcessId(handle,out id);try{using(var process=Process.GetProcessById((int)id))if(process.ProcessName.Equals("SamsungSettingsHost",StringComparison.OrdinalIgnoreCase)||process.ProcessName.Equals("SamsungSettings",StringComparison.OrdinalIgnoreCase)){result=handle;return false;}}catch{}return true;},IntPtr.Zero);return result;
        }
        static bool Select(IntPtr window,int value)
        {
            string[] names=value==2?new[]{"최적화","Optimized","Optimised"}:value==1?new[]{"저소음","Silent"}:value==0?new[]{"조용함","Quiet","No fan"}:new[]{"고성능","High performance"};
            try{var root=AutomationElement.FromHandle(window);if(root==null)return false;var radios=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.RadioButton));foreach(AutomationElement radio in radios){string label=radio.Current.Name??"";if(!radio.Current.IsEnabled||!names.Any(n=>label.Equals(n,StringComparison.OrdinalIgnoreCase)||label.StartsWith(n+"\n",StringComparison.OrdinalIgnoreCase)))continue;object pattern;if(radio.TryGetCurrentPattern(SelectionItemPattern.Pattern,out pattern)){((SelectionItemPattern)pattern).Select();return true;}if(radio.TryGetCurrentPattern(InvokePattern.Pattern,out pattern)){((InvokePattern)pattern).Invoke();return true;}}}catch(Exception error){AppLog.Once("samsung-performance-select",error.GetType().Name);}return false;
        }
    }
}
