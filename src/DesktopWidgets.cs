using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed class DesktopWidgets : IDisposable
    {
        readonly ShellController shell;
        readonly Dictionary<string,DesktopWidgetHost> hosts=new Dictionary<string,DesktopWidgetHost>();
        DesktopWidgetStore store;
        DesktopWidgetGallery gallery;
        IntPtr parent;
        DateTime discoveryAt;
        bool disposed,editing,changing;
        int interfaceVersion;
        DesktopWidgetSlot dragging;
        Rect dragPixels;
        internal ShellController Shell {get{return shell;}}
        internal string Status {get;private set;}
        internal int HostCount {get{return hosts.Count;}}
        internal bool Editing {get{return editing;}}
        internal DesktopWidgetStore Store
        {
            get
            {
                if(store==null)store=new DesktopWidgetStore(Path.Combine(AppLog.Data,"desktop-widgets.json"));
                return store;
            }
        }
        internal DesktopWidgets(ShellController controller)
        {
            shell=controller;
            Preferences.DesktopWidgetsChanged+=PreferenceChanged;
            Status="바탕화면 위젯이 꺼져 있습니다";
        }
        void PreferenceChanged()
        {
            if(disposed)return;
            if(!Preferences.Current.DesktopWidgets)
            {
                SetEditing(false);
                Clear();
                Status="바탕화면 위젯이 꺼져 있습니다";
            }
            else Pulse();
            if(gallery!=null)gallery.RefreshStatus();
        }
        internal void OpenGallery()
        {
            if(disposed)return;
            if(gallery!=null){gallery.Activate();return;}
            gallery=new DesktopWidgetGallery(this);
            gallery.Closed+=(s,e)=>
            {
                gallery=null;
                SetEditing(false);
            };
            gallery.Show();
        }
        internal void SetEditing(bool value)
        {
            editing=value&&Preferences.Current.DesktopWidgets;
            foreach(var host in hosts.Values)host.View.Editing(editing);
            if(!editing&&store!=null)store.Save();
            if(gallery!=null)gallery.RefreshStatus();
        }
        internal void Add(string key,Forms.Screen screen)
        {
            if(disposed||screen==null)return;
            double scale=Native.DropMonitorScale(screen.Bounds);
            var item=Store.Add(key,screen.DeviceName,new Size(screen.WorkingArea.Width/scale,screen.WorkingArea.Height/scale),IconRects(screen,scale));
            RefreshIcons();
            if(item==null)
            {
                Status="위젯은 최대 "+DesktopWidgetStore.Limit+"개까지 추가할 수 있습니다";
                if(gallery!=null)gallery.RefreshStatus();
                return;
            }
            if(!Preferences.Current.DesktopWidgets)Preferences.SetDesktopWidgets(true);
            SetEditing(true);
            Pulse();
        }
        // Windows desktop icons, read in the background on demand and when the desktop is showing.
        List<Rect> iconPixels=new List<Rect>();
        DateTime iconsAt=DateTime.MinValue;
        bool iconsBusy;
        internal int IconCount {get{return iconPixels.Count;}}
        List<Rect> IconRects(Forms.Screen screen,double scale)
        {
            var area=screen.WorkingArea;
            return iconPixels.Where(r=>screen.Bounds.IntersectsWith(new System.Drawing.Rectangle((int)r.X,(int)r.Y,(int)r.Width,(int)r.Height)))
                .Select(r=>new Rect((r.X-area.Left)/scale,(r.Y-area.Top)/scale,r.Width/scale,r.Height/scale)).ToList();
        }
        async void RefreshIcons()
        {
            if(disposed||iconsBusy||!DesktopWidgetNative.Available(parent))return;
            iconsBusy=true;
            iconsAt=DateTime.UtcNow.AddSeconds(30);
            IntPtr view=parent;
            try
            {
                iconPixels=await BackgroundWork.Run(()=>DesktopIcons.Read(view));
                if(!disposed)Settle();
            }
            catch(Exception e)
            {
                AppLog.Once("desktop-icons",e.GetType().Name);
            }
            finally{iconsBusy=false;}
        }
        List<Rect> Blockers(DesktopWidgetSlot item,Forms.Screen screen,double scale)
        {
            var blocked=IconRects(screen,scale);
            var available=new Size(screen.WorkingArea.Width/scale,screen.WorkingArea.Height/scale);
            foreach(var other in Store.Items)
            {
                if(other!=item&&ScreenFor(other).DeviceName==screen.DeviceName)blocked.Add(DesktopWidgetStore.Fit(other,available));
            }
            return blocked;
        }
        // Moves a widget to the nearest free position only when something actually overlaps it.
        bool Clear(DesktopWidgetSlot item,bool always)
        {
            var screen=ScreenFor(item);
            double scale=Native.DropMonitorScale(screen.Bounds);
            var available=new Size(screen.WorkingArea.Width/scale,screen.WorkingArea.Height/scale);
            var fitted=DesktopWidgetStore.Fit(item,available);
            var blocked=Blockers(item,screen,scale);
            if(!always&&!blocked.Any(r=>fitted.IntersectsWith(r)))return false;
            var free=DesktopWidgetStore.FindFree(fitted,blocked,available);
            if(free==fitted&&!always)return false;
            item.X=free.X;
            item.Y=free.Y;
            DesktopWidgetHost host;
            if(hosts.TryGetValue(item.Id,out host))host.Place(Physical(item,screen,scale),scale);
            return true;
        }
        void Settle()
        {
            if(dragging!=null||!Preferences.Current.DesktopWidgets)return;
            bool moved=false;
            foreach(var item in Store.Items)moved|=Clear(item,false);
            if(moved)Store.Save();
        }
        internal void Resize(DesktopWidgetSlot item,string token)
        {
            if(disposed||!InfoTileCatalog.Known(token))return;
            DesktopWidgetHost host;
            if(hosts.TryGetValue(item.Id,out host))
            {
                hosts.Remove(item.Id);
                host.Dispose();
            }
            var size=DesktopWidgetCatalog.Initial(token);
            item.Key=token;
            item.Width=size.Width;
            item.Height=size.Height;
            Clear(item,true);
            Store.Save();
            Pulse();
        }
        void Menu(DesktopWidgetSlot item)
        {
            var menu=new System.Windows.Controls.ContextMenu();
            if(InfoTileCatalog.Known(item.Key))
            {
                var family=InfoTileCatalog.Family(item.Key);
                foreach(string span in family.Spans)
                {
                    string token=family.Key+":"+span;
                    var entry=new System.Windows.Controls.MenuItem {Header="크기 "+InfoTileCatalog.SpanLabel(token),IsChecked=token==item.Key};
                    entry.Click+=(s,e)=>Resize(item,token);
                    menu.Items.Add(entry);
                }
                menu.Items.Add(new System.Windows.Controls.Separator());
            }
            var add=new System.Windows.Controls.MenuItem {Header="위젯 추가"};
            add.Click+=(s,e)=>OpenGallery();
            menu.Items.Add(add);
            var layout=new System.Windows.Controls.MenuItem {Header=editing?"크기 조절 마치기":"크기 자유롭게 조절"};
            layout.Click+=(s,e)=>SetEditing(!editing);
            menu.Items.Add(layout);
            var remove=new System.Windows.Controls.MenuItem {Header="제거"};
            remove.Click+=(s,e)=>Remove(item);
            menu.Items.Add(remove);
            menu.IsOpen=true;
        }
        void Remove(DesktopWidgetSlot item)
        {
            DesktopWidgetHost host;
            if(hosts.TryGetValue(item.Id,out host))
            {
                hosts.Remove(item.Id);
                host.Dispose();
            }
            Store.Items.Remove(item);
            Store.Save();
            Pulse();
            if(gallery!=null)gallery.RefreshStatus();
        }
        static Forms.Screen ScreenFor(DesktopWidgetSlot item)
        {
            return Forms.Screen.AllScreens.FirstOrDefault(s=>String.Equals(s.DeviceName,item.Monitor,StringComparison.OrdinalIgnoreCase))??Forms.Screen.PrimaryScreen;
        }
        internal static Rect Physical(DesktopWidgetSlot item,Forms.Screen screen,double scale)
        {
            var area=screen.WorkingArea;
            var fitted=DesktopWidgetStore.Fit(item,new Size(area.Width/scale,area.Height/scale));
            return new Rect(area.Left+fitted.X*scale,area.Top+fitted.Y*scale,fitted.Width*scale,fitted.Height*scale);
        }
        void Drag(DesktopWidgetSlot item,System.Drawing.Point start,System.Drawing.Point end,bool resize,bool commit)
        {
            if(disposed||changing)return;
            DesktopWidgetHost host;
            if(!hosts.TryGetValue(item.Id,out host))return;
            if(dragging==null||dragging.Id!=item.Id)
            {
                dragging=item.Copy();
                dragPixels=host.Pixels;
                RefreshIcons();
            }
            var screen=resize?ScreenFor(dragging):Forms.Screen.FromPoint(end);
            double scale=Native.DropMonitorScale(screen.Bounds);
            if(resize)
            {
                item.Width=dragging.Width+(end.X-start.X)/scale;
                item.Height=dragging.Height+(end.Y-start.Y)/scale;
            }
            else
            {
                item.Monitor=screen.DeviceName;
                item.X=(dragPixels.X+end.X-start.X-screen.WorkingArea.Left)/scale;
                item.Y=(dragPixels.Y+end.Y-start.Y-screen.WorkingArea.Top)/scale;
            }
            DesktopWidgetStore.Normalize(item);
            var fit=DesktopWidgetStore.Fit(item,new Size(screen.WorkingArea.Width/scale,screen.WorkingArea.Height/scale));
            item.X=fit.X;
            item.Y=fit.Y;
            item.Width=fit.Width;
            item.Height=fit.Height;
            host.Place(Physical(item,screen,scale),scale);
            if(commit)
            {
                dragging=null;
                if(!resize)Clear(item,true);
                Store.Save();
            }
        }
        internal void Pulse()
        {
            if(disposed||!Preferences.Current.DesktopWidgets)return;
            if(interfaceVersion!=Preferences.Current.UIVersion)
            {
                Clear();
                discoveryAt=DateTime.MinValue;
                interfaceVersion=Preferences.Current.UIVersion;
            }
            if(!shell.DisplayAwake)
            {
                foreach(var hidden in hosts.Values)hidden.Present(false);
                return;
            }
            if(!DesktopWidgetNative.Available(parent))
            {
                if(DateTime.UtcNow<discoveryAt)return;
                discoveryAt=DateTime.UtcNow.AddSeconds(5);
                Clear();
                parent=DesktopWidgetNative.FindDesktop();
                if(!DesktopWidgetNative.Available(parent))
                {
                    Status="Windows 바탕화면을 기다리는 중입니다";
                    return;
                }
            }
            IntPtr foreground=Native.GetForegroundWindow();
            Native.RectI foregroundRect=new Native.RectI();
            bool covered=foreground!=IntPtr.Zero&&foreground!=parent&&Native.GetWindowRect(foreground,out foregroundRect);
            var root=Native.GetAncestor(parent,2);
            if(foreground==root)covered=false;
            var bounds=covered?new Rect(foregroundRect.Left,foregroundRect.Top,Math.Max(0,foregroundRect.Right-foregroundRect.Left),Math.Max(0,foregroundRect.Bottom-foregroundRect.Top)):Rect.Empty;
            if((!covered||editing)&&Store.Items.Count>0&&DateTime.UtcNow>=iconsAt)RefreshIcons();
            foreach(var item in Store.Items)
            {
                DesktopWidgetHost host;
                if(hosts.TryGetValue(item.Id,out host)&&!host.Alive)
                {
                    host.Dispose();
                    hosts.Remove(item.Id);
                    host=null;
                }
                if(host==null)
                {
                    try
                    {
                        host=new DesktopWidgetHost(parent,new DesktopWidgetView(shell,item,Remove,Drag,Menu));
                        hosts.Add(item.Id,host);
                        host.View.Editing(editing);
                    }
                    catch(Exception e)
                    {
                        AppLog.Once("desktop-widget-host",e.GetType().Name+": "+e.Message);
                        Status="바탕화면 위젯을 표시하지 못했습니다";
                        return;
                    }
                }
                var screen=ScreenFor(item);
                double scale=Native.DropMonitorScale(screen.Bounds);
                host.Place(Physical(item,screen,scale),scale);
                bool visible=editing||bounds.IsEmpty||!bounds.Contains(host.Pixels);
                host.Present(visible);
                if(visible)host.View.Refresh();
            }
            Status=Store.Items.Count==0?"추가할 위젯을 선택하세요":Store.Items.Count+"개 위젯 · "+(editing?"배치 편집 중":"바탕화면에 표시 중");
        }
        internal void DisplayChanged()
        {
            dragging=null;
            Pulse();
        }
        void Clear()
        {
            changing=true;
            try
            {
                foreach(var host in hosts.Values)host.Dispose();
                hosts.Clear();
                parent=IntPtr.Zero;
                dragging=null;
            }
            finally{changing=false;}
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            Preferences.DesktopWidgetsChanged-=PreferenceChanged;
            if(gallery!=null)gallery.Close();
            Clear();
        }
    }
}
