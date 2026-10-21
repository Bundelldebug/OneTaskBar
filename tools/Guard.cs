using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

internal static class Guard
{
    delegate IntPtr Hook(int code,IntPtr message,IntPtr info);
    [StructLayout(LayoutKind.Sequential)]struct Message {internal IntPtr Window;internal uint Id;internal UIntPtr W;internal IntPtr L;internal uint Time;internal int X,Y;}
    [DllImport("user32.dll")]static extern IntPtr SetWindowsHookEx(int type,Hook hook,IntPtr module,uint thread);
    [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr info);
    [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]static extern bool PeekMessage(out Message message,IntPtr hwnd,uint min,uint max,uint remove);
    [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
    static bool enabled=true,exit;static IntPtr handle;static readonly Hook callback=Keyboard;
    static bool Held(int key){return (GetAsyncKeyState(key)&0x8000)!=0;}
    static IntPtr Keyboard(int code,IntPtr message,IntPtr info)
    {if(code>=0&&enabled&&(message==new IntPtr(0x100)||message==new IntPtr(0x104))&&(Marshal.ReadInt32(info)==0x31||Marshal.ReadInt32(info)==0x61)&&Held(0x11)&&Held(0x10)&&Held(0x09)){exit=true;return new IntPtr(1);}return CallNextHookEx(handle,code,message,info);}
    static string Field(string json,string name){var match=Regex.Match(json,"\""+name+"\"\\s*:\\s*(?:\"([^\"]*)\"|([0-9]+))");return match.Success?(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value):null;}
    [STAThread]static int Main(string[] args)
    {
        int pid;long started;if(args.Length!=4||!Int32.TryParse(args[0],out pid)||!Int64.TryParse(args[1],out started))return 2;
        string executable=Path.GetFullPath(args[2]),data=Path.GetFullPath(args[3]);
        if(!String.Equals(Path.GetDirectoryName(executable),Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),StringComparison.OrdinalIgnoreCase)||!String.Equals(Path.GetFileName(executable),"OneTaskBar.exe",StringComparison.OrdinalIgnoreCase))return 2;
        string session=null;DateTime check=DateTime.MinValue;
        try {using(var process=Process.GetProcessById(pid)) {
            if(process.StartTime.ToUniversalTime().Ticks!=started||!String.Equals(process.MainModule.FileName,executable,StringComparison.OrdinalIgnoreCase))return 2;
            handle=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);if(handle==IntPtr.Zero)return 3;
            Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"guard-"+pid+".ready"),"ready");
            while(!process.WaitForExit(15)) {
                Message message;while(PeekMessage(out message,IntPtr.Zero,0,0,1)){}
                if(DateTime.UtcNow>=check){check=DateTime.UtcNow.AddMilliseconds(400);try{string settings=File.ReadAllText(Path.Combine(data,"settings.json"));enabled=!Regex.IsMatch(settings,"\"EmergencyExit\"\\s*:\\s*false");}catch{}
                    try{string lease=File.ReadAllText(Path.Combine(data,"desktop-lease.json"));if(Field(lease,"OwnerPid")==pid.ToString()&&Field(lease,"OwnerStarted")==started.ToString())session=Field(lease,"Session");}catch{}
                }
                if(exit&&enabled){if(!process.HasExited&&process.StartTime.ToUniversalTime().Ticks==started)process.Kill();process.WaitForExit(3000);break;}
            }
        }}catch(ArgumentException){}catch(Exception error){try{Directory.CreateDirectory(data);File.AppendAllText(Path.Combine(data,"guard.log"),error.Message+Environment.NewLine);}catch{}}
        finally{if(handle!=IntPtr.Zero)UnhookWindowsHookEx(handle);try{File.Delete(Path.Combine(data,"guard-"+pid+".ready"));}catch{}}
        try{string lease=File.ReadAllText(Path.Combine(data,"desktop-lease.json"));if(Field(lease,"OwnerPid")==pid.ToString()&&Field(lease,"OwnerStarted")==started.ToString())session=Field(lease,"Session");else session=null;}catch{}
        if(!String.IsNullOrEmpty(session)&&Regex.IsMatch(session,"^[a-fA-F0-9]{32}$"))try{var start=new ProcessStartInfo(executable,"--recover --session "+session){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};start.EnvironmentVariables["ONE_TASKBAR_DATA"]=data;using(var recover=Process.Start(start))recover.WaitForExit(8000);}catch{}
        return 0;
    }
}
