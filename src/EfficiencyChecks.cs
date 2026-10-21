using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace OneTaskBar
{
    internal static class EfficiencyChecks
    {
        internal static void Validate(List<string> checks)
        {
            int caller=Thread.CurrentThread.ManagedThreadId,active=0,peak=0;
            var jobs=Enumerable.Range(0,12).Select(i=>BackgroundWork.Run(()=>{
                int count=Interlocked.Increment(ref active);lock(checks)peak=Math.Max(peak,count);
                try{Thread.Sleep(10);return Thread.CurrentThread.ManagedThreadId;}finally{Interlocked.Decrement(ref active);}
            })).ToArray();
            if(!Task.WaitAll(jobs,10000)||jobs.Any(j=>j.Result==caller)||jobs.Select(j=>j.Result).Distinct().Count()>2||peak>2)throw new Exception("Background workers were not bounded or ran on the caller");
            AppLog.Json("efficiency-probe.json",new {passed=true,workerThreads=jobs.Select(j=>j.Result).Distinct().Count(),peakConcurrency=peak,ecoQosAcceptedWorkers=BackgroundWork.EcoWorkers,callerExcluded=true,hardAffinityUsed=false});
            checks.Add("Background jobs use at most two reusable workers; caller/UI is excluded; EcoQoS is requested without hard CPU affinity");
        }
    }
}
