using System;
using System.Collections.Generic;
using System.Linq;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed partial class ShellController
    {
        internal DynamicIsland Island;
        IslandSafeArea islandSpace;
        bool lastIslandPreference=ReleaseFeatures.GlassUI&&Preferences.Current.DynamicIsland||Preferences.Current.TopStatusBar;
        TopStatusBar topLeft,topRight;
        bool TopBarEnabled {get{return Preferences.Current.TopStatusBar&&Desktop&&!disposed;}}
        internal bool TopBarActive {get{return TopBarEnabled&&topLeft!=null&&topLeft.IsVisible;}}
        internal DockHost DockFor(Forms.Screen screen){return docks.FirstOrDefault(d=>d.Screen.DeviceName==screen.DeviceName)??docks.FirstOrDefault();}
        internal bool IslandExpanded {get{return Island!=null&&Island.IsVisible&&Island.Expanded;}}
        bool IslandEnabled {get{return ReleaseFeatures.GlassUI&&Preferences.Current.DynamicIsland&&Desktop&&!disposed;}}
        void IslandPreferenceChanged()
        {
            bool next=ReleaseFeatures.GlassUI&&Preferences.Current.DynamicIsland||Preferences.Current.TopStatusBar;
            if(next==lastIslandPreference)return;
            lastIslandPreference=next;
            if(Desktop)Relayout();else CloseIsland();
        }
        void RefreshIsland(IEnumerable<FullscreenCandidate> windows)
        {
            if(!IslandEnabled&&!TopBarEnabled){CloseIsland();return;}
            var dock=docks.FirstOrDefault(item=>item.Screen.Primary)??docks.FirstOrDefault();
            if(dock==null)return;
            var monitor=dock.Screen;
            double dpi=dock.Scale;
            bool full=!DisplayAwake||Overview!=null&&Overview.IsVisible||FullscreenVisibility.IsFullScreen(monitor.Bounds,windows);
            if(full)
            {
                if(Island!=null)Island.Suspend();
                CloseTopBar();
                if(islandSpace!=null)islandSpace.SetActive(false);
                return;
            }
            try
            {
                if(islandSpace!=null&&(islandSpace.Monitor!=monitor.DeviceName||Math.Abs(islandSpace.Scale-dpi)>.001||islandSpace.Bottom!=(dock.Edge==1)))CloseIsland();
                PrepareIslandSpace(monitor,dpi);
                var work=islandSpace.PlacementArea();
                if(!DynamicIslandLayout.Clear(monitor.Bounds,work,dpi,windows)){CloseIsland();return;}
                if(IslandEnabled)
                {
                    if(Island==null)Island=new DynamicIsland(Media,SeekMedia,()=>ShowDetail("sound"),OpenMediaApplication,()=>PrepareWindowSwitcher(),id=>MediaApplication.Resolve(id,Running.Concat(Apps)));
                    Lap("island-prep");Island.Present(Services.Media,Services.OutputName,monitor,dpi,work,islandSpace.Bottom);Lap("island-present");
                }
                else if(Island!=null){Island.Close();Island=null;}
                if(TopBarEnabled)
                {
                    if(topLeft==null)topLeft=new TopStatusBar(this,TopStatusBar.Side.Left);
                    if(topRight==null)topRight=new TopStatusBar(this,TopStatusBar.Side.Right);
                    topLeft.Present(monitor,dpi,islandSpace.Bottom);Lap("top-left");
                    topRight.Present(monitor,dpi,islandSpace.Bottom);Lap("top-right");
                }
                else CloseTopBar();
            }
            catch(Exception error){CloseIsland();AppLog.Once("island-safety-area",error.ToString());}
        }
        void PrepareIslandSpace(Forms.Screen monitor,double dpi)
        {
            bool bottom=DockGeometry.Edge==1;
            if(islandSpace==null)islandSpace=new IslandSafeArea(monitor,dpi,bottom);
            if(islandSpace.Active)return;
            var overlapping=reservations.Where(item=>item.IsOn(monitor.DeviceName,bottom?3:1)).ToArray();
            foreach(var item in overlapping)item.SetAutoHide(true);
            try{islandSpace.SetActive(true);}
            finally{foreach(var item in overlapping)item.SetAutoHide(DockAutoHide);}
            foreach(var dock in docks)
            {
                if(dock.Screen.DeviceName==monitor.DeviceName&&dock.Edge==(bottom?3:1))dock.Rebuild();
            }
        }
        bool IslandFeedback(string kind,double value,bool muted)
        {
            if(!IslandEnabled)return false;
            RefreshIsland(FullscreenVisibility.Capture());
            if(islandSpace==null||Forms.Screen.FromPoint(Forms.Cursor.Position).DeviceName!=islandSpace.Monitor||Island==null||!Island.IsVisible||Island.Expanded)return false;
            Island.ShowFeedback(kind,value,muted);
            if(hud!=null&&hud.IsVisible)hud.HideImmediately();
            return true;
        }
        internal bool IslandNotification(NotificationEntry entry,Action<NotificationEntry> open)
        {
            return IslandEnabled&&Island!=null&&Island.ShowNotification(entry,open);
        }
        void CloseTopBar()
        {
            if(topLeft!=null){topLeft.Close();topLeft=null;}
            if(topRight!=null){topRight.Close();topRight=null;}
        }
        void CloseIsland()
        {
            CloseTopBar();
            if(Island!=null){Island.Close();Island=null;}
            if(islandSpace!=null){islandSpace.Dispose();islandSpace=null;}
        }
    }
}
