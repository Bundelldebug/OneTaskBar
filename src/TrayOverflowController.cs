using System;
using System.Linq;
using System.Threading.Tasks;

namespace OneTaskBar
{
    internal sealed partial class ShellController
    {
        internal TrayOverflowWindow TrayPopup;
        int trayRequest;
        internal async void OpenTrayOverflow(DockHost dock)
        {
            if(disposed||!Desktop||trayBusy)return;
            if(TrayPopup!=null&&TrayPopup.IsVisible){TrayPopup.Dismiss();return;}
            trayDock=dock??docks.FirstOrDefault();
            if(trayDock==null)return;
            int request=++trayRequest;trayBusy=true;
            var anchor=trayDock.TrayAnchor();
            var metadata=Apps.Concat(Running).ToArray();
            CloseDesktopOverview();Switcher.Cancel();Previews.Close();
            foreach(var panel in new GlassWindow[]{StartMenu,Quick,Center,Detail,Drawer,Finder})if(panel!=null)panel.HideImmediately();
            TrayIconSession session=null;
            try{
                session=await Task.Run(()=>TrayIcons.Open(anchor,metadata));
                if(disposed||!Desktop||request!=trayRequest)return;
                if(session==null){AppLog.Once("tray-provider","Native tray retained because its accessibility provider is unavailable");return;}
                if(!session.Mask()){
                    session.LeaveNative();
                    AppLog.Once("tray-mask","Native tray retained because its host cannot be represented safely");
                    return;
                }
                // Capture the session independently: this local is cleared after ownership transfers.
                var owned=session;
                var popup=new TrayOverflowWindow(owned,(item,context)=>owned.Invoke(item,context));
                TrayPopup=popup;popup.Closed+=(s,e)=>{if(TrayPopup==popup)TrayPopup=null;};
                popup.Present(anchor);session=null;
                AppLog.Json("tray-proxy-state.json",new {visible=true,icons=popup.IconCount,nativeFallback=false});
            }catch(Exception error){AppLog.Once("tray-popup",error.GetType().Name);}
            finally{trayBusy=false;if(session!=null)session.Dispose();}
        }
        void CloseTrayOverflow()
        {
            trayRequest++;
            if(TrayPopup!=null){TrayPopup.Close();TrayPopup=null;}
        }
    }
}
