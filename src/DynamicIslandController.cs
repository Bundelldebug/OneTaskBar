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
        bool lastIslandPreference=ReleaseFeatures.GlassUI&&Preferences.Current.DynamicIsland;
        internal bool IslandExpanded {get{return Island!=null&&Island.IsVisible&&Island.Expanded;}}
        bool IslandEnabled {get{return ReleaseFeatures.GlassUI&&Preferences.Current.DynamicIsland&&Desktop&&!disposed;}}
        void IslandPreferenceChanged()
        {
            bool next=ReleaseFeatures.GlassUI&&Preferences.Current.DynamicIsland;
            if(next==lastIslandPreference)return;
            lastIslandPreference=next;
            if(Desktop)Relayout();else CloseIsland();
        }
        void RefreshIsland(IEnumerable<FullscreenCandidate> windows)
        {
            if(!IslandEnabled){CloseIsland();return;}
            var dock=docks.FirstOrDefault(item=>item.Screen.Primary)??docks.FirstOrDefault();
            if(dock==null)return;
            var monitor=dock.Screen;
            double dpi=dock.Scale;
            bool full=!DisplayAwake||Overview!=null&&Overview.IsVisible||FullscreenVisibility.IsFullScreen(monitor.Bounds,windows);
            if(full)
            {
                if(Island!=null)Island.Suspend();
                if(islandSpace!=null)islandSpace.SetActive(false);
                return;
            }
            try
            {
                if(islandSpace!=null&&(islandSpace.Monitor!=monitor.DeviceName||Math.Abs(islandSpace.Scale-dpi)>.001))CloseIsland();
                PrepareIslandSpace(monitor,dpi);
                var work=islandSpace.PlacementArea();
                if(!DynamicIslandLayout.Clear(monitor.Bounds,work,dpi,windows)){CloseIsland();return;}
                if(Island==null)Island=new DynamicIsland(Media,SeekMedia,()=>ShowDetail("sound"),OpenMediaApplication,()=>PrepareWindowSwitcher(),id=>MediaApplication.Resolve(id,Running.Concat(Apps)));
                Island.Present(Services.Media,Services.OutputName,monitor,dpi,work);
            }
            catch(Exception error){CloseIsland();AppLog.Once("island-safety-area",error.ToString());}
        }
        void PrepareIslandSpace(Forms.Screen monitor,double dpi)
        {
            if(islandSpace==null)islandSpace=new IslandSafeArea(monitor,dpi);
            if(islandSpace.Active)return;
            var top=reservations.Where(item=>item.IsTopOn(monitor.DeviceName)).ToArray();
            foreach(var item in top)item.SetAutoHide(true);
            try{islandSpace.SetActive(true);}
            finally{foreach(var item in top)item.SetAutoHide(DockAutoHide);}
            foreach(var dock in docks)if(dock.Screen.DeviceName==monitor.DeviceName&&dock.Edge==1)dock.Rebuild();
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
        void CloseIsland()
        {
            if(Island!=null){Island.Close();Island=null;}
            if(islandSpace!=null){islandSpace.Dispose();islandSpace=null;}
        }
    }
}
