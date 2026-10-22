using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class QuickFeatureChecks
    {
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;QuickWindow quick=null;DetailWindow detail=null;bool animations=Preferences.Current.Animations;PerformanceMode power=null;DisturbState dnd=null;bool muted=false,audio=false;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try {
                    Program.Inspectable=true;shell=new ShellController(true);shell.Audio.Read();audio=shell.Audio.Available;muted=shell.Audio.Muted;power=Native.ReadPerformanceMode();dnd=await shell.Toggles.Disturb.RefreshAsync();quick=new QuickWindow(shell);quick.Refresh(shell.Power,60,shell.Audio.Value,muted,audio,true);var screen=System.Windows.Forms.Screen.PrimaryScreen;quick.ShowAt(screen.Bounds,30,30,380,650);quick.SetExpanded(true);await Task.Delay(370);
                    Action<string> click=key=>{var peer=UIElementAutomationPeer.CreatePeerForElement(quick.FindToggle(key));((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)).Toggle();};
                    click("awake");bool awakeOn=shell.Toggles.Awake&&quick.FindToggle("awake").Checked;click("awake");bool awakeOff=!shell.Toggles.Awake&&!quick.FindToggle("awake").Checked;
                    click("animations");bool animationChanged=Preferences.Current.Animations!=animations&&quick.FindToggle("animations").Checked==Preferences.Current.Animations;click("animations");
                    bool audioChanged=true;if(audio){click("mute");shell.Audio.Read();audioChanged=shell.Audio.Muted!=muted&&quick.FindToggle("mute").Checked==shell.Audio.Muted;click("mute");shell.Audio.Read();audioChanged&=shell.Audio.Muted==muted;}
                    bool efficiencyChanged=true;if(power.Available&&Native.PerformanceModes().Any(m=>m.Id==power.Selected)){click("efficiency");var after=Native.ReadPerformanceMode();efficiencyChanged=after.Selected!=power.Selected&&quick.FindToggle("efficiency").Checked==(after.Selected==QuickActions.EfficientMode);click("efficiency");efficiencyChanged&=Native.ReadPerformanceMode().Selected==power.Selected;}
                    bool disturbChanged=dnd.Available;if(dnd.Available&&!dnd.Automatic){click("dnd");for(int i=0;i<100&&!quick.FindToggle("dnd").IsEnabled;i++)await Task.Delay(25);disturbChanged=quick.FindToggle("dnd").IsEnabled&&quick.FindToggle("dnd").Checked!=dnd.Enabled;click("dnd");for(int i=0;i<100&&!quick.FindToggle("dnd").IsEnabled;i++)await Task.Delay(25);var after=await shell.Toggles.Disturb.RefreshAsync();disturbChanged&=after.Available&&after.Selected==dnd.Selected&&after.Enabled==dnd.Enabled;}
                    detail=new DetailWindow(shell,"display");bool palette=quick.Surface.Background==detail.Surface.Background&&detail.Surface.Background==QuickPanel.Glass;bool layout=quick.LayoutFits();Program.Save(quick.Surface,380,650,Path.Combine(AppLog.Data,"quick-toggles-expanded.png"));
                    result=quick.ToggleKeys.Length==10&&awakeOn&&awakeOff&&animationChanged&&audioChanged&&efficiencyChanged&&disturbChanged&&palette&&layout?0:1;AppLog.Json("quick-feature-probe.json",new {passed=result==0,toggles=quick.ToggleKeys,awakeEnabled=awakeOn,awakeReleased=awakeOff,animationChanged=animationChanged,audioAvailable=audio,audioChangedAndRestored=audioChanged,powerAvailable=power.Available,efficiencyChangedAndRestored=efficiencyChanged,dndAvailable=dnd.Available,dndAutomatic=dnd.Automatic,dndChangedAndRestored=disturbChanged,dndError=dnd.Error,sharedPalette=palette,layoutFits=layout,networkOrStartupChanged=false,powerActionsExecuted=false});
                }catch(Exception error){AppLog.Json("quick-feature-probe.json",new {passed=false,error=error.ToString()});}
                finally {if(shell!=null){if(dnd!=null&&dnd.Available&&!dnd.Automatic)try{shell.Toggles.Disturb.Restore(dnd.Selected).GetAwaiter().GetResult();}catch{}shell.Toggles.Dispose();if(audio)try{shell.Audio.Mute(muted);}catch{}if(power!=null&&power.Available&&Native.PerformanceModes().Any(m=>m.Id==power.Selected))try{Native.SetPerformanceMode(power.Selected);}catch{}}Preferences.Set(p=>p.Animations=animations);if(detail!=null)detail.Close();if(quick!=null)quick.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
