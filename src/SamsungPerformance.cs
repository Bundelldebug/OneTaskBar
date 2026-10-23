using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;
namespace OneTaskBar
{
    internal sealed class SamsungMode {internal int Id;internal string Name;}
    internal sealed class SamsungPerformanceState
    {
        internal bool Available,Stale;internal int Selected=-1;internal bool? MaximumFan;internal string Error;
        internal List<SamsungMode> Modes=new List<SamsungMode>();
    }
    internal static class SamsungPerformance
    {
        const string registry=@"SOFTWARE\Samsung\SamsungSettings\ModulePerformance";
        static readonly object cacheGate=new object(),requestGate=new object();static List<SamsungMode> previousModes;
        static DateTime checkedAt;static volatile SamsungDirectAccess access;
        internal static SamsungDirectAccess Access {get{return access;}}
        internal static string AccessMessage {get{return access==SamsungDirectAccess.Denied?"이 삼성 버전은 외부 앱의 직접 변경을 허용하지 않습니다. 삼성 설정에서 변경해 주세요.":access==SamsungDirectAccess.Unavailable?"삼성 성능 모드 서비스에 연결하지 못했습니다. 다시 검색하거나 삼성 설정에서 변경해 주세요.":access==SamsungDirectAccess.Unknown?"삼성 성능 모드 연결을 확인하는 중…":"삼성 성능 모드를 이 화면에서 변경할 수 있습니다.";}}
        internal static int Current(){try{using(var key=Registry.LocalMachine.OpenSubKey(registry))return key!=null?Convert.ToInt32(key.GetValue("Value",-1)):-1;}catch{return -1;}}
        internal static SamsungPerformanceState Read(bool installed)
        {
            if(!installed)return new SamsungPerformanceState();string modes=null;int selected=-1;bool supported=false;bool? fan=null;
            try{using(var key=Registry.LocalMachine.OpenSubKey(registry)){if(key!=null){supported=Convert.ToInt32(key.GetValue("Support",0))==1;modes=Convert.ToString(key.GetValue("ModeList",""));selected=Convert.ToInt32(key.GetValue("Value",-1));object raw=key.GetValue("FanMaxMode",null);int flag;if(raw!=null&&Int32.TryParse(Convert.ToString(raw),out flag)&&(flag==0||flag==1))fan=flag==1;}}}
            catch(Exception error){AppLog.Once("samsung-performance-read",error.GetType().Name);}
            lock(cacheGate){var state=Merge(supported,modes,selected,previousModes,fan);if(state.Available&&!state.Stale)previousModes=state.Modes.ToList();return state;}
        }
        internal static SamsungPerformanceState Merge(bool supported,string modes,int selected,List<SamsungMode> previous,bool? maximumFan=null)
        {
            var state=supported?Parse(modes,selected,maximumFan):new SamsungPerformanceState();
            if(state.Modes.Count==0&&previous!=null&&previous.Count>0){state.Modes=previous.ToList();state.Available=true;state.Stale=true;ApplyCurrent(state,selected,maximumFan);state.Error="삼성 모드 정보를 다시 확인하는 중입니다.";}return state;
        }
        internal static SamsungPerformanceState Parse(string modes,int selected,bool? maximumFan=null)
        {
            string[] supported;try{supported=new JavaScriptSerializer().Deserialize<string[]>(modes??"[]")??new string[0];}catch{supported=new string[0];}var state=new SamsungPerformanceState {Selected=selected};
            foreach(var mode in new[]{new SamsungMode {Id=0,Name="조용함"},new SamsungMode {Id=1,Name="저소음"},new SamsungMode {Id=2,Name="최적화"},new SamsungMode {Id=3,Name="고성능"}})if(supported.Contains(mode.Id.ToString()))state.Modes.Add(mode);
            state.Available=state.Modes.Count>0;ApplyCurrent(state,selected,maximumFan);return state;
        }
        // OEM value 6 is high performance with maximum fan, not a fifth power mode.
        static void ApplyCurrent(SamsungPerformanceState state,int value,bool? maximumFan){state.Selected=value==6?3:value;if(!state.Modes.Any(m=>m.Id==state.Selected))state.Selected=-1;state.MaximumFan=state.Selected==3?(value==6?(bool?)true:maximumFan):null;}
        internal static void Open(){Native.Open("samsungsettings15:-Go-PerformanceMode");}
        internal static SamsungDirectAccess Probe(bool force=false)
        {
            lock(requestGate){if(!force&&access!=SamsungDirectAccess.Unknown&&(DateTime.UtcNow-checkedAt).TotalMinutes<2)return access;checkedAt=DateTime.UtcNow;
                try{string value=Exchange(1,"Support","");access=value=="1"||String.Equals(value,"true",StringComparison.OrdinalIgnoreCase)?SamsungDirectAccess.Ready:SamsungDirectAccess.Unavailable;}
                catch(UnauthorizedAccessException){access=SamsungDirectAccess.Denied;}catch(Exception error){access=SamsungDirectAccess.Unavailable;AppLog.Once("samsung-performance-service",error.GetType().Name);}return access;}
        }
        // Use only the OEM SDK's ModulePerformance/Value request. In particular a
        // client-signature rejection never falls back to launching/automating its UI.
        internal static SamsungPerformanceState Set(AppEntry app,int value)
        {
            var before=Read(app!=null);if(!before.Available||!before.Modes.Any(m=>m.Id==value))throw new InvalidOperationException("이 기기에서 지원하지 않는 삼성 성능 모드입니다.");if(before.Selected==value)return before;
            if(Probe()!=SamsungDirectAccess.Ready)throw new InvalidOperationException(AccessMessage);
            lock(requestGate)Exchange(2,"Value",value.ToString(System.Globalization.CultureInfo.InvariantCulture));var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<2000){int current=Current();if(current==value||value==3&&current==6)return Read(true);Thread.Sleep(100);}
            throw new InvalidOperationException("삼성 서비스가 선택한 모드의 적용을 확인하지 못했습니다. 현재 모드를 유지합니다.");
        }
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint id);
        internal static string Response(string json,int action,string key)
        {
            if(String.IsNullOrWhiteSpace(json)||json.Length>8192)throw new InvalidDataException("삼성 서비스의 응답을 확인하지 못했습니다.");
            if(json.IndexOf("No Verify Client",StringComparison.OrdinalIgnoreCase)>=0||json.IndexOf("Digital Sign",StringComparison.OrdinalIgnoreCase)>=0)throw new UnauthorizedAccessException("삼성 서비스가 외부 앱의 직접 변경을 허용하지 않습니다.");
            var result=new JavaScriptSerializer().Deserialize<SamsungCommand>(json);if(result==null||result.ModuleName!="ModulePerformance"||result.Action!=action||result.Key!=key)throw new InvalidDataException("삼성 서비스의 응답 형식이 일치하지 않습니다.");return result.Output;
        }
        static string Exchange(int action,string key,string input)
        {
            if(!(action==1&&key=="Support"&&input==""||action==2&&key=="Value"&&(input=="0"||input=="1"||input=="2"||input=="3")))throw new ArgumentException("Unsupported OEM request");
            using(var pipe=new NamedPipeClientStream(".","{9187E7E8-51A8-473D-A0D5-ED2B2E7D0770}_SamsungSystemSupportEngine",PipeDirection.InOut,PipeOptions.Asynchronous)){
                pipe.Connect(700);pipe.ReadMode=PipeTransmissionMode.Message;uint pid;if(!GetNamedPipeServerProcessId(pipe.SafePipeHandle,out pid))throw new IOException("삼성 서비스를 확인하지 못했습니다.");using(var server=Process.GetProcessById((int)pid))if(!server.ProcessName.Equals("SamsungSystemSupportEngine",StringComparison.OrdinalIgnoreCase))throw new IOException("삼성 서비스가 아닙니다.");
                var request=new SamsungCommand {ModuleName="ModulePerformance",Action=action,Key=key,Input=input,Output=""};byte[] bytes=Encoding.Unicode.GetBytes(new JavaScriptSerializer().Serialize(request));var writing=pipe.WriteAsync(bytes,0,bytes.Length);if(!writing.Wait(1000))throw new TimeoutException("삼성 서비스 요청 시간이 초과됐습니다.");writing.GetAwaiter().GetResult();
                using(var output=new MemoryStream()){var timer=Stopwatch.StartNew();var buffer=new byte[2048];do{var reading=pipe.ReadAsync(buffer,0,buffer.Length);int remaining=Math.Max(1,2500-(int)timer.ElapsedMilliseconds);if(!reading.Wait(remaining))throw new TimeoutException("삼성 서비스 응답 시간이 초과됐습니다.");int length=reading.GetAwaiter().GetResult();if(length<=0)throw new EndOfStreamException();output.Write(buffer,0,length);if(output.Length>16384)throw new InvalidDataException("삼성 서비스 응답이 너무 큽니다.");}while(!pipe.IsMessageComplete);return Response(Encoding.Unicode.GetString(output.ToArray()).TrimEnd('\0'),action,key);}
            }
        }
    }
    internal enum SamsungDirectAccess {Unknown,Ready,Denied,Unavailable}
    internal sealed class SamsungCommand {public string ModuleName {get;set;}public int Action {get;set;}public string Key {get;set;}public string Input {get;set;}public string Output {get;set;}}
}
