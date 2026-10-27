using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal sealed partial class ShellController
    {
        bool renderedPanelDark=Glass95.Dark,renderedDockDark=Glass95.DockDark,themeRefreshQueued;DispatcherTimer themeRefresh;
        void QueuePanelTheme(){if(themeRefreshQueued)return;themeRefreshQueued=true;Application.Current.Dispatcher.BeginInvoke(new Action(()=>{themeRefreshQueued=false;if(!disposed)RefreshPanelTheme();}),DispatcherPriority.Background);}
        internal void RefreshPanelTheme()
        {
            if(disposed)return;if(renderedPanelDark==Glass95.Dark){if(renderedDockDark!=Glass95.DockDark){CloseTrayOverflow();renderedDockDark=Glass95.DockDark;foreach(var dock in docks)dock.Rebuild();}return;}
            if(DragReorder.Active||Detail!=null&&!Detail.CanRelease){if(themeRefresh==null){themeRefresh=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(200)};themeRefresh.Tick+=(s,e)=>{if(disposed){themeRefresh.Stop();return;}RefreshPanelTheme();};}themeRefresh.Start();return;}
            if(themeRefresh!=null)themeRefresh.Stop();CloseTrayOverflow();CloseDesktopOverview();renderedPanelDark=Glass95.Dark;renderedDockDark=Glass95.DockDark;Previews.Close();Switcher.Cancel();
            Quick=SwapTheme(Quick,()=>new QuickWindow(this),w=>w.Refresh(Power,Brightness,Audio.Value,Audio.Muted,Audio.Available,Brightness>=0));
            Center=SwapTheme(Center,()=>new NotificationWindow(this),w=>w.Refresh());string detailPage=Detail!=null?Detail.PageKey:null;
            Detail=SwapTheme(Detail,()=>new DetailWindow(this,detailPage),null);Calendar=SwapTheme(Calendar,()=>new CalendarWindow(),null);
            string query=Finder!=null?Finder.Query.Text:null;Finder=SwapTheme(Finder,()=>new SearchWindow(this),w=>{w.Configure(w.Width,w.Height);w.Query.Text=query??"";w.Reload();});
            string drawerQuery=Drawer!=null?Drawer.Search.Text:null,drawerFolder=Drawer!=null?Drawer.OpenFolderKey:null,startFolder=StartMenu!=null?StartMenu.OpenFolderKey:null;bool recentPage=StartMenu!=null&&StartMenu.RecentPage;
            Drawer=SwapTheme(Drawer,()=>new DrawerWindow(this),w=>{w.Configure(w.Width,w.Height);w.Reload();w.RestoreSearch(drawerQuery);if(!String.IsNullOrEmpty(drawerFolder))w.OpenFolder(drawerFolder);});StartMenu=SwapTheme(StartMenu,()=>new StartWindow(this),w=>{w.Configure(w.Width,w.Height);w.Reload();if(!String.IsNullOrEmpty(startFolder))w.OpenPinFolder(startFolder);if(recentPage)w.ShowRecentPage(true);});
            string page=Welcome!=null?Welcome.SettingsPage:null,search=Welcome!=null?Welcome.SearchSettings.Text:null;
            Welcome=SwapTheme(Welcome,()=>new WelcomeWindow(this),w=>{w.UpdateMode();w.SearchSettings.Text=search??"";if(!String.IsNullOrEmpty(page))w.OpenSettings(page);});
            if(hud!=null){hud.Close();hud=null;hudScreen=null;}foreach(var dock in docks)dock.Rebuild();
        }
        static T SwapTheme<T>(T window,Func<T> create,Action<T> prepare) where T:GlassWindow
        {
            if(window==null)return null;bool visible=window.IsVisible,active=window.IsActive;Native.RectI bounds;Native.GetWindowRect(new WindowInteropHelper(window).Handle,out bounds);var screen=System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle);double width=window.Width,height=window.Height;
            var settings=window as WelcomeWindow;if(settings!=null)settings.ReleaseIdle();else window.Close();if(!visible)return null;
            var next=create();next.Width=width;next.Height=height;if(prepare!=null)prepare(next);double scale=Native.PrepareMonitor(next,screen.Bounds);next.ShowAt(screen.Bounds,(bounds.Left-screen.Bounds.Left)/scale,(bounds.Top-screen.Bounds.Top)/scale,width,height);if(active)next.Activate();return next;
        }
    }
}
