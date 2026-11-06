using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
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
        bool disposed,changing;
        string selectedId;
        Popup toolbar;
        FrameworkElement toolbarBody;
        DispatcherTimer selectionWatch;
        IntPtr selectedForeground;
        int interfaceVersion;
        DesktopWidgetSlot dragging;
        Rect dragPixels;
        internal ShellController Shell {get{return shell;}}
        internal string Status {get;private set;}
        internal int HostCount {get{return hosts.Count;}}
        internal bool Editing {get{return selectedId!=null;}}
        internal string SelectedId {get{return selectedId;}}
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
            Preferences.WidgetMaterialChanged+=MaterialChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged+=SystemPreferenceChanged;
            Status="바탕화면 위젯이 꺼져 있습니다";
            DesktopMenuEntry.Sync();
        }
        // The first time widgets are turned on with nothing placed, offer the tidy default arrangement.
        internal int SeedDefault(bool replace)
        {
            if(disposed)return 0;
            var screen=Forms.Screen.PrimaryScreen;
            double scale=Native.DropMonitorScale(screen.Bounds);
            var available=new Size(Work(screen).Width/scale,Work(screen).Height/scale);
            var layout=DesktopWidgetLayouts.Default(available,shell.Power.Known,WeatherService.Configured);
            if(layout.Count==0)return 0;
            Deselect();
            if(replace)
            {
                Clear();
                Store.Items.Clear();
            }
            foreach(var entry in layout)Store.Place(entry.Key,MonitorIdentity.Of(screen),entry.Value);
            Store.Save();
            Preferences.SetWidgetMaterial(p=>p.WidgetsSeeded=true);
            discoveryAt=DateTime.MinValue;
            if(!Preferences.Current.DesktopWidgets)Preferences.SetDesktopWidgets(true);
            else Pulse();
            if(gallery!=null)gallery.RefreshStatus();
            return layout.Count;
        }
        void PreferenceChanged()
        {
            if(disposed)return;
            if(!Preferences.Current.DesktopWidgets)
            {
                Deselect();
                Clear();
                Status="바탕화면 위젯이 꺼져 있습니다";
            }
            else
            {
                if(!Preferences.Current.WidgetsSeeded&&Store.Items.Count==0)SeedDefault(false);
                else if(!Preferences.Current.WidgetsSeeded)Preferences.SetWidgetMaterial(p=>p.WidgetsSeeded=true);
                Pulse();
            }
            if(gallery!=null)gallery.RefreshStatus();
        }
        internal void OpenGallery(System.Drawing.Point? anchor=null)
        {
            if(disposed)return;
            if(gallery!=null){gallery.Anchor=anchor;gallery.Activate();return;}
            gallery=new DesktopWidgetGallery(this) {Anchor=anchor};
            gallery.Closed+=(s,e)=>
            {
                gallery=null;
            };
            gallery.Show();
        }
        readonly Dictionary<string,Brush> wallpapers=new Dictionary<string,Brush>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> wallpapersLoading=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DispatcherTimer materialDelay;
        bool tintedInk;
        internal int MaterialRenders {get;private set;}
        void SystemPreferenceChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if(e.Category!=Microsoft.Win32.UserPreferenceCategory.Desktop)return;
            Application.Current.Dispatcher.BeginInvoke(new Action(()=>
            {
                if(disposed)return;
                wallpapers.Clear();
                PaintAll();
            }));
        }
        // Slider drags arrive quickly, and each one can redraw every widget.
        void MaterialChanged()
        {
            if(disposed)return;
            DesktopMenuEntry.Sync();
            if(materialDelay==null)
            {
                materialDelay=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(150)};
                materialDelay.Tick+=(s,e)=>
                {
                    materialDelay.Stop();
                    ApplyMaterial();
                };
            }
            materialDelay.Stop();
            materialDelay.Start();
        }
        internal void ApplyMaterial()
        {
            if(disposed)return;
            bool tinted=WidgetMaterial.Tinted;
            if(tinted!=tintedInk)
            {
                tintedInk=tinted;
                // Text color is chosen when a widget is built, so crossing the line rebuilds them.
                Deselect();
                Clear();
                discoveryAt=DateTime.MinValue;
                Pulse();
                return;
            }
            PaintAll();
        }
        void PaintAll()
        {
            foreach(var item in Store.Items)
            {
                DesktopWidgetHost host;
                if(hosts.TryGetValue(item.Id,out host))PaintWidget(item,host);
            }
        }
        async void LoadWallpaper(Forms.Screen screen)
        {
            string device=screen.DeviceName;
            if(!wallpapersLoading.Add(device))return;
            var bounds=screen.Bounds;
            try
            {
                var snapshot=await BackgroundWork.Run(()=>DesktopWallpaper.Read(bounds));
                if(disposed)return;
                wallpapers[device]=snapshot.Brush;
                if(tintedInk)
                {
                    Clear();
                    discoveryAt=DateTime.MinValue;
                    Pulse();
                }
                else PaintAll();
            }
            catch(Exception e)
            {
                AppLog.Once("widget-wallpaper",e.GetType().Name);
            }
            finally{wallpapersLoading.Remove(device);}
        }
        Brush WallpaperFor(Forms.Screen screen)
        {
            Brush wallpaper;
            if(wallpapers.TryGetValue(screen.DeviceName,out wallpaper))return wallpaper;
            LoadWallpaper(screen);
            return null;
        }
        void PaintWidget(DesktopWidgetSlot item,DesktopWidgetHost host)
        {
            bool glass=WidgetMaterial.Glass;
            int opacity=glass?60:Preferences.Current.WidgetOpacity,blur=WidgetMaterial.Blur;
            var screen=ScreenFor(item);
            Brush picture=null;
            if(blur>0)
            {
                var wallpaper=WallpaperFor(screen);
                double scale=Native.DropMonitorScale(screen.Bounds);
                picture=WidgetMaterial.Blurred(wallpaper,new Rect(screen.Bounds.X,screen.Bounds.Y,screen.Bounds.Width,screen.Bounds.Height),Physical(item,screen,scale),blur*scale);
                if(picture!=null)MaterialRenders++;
            }
            host.View.Material(picture,opacity,glass);
        }
        TileScheme SchemeFor(DesktopWidgetSlot item)
        {
            if(!WidgetMaterial.Tinted)return TileScheme.Desktop();
            var wallpaper=WallpaperFor(ScreenFor(item));
            return wallpaper!=null?TileScheme.FromWallpaper(wallpaper):TileScheme.Desktop();
        }
        internal bool ToolbarOpen {get{return toolbar!=null&&toolbar.IsOpen;}}
        internal string ToolbarWidget {get{return selectedId;}}
        // One widget is edited at a time, as on a phone home screen.
        internal void Select(DesktopWidgetSlot item)
        {
            if(disposed||!Preferences.Current.DesktopWidgets||!Store.Items.Contains(item))return;
            selectedId=item.Id;
            foreach(var pair in hosts)pair.Value.View.Editing(pair.Key==selectedId);
            ShowToolbar(item);
            if(selectionWatch==null)
            {
                selectionWatch=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(120)};
                selectionWatch.Tick+=(s,e)=>WatchSelection();
            }
            DesktopWidgetNative.ClickedSinceLastCheck();
            selectedForeground=Native.GetForegroundWindow();
            selectionWatch.Start();
            RefreshIcons();
        }
        internal void Deselect()
        {
            if(selectionWatch!=null)selectionWatch.Stop();
            CloseToolbar();
            if(selectedId==null)return;
            selectedId=null;
            foreach(var host in hosts.Values)host.View.Editing(false);
            if(store!=null)store.Save();
        }
        // The timer exists only while a widget is selected: Explorer owns clicks on the empty desktop.
        void WatchSelection()
        {
            var at=Forms.Cursor.Position;
            CheckSelection(DesktopWidgetNative.ClickedSinceLastCheck(),new Point(at.X,at.Y),Native.GetForegroundWindow());
        }
        internal void CheckSelection(bool clicked,Point point,IntPtr foreground)
        {
            if(selectedId==null||dragging!=null)return;
            // The toolbar floats above other apps, so editing cannot outlive a switch to one of them.
            if(foreground!=selectedForeground)
            {
                selectedForeground=foreground;
                if(ForeignApp(foreground))
                {
                    Deselect();
                    return;
                }
            }
            if(!clicked)return;
            DesktopWidgetHost host;
            if(hosts.TryGetValue(selectedId,out host)&&host.Pixels.Contains(point))return;
            if(InToolbar(point))return;
            Deselect();
        }
        bool ForeignApp(IntPtr window)
        {
            if(window==IntPtr.Zero||window==parent||window==Native.GetAncestor(parent,2))return false;
            uint process;
            Native.GetWindowThreadProcessId(window,out process);
            return process!=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }
        bool InToolbar(Point point)
        {
            if(toolbar==null||!toolbar.IsOpen||toolbarBody==null)return false;
            var source=PresentationSource.FromVisual(toolbarBody);
            if(source==null||source.CompositionTarget==null)return false;
            var scale=source.CompositionTarget.TransformToDevice;
            var corner=toolbarBody.PointToScreen(new Point(0,0));
            return new Rect(corner.X,corner.Y,toolbarBody.ActualWidth*scale.M11,toolbarBody.ActualHeight*scale.M22).Contains(point);
        }
        void CloseToolbar()
        {
            if(toolbar!=null)toolbar.IsOpen=false;
            toolbar=null;
            toolbarBody=null;
        }
        void ShowToolbar(DesktopWidgetSlot item)
        {
            CloseToolbar();
            DesktopWidgetHost host;
            if(!hosts.TryGetValue(item.Id,out host))return;
            var scheme=TileScheme.Desktop();
            var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(6)};
            Func<string,Action,bool,Button> chip=(text,action,chosen)=>
            {
                var button=Paint.Button(Paint.TextBlock(text,13,scheme.Ink),action,text);
                button.Background=chosen?scheme.Track:Brushes.Transparent;
                button.Padding=new Thickness(12,7,12,7);
                button.Resources["ButtonRadius"]=new CornerRadius(16);
                row.Children.Add(button);
                return button;
            };
            if(InfoTileCatalog.Known(item.Key))
            {
                var family=InfoTileCatalog.Family(item.Key);
                foreach(string span in family.Spans)
                {
                    string token=family.Key+":"+span;
                    chip(InfoTileCatalog.SpanLabel(token),()=>Resize(item,token),token==item.Key);
                }
            }
            chip("위젯 추가",()=>OpenGallery(),false);
            chip("제거",()=>Remove(item),false);
            chip("완료",Deselect,false);
            toolbarBody=new Border {Background=scheme.Surface,CornerRadius=new CornerRadius(22),BorderBrush=scheme.Track,BorderThickness=new Thickness(1),Child=row};
            toolbar=new Popup {PlacementTarget=host.View,Placement=PlacementMode.Top,VerticalOffset=-8,AllowsTransparency=true,StaysOpen=true,Child=toolbarBody};
            toolbar.IsOpen=true;
        }
        // The tile closest to the dragged size, by width and height, so a drag steps between real sizes.
        internal static string NearestSize(InfoTileFamily family,double width,double height)
        {
            string best=null;
            double distance=Double.MaxValue;
            foreach(string span in family.Spans)
            {
                string token=family.Key+":"+span;
                var design=InfoTileCatalog.Design(token);
                double gap=Math.Abs(Math.Log(Math.Max(1,width)/design.Width))+Math.Abs(Math.Log(Math.Max(1,height)/design.Height));
                if(gap>=distance)continue;
                distance=gap;
                best=token;
            }
            return best;
        }
        // A resize ends on a real size, never a stretched one, so equal widgets are equal.
        void Snap(DesktopWidgetSlot item)
        {
            if(InfoTileCatalog.Known(item.Key))
            {
                string token=NearestSize(InfoTileCatalog.Family(item.Key),item.Width,item.Height);
                if(token!=item.Key)
                {
                    DesktopWidgetHost host;
                    if(hosts.TryGetValue(item.Id,out host))
                    {
                        hosts.Remove(item.Id);
                        host.Dispose();
                    }
                    item.Key=token;
                }
            }
            DesktopWidgetStore.Canonicalize(item);
            Clear(item,false);
            Pulse();
        }
        // Where a widget was asked for from the desktop menu: the new widget starts there.
        internal void Add(string key,Forms.Screen screen,System.Drawing.Point? anchor=null)
        {
            if(disposed||screen==null)return;
            double scale=Native.DropMonitorScale(screen.Bounds);
            Point? near=anchor.HasValue?(Point?)new Point((anchor.Value.X-Work(screen).Left)/scale,(anchor.Value.Y-Work(screen).Top)/scale):null;
            var item=Store.Add(key,MonitorIdentity.Of(screen),new Size(Work(screen).Width/scale,Work(screen).Height/scale),IconRects(screen,scale),near);
            RefreshIcons();
            if(item==null)
            {
                Status="위젯은 최대 "+DesktopWidgetStore.Limit+"개까지 추가할 수 있습니다";
                if(gallery!=null)gallery.RefreshStatus();
                return;
            }
            if(!Preferences.Current.DesktopWidgets)Preferences.SetDesktopWidgets(true);
            Pulse();
            Select(item);
        }
        // Windows desktop icons, read in the background on demand and when the desktop is showing.
        List<Rect> iconPixels=new List<Rect>();
        DateTime iconsAt=DateTime.MinValue;
        bool iconsBusy;
        internal int IconCount {get{return iconPixels.Count;}}
        List<Rect> IconRects(Forms.Screen screen,double scale)
        {
            var area=Work(screen);
            return iconPixels.Where(r=>screen.Bounds.IntersectsWith(new System.Drawing.Rectangle((int)r.X,(int)r.Y,(int)r.Width,(int)r.Height)))
                .Select(r=>new Rect((r.X-area.Left)/scale,(r.Y-area.Top)/scale,r.Width/scale,r.Height/scale)).ToList();
        }
        async void RefreshIcons()
        {
            if(disposed||iconsBusy||!DesktopWidgetNative.Available(parent))return;
            iconsBusy=true;
            iconsAt=DateTime.UtcNow.AddSeconds(120);
            IntPtr view=parent;
            int revision=displayRevision;
            try
            {
                iconPixels=await BackgroundWork.Run(()=>DesktopIcons.Read(view));
                // Icon positions read while the monitors were changing describe a layout that is gone. Tidying widgets
                // around them moved widgets for good, so a read that overlapped a change is read again instead.
                if(!disposed&&revision==displayRevision)Settle();
                else if(!disposed)iconsAt=DateTime.UtcNow.AddSeconds(2);
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
            var available=new Size(Work(screen).Width/scale,Work(screen).Height/scale);
            foreach(var other in Store.Items)
            {
                if(other!=item&&OnScreen(other)&&MonitorIdentity.Of(ScreenFor(other))==MonitorIdentity.Of(screen))blocked.Add(DesktopWidgetStore.Fit(other,available));
            }
            return blocked;
        }
        // Moves a widget to the nearest free position only when something actually overlaps it.
        bool Clear(DesktopWidgetSlot item,bool always)
        {
            if(!OnScreen(item))return false;
            var screen=ScreenFor(item);
            double scale=Native.DropMonitorScale(screen.Bounds);
            var available=new Size(Work(screen).Width/scale,Work(screen).Height/scale);
            var fitted=DesktopWidgetStore.Fit(item,available);
            var blocked=Blockers(item,screen,scale);
            if(!always&&!blocked.Any(r=>fitted.IntersectsWith(r)))return false;
            var free=DesktopWidgetStore.FindFree(fitted,blocked,available);
            if(free==fitted&&!always)return false;
            item.X=free.X;
            item.Y=free.Y;
            DesktopWidgetHost host;
            if(hosts.TryGetValue(item.Id,out host))
            {
                host.Place(Physical(item,screen,scale),scale);
                PaintWidget(item,host);
            }
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
            item.Key=token;
            var size=DesktopWidgetCatalog.Initial(token);
            item.Width=size.Width;
            item.Height=size.Height;
            DesktopWidgetStore.Canonicalize(item);
            Clear(item,true);
            Store.Save();
            Pulse();
            if(selectedId==item.Id)ShowToolbar(item);
        }
        internal void Remove(DesktopWidgetSlot item)
        {
            if(selectedId==item.Id)Deselect();
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
        // The monitor's work area as the system reports it now. A cached Screen keeps the old one when the dock
        // reserves a different edge, which left widgets under the dock after it moved to a side.
        internal static System.Drawing.Rectangle Work(Forms.Screen screen)
        {
            var area=Native.WorkArea(screen);
            return area.Right>area.Left&&area.Bottom>area.Top?new System.Drawing.Rectangle(area.Left,area.Top,area.Right-area.Left,area.Bottom-area.Top):screen.WorkingArea;
        }
        // A widget whose monitor is not connected is simply not shown. It keeps its saved place and returns with the monitor.
        static bool OnScreen(DesktopWidgetSlot item){return MonitorIdentity.Find(item.Monitor)!=null;}
        static Forms.Screen ScreenFor(DesktopWidgetSlot item)
        {
            return MonitorIdentity.Find(item.Monitor)??Forms.Screen.PrimaryScreen;
        }
        internal static Rect Physical(DesktopWidgetSlot item,Forms.Screen screen,double scale)
        {
            var area=Work(screen);
            var fitted=DesktopWidgetStore.Fit(item,new Size(area.Width/scale,area.Height/scale));
            // The size is rounded on its own, so equal widgets are equal pixels wherever they sit.
            return new Rect(area.Left+Math.Round(fitted.X*scale),area.Top+Math.Round(fitted.Y*scale),Math.Round(fitted.Width*scale),Math.Round(fitted.Height*scale));
        }
        internal void Drag(DesktopWidgetSlot item,System.Drawing.Point start,System.Drawing.Point end,bool resize,bool commit)
        {
            if(disposed||changing)return;
            DesktopWidgetHost host;
            if(!hosts.TryGetValue(item.Id,out host))return;
            if(dragging==null||dragging.Id!=item.Id)
            {
                dragging=item.Copy();
                dragPixels=host.Pixels;
                if(toolbar!=null)toolbar.IsOpen=false;
                RefreshIcons();
            }
            var screen=resize?ScreenFor(dragging):Forms.Screen.FromPoint(end);
            double scale=Native.DropMonitorScale(screen.Bounds);
            if(resize)
            {
                int corner=host.View.ResizeCorner;
                bool fromLeft=corner==2||corner==4,fromTop=corner==3||corner==4;
                double dx=(end.X-start.X)/scale,dy=(end.Y-start.Y)/scale;
                item.Width=dragging.Width+(fromLeft?-dx:dx);
                item.Height=dragging.Height+(fromTop?-dy:dy);
                DesktopWidgetStore.Normalize(item);
                if(fromLeft)item.X=dragging.X+dragging.Width-item.Width;
                if(fromTop)item.Y=dragging.Y+dragging.Height-item.Height;
            }
            else
            {
                item.Monitor=MonitorIdentity.Of(screen);
                item.X=(dragPixels.X+end.X-start.X-Work(screen).Left)/scale;
                item.Y=(dragPixels.Y+end.Y-start.Y-Work(screen).Top)/scale;
            }
            DesktopWidgetStore.Normalize(item);
            var fit=DesktopWidgetStore.Fit(item,new Size(Work(screen).Width/scale,Work(screen).Height/scale));
            item.X=fit.X;
            item.Y=fit.Y;
            item.Width=fit.Width;
            item.Height=fit.Height;
            host.Place(Physical(item,screen,scale),scale);
            if(commit)
            {
                dragging=null;
                RepaintMoved(item);
                if(resize)Snap(item);
                else Clear(item,true);
                Store.Save();
                if(selectedId==item.Id)ShowToolbar(item);
            }
        }
        internal void Pulse()
        {
            if(disposed||!Preferences.Current.DesktopWidgets)return;
            if(tintedInk!=WidgetMaterial.Tinted)ApplyMaterial();
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
            if(!covered&&Store.Items.Count>0&&DateTime.UtcNow>=iconsAt)RefreshIcons();
            // Explorer can restack its icon list, for example after a refresh.
            if(DesktopWidgetNative.IconsOnTop(parent))
            {
                foreach(var above in hosts.Values)above.Raise();
            }
            MigrateMonitors();
            foreach(var item in Store.Items)
            {
                DesktopWidgetHost host;
                if(!OnScreen(item))
                {
                    // Its monitor is not connected: leave the saved place alone and show it again when the monitor returns.
                    DesktopWidgetHost away;
                    if(hosts.TryGetValue(item.Id,out away))away.Present(false);
                    continue;
                }
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
                        host=new DesktopWidgetHost(parent,new DesktopWidgetView(shell,item,Drag,Select,SchemeFor(item)));
                        PaintWidget(item,host);
                        hosts.Add(item.Id,host);
                        host.View.Editing(item.Id==selectedId);
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
                // A window over a widget covers it by itself. Hiding the widget as well made it flash away and back
                // as windows moved, so it stays up; only the redraw is skipped while a window fully hides it.
                bool hidden=item.Id!=selectedId&&!bounds.IsEmpty&&bounds.Contains(host.Pixels);
                host.Present(true);
                if(!hidden)host.View.Refresh();
            }
            Status=Store.Items.Count==0?"추가할 위젯을 선택하세요":Store.Items.Count+"개 위젯 · "+"바탕화면에 표시 중";
        }
        void RepaintMoved(DesktopWidgetSlot item)
        {
            DesktopWidgetHost moved;
            if(hosts.TryGetValue(item.Id,out moved))PaintWidget(item,moved);
        }
        internal void DisplayChanged()
        {
            MonitorIdentity.Refresh();
            displayRevision++;
            iconsAt=DateTime.UtcNow.AddSeconds(2);
            wallpapers.Clear();
            dragging=null;
            Pulse();
        }
        int displayRevision;
        bool monitorsMigrated;
        // Placements saved before monitors had identities name a display number. They take the identity of the monitor
        // that has that number now, or of the main monitor if it is gone, once.
        void MigrateMonitors()
        {
            if(monitorsMigrated)return;
            monitorsMigrated=true;
            bool changed=false;
            foreach(var item in Store.Items)
            {
                if(item.Monitor!=null&&item.Monitor.StartsWith("id:",StringComparison.Ordinal))continue;
                item.Monitor=MonitorIdentity.Of(MonitorIdentity.Find(item.Monitor)??Forms.Screen.PrimaryScreen);
                changed=true;
            }
            if(changed)Store.Save();
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
                CloseToolbar();
                selectedId=null;
                if(selectionWatch!=null)selectionWatch.Stop();
            }
            finally{changing=false;}
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            Preferences.DesktopWidgetsChanged-=PreferenceChanged;
            Preferences.WidgetMaterialChanged-=MaterialChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged-=SystemPreferenceChanged;
            if(materialDelay!=null)materialDelay.Stop();
            if(gallery!=null)gallery.Close();
            Clear();
        }
    }
}
