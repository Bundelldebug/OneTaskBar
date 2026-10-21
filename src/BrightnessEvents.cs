using System;
using System.Management;

namespace OneTaskBar
{
    // This is a provider event, without an intrinsic-event WITHIN polling query.
    internal sealed class BrightnessEvents : IDisposable
    {
        readonly object gate=new object();readonly Action<int> changed;
        ManagementEventWatcher watcher;volatile bool disposed;internal volatile bool Ready;
        internal BrightnessEvents(Action<int> changed)
        {
            this.changed=changed;
            BackgroundWork.Run(()=>{
                ManagementEventWatcher next=null;
                try {
                    next=new ManagementEventWatcher(new ManagementScope(@"\\.\root\wmi"),new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));next.EventArrived+=Arrived;next.Start();
                    lock(gate){if(disposed){next.Stop();next.Dispose();}else{watcher=next;Ready=true;}next=null;}
                }catch(Exception error){if(next!=null)next.Dispose();AppLog.Once("brightness-events",error.Message);}return true;
            });
        }
        void Arrived(object sender,EventArrivedEventArgs args)
        {
            try{using(var item=args.NewEvent){if(disposed||!Convert.ToBoolean(item["Active"]))return;int value=Convert.ToInt32(item["Brightness"]);if(value>=0&&value<=100)changed(value);}}catch{}
        }
        public void Dispose()
        {
            ManagementEventWatcher current;lock(gate){disposed=true;Ready=false;current=watcher;watcher=null;}
            if(current!=null){current.EventArrived-=Arrived;try{current.Stop();}catch{}current.Dispose();}
        }
    }
}
