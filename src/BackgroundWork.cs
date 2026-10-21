using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace OneTaskBar
{
    // Two reusable workers avoid bursts across the general thread pool. EcoQoS lets
    // Windows / Thread Director choose efficient cores without a hard CPU affinity.
    internal static class BackgroundWork
    {
        [StructLayout(LayoutKind.Sequential)]struct Throttling {public uint Version,ControlMask,StateMask;}
        [DllImport("kernel32.dll")]static extern IntPtr GetCurrentThread();
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetThreadInformation(IntPtr thread,int kind,ref Throttling state,uint size);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetProcessInformation(IntPtr process,int kind,ref Throttling state,uint size);
        static readonly BlockingCollection<Action> jobs=new BlockingCollection<Action>();
        internal static int EcoWorkers,Completed;
        static BackgroundWork(){for(int i=0;i<2;i++){var worker=new Thread(Work){IsBackground=true,Name="OneTaskBar.EfficientWorker."+i,Priority=ThreadPriority.BelowNormal};worker.Start();}}
        static void Work()
        {
            try{var state=new Throttling {Version=1,ControlMask=1,StateMask=1};if(SetThreadInformation(GetCurrentThread(),3,ref state,12))Interlocked.Increment(ref EcoWorkers);}catch(EntryPointNotFoundException){}
            foreach(var job in jobs.GetConsumingEnumerable()){job();Interlocked.Increment(ref Completed);}
        }
        internal static Task<T> Run<T>(Func<T> function){var completion=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);jobs.Add(()=>{try{completion.SetResult(function());}catch(Exception error){completion.SetException(error);}});return completion.Task;}
        internal static void HintChild(Process process){try{var state=new Throttling {Version=1,ControlMask=1,StateMask=1};SetProcessInformation(process.Handle,4,ref state,12);process.PriorityClass=ProcessPriorityClass.BelowNormal;}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}catch(EntryPointNotFoundException){}}
    }
}
