using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    internal sealed partial class ShellController
    {
        FullscreenMedia fullscreenMedia;
        internal bool FullscreenMediaVisible {get{return fullscreenMedia!=null&&fullscreenMedia.IsVisible;}}
        internal void OpenFullscreenMedia()
        {
            if(!ReleaseFeatures.GlassUI||disposed)return;
            PrepareWindowSwitcher();
            var screen=LastApplication!=IntPtr.Zero?Forms.Screen.FromHandle(LastApplication):Forms.Screen.PrimaryScreen;var dock=docks.FirstOrDefault(d=>d.Screen.DeviceName==screen.DeviceName);double scale=dock!=null?dock.Scale:1;
            if(fullscreenMedia==null){fullscreenMedia=new FullscreenMedia(Media,SeekMedia,()=>ShowDetail("sound"),()=>{fullscreenMedia.HideImmediately();try{SystemPower.Execute(SystemPowerAction.Lock);}catch(Exception e){AppLog.Write("media-screen-lock",e.Message);}});SystemEvents.SessionSwitch+=MediaSessionChanged;}
            if(dock==null)scale=Native.PrepareMonitor(fullscreenMedia,screen.Bounds);fullscreenMedia.Present(screen,scale);fullscreenMedia.Update(Services.Media);
        }
        void MediaSessionChanged(object sender,SessionSwitchEventArgs args){if(args.Reason==SessionSwitchReason.SessionLock)Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(fullscreenMedia!=null)fullscreenMedia.HideImmediately();}));}
        void RefreshFullscreenMedia()
        {
            if(fullscreenMedia==null)return;
            if(!DisplayAwake)fullscreenMedia.HideImmediately();
            else if(fullscreenMedia.IsVisible)fullscreenMedia.Update(Services.Media);
            if(fullscreenMedia.IdleForRelease(DateTime.UtcNow))CloseFullscreenMedia();
        }
        void CloseFullscreenMedia(){SystemEvents.SessionSwitch-=MediaSessionChanged;if(fullscreenMedia!=null){fullscreenMedia.Close();fullscreenMedia=null;}}
    }
}
