using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;

namespace OneTaskBar
{
    internal sealed partial class ShellController
    {
        readonly List<EventWaitHandle> panelCommands=new List<EventWaitHandle>();
        void RegisterPanelCommands()
        {
            foreach(string panel in new[]{"notifications","quick","start","search"})
            {
                string key=panel;
                var signal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\OneTaskBar.Panel-"+key);
                panelCommands.Add(signal);
                waits.Add(ThreadPool.RegisterWaitForSingleObject(signal,(state,timedOut)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>OpenPanelCommand(key))),null,Timeout.Infinite,false));
            }
        }
        void OpenPanelCommand(string key)
        {
            if(disposed)return;
            var dock=docks.FirstOrDefault();
            if(key=="notifications")ToggleNotifications(dock);
            else if(key=="quick")ToggleQuick(dock);
            else if(key=="start")ToggleStart(dock);
            else if(key=="search")ToggleSearch(dock);
        }
    }
}
