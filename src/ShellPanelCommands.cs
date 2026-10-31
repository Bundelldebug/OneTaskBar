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
            var commands=new[]{"notifications","quick","start","search","desktops","media-screen"}.Concat(Program.CaptureReview?new[]{"edge0","edge1","edge2","edge3","background0","background1","background2","background3"}:new string[0]);
            foreach(string panel in commands)
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
            if(Program.CaptureReview&&key.StartsWith("edge",StringComparison.Ordinal)){Preferences.Current.DockEdge=key[4]-'0';Relayout();return;}
            if(Program.CaptureReview&&key.StartsWith("background",StringComparison.Ordinal)){Preferences.Current.DockBackgroundKind=key[10]-'0';Preferences.Current.DockSolidBackground=key[10]=='1';Relayout();return;}
            var dock=docks.FirstOrDefault();
            if(key=="notifications")ToggleNotifications(dock);
            else if(key=="quick")ToggleQuick(dock);
            else if(key=="start")ToggleStart(dock);
            else if(key=="search")ToggleSearch(dock);
            else if(key=="desktops")OpenTaskView();
            else if(key=="media-screen")OpenFullscreenMedia();
        }
    }
}
