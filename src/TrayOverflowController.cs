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
            TrayOpenMask opening=null;
            try{
                if(Preferences.Current.NativeTrayOverflow)
                {
                    await Task.Run(()=>
                    {
                        var native=Native.FindWindow("TopLevelWindowForOverflowXamlIsland",null);
                        if(native!=IntPtr.Zero)TaskbarMask.Restore(native);
                        native=Native.FindWindow("NotifyIconOverflowWindow",null);
                        if(native!=IntPtr.Zero)TaskbarMask.Restore(native);
                        if(ExplorerBridge.TrayVisible())
                        {
                            Native.ShowWindow(ExplorerBridge.OverflowHandle(),0);
                            return;
                        }
                        ExplorerBridge.OpenTray(anchor);
                    });
                    return;
                }
                opening=new TrayOpenMask();
                session=await Task.Run(()=>TrayIcons.Open(anchor,metadata));
                if(disposed||!Desktop||request!=trayRequest)return;
                if(session==null)
                {
                    opening.Dispose();opening=null;
                    bool native=await Task.Run(()=>
                    {
                        if(ExplorerBridge.TrayVisible())return ExplorerBridge.AnchorTray(anchor);
                        return ExplorerBridge.OpenTray(anchor);
                    });
                    AppLog.Once("tray-provider","Native tray retained because its accessibility provider is unavailable");
                    if(!native)ActionNotice.Show("숨겨진 트레이 아이콘을 열지 못했습니다. Explorer 상태를 확인해 주세요.");
                    return;
                }
                if(!session.Mask()){
                    session.LeaveNative();
                    AppLog.Once("tray-mask","Native tray retained because its host cannot be represented safely");
                    return;
                }
                // Capture the session independently: this local is cleared after ownership transfers.
                var owned=session;
                opening.Transfer();
                var popup=new TrayOverflowWindow(owned,(item,context)=>owned.Invoke(item,context));
                TrayPopup=popup;popup.Closed+=(s,e)=>{if(TrayPopup==popup)TrayPopup=null;};
                popup.Present(anchor);session=null;
                AppLog.Json("tray-proxy-state.json",new {visible=true,icons=popup.IconCount,nativeFallback=false});
            }catch(Exception error){AppLog.Once("tray-popup",error.ToString());}
            finally{trayBusy=false;if(session!=null)session.Dispose();if(opening!=null)opening.Dispose();}
        }
        void CloseTrayOverflow()
        {
            trayRequest++;
            var popup=TrayPopup;TrayPopup=null;
            if(popup!=null)popup.Close();
        }
    }
}
