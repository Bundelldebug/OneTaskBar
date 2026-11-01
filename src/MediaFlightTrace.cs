using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace OneTaskBar
{
    // Bounded timing samples only: no window handles, titles, artwork or paths.
    internal sealed class MediaFlightTrace
    {
        readonly Stopwatch elapsed=Stopwatch.StartNew();
        readonly List<double> gaps=new List<double>(160);
        readonly Dictionary<DispatcherOperation,long> operations=new Dictionary<DispatcherOperation,long>();
        readonly DispatcherHooks hooks;
        double previous,maximumWork,setup,maximumDispatch;
        string slowPriority;
        bool first=true;
        internal MediaFlightTrace()
        {
            hooks=Dispatcher.CurrentDispatcher.Hooks;
            hooks.OperationStarted+=OperationStarted;
            hooks.OperationCompleted+=OperationCompleted;
        }
        void OperationStarted(object sender,DispatcherHookEventArgs args)
        {
            if(operations.Count<16)operations[args.Operation]=Stopwatch.GetTimestamp();
        }
        void OperationCompleted(object sender,DispatcherHookEventArgs args)
        {
            long start;
            if(!operations.TryGetValue(args.Operation,out start))return;
            operations.Remove(args.Operation);
            double duration=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
            if(duration>maximumDispatch){maximumDispatch=duration;slowPriority=args.Operation.Priority.ToString();}
        }
        internal void Detach()
        {
            hooks.OperationStarted-=OperationStarted;
            hooks.OperationCompleted-=OperationCompleted;
            operations.Clear();
        }
        internal void Frame()
        {
            double now=elapsed.Elapsed.TotalMilliseconds;
            if(first)
            {
                first=false;
                setup=now;
            }
            else if(gaps.Count<240)gaps.Add(now-previous);
            previous=now;
        }
        internal void Work(long start)
        {
            maximumWork=Math.Max(maximumWork,(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency);
        }
        internal object Finish(bool restoring,int frames,double renderGap)
        {
            var sorted=gaps.OrderBy(n=>n).ToArray();
            return new {
                time=DateTimeOffset.Now.ToString("o"),restoring=restoring,frames=frames,
                setupMilliseconds=Math.Round(setup,2),durationMilliseconds=Math.Round(elapsed.Elapsed.TotalMilliseconds,2),
                maximumWorkMilliseconds=Math.Round(maximumWork,2),maximumRenderGapMilliseconds=Math.Round(renderGap,2),
                maximumDispatchMilliseconds=Math.Round(maximumDispatch,2),slowPriority=slowPriority,
                renderTier=System.Windows.Media.RenderCapability.Tier>>16,
                maximumWallGapMilliseconds=Math.Round(sorted.Length==0?0:sorted[sorted.Length-1],2),
                p95WallGapMilliseconds=Math.Round(sorted.Length==0?0:sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*.95))],2)
            };
        }
        internal static void Save(object report)
        {
            System.Threading.Tasks.Task.Run(()=> {
                try{AppLog.Json("media-flight-timing.json",report);}
                catch(System.IO.IOException){}
                catch(UnauthorizedAccessException){}
            });
        }
    }
}
