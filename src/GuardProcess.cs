using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace OneTaskBar
{
    // The watchdog has no UI, plug-ins, or notification APIs. Keep it out of the
    // desktop package runtime. Images under a registered external package's
    // installation directory can inherit that identity even with breakaway set.
    internal static class GuardProcess
    {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct StartupInfo {public int Size;public string Reserved,Desktop,Title;public int X,Y,Width,Height,CharsX,CharsY,Fill,Flags;public short Show,ReservedSize;public IntPtr ReservedData,Input,Output,Error;}
        [StructLayout(LayoutKind.Sequential)]struct StartupInfoEx {public StartupInfo Startup;public IntPtr Attributes;}
        [StructLayout(LayoutKind.Sequential)]struct ProcessInfo {public IntPtr Process,Thread;public uint Pid,Tid;}
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref IntPtr size);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returned);
        [DllImport("kernel32.dll")]static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string application,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfoEx startup,out ProcessInfo process);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll",SetLastError=true)]static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetExitCodeProcess(IntPtr process,out uint code);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessTimes(IntPtr process,out long created,out long exited,out long kernel,out long user);
        internal sealed class Child : IDisposable
        {
            IntPtr handle;internal readonly int Id;
            internal Child(IntPtr handle,uint pid){this.handle=handle;Id=(int)pid;}
            internal bool HasExited {get{return WaitForExit(0);}}
            internal bool WaitForExit(int milliseconds){uint result=WaitForSingleObject(handle,unchecked((uint)milliseconds));if(result==0)return true;if(result==258)return false;throw new Win32Exception(Marshal.GetLastWin32Error());}
            internal int ExitCode {get{if(!HasExited)throw new InvalidOperationException("Helper has not exited");uint code;if(!GetExitCodeProcess(handle,out code))throw new Win32Exception(Marshal.GetLastWin32Error());return unchecked((int)code);}}
            internal TimeSpan TotalProcessorTime {get{long created,exited,kernel,user;if(!GetProcessTimes(handle,out created,out exited,out kernel,out user))throw new Win32Exception(Marshal.GetLastWin32Error());return TimeSpan.FromTicks(kernel+user);}}
            public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
        }
        internal static Child Start(Process owner,string executable,string data)
        {
            string source=Path.Combine(Path.GetDirectoryName(executable),"OneTaskBar.Guard.exe");
            string hash;using(var sha=SHA256.Create())using(var stream=File.OpenRead(source))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            string helper=Path.Combine(data,"Recovery",hash.Substring(0,16),"OneTaskBar.Guard.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(helper));if(!File.Exists(helper))File.Copy(source,helper);
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(helper))if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")!=hash)throw new IOException("Recovery helper does not match the installed build");
            string arguments=owner.Id+" "+owner.StartTime.ToUniversalTime().Ticks+" \""+executable+"\" \""+data+"\"";
            IntPtr size=IntPtr.Zero,list=IntPtr.Zero,policy=IntPtr.Zero;bool initialized=false;
            try {
                InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);
                if(size==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
                list=Marshal.AllocHGlobal(size);
                if(!InitializeProcThreadAttributeList(list,1,0,ref size))throw new Win32Exception(Marshal.GetLastWin32Error());initialized=true;
                policy=Marshal.AllocHGlobal(4);Marshal.WriteInt32(policy,1);
                if(!UpdateProcThreadAttribute(list,0,new IntPtr(0x20012),policy,new IntPtr(4),IntPtr.Zero,IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());
                var startup=new StartupInfoEx {Startup=new StartupInfo {Size=Marshal.SizeOf(typeof(StartupInfoEx)),Flags=1,Show=0},Attributes=list};ProcessInfo process;
                if(!CreateProcess(helper,new StringBuilder("\""+helper+"\" "+arguments),IntPtr.Zero,IntPtr.Zero,false,0x08080000,IntPtr.Zero,Path.GetDirectoryName(helper),ref startup,out process))throw new Win32Exception(Marshal.GetLastWin32Error());
                CloseHandle(process.Thread);return new Child(process.Process,process.Pid);
            } finally {
                if(initialized)DeleteProcThreadAttributeList(list);
                if(list!=IntPtr.Zero)Marshal.FreeHGlobal(list);if(policy!=IntPtr.Zero)Marshal.FreeHGlobal(policy);
            }
        }
    }
}
