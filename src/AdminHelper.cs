using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OneTaskBar
{
    // Windows keeps a normal-privilege app from switching to, restoring or watching the keys of an administrator window,
    // and it refuses notification access to an administrator app. So One TaskBar itself stays normal and a small
    // administrator helper (started by its own sign-in task, no prompt after the first consent) does those two jobs.
    internal static class AdminPipes
    {
        internal static string Sid {get{using(var identity=WindowsIdentity.GetCurrent())return identity.User.Value;}}
        internal static string Command {get{return "OneTaskBar.Admin."+Sid+".command";}}
        internal static string Keys {get{return "OneTaskBar.Admin."+Sid+".keys";}}
        // Only the signed-in user may talk to either end.
        internal static PipeSecurity Security()
        {
            var security=new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(Sid),PipeAccessRights.FullControl,AccessControlType.Allow));
            return security;
        }
    }
    internal static class ProcessElevation
    {
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint process);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool GetTokenInformation(IntPtr token,int information,out int value,int length,out int returned);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        // A process this user cannot even open is treated as elevated: that is what Windows answers for one.
        internal static bool IsElevated(uint process)
        {
            IntPtr handle=OpenProcess(0x1000,false,process);
            if(handle==IntPtr.Zero)return Marshal.GetLastWin32Error()==5;
            try
            {
                IntPtr token;
                if(!OpenProcessToken(handle,8,out token))return Marshal.GetLastWin32Error()==5;
                try
                {
                    int value,returned;
                    return GetTokenInformation(token,20,out value,4,out returned)&&value!=0;
                }
                finally{CloseHandle(token);}
            }
            finally{CloseHandle(handle);}
        }
        internal static bool WindowIsElevated(IntPtr window)
        {
            uint process;
            Native.GetWindowThreadProcessId(window,out process);
            return process!=0&&IsElevated(process);
        }
    }
    // The normal app's side: asks the helper to bring an administrator window forward and answers its key queries.
    internal static class AdminBridge
    {
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int process);
        static volatile bool registered;
        static DateTime offlineUntil=DateTime.MinValue,launchedAt=DateTime.MinValue;
        static bool started;
        static AdminKeyServer server;
        static VolumeKeys keyHook;
        static readonly object gate=new object();
        internal static void Start(VolumeKeys keys)
        {
            keyHook=keys;
            if(started||LogonStartup.IsAdministrator)return;
            started=true;
            Task.Run(()=>Refresh());
            var watcher=new Thread(()=>
            {
                // A helper that went away (an update, a crash) is started again; nothing happens while the feature is off.
                while(started)
                {
                    Thread.Sleep(10000);
                    if(registered)EnsureRunning();
                }
            }){IsBackground=true,Name="One TaskBar admin helper watch"};
            watcher.Start();
            var application=Application.Current;
            if(application!=null)application.Dispatcher.BeginInvoke(new Action(()=>
            {
                var current=Application.Current;
                // The helper leaves when the app does, so an update can replace the files; it is started again with the app.
                if(current!=null)current.Exit+=(s,e)=>{if(registered)Quit();};
            }));
        }
        // Reads whether the helper task exists and brings the app in line: starts or stops serving the helper's keys.
        internal static void Refresh()
        {
            try
            {
                bool now=LogonStartup.AdminHelperRegistered;
                lock(gate)
                {
                    registered=now;
                    if(now)
                    {
                        if(server==null&&keyHook!=null)server=new AdminKeyServer(keyHook);
                    }
                    else
                    {
                        var current=server;
                        server=null;
                        if(current!=null)current.Dispose();
                    }
                }
                if(now)EnsureRunning();
            }
            catch(Exception error){AppLog.Once("admin-helper-refresh",error.GetType().Name);}
        }
        internal static void Stop()
        {
            started=false;
            lock(gate)
            {
                var current=server;
                server=null;
                if(current!=null)current.Dispose();
            }
        }        static bool Running {get{return File.Exists(@"\\.\pipe\"+AdminPipes.Command);}}
        static void EnsureRunning()
        {
            if(Running||(DateTime.UtcNow-launchedAt).TotalSeconds<20)return;
            launchedAt=DateTime.UtcNow;
            try
            {
                var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"schtasks.exe"),"/run /tn "+LogonStartup.AdminHelperTask){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
                using(Process.Start(info)){}
            }
            catch(Exception error){AppLog.Once("admin-helper-run",error.GetType().Name);}
        }
        static string Ask(string line,int timeout)
        {
            try
            {
                using(var pipe=new NamedPipeClientStream(".",AdminPipes.Command,PipeDirection.InOut,PipeOptions.None,TokenImpersonationLevel.Identification))
                {
                    pipe.Connect(timeout);
                    var bytes=Encoding.ASCII.GetBytes(line);
                    pipe.Write(bytes,0,bytes.Length);
                    var answer=new byte[1];
                    var read=pipe.ReadAsync(answer,0,1);
                    if(!read.Wait(timeout*4)||read.Result!=1)return null;
                    return ((char)answer[0]).ToString();
                }
            }
            catch(Exception){return null;}
        }
        // True only when the window is an administrator window and the helper has brought it forward.
        internal static bool Activate(IntPtr window)
        {
            if(!registered||LogonStartup.IsAdministrator||DateTime.UtcNow<offlineUntil||!ProcessElevation.WindowIsElevated(window))return false;
            AllowSetForegroundWindow(-1);
            string answer=Ask("A "+window.ToInt64(),60);
            if(answer==null)
            {
                offlineUntil=DateTime.UtcNow.AddSeconds(3);
                Task.Run(()=>EnsureRunning());
                return false;
            }
            return answer=="1";
        }
        internal static bool WantsHelper(IntPtr window)
        {
            return registered&&!LogonStartup.IsAdministrator&&ProcessElevation.WindowIsElevated(window);
        }
        internal static void Quit()
        {
            Ask("Q",100);
        }
    }
    // Receives the helper's key events and answers whether the Alt+Tab switcher swallowed them.
    internal sealed class AdminKeyServer : IDisposable
    {
        readonly VolumeKeys keys;
        volatile bool stopped;
        internal AdminKeyServer(VolumeKeys keys)
        {
            this.keys=keys;
            var thread=new Thread(Serve){IsBackground=true,Name="One TaskBar admin keys"};
            thread.Start();
        }
        public void Dispose()
        {
            stopped=true;
        }
        void Serve()
        {
            while(!stopped)
            {
                try
                {
                    using(var pipe=new NamedPipeServerStream(AdminPipes.Keys,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.None,64,64,AdminPipes.Security()))
                    {
                        pipe.WaitForConnection();
                        var request=new byte[28];
                        while(!stopped&&Fill(pipe,request))
                        {
                            pipe.WriteByte(Handle(BitConverter.ToInt32(request,0),BitConverter.ToInt32(request,4),BitConverter.ToInt32(request,8),BitConverter.ToUInt32(request,12),BitConverter.ToUInt32(request,16),BitConverter.ToInt64(request,20)));
                        }
                    }
                }
                catch(Exception error)
                {
                    AppLog.Once("admin-keys",error.GetType().Name);
                    Thread.Sleep(500);
                }
            }
        }
        static bool Fill(Stream stream,byte[] buffer)
        {
            int total=0;
            while(total<buffer.Length)
            {
                int count=stream.Read(buffer,total,buffer.Length-total);
                if(count<=0)return false;
                total+=count;
            }
            return true;
        }
        byte Handle(int message,int key,int scan,uint flags,uint time,long extra)
        {
            var application=Application.Current;
            if(application==null)return 0;
            try
            {
                object answer=application.Dispatcher.Invoke(DispatcherPriority.Send,TimeSpan.FromMilliseconds(120),new Func<int>(()=>keys.ObserveRemote(message,key,scan,flags,time,extra)));
                if(answer==null)return 0;
                return (byte)(int)answer;
            }
            catch(Exception){return 0;}
        }
    }
    // The administrator helper process (--admin-helper). It has no window: it brings administrator windows forward on request
    // and, while an administrator window has focus, hands the Alt+Tab keys to the normal app.
    internal static class AdminHelperHost
    {
        delegate IntPtr Hook(int code,IntPtr message,IntPtr info);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int type,Hook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr info);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string module);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread,uint message,IntPtr wp,IntPtr lp);
        static uint uiThread;
        static Hook callback;
        static IntPtr hook;
        static NamedPipeClientStream keyPipe;
        static bool active;
        static DateTime retryAt=DateTime.MinValue,lastSeen=DateTime.UtcNow;
        static IntPtr cachedWindow;
        static bool cachedElevated;
        static DateTime cachedAt=DateTime.MinValue;
        internal static int Run()
        {
            if(!LogonStartup.IsAdministrator)return 2;
            bool created;
            using(var mutex=new Mutex(true,"Local\\OneTaskBar.AdminHelper."+AdminPipes.Sid,out created))
            {
                if(!created)return 0;
                uiThread=GetCurrentThreadId();
                var commands=new Thread(Commands){IsBackground=true,Name="One TaskBar admin commands"};
                commands.Start();
                callback=OnKey;
                hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
                var timer=new System.Windows.Forms.Timer{Interval=3000};
                timer.Tick+=(s,e)=>
                {
                    Connect();
                    // A pipe whose other end has gone can still look connected, so the app's own process is what counts.
                    int own=Process.GetCurrentProcess().Id;
                    foreach(var other in Process.GetProcessesByName("OneTaskBar"))
                    {
                        using(other)if(other.Id!=own)lastSeen=DateTime.UtcNow;
                    }
                    // The normal app has been gone for a while: nothing left to help, and the installed files must be free for an update.
                    if((DateTime.UtcNow-lastSeen).TotalSeconds>20)System.Windows.Forms.Application.Exit();
                };
                timer.Start();
                System.Windows.Forms.Application.Run();
                timer.Stop();
                if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);
                Drop();
            }
            return 0;
        }
        static void Commands()
        {
            while(true)
            {
                try
                {
                    using(var pipe=new NamedPipeServerStream(AdminPipes.Command,PipeDirection.InOut,4,PipeTransmissionMode.Byte,PipeOptions.None,256,256,AdminPipes.Security()))
                    {
                        pipe.WaitForConnection();
                        var buffer=new byte[64];
                        var read=pipe.ReadAsync(buffer,0,buffer.Length);
                        if(!read.Wait(1000)||read.Result<=0)continue;
                        string line=Encoding.ASCII.GetString(buffer,0,read.Result).Trim();
                        bool ok=false,quit=false;
                        if(line.StartsWith("A ",StringComparison.Ordinal))
                        {
                            long value;
                            if(Int64.TryParse(line.Substring(2),out value))
                            {
                                var window=new IntPtr(value);
                                ok=Native.IsWindow(window)&&WindowActivation.TryActivate(window);
                            }
                        }
                        else if(line=="Q"){ok=true;quit=true;}
                        pipe.WriteByte((byte)(ok?'1':'0'));
                        pipe.Flush();
                        if(quit)
                        {
                            PostThreadMessage(uiThread,0x12,IntPtr.Zero,IntPtr.Zero);
                            return;
                        }
                    }
                }
                catch(Exception){Thread.Sleep(200);}
            }
        }
        static void Drop()
        {
            var pipe=keyPipe;
            keyPipe=null;
            if(pipe!=null)try{pipe.Dispose();}catch(Exception){}
        }
        static bool Connect()
        {
            if(keyPipe!=null&&keyPipe.IsConnected)return true;
            if(DateTime.UtcNow<retryAt)return false;
            Drop();
            var pipe=new NamedPipeClientStream(".",AdminPipes.Keys,PipeDirection.InOut);
            try
            {
                pipe.Connect(15);
                keyPipe=pipe;
                lastSeen=DateTime.UtcNow;
                return true;
            }
            catch(Exception)
            {
                pipe.Dispose();
                retryAt=DateTime.UtcNow.AddSeconds(1);
                return false;
            }
        }
        static bool ForegroundElevated()
        {
            var window=Native.GetForegroundWindow();
            if(window==IntPtr.Zero)return false;
            if(window==cachedWindow&&(DateTime.UtcNow-cachedAt).TotalSeconds<2)return cachedElevated;
            cachedWindow=window;
            cachedElevated=ProcessElevation.WindowIsElevated(window);
            cachedAt=DateTime.UtcNow;
            return cachedElevated;
        }
        // Returns the normal app's answer (bit 0 swallow, bit 1 switcher open), or -1 when it could not be asked in time.
        static int Forward(int message,int key,int scan,uint flags,uint time,long extra)
        {
            try
            {
                if(!Connect())return -1;
                var request=new byte[28];
                Buffer.BlockCopy(BitConverter.GetBytes(message),0,request,0,4);
                Buffer.BlockCopy(BitConverter.GetBytes(key),0,request,4,4);
                Buffer.BlockCopy(BitConverter.GetBytes(scan),0,request,8,4);
                Buffer.BlockCopy(BitConverter.GetBytes(flags),0,request,12,4);
                Buffer.BlockCopy(BitConverter.GetBytes(time),0,request,16,4);
                Buffer.BlockCopy(BitConverter.GetBytes(extra),0,request,20,8);
                keyPipe.Write(request,0,request.Length);
                var answer=new byte[1];
                var read=keyPipe.ReadAsync(answer,0,1);
                if(!read.Wait(80)||read.Result!=1){Drop();retryAt=DateTime.UtcNow.AddSeconds(1);return -1;}
                return answer[0];
            }
            catch(Exception)
            {
                Drop();
                retryAt=DateTime.UtcNow.AddSeconds(1);
                return -1;
            }
        }
        static IntPtr OnKey(int code,IntPtr message,IntPtr info)
        {
            if(code>=0)
            {
                try
                {
                    int kind=message.ToInt32();
                    bool down=kind==0x100||kind==0x104,up=kind==0x101||kind==0x105;
                    if(down||up)
                    {
                        int key=Marshal.ReadInt32(info);
                        uint flags=(uint)Marshal.ReadInt32(info,8);
                        bool alt=(flags&0x20)!=0||(GetAsyncKeyState(0x12)&0x8000)!=0;
                        // Only the shortcuts One TaskBar owns are handed over (Alt+Tab, the Win key and its chords, volume keys);
                        // everything else a game receives untouched. While a switcher or chord is in progress every key is sent.
                        bool win=(GetAsyncKeyState(0x5b)&0x8000)!=0||(GetAsyncKeyState(0x5c)&0x8000)!=0;
                        bool owned=(down&&key==9&&alt)||key==0x5b||key==0x5c||win||(key>=0xAD&&key<=0xAF)||active;
                        if(owned&&(flags&0x10)==0&&ForegroundElevated())
                        {
                            int scan=Marshal.ReadInt32(info,4),time=Marshal.ReadInt32(info,12);
                            long extra=Marshal.ReadInt64(info,16);
                            int answer=Forward(kind,key,scan,flags,(uint)time,extra);
                            if(answer>=0)
                            {
                                active=(answer&2)!=0;
                                if((answer&1)!=0)return new IntPtr(1);
                            }
                            else active=false;
                        }
                    }
                }
                catch(Exception){}
            }
            return CallNextHookEx(hook,code,message,info);
        }
    }
}
