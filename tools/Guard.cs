using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

internal static class Guard
{
    [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll",SetLastError=true)]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll")]static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")]static extern bool TerminateProcess(IntPtr process,uint code);
    [DllImport("kernel32.dll")]static extern IntPtr GetCurrentThread();
    [StructLayout(LayoutKind.Sequential)]struct Throttling {public uint Version,ControlMask,StateMask;}
    [DllImport("kernel32.dll")]static extern bool SetThreadInformation(IntPtr thread,int kind,ref Throttling state,uint size);
    static bool Held(int key){return (GetAsyncKeyState(key)&0x8000)!=0;}
    static bool Enabled(string data){try{return !Regex.IsMatch(File.ReadAllText(Path.Combine(data,"settings.json")),"\"EmergencyExit\"\\s*:\\s*false");}catch{return true;}}
    static string Field(string json,string name){var match=Regex.Match(json,"\""+name+"\"\\s*:\\s*(?:\"([^\"]*)\"|([0-9]+))");return match.Success?(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value):null;}
    [MTAThread]static int Main(string[] args){int code=Run(args);TerminateProcess(GetCurrentProcess(),unchecked((uint)code));return code;}
    static int Run(string[] args)
    {
        int pid;long started;if(args.Length!=4||!Int32.TryParse(args[0],out pid)||!Int64.TryParse(args[1],out started))return 2;
        string executable=Path.GetFullPath(args[2]),data=Path.GetFullPath(args[3]);
        if(!String.Equals(Path.GetDirectoryName(executable),Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),StringComparison.OrdinalIgnoreCase)||!String.Equals(Path.GetFileName(executable),"OneTaskBar.exe",StringComparison.OrdinalIgnoreCase))return 2;
        bool created;using(var singleton=new Mutex(true,@"Local\OneTaskBar.Guard."+pid+"."+started,out created)){if(!created)return 4;
        try{using(var process=Process.GetProcessById(pid)){
            if(process.StartTime.ToUniversalTime().Ticks!=started||!String.Equals(process.MainModule.FileName,executable,StringComparison.OrdinalIgnoreCase))return 2;
            try{var eco=new Throttling {Version=1,ControlMask=1,StateMask=1};SetThreadInformation(GetCurrentThread(),3,ref eco,12);}catch(EntryPointNotFoundException){}
            var owner=process.Handle;uint initial=WaitForSingleObject(owner,0);if(initial!=258)return initial==0?0:3;
            Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"guard-"+pid+".ready"),"ready");bool latched=false;
            // A kernel wait sleeps between checks. No hook, message pump, file watcher,
            // or package/COM message traffic can keep this helper busy.
            while(true){
                uint wait=WaitForSingleObject(owner,20);if(wait==0)break;if(wait!=258)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                bool chord=Held(0x11)&&Held(0x10)&&Held(0x09)&&(Held(0x31)||Held(0x61));
                if(chord&&!latched&&Enabled(data)){if(process.StartTime.ToUniversalTime().Ticks==started)process.Kill();WaitForSingleObject(owner,3000);break;}latched=chord;
            }
        }}catch(ArgumentException){}catch(Exception error){try{Directory.CreateDirectory(data);File.AppendAllText(Path.Combine(data,"guard.log"),error.Message+Environment.NewLine);}catch{}}
        finally{try{File.Delete(Path.Combine(data,"guard-"+pid+".ready"));}catch{}}
        string session=null;try{string lease=File.ReadAllText(Path.Combine(data,"desktop-lease.json"));if(Field(lease,"OwnerPid")==pid.ToString()&&Field(lease,"OwnerStarted")==started.ToString()&&Regex.IsMatch(lease,"\"Active\"\\s*:\\s*true"))session=Field(lease,"Session");}catch{}
        if(!String.IsNullOrEmpty(session)&&Regex.IsMatch(session,"^[a-fA-F0-9]{32}$"))try{var start=new ProcessStartInfo(executable,"--recover --session "+session){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};start.EnvironmentVariables["ONE_TASKBAR_DATA"]=data;using(var recover=Process.Start(start))WaitForSingleObject(recover.Handle,8000);}catch{}
        return 0;}
    }
}
