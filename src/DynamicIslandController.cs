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
        SafeAreaCurtain curtain;
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
            if(!full&&IslandEnabled&&Preferences.Current.FullscreenBand&&FullscreenVisibility.BandFriendly(FullscreenVisibility.BandCover(monitor.Bounds,windows)))full=true;
            if(full)
            {
                if(Island!=null)Island.Suspend();
                if(FullscreenBand(monitor,dpi,windows,dock))return;
                CloseTopBar();
                CloseCurtain();
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
                    if(Island==null)Island=new DynamicIsland(Media,SeekMedia,()=>ShowDetail("sound"),OpenMediaApplication,()=>PrepareWindowSwitcher(),id=>MediaApplication.Resolve(id,Running.Concat(Apps)),IslandFind,app=>LaunchApp(app,System.Windows.Input.Keyboard.Modifiers),text=>LauncherSearch.OpenWeb(LauncherSearch.WebAddress(text)));
                    Lap("island-prep");Island.Present(Services.Media,Services.OutputName,monitor,dpi,work,islandSpace.Bottom);Lap("island-present");
                }
                else if(Island!=null){Island.Close();Island=null;}
                bool shade=ShadeWanted(monitor,windows);
                // With the band turned black the status bar is still built: it is what the pointer reveals.
                if(TopBarEnabled||shade&&!Preferences.Current.TopStatusBar&&Desktop&&!disposed)
                {
                    if(topLeft==null)topLeft=new TopStatusBar(this,TopStatusBar.Side.Left);
                    if(topRight==null)topRight=new TopStatusBar(this,TopStatusBar.Side.Right);
                    topLeft.Present(monitor,dpi,islandSpace.Bottom);Lap("top-left");
                    topRight.Present(monitor,dpi,islandSpace.Bottom);Lap("top-right");
                }
                else CloseTopBar();
                UpdateCurtain(monitor,dpi,shade,islandSpace.Bottom);
            }
            catch(Exception error){CloseIsland();AppLog.Once("island-safety-area",error.ToString());}
        }
        List<AppEntry> IslandFind(string text)
        {
            if(String.IsNullOrWhiteSpace(text))return RecentApps.Recommend(Apps,Running,4);
            return LauncherSearch.Apps(Apps,text.Trim()).Take(4).ToList();
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
        // A maximized window under the island turns the reserved band black; the status bar returns on hover.
        // A browser or video player in fullscreen still gets the black band, with the status bar on hover.
        bool FullscreenBand(Forms.Screen monitor,double dpi,IEnumerable<FullscreenCandidate> windows,DockHost dock)
        {
            if(!Preferences.Current.FullscreenBand||!Desktop||disposed||!IslandEnabled||!DisplayAwake)return false;
            if(Overview!=null&&Overview.IsVisible)return false;
            var cover=FullscreenVisibility.BandCover(monitor.Bounds,windows);
            if(!FullscreenVisibility.BandFriendly(cover))return false;
            bool bottom=islandSpace!=null?islandSpace.Bottom:dock.Edge==1;
            if(islandSpace!=null)islandSpace.SetActive(false);
            if(topLeft==null)topLeft=new TopStatusBar(this,TopStatusBar.Side.Left);
            if(topRight==null)topRight=new TopStatusBar(this,TopStatusBar.Side.Right);
            topLeft.Present(monitor,dpi,bottom);
            topRight.Present(monitor,dpi,bottom);
            UpdateCurtain(monitor,dpi,true,bottom);
            return true;
        }
        bool ShadeWanted(Forms.Screen monitor,IEnumerable<FullscreenCandidate> windows)
        {
            // The Android-style top bar stays visible; only the island turns its band black.
            return IslandEnabled&&!Preferences.Current.MaximizedBehindDock&&MaximizedAbove(monitor,windows);
        }
        void UpdateCurtain(Forms.Screen monitor,double dpi,bool wanted,bool bottom)
        {
            if(!wanted){CloseCurtain();return;}
            bool fresh=curtain==null;
            if(fresh)
            {
                curtain=new SafeAreaCurtain(monitor,dpi,bottom);
                curtain.Hold=()=>Quick!=null&&Quick.IsVisible||Center!=null&&Center.IsVisible||topLeft!=null&&topLeft.CardOpen;
                curtain.RevealChanged=revealed=>{if(topLeft!=null)topLeft.Conceal(!revealed,true);if(topRight!=null)topRight.Conceal(!revealed,true);};
            }
            curtain.Present();
            if(topLeft!=null)topLeft.Conceal(!curtain.Revealed,true);
            if(topRight!=null)topRight.Conceal(!curtain.Revealed,true);
            // Created last, the curtain would sit over the island and the bar: raise them once above it.
            if(fresh)
            {
                if(Island!=null&&Island.IsVisible)Native.SetWindowPos(new System.Windows.Interop.WindowInteropHelper(Island).Handle,new IntPtr(-1),0,0,0,0,0x53);
                if(topLeft!=null&&topLeft.IsVisible)Native.SetWindowPos(new System.Windows.Interop.WindowInteropHelper(topLeft).Handle,new IntPtr(-1),0,0,0,0,0x53);
                if(topRight!=null&&topRight.IsVisible)Native.SetWindowPos(new System.Windows.Interop.WindowInteropHelper(topRight).Handle,new IntPtr(-1),0,0,0,0,0x53);
            }
        }
        static bool MaximizedAbove(Forms.Screen monitor,IEnumerable<FullscreenCandidate> windows)
        {
            foreach(var window in windows)
            {
                var overlap=System.Drawing.Rectangle.Intersect(monitor.Bounds,System.Drawing.Rectangle.FromLTRB(window.Bounds.Left,window.Bounds.Top,window.Bounds.Right,window.Bounds.Bottom));
                if(overlap.Width<=0||overlap.Height<=0)continue;
                // Overlays and pop-ups without a frame are not the app being used; the first real window decides.
                if(window.Monitor==monitor.Bounds&&(window.Maximized||window.Framed))return window.Maximized;
            }
            return false;
        }
        void CloseCurtain()
        {
            if(curtain==null)return;
            curtain.Retire();
            curtain=null;
            if(topLeft!=null)topLeft.Conceal(false,false);
            if(topRight!=null)topRight.Conceal(false,false);
        }
        void CloseTopBar()
        {
            if(topLeft!=null){topLeft.Close();topLeft=null;}
            if(topRight!=null){topRight.Close();topRight=null;}
        }
        void CloseIsland()
        {
            CloseCurtain();
            CloseTopBar();
            if(Island!=null){Island.Close();Island=null;}
            if(islandSpace!=null){islandSpace.Dispose();islandSpace=null;}
        }
    }
}
