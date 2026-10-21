using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

internal static class Guard
{
    [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll",SetLastError=true)]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll")]static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")]static extern bool TerminateProcess(IntPtr process,uint code);
    [DllImport("kernel32.dll")]static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string module);
    delegate IntPtr KeyboardHook(int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int kind,KeyboardHook callback,IntPtr module,uint thread);
    [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)]struct Point {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]struct Message {public IntPtr Window;public uint Id;public UIntPtr WParam;public IntPtr LParam;public uint Time;public Point Position;public uint Private;}
    [DllImport("user32.dll")]static extern bool PeekMessage(out Message message,IntPtr window,uint first,uint last,uint flags);
    [DllImport("user32.dll")]static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")]static extern IntPtr DispatchMessage(ref Message message);
    [DllImport("user32.dll")]static extern bool PostThreadMessage(uint thread,uint message,UIntPtr wp,IntPtr lp);
    [DllImport("user32.dll",SetLastError=true)]static extern uint MsgWaitForMultipleObjectsEx(uint count,IntPtr[] handles,uint timeout,uint wakeMask,uint flags);
    [StructLayout(LayoutKind.Sequential)]struct Throttling {public uint Version,ControlMask,StateMask;}
    [DllImport("kernel32.dll")]static extern bool SetThreadInformation(IntPtr thread,int kind,ref Throttling state,uint size);
    [DllImport("kernel32.dll")]static extern bool SetProcessInformation(IntPtr process,int kind,ref Throttling state,uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern int GetCurrentPackageFullName(ref uint length,StringBuilder name);
    static string Hash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream));}
    static bool Held(int key){return (GetAsyncKeyState(key)&0x8000)!=0;}
    static bool Down(int current,int key){return current==key||Held(key);}
    static bool Enabled(string data){try{return !Regex.IsMatch(File.ReadAllText(Path.Combine(data,"settings.json")),"\"EmergencyExit\"\\s*:\\s*false");}catch{return true;}}
    static string Field(string json,string name){var match=Regex.Match(json,"\""+name+"\"\\s*:\\s*(?:\"([^\"]*)\"|([0-9]+))");return match.Success?(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value):null;}
    [MTAThread]static int Main(string[] args){int code=Run(args);TerminateProcess(GetCurrentProcess(),unchecked((uint)code));return code;}
    static int Run(string[] args)
    {
        int pid;long started;if(args.Length!=4||!Int32.TryParse(args[0],out pid)||!Int64.TryParse(args[1],out started))return 2;
        string executable=Path.GetFullPath(args[2]),data=Path.GetFullPath(args[3]);
        if(!String.Equals(Path.GetFileName(executable),"OneTaskBar.exe",StringComparison.OrdinalIgnoreCase))return 2;
        string installedHelper=Path.Combine(Path.GetDirectoryName(executable),"OneTaskBar.Guard.exe");
        try{if(Hash(installedHelper)!=Hash(Assembly.GetExecutingAssembly().Location))return 2;}catch{return 2;}
        bool created;using(var singleton=new Mutex(true,@"Local\OneTaskBar.Guard."+pid+"."+started,out created)){if(!created)return 4;
        try{using(var process=Process.GetProcessById(pid)){
            if(process.StartTime.ToUniversalTime().Ticks!=started||!String.Equals(process.MainModule.FileName,executable,StringComparison.OrdinalIgnoreCase))return 2;
            try{var eco=new Throttling {Version=1,ControlMask=1,StateMask=1};SetProcessInformation(GetCurrentProcess(),4,ref eco,12);SetThreadInformation(GetCurrentThread(),3,ref eco,12);}catch(EntryPointNotFoundException){}
            var owner=process.Handle;uint initial=WaitForSingleObject(owner,0);if(initial!=258)return initial==0?0:3;
            Directory.CreateDirectory(data);uint thread=GetCurrentThreadId();bool latched=false;Message message;PeekMessage(out message,IntPtr.Zero,0,0,0);
            KeyboardHook callback=(code,eventId,input)=>{
                if(code>=0){int kind=eventId.ToInt32(),key=Marshal.ReadInt32(input);bool down=kind==0x100||kind==0x104;
                    if(kind==0x101||kind==0x105)latched=false;
                    if(down){bool chord=(Down(key,0x11)||key==0xA2||key==0xA3)&&(Down(key,0x10)||key==0xA0||key==0xA1)&&Down(key,0x09)&&(Down(key,0x31)||Down(key,0x61));
                        if(chord&&!latched){latched=true;PostThreadMessage(thread,0x8001,UIntPtr.Zero,IntPtr.Zero);}}
                }
                return CallNextHookEx(IntPtr.Zero,code,eventId,input);
            };
            IntPtr hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);if(hook==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try {
                uint packageLength=0;bool packaged=GetCurrentPackageFullName(ref packageLength,null)!=15700;
                File.WriteAllText(Path.Combine(data,"guard-"+pid+".ready"),"event-driven|"+started+"|packaged="+packaged);bool done=false;var handles=new[]{owner};
                // Input and owner exit wake this thread. An idle helper has no polling timer.
                while(!done){uint wait=MsgWaitForMultipleObjectsEx(1,handles,UInt32.MaxValue,0x4FF,4);if(wait==0)break;if(wait!=1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    while(PeekMessage(out message,IntPtr.Zero,0,0,1)){
                        if(message.Id==0x12){done=true;break;}
                        if(message.Id==0x8001&&Enabled(data)){if(process.StartTime.ToUniversalTime().Ticks==started)process.Kill();WaitForSingleObject(owner,3000);done=true;break;}
                        TranslateMessage(ref message);DispatchMessage(ref message);
                    }
                }
            }finally{UnhookWindowsHookEx(hook);GC.KeepAlive(callback);}
        }}catch(ArgumentException){}catch(Exception error){try{Directory.CreateDirectory(data);File.AppendAllText(Path.Combine(data,"guard.log"),error.Message+Environment.NewLine);}catch{}}
        finally{try{File.Delete(Path.Combine(data,"guard-"+pid+".ready"));}catch{}}
        string session=null;try{string lease=File.ReadAllText(Path.Combine(data,"desktop-lease.json"));if(Field(lease,"OwnerPid")==pid.ToString()&&Field(lease,"OwnerStarted")==started.ToString()&&Regex.IsMatch(lease,"\"Active\"\\s*:\\s*true"))session=Field(lease,"Session");}catch{}
        if(!String.IsNullOrEmpty(session)&&Regex.IsMatch(session,"^[a-fA-F0-9]{32}$"))try{var start=new ProcessStartInfo(executable,"--recover --session "+session){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};start.EnvironmentVariables["ONE_TASKBAR_DATA"]=data;using(var recover=Process.Start(start))WaitForSingleObject(recover.Handle,8000);}catch{}
        return 0;}
    }
}
