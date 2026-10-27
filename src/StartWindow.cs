using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // The home page of the launcher. All app enumeration stays in the existing drawer.
    internal sealed partial class StartWindow : GlassWindow
    {
        readonly ShellController shell;
        readonly UniformGrid pins=new UniformGrid {Columns=6},recent=new UniformGrid {Columns=2};
        readonly TextBlock page=Paint.TextBlock("",11,Paint.Muted);
        readonly Button previous,next;
        readonly ColumnDefinition phoneColumn;internal readonly PhonePane Phone;
        PhoneSnapshot phoneSnapshot;DateTime phoneUntil;bool phoneBusy;
        internal readonly TextBox Search=new TextBox {FontSize=14,Foreground=Paint.Ink,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(6,9,6,9),MaxLength=120};
        List<StartTile> pinned=new List<StartTile>();List<StartRecent> cachedRecent;
        readonly Grid pinHeading,folderHeading;readonly TextBox folderName;readonly Grid recentHeading;
        readonly Button recentMore;bool moreRecent;
        readonly StackPanel body=new StackPanel(),pagination;readonly SmoothScrollViewer bodyScroll;
        readonly DockPanel launcherRoot;System.Windows.Forms.Screen autoScreen;double autoScale=1,preferredHomeHeight=620;bool fitting;
        internal SmoothScrollViewer ContentScroll {get{return bodyScroll;}}
        internal UniformGrid RecentItems {get{return recent;}}
        internal Button RecentMoreButton {get{return recentMore;}}
        internal bool RecentPage {get{return moreRecent;}}
        readonly Dictionary<string,Button> pinButtons=new Dictionary<string,Button>(StringComparer.OrdinalIgnoreCase);string dragPreview,groupTarget,openFolder;bool renaming;
        DateTime recentUntil;bool recentBusy,composing,queued,closed,outsideFixture;int pinPage,load;IDisposable outsideClick;
        internal int PinnedCount {get{return pinButtons.Count;}}
        internal int RecentCount {get{return recent.Children.Count;}}
        internal int FolderCount {get{return shell.StartPins.FolderCount;}}
        internal string OpenFolderKey {get{return openFolder;}}
        internal TextBox FolderNameEditor {get{return folderName;}}
        internal string LastPinDrop;
        internal StartWindow(ShellController owner)
        {
            shell=owner;Title="One TaskBar · 시작";Width=940;Height=620;Radius=32;Dark=true;StableSurface=true;SmoothLauncher=true;
            Resources[SystemParameters.VerticalScrollBarWidthKey]=4.0;
            Surface.CornerRadius=new CornerRadius(Radius);Surface.Background=QuickPanel.Glass;Surface.BorderBrush=Paint.Brush("#58FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var root=new DockPanel {Margin=new Thickness(24,23,24,16)};
            launcherRoot=root;
            var searchBox=LauncherSearchField.Create(Search,()=>{Search.Clear();Search.Focus();});searchBox.Margin=new Thickness(0,0,0,22);
            Search.TextChanged+=(s,e)=> {if(!composing)QueueSearch();};
            TextCompositionManager.AddPreviewTextInputStartHandler(Search,(s,e)=>composing=true);TextCompositionManager.AddPreviewTextInputUpdateHandler(Search,(s,e)=>composing=true);Search.PreviewTextInput+=(s,e)=> {composing=false;QueueSearch();};
            Search.PreviewMouseLeftButtonDown+=(s,e)=> {if(Search.Text.Length==0){shell.ToggleSearch(null);e.Handled=true;}};
            DockPanel.SetDock(searchBox,Dock.Top);root.Children.Add(searchBox);

            var footer=new Grid {Height=52,Margin=new Thickness(0,14,0,0)};
            var userRow=new StackPanel {Orientation=Orientation.Horizontal};userRow.Children.Add(new Glyph("user",20){Margin=new Thickness(0,0,9,0)});userRow.Children.Add(Paint.TextBlock(Environment.UserName,12,Paint.Text));
            var user=Paint.Button(userRow,()=>OpenFolder(Environment.SpecialFolder.UserProfile),"사용자 폴더 열기");user.HorizontalAlignment=HorizontalAlignment.Left;user.MaxWidth=240;user.Background=Brushes.Transparent;footer.Children.Add(user);
            var tools=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            tools.Children.Add(Tool("folder","문서",()=>OpenFolder(Environment.SpecialFolder.MyDocuments)));
            tools.Children.Add(Tool("phone","휴대폰과 연결",()=>OpenPhone("ms-settings:mobile-devices-addphone-direct")));
            tools.Children.Add(Tool("home","One TaskBar 설정",async()=> {if(await DismissAsync())shell.OpenHomeSettings();}));
            Button power=null;power=Tool("power","전원",()=> {power.ContextMenu=PowerMenu(power);power.ContextMenu.IsOpen=true;});tools.Children.Add(power);footer.Children.Add(tools);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);

            pinHeading=Heading("고정됨");var all=Paint.Action("모든 앱  ›",()=>shell.ToggleDrawer(null));all.HorizontalAlignment=HorizontalAlignment.Right;all.FontSize=11;all.Height=31;pinHeading.Children.Add(all);body.Children.Add(pinHeading);
            folderHeading=new Grid {Height=40,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,7)};folderHeading.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});folderHeading.ColumnDefinitions.Add(new ColumnDefinition());
            var back=Tool("back","고정 앱으로 돌아가기 · 앱을 놓으면 폴더 밖으로 이동",()=> {CommitFolderName();openFolder=null;pinPage=0;RenderPins();});folderHeading.Children.Add(back);
            folderName=new TextBox {FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=Paint.Text,Background=Brushes.Transparent,BorderThickness=new Thickness(0),MaxLength=48,Padding=new Thickness(6,7,6,7),ToolTip="폴더 이름 변경"};Grid.SetColumn(folderName,1);folderHeading.Children.Add(folderName);body.Children.Add(folderHeading);
            folderName.LostKeyboardFocus+=(s,e)=>CommitFolderName();folderName.PreviewKeyDown+=(s,e)=> {if(e.Key==Key.Enter){CommitFolderName();Keyboard.ClearFocus();e.Handled=true;}else if(e.Key==Key.Escape){openFolder=null;RenderPins();e.Handled=true;}};
            DragReorder.DropArea(folderHeading,"start.pins",()=>openFolder!=null,(key,point)=> {bool moved=shell.StartPins.Extract(key);if(moved)openFolder=null;return moved;},(key,point)=>dragPreview="extract");
            pins.Margin=new Thickness(0,7,0,0);body.Children.Add(pins);
            DragReorder.DropArea(pins,"start.pins",()=>!closed&&IsVisible,DropPin,PreviewPinDrop,true);
            pagination=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Height=28};
            previous=Tool("back","이전 고정 앱",()=> {pinPage--;RenderPins();LauncherMotion.Page(bodyScroll);});previous.Width=28;previous.Height=26;
            next=Tool("chevron","다음 고정 앱",()=> {pinPage++;RenderPins();LauncherMotion.Page(bodyScroll);});next.Width=28;next.Height=26;pagination.Children.Add(previous);page.Margin=new Thickness(7,0,7,0);pagination.Children.Add(page);pagination.Children.Add(next);body.Children.Add(pagination);
            recentHeading=Heading("최근 항목");recentHeading.Margin=new Thickness(8,14,8,9);recentMore=Paint.Action("더 보기  ›",()=>ShowRecentPage(!moreRecent));recentMore.HorizontalAlignment=HorizontalAlignment.Right;recentMore.FontSize=11;recentMore.Height=31;recentHeading.Children.Add(recentMore);body.Children.Add(recentHeading);
            recent.Margin=new Thickness(0,0,0,3);body.Children.Add(recent);
            bodyScroll=new SmoothScrollViewer {Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(bodyScroll);
            var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition());phoneColumn=new ColumnDefinition {Width=new GridLength(278)};layout.ColumnDefinitions.Add(phoneColumn);layout.Children.Add(root);Phone=new PhonePane(OpenPhone,()=>LoadPhone(true)){Margin=new Thickness(0,18,18,18)};Grid.SetColumn(Phone,1);layout.Children.Add(Phone);
            Surface.Child=layout;TransitionContent=layout;
            SizeChanged+=(s,e)=>UpdatePinColumns();
            PreviewTextInput+=(s,e)=> {if(!Search.IsKeyboardFocusWithin&&!folderName.IsKeyboardFocusWithin&&!String.IsNullOrEmpty(e.Text)&&(Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Alt))==0){Search.Text+=e.Text;QueueSearch();e.Handled=true;}};
            Deactivated+=(s,e)=> {if(!Program.Inspectable&&!DragReorder.Active&&IsVisible&&!IsMouseOver&&!(power.ContextMenu!=null&&power.ContextMenu.IsOpen)&&!pinButtons.Values.Any(b=>b.ContextMenu!=null&&b.ContextMenu.IsOpen))Hide();};
            IsVisibleChanged+=(s,e)=>SyncOutsideClick();PreviewMouseLeftButtonUp+=(s,e)=>Dispatcher.BeginInvoke(new Action(FlushRetainedUpdates),System.Windows.Threading.DispatcherPriority.Background);DragReorder.Finished+=FinishPinDrag;Closed+=(s,e)=> {closed=true;++load;StopOutsideClick();DragReorder.Finished-=FinishPinDrag;};
        }
        void StopOutsideClick(){if(outsideClick!=null){outsideClick.Dispose();outsideClick=null;}}
        void SyncOutsideClick(){StopOutsideClick();if(IsVisible&&!closed&&(!Program.Inspectable||outsideFixture))outsideClick=MenuDismiss.Watch(ContainsStartPoint,()=>{if(IsVisible&&!IsDismissing)Hide();});}
        bool ContainsStartPoint(Native.PointI point){if(!IsVisible||IsDismissing||DragReorder.Active||MenuDismiss.ContainsMenu(point))return true;Native.RectI bounds;return Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(this).Handle,out bounds)&&point.X>=bounds.Left&&point.X<bounds.Right&&point.Y>=bounds.Top&&point.Y<bounds.Bottom;}
        internal void ObserveOutsideForFixture(){if(!Program.Inspectable)throw new InvalidOperationException("Start outside-click fixture only");outsideFixture=true;SyncOutsideClick();}
        static Button Tool(string glyph,string label,Action action){var button=Paint.Button(new Glyph(glyph,20),action,label);button.Background=Brushes.Transparent;button.Padding=new Thickness(9);button.Width=button.Height=40;return button;}
        static Grid Heading(string text){var row=new Grid {Height=34,Margin=new Thickness(8,0,8,0)};var title=Paint.TextBlock(text,16,Paint.Text);title.FontWeight=FontWeights.SemiBold;row.Children.Add(title);return row;}
        void QueueSearch(){if(queued||Search.Text.Length==0)return;queued=true;Dispatcher.BeginInvoke(new Action(()=> {queued=false;if(!composing&&IsVisible&&!String.IsNullOrWhiteSpace(Search.Text))shell.ToggleSearch(null,Search.Text);}),System.Windows.Threading.DispatcherPriority.Background);}
        internal void Reload()
        {
            Search.Text="";openFolder=null;pinPage=0;moreRecent=false;RefreshPins();
            if(!StartRecentFeed.Enabled()){cachedRecent=null;recentUntil=DateTime.MinValue;}RenderRecent(cachedRecent??new List<StartRecent>());LoadRecent();if(Phone.Visibility==Visibility.Visible)LoadPhone();
        }
        internal void ShowRecentPage(bool value){moreRecent=value;UpdateRecentPage();RenderRecent(cachedRecent??new List<StartRecent>());bodyScroll.ScrollToTop();LauncherMotion.Page(bodyScroll);}
        void UpdateRecentPage(){pins.Visibility=moreRecent?Visibility.Collapsed:Visibility.Visible;pagination.Visibility=!moreRecent&&CurrentTiles().Count>18?Visibility.Visible:Visibility.Collapsed;pinHeading.Visibility=!moreRecent&&openFolder==null?Visibility.Visible:Visibility.Collapsed;folderHeading.Visibility=!moreRecent&&openFolder!=null?Visibility.Visible:Visibility.Collapsed;recentHeading.Visibility=recent.Visibility=openFolder==null?Visibility.Visible:Visibility.Collapsed;recentHeading.Margin=new Thickness(8,moreRecent?0:14,8,9);}
        protected override void OnPreviewKeyDown(KeyEventArgs e){if(e.Key==Key.Escape&&moreRecent){ShowRecentPage(false);e.Handled=true;return;}base.OnPreviewKeyDown(e);}
        internal void Configure(double width,double height){Width=width;Height=height;bool wide=width>=830;phoneColumn.Width=new GridLength(wide?278:0);Phone.Visibility=wide?Visibility.Visible:Visibility.Collapsed;UpdatePinColumns();}
        internal double PreferredHomeHeight()
        {
            if(moreRecent||openFolder!=null)return preferredHomeHeight;
            launcherRoot.Measure(new Size(Math.Max(1,Width-phoneColumn.Width.Value-Surface.BorderThickness.Left-Surface.BorderThickness.Right),Double.PositiveInfinity));
            double fourRows=4*(56+3)+recent.Margin.Top+recent.Margin.Bottom;preferredHomeHeight=Math.Max(620,Math.Ceiling(launcherRoot.DesiredSize.Height+Math.Max(0,fourRows-recent.DesiredSize.Height)+Surface.BorderThickness.Top+Surface.BorderThickness.Bottom));launcherRoot.InvalidateMeasure();return preferredHomeHeight;
        }
        internal void ShowForMonitor(System.Windows.Forms.Screen screen,double scale){autoScreen=screen;autoScale=scale;FitWindow(true);}
        void FitWindow(bool show)
        {
            if(fitting||closed||autoScreen==null)return;fitting=true;try{double oldWidth=Width,oldHeight=Height;var work=Native.WorkArea(autoScreen);var limits=StartMenuLayout.Fit(autoScreen.Bounds,work,autoScale,620);Configure(limits.Width,Height);var fit=StartMenuLayout.Fit(autoScreen.Bounds,work,autoScale,PreferredHomeHeight());if(show||Math.Abs(oldWidth-fit.Width)>.1||Math.Abs(oldHeight-fit.Height)>.1){Configure(fit.Width,fit.Height);ShowAt(autoScreen.Bounds,fit.X,fit.Y,fit.Width,fit.Height);}}finally{fitting=false;}
        }
        void FitVisible(){if(IsVisible&&!IsDismissing&&!moreRecent&&openFolder==null)FitWindow(false);}
        async void LoadPhone(bool force=false)
        {
            if(closed||phoneBusy)return;if(!force&&phoneSnapshot!=null&&DateTime.UtcNow<phoneUntil){Phone.Update(phoneSnapshot);return;}phoneBusy=true;
            try{var state=await BackgroundWork.Run(()=>PhoneCompanion.Read(force));if(closed)return;await LauncherMotion.WhenIdle(TransitionContent);if(closed)return;phoneSnapshot=state;phoneUntil=DateTime.UtcNow.AddSeconds(30);if(IsVisible)Phone.Update(state);}catch(Exception error){AppLog.Once("start-phone",error.GetType().Name);}finally{phoneBusy=false;}
        }
        async void OpenPhone(string uri){if(await DismissAsync())Native.Open(uri);}
        internal void FixturePhone(PhoneSnapshot snapshot){phoneSnapshot=snapshot;phoneUntil=DateTime.UtcNow.AddHours(1);Phone.Update(snapshot);}
        internal void RefreshPins(){pinned=shell.StartPins.Tiles(shell.Apps);if(openFolder!=null&&!pinned.Any(p=>p.Folder&&p.Key==openFolder))openFolder=null;RenderPins();}
        List<StartTile> CurrentTiles(){var folder=pinned.FirstOrDefault(p=>p.Folder&&p.Key==openFolder);return folder==null?pinned:folder.Children;}
        void UpdatePinColumns(){pins.Columns=pinned.Count==0?1:Math.Max(2,Math.Min(6,(int)Math.Floor((Width-48-(phoneColumn==null?0:phoneColumn.Width.Value))/86)));}
        void RenderPins()
        {
            if(IsVisible&&pins.IsMouseOver&&Mouse.LeftButton==MouseButtonState.Pressed&&!DragReorder.Active){pinsPending=true;return;}
            string signature=PinSignature();
            if(signature==pinSignature&&pins.Children.Count>0){UpdatePinColumns();UpdateRecentPage();return;}
            pinSignature=signature;
            var current=CurrentTiles();int pages=Math.Max(1,(current.Count+17)/18);pinPage=Math.Max(0,Math.Min(pages-1,pinPage));pins.Children.Clear();pinButtons.Clear();UpdatePinColumns();
            pinHeading.Visibility=openFolder==null?Visibility.Visible:Visibility.Collapsed;folderHeading.Visibility=openFolder==null?Visibility.Collapsed:Visibility.Visible;recentHeading.Visibility=recent.Visibility=openFolder==null?Visibility.Visible:Visibility.Collapsed;
            if(openFolder!=null){var folder=pinned.First(t=>t.Key==openFolder);folderName.Text=folder.Name;}
            foreach(var item in current.Skip(pinPage*18).Take(18)){
                var tile=item;var content=new StackPanel();var icon=tile.Folder?FolderIcon(tile):AppIcons.Visual(tile.App,40);icon.HorizontalAlignment=HorizontalAlignment.Center;icon.Margin=new Thickness(0,2,0,7);content.Children.Add(icon);
                var name=Paint.TextBlock(tile.Name,11,Paint.Text);name.TextAlignment=TextAlignment.Center;name.TextWrapping=TextWrapping.Wrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.MaxHeight=30;content.Children.Add(name);
                var button=Paint.Button(content,()=> {if(tile.Folder)OpenPinFolder(tile.Key);else Launch(tile.App);},tile.Folder?tile.Name+" · 앱 "+tile.Children.Count+"개":tile.Name);button.Height=89;button.Margin=new Thickness(1);button.Padding=new Thickness(4,5,4,5);button.Background=Brushes.Transparent;button.ContextMenu=PinMenu(tile);button.RenderTransform=new TranslateTransform();pins.Children.Add(button);pinButtons[tile.Key]=button;
                DragReorder.Attach(button,button,"start.pins",tile.Key,Orientation.Horizontal,()=>!closed&&IsVisible,MovePin,PreviewPin);
            }
            if(current.Count==0){var empty=Paint.TextBlock("앱을 우클릭해 시작 화면에 고정해 보세요.",12,Paint.Muted);empty.TextWrapping=TextWrapping.Wrap;pins.Children.Add(empty);}
            page.Text=pages>1?(pinPage+1)+" / "+pages:"";previous.Visibility=next.Visibility=pages>1?Visibility.Visible:Visibility.Collapsed;previous.IsEnabled=pinPage>0;next.IsEnabled=pinPage<pages-1;
            UpdateRecentPage();
            FitVisible();
        }
        internal Button PinButton(string key){Button button;return pinButtons.TryGetValue(key,out button)?button:null;}
        internal string[] PinnedKeys {get{return CurrentTiles().Select(p=>p.Key).ToArray();}}
        internal bool MovePin(string source,string target,bool after){return openFolder==null?shell.StartPins.Move(source,target,after):shell.StartPins.MoveWithin(openFolder,source,target,after);}
        internal void OpenPinFolder(string key){if(!pinned.Any(p=>p.Folder&&p.Key==key))return;openFolder=key;pinPage=0;RenderPins();LauncherMotion.Page(bodyScroll);}
        internal void ClosePinFolder(){CommitFolderName();openFolder=null;pinPage=0;RenderPins();}
        void CommitFolderName(){if(renaming||openFolder==null)return;renaming=true;try{if(shell.StartPins.Rename(openFolder,folderName.Text))pinned=shell.StartPins.Tiles(shell.Apps);var tile=pinned.FirstOrDefault(p=>p.Key==openFolder);if(tile!=null)folderName.Text=tile.Name;}finally{renaming=false;}}
        internal static FrameworkElement FolderIcon(StartTile tile)
        {
            var icons=new UniformGrid {Columns=2,Rows=2,Width=32,Height=32,UseLayoutRounding=true};foreach(var app in tile.Children.Take(4)){var icon=AppIcons.Visual(app.App,13);icon.Margin=new Thickness(1);icon.HorizontalAlignment=HorizontalAlignment.Center;icon.VerticalAlignment=VerticalAlignment.Center;icons.Children.Add(icon);}return new Border {Width=40,Height=40,Padding=new Thickness(4),CornerRadius=new CornerRadius(11),Background=Paint.Brush("#32FFFFFF"),UseLayoutRounding=true,Child=icons};
        }
        ContextMenu PinMenu(StartTile tile)
        {
            var menu=tile.Folder?new ContextMenu():WindowPicker.Menu(tile.App,shell,allowAdmin:true);
            if(tile.Folder){var rename=new MenuItem {Header="폴더 이름 변경"};rename.Click+=(s,e)=> {OpenPinFolder(tile.Key);folderName.Focus();folderName.SelectAll();};menu.Items.Add(rename);var dissolve=new MenuItem {Header="폴더 풀기 · 앱 유지"};dissolve.Click+=(s,e)=> {if(shell.StartPins.Dissolve(tile.Key))RefreshPins();};menu.Items.Add(dissolve);}
            else if(openFolder!=null){menu.Items.Add(new Separator());var extract=new MenuItem {Header="폴더 밖으로 꺼내기"};extract.Click+=(s,e)=> {if(shell.StartPins.Extract(tile.Key)){openFolder=null;RefreshPins();}};menu.Items.Add(extract);}
            else {var group=new MenuItem {Header="폴더로 묶기"};group.SubmenuOpened+=(s,e)=> {group.Items.Clear();foreach(var value in pinned.Where(p=>p.Key!=tile.Key)){var target=value;var option=new MenuItem {Header=target.Folder?target.Name+"에 추가":target.Name+"와 새 폴더"};option.Click+=(sender,args)=> {if(shell.StartPins.Combine(tile.Key,target.Key))RefreshPins();};group.Items.Add(option);}group.IsEnabled=group.Items.Count>0;};menu.Items.Add(new Separator());menu.Items.Add(group);}return menu;
        }
        internal bool CanGroup(string source,string target,Point point)
        {
            Button button;return openFolder==null&&source!=target&&pinned.Any(p=>p.Key==source&&!p.Folder)&&pinButtons.TryGetValue(target,out button)&&point.X>button.ActualWidth*.27&&point.X<button.ActualWidth*.73&&point.Y>8&&point.Y<button.ActualHeight*.68;
        }
        string DropTarget(Point point,out Point local)
        {
            foreach(var pair in pinButtons){var slot=LayoutInformation.GetLayoutSlot(pair.Value);if(slot.Contains(point)){local=new Point(point.X-slot.Left-pair.Value.Margin.Left,point.Y-slot.Top-pair.Value.Margin.Top);return pair.Key;}}local=new Point();return null;
        }
        bool DropPin(string source,Point point){Point local;string target=DropTarget(point,out local);LastPinDrop=CurrentTiles().FindIndex(t=>t.Key==source)+":"+CurrentTiles().FindIndex(t=>t.Key==target)+":"+Math.Round(point.X)+","+Math.Round(point.Y);if(target==null||target==source)return false;return CanGroup(source,target,local)?shell.StartPins.Combine(source,target):MovePin(source,target,local.X>=pinButtons[target].ActualWidth/2);}
        void PreviewPinDrop(string source,Point point)
        {
            Point local;string target=DropTarget(point,out local);if(target==null||target==source){PreviewGroup(source,target,false);foreach(var button in pinButtons.Values)AnimatePin(button,0,0);dragPreview="reset";return;}
            bool combine=CanGroup(source,target,local);PreviewGroup(source,target,combine);if(!combine)PreviewPin(source,target,local.X>=pinButtons[target].ActualWidth/2);
        }
        void PreviewGroup(string source,string target,bool combine)
        {
            if(groupTarget!=null&&pinButtons.ContainsKey(groupTarget))pinButtons[groupTarget].Background=Brushes.Transparent;groupTarget=null;if(!combine)return;
            groupTarget=target;dragPreview="folder|"+source+"|"+target;foreach(var button in pinButtons.Values)AnimatePin(button,0,0);pinButtons[target].Background=Paint.Brush("#38FFFFFF");
        }
        internal void PreviewPin(string source,string target,bool after)
        {
            string signature=source+"|"+target+"|"+after;if(dragPreview==signature||!pinButtons.ContainsKey(source)||!pinButtons.ContainsKey(target))return;dragPreview=signature;
            var keys=CurrentTiles().Skip(pinPage*18).Take(18).Select(p=>p.Key).ToList();var slots=keys.ToDictionary(k=>k,k=>LayoutInformation.GetLayoutSlot(pinButtons[k]).TopLeft,StringComparer.OrdinalIgnoreCase);var original=keys.ToArray();keys.Remove(source);keys.Insert(keys.IndexOf(target)+(after?1:0),source);
            for(int i=0;i<keys.Count;i++){var key=keys[i];var from=slots[key];var to=slots[original[i]];AnimatePin(pinButtons[key],to.X-from.X,to.Y-from.Y);}
        }
        static void AnimatePin(Button button,double x,double y)
        {
            var shift=button.RenderTransform as TranslateTransform;if(shift==null){shift=new TranslateTransform();button.RenderTransform=shift;}double fromX=shift.X,fromY=shift.Y;shift.BeginAnimation(TranslateTransform.XProperty,null);shift.BeginAnimation(TranslateTransform.YProperty,null);shift.X=x;shift.Y=y;
            if(Motion.Enabled){shift.BeginAnimation(TranslateTransform.XProperty,Motion.Tween(fromX,x,170));shift.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(fromY,y,170));}
        }
        internal void FinishPinDrag()
        {
            if(closed||dragPreview==null)return;dragPreview=null;groupTarget=null;var positions=pinButtons.ToDictionary(p=>p.Key,p=>p.Value.TranslatePoint(new Point(),pins),StringComparer.OrdinalIgnoreCase);RefreshPins();pins.UpdateLayout();
            foreach(var pair in pinButtons){Point old;if(!positions.TryGetValue(pair.Key,out old))continue;var position=pair.Value.TranslatePoint(new Point(),pins);var shift=(TranslateTransform)pair.Value.RenderTransform;shift.X=old.X-position.X;shift.Y=old.Y-position.Y;AnimatePin(pair.Value,0,0);}
        }
        async void LoadRecent()
        {
            if(recentBusy||DateTime.UtcNow<recentUntil)return;recentBusy=true;int request=++load;
            try {var items=await BackgroundWork.Run(StartRecentFeed.Read);await LauncherMotion.WhenIdle(TransitionContent);if(closed||request!=load)return;cachedRecent=items;recentUntil=DateTime.UtcNow.AddSeconds(5);if(IsVisible)RenderRecent(items);}
            catch(Exception error){AppLog.Once("start-recent",error.Message);}finally{recentBusy=false;}
        }
        void RenderRecent(List<StartRecent> entries)
        {
            if(IsVisible&&recent.IsMouseOver&&Mouse.LeftButton==MouseButtonState.Pressed){recentPending=entries;return;}
            string signature=RecentSignature(entries);
            if(recentSignature==signature&&recent.Children.Count>0)return;
            recentSignature=signature;
            recentMore.Content=Paint.TextBlock(moreRecent?"‹  뒤로":"더 보기  ›",11,Paint.Text);recentMore.ToolTip=moreRecent?"시작 화면으로 돌아가기":"최근 항목 화면 열기";recentMore.IsEnabled=moreRecent||entries.Count>8;recentMore.Opacity=recentMore.IsEnabled?1:.45;
            recent.Children.Clear();foreach(var value in entries.Take(moreRecent?24:8)){var item=value;var app=item.App;var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(58)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(new RecentThumbnail(app));
                var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};var name=Paint.TextBlock(app.Name,12,Paint.Text);name.TextTrimming=TextTrimming.CharacterEllipsis;words.Children.Add(name);words.Children.Add(Paint.TextBlock("최근 열람 · "+item.Time.ToString("M월 d일  H:mm"),10,Paint.Muted));Grid.SetColumn(words,1);row.Children.Add(words);
                var button=Paint.Button(row,()=>Launch(app),app.Name);button.Background=Brushes.Transparent;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(1,1,1,2);button.Padding=new Thickness(9);button.Height=56;recent.Children.Add(button);
            }
            if(entries.Count==0){var empty=Paint.TextBlock("Windows에서 기록한 최근 항목이 여기에 표시됩니다.",12,Paint.Muted);empty.TextWrapping=TextWrapping.Wrap;empty.Margin=new Thickness(9,12,9,15);recent.Children.Add(empty);}
            FitVisible();
        }
        internal void FixtureRecent(List<StartRecent> entries){cachedRecent=entries;recentUntil=DateTime.UtcNow.AddHours(1);RenderRecent(entries);}
        async void Launch(AppEntry entry){var modifiers=Keyboard.Modifiers;if(await DismissAsync())shell.LaunchApp(entry,modifiers);}
        async void OpenFolder(Environment.SpecialFolder folder){if(await DismissAsync())Native.Open(Environment.GetFolderPath(folder));}
        internal ContextMenu PowerMenu(FrameworkElement target)
        {
            var menu=new ContextMenu {PlacementTarget=target,Placement=PlacementMode.Top};foreach(var value in new[]{SystemPowerAction.Lock,SystemPowerAction.Sleep,SystemPowerAction.ShutDown,SystemPowerAction.Restart}){
                var action=value;var item=new MenuItem {Tag=action,Header=action==SystemPowerAction.Lock?"잠금":action==SystemPowerAction.Sleep?"절전":action==SystemPowerAction.ShutDown?"전원 끄기":"다시 시작",Icon=new Glyph(action==SystemPowerAction.Lock?"lock":action==SystemPowerAction.Sleep?"moon":action==SystemPowerAction.ShutDown?"power":"rotation",18){Color=Paint.Ink}};
                item.Click+=async(s,e)=> {if(!await DismissAsync())return;try{SystemPower.Execute(action);}catch(Exception error){shell.ShowWelcome();shell.Welcome.Feedback(error.Message);}};menu.Items.Add(item);
            }return menu;
        }
    }
    internal sealed class StartRecent {internal AppEntry App;internal DateTime Time;internal string RecentLink;}
}
