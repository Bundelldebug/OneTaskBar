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
    internal sealed class StartWindow : GlassWindow
    {
        readonly ShellController shell;
        readonly UniformGrid pins=new UniformGrid {Columns=6},recent=new UniformGrid {Columns=2};
        readonly TextBlock page=Paint.TextBlock("",11,Paint.Muted);
        readonly Button previous,next;
        internal readonly TextBox Search=new TextBox {FontSize=14,Foreground=Paint.Ink,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(6,9,6,9),MaxLength=120};
        List<AppEntry> pinned=new List<AppEntry>();List<StartRecent> cachedRecent;
        readonly Dictionary<string,Button> pinButtons=new Dictionary<string,Button>(StringComparer.OrdinalIgnoreCase);string dragPreview;
        DateTime recentUntil;bool recentBusy,composing,queued,closed;int pinPage,load;
        internal int PinnedCount {get{return pinButtons.Count;}}
        internal int RecentCount {get{return recent.Children.Count;}}
        internal StartWindow(ShellController owner)
        {
            shell=owner;Title="One TaskBar · 시작";Width=640;Height=620;Radius=32;Dark=true;StableSurface=true;
            Surface.CornerRadius=new CornerRadius(Radius);Surface.Background=QuickPanel.Glass;Surface.BorderBrush=Paint.Brush("#58FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var root=new DockPanel {Margin=new Thickness(24,23,24,16)};
            var searchBox=new Border {Background=Paint.ControlWhite,CornerRadius=new CornerRadius(23),Padding=new Thickness(16,2,16,2),Margin=new Thickness(0,0,0,22)};
            var field=new Grid();field.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(26)});field.ColumnDefinitions.Add(new ColumnDefinition());
            field.Children.Add(new Glyph("search",19){Color=Paint.Ink});Grid.SetColumn(Search,1);field.Children.Add(Search);
            var hint=Paint.TextBlock("앱, 설정 및 파일 검색",14,LightPanel.Muted);hint.Margin=new Thickness(6,0,0,0);hint.IsHitTestVisible=false;Grid.SetColumn(hint,1);field.Children.Add(hint);searchBox.Child=field;
            Search.TextChanged+=(s,e)=> {hint.Visibility=Search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;if(!composing)QueueSearch();};
            TextCompositionManager.AddPreviewTextInputStartHandler(Search,(s,e)=>composing=true);TextCompositionManager.AddPreviewTextInputUpdateHandler(Search,(s,e)=>composing=true);Search.PreviewTextInput+=(s,e)=> {composing=false;QueueSearch();};
            Search.PreviewMouseLeftButtonDown+=(s,e)=> {if(Search.Text.Length==0){shell.ToggleSearch(null);e.Handled=true;}};
            DockPanel.SetDock(searchBox,Dock.Top);root.Children.Add(searchBox);

            var footer=new Grid {Height=52,Margin=new Thickness(0,14,0,0)};
            var userRow=new StackPanel {Orientation=Orientation.Horizontal};userRow.Children.Add(new Glyph("user",20){Margin=new Thickness(0,0,9,0)});userRow.Children.Add(Paint.TextBlock(Environment.UserName,12,Paint.Text));
            var user=Paint.Button(userRow,()=>OpenFolder(Environment.SpecialFolder.UserProfile),"사용자 폴더 열기");user.HorizontalAlignment=HorizontalAlignment.Left;user.MaxWidth=240;user.Background=Brushes.Transparent;footer.Children.Add(user);
            var tools=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            tools.Children.Add(Tool("folder","문서",()=>OpenFolder(Environment.SpecialFolder.MyDocuments)));
            tools.Children.Add(Tool("settings","Windows 설정",async()=> {if(await DismissAsync())shell.OpenWindowsSettings();}));
            Button power=null;power=Tool("power","전원",()=> {power.ContextMenu=PowerMenu(power);power.ContextMenu.IsOpen=true;});tools.Children.Add(power);footer.Children.Add(tools);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);

            var body=new StackPanel();
            var heading=Heading("고정됨");var all=Paint.Action("모든 앱  ›",()=>shell.ToggleDrawer(null));all.HorizontalAlignment=HorizontalAlignment.Right;all.FontSize=11;all.Height=31;heading.Children.Add(all);body.Children.Add(heading);
            pins.Margin=new Thickness(0,7,0,0);body.Children.Add(pins);
            var pagination=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Height=28};
            previous=Tool("back","이전 고정 앱",()=> {pinPage--;RenderPins();});previous.Width=28;previous.Height=26;
            next=Tool("chevron","다음 고정 앱",()=> {pinPage++;RenderPins();});next.Width=28;next.Height=26;pagination.Children.Add(previous);page.Margin=new Thickness(7,0,7,0);pagination.Children.Add(page);pagination.Children.Add(next);body.Children.Add(pagination);
            var recentHeading=Heading("최근 항목");recentHeading.Margin=new Thickness(8,14,8,9);var documents=Paint.Action("폴더 열기  ›",()=>OpenFolder(Environment.SpecialFolder.Recent));documents.HorizontalAlignment=HorizontalAlignment.Right;documents.FontSize=11;documents.Height=31;recentHeading.Children.Add(documents);body.Children.Add(recentHeading);
            recent.Margin=new Thickness(0,0,0,3);body.Children.Add(recent);
            root.Children.Add(new SmoothScrollViewer {Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            Surface.Child=root;TransitionContent=root;
            SizeChanged+=(s,e)=>UpdatePinColumns();
            PreviewTextInput+=(s,e)=> {if(!Search.IsKeyboardFocusWithin&&!String.IsNullOrEmpty(e.Text)&&(Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Alt))==0){Search.Text+=e.Text;QueueSearch();e.Handled=true;}};
            Deactivated+=(s,e)=> {if(!Program.Inspectable&&!DragReorder.Active&&IsVisible&&!IsMouseOver&&!(power.ContextMenu!=null&&power.ContextMenu.IsOpen))Hide();};
            DragReorder.Finished+=FinishPinDrag;Closed+=(s,e)=> {closed=true;++load;DragReorder.Finished-=FinishPinDrag;};
        }
        static Button Tool(string glyph,string label,Action action){var button=Paint.Button(new Glyph(glyph,20),action,label);button.Background=Brushes.Transparent;button.Padding=new Thickness(9);button.Width=button.Height=40;return button;}
        static Grid Heading(string text){var row=new Grid {Height=34,Margin=new Thickness(8,0,8,0)};var title=Paint.TextBlock(text,16,Paint.Text);title.FontWeight=FontWeights.SemiBold;row.Children.Add(title);return row;}
        void QueueSearch(){if(queued||Search.Text.Length==0)return;queued=true;Dispatcher.BeginInvoke(new Action(()=> {queued=false;if(!composing&&IsVisible&&!String.IsNullOrWhiteSpace(Search.Text))shell.ToggleSearch(null,Search.Text);}),System.Windows.Threading.DispatcherPriority.Background);}
        internal void Reload()
        {
            Search.Text="";pinPage=0;RefreshPins();
            RenderRecent(cachedRecent??new List<StartRecent>());LoadRecent();
        }
        internal void RefreshPins(){pinned=shell.StartPins.Resolve(shell.Apps);RenderPins();}
        void UpdatePinColumns(){pins.Columns=pinned.Count==0?1:Math.Max(2,Math.Min(6,(int)Math.Floor((Width-48)/86)));}
        void RenderPins()
        {
            int pages=Math.Max(1,(pinned.Count+17)/18);pinPage=Math.Max(0,Math.Min(pages-1,pinPage));pins.Children.Clear();pinButtons.Clear();UpdatePinColumns();
            foreach(var item in pinned.Skip(pinPage*18).Take(18)){
                var app=item;var content=new StackPanel();var icon=AppIcons.Visual(app,40);icon.HorizontalAlignment=HorizontalAlignment.Center;icon.Margin=new Thickness(0,2,0,7);content.Children.Add(icon);
                var name=Paint.TextBlock(app.Name,11,Paint.Text);name.TextAlignment=TextAlignment.Center;name.TextWrapping=TextWrapping.Wrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.MaxHeight=30;content.Children.Add(name);
                var button=Paint.Button(content,()=>Launch(app),app.Name);button.Height=89;button.Margin=new Thickness(1);button.Padding=new Thickness(4,5,4,5);button.Background=Brushes.Transparent;button.ContextMenu=WindowPicker.Menu(app,shell);button.RenderTransform=new TranslateTransform();pins.Children.Add(button);pinButtons[app.Key]=button;
                DragReorder.Attach(button,button,"start.pins",app.Key,Orientation.Horizontal,()=>!closed&&IsVisible,MovePin,PreviewPin);
            }
            if(pinned.Count==0){var empty=Paint.TextBlock("앱을 우클릭해 시작 화면에 고정해 보세요.",12,Paint.Muted);empty.TextWrapping=TextWrapping.Wrap;pins.Children.Add(empty);}
            page.Text=pages>1?(pinPage+1)+" / "+pages:"";previous.Visibility=next.Visibility=pages>1?Visibility.Visible:Visibility.Collapsed;previous.IsEnabled=pinPage>0;next.IsEnabled=pinPage<pages-1;
        }
        internal Button PinButton(string key){Button button;return pinButtons.TryGetValue(key,out button)?button:null;}
        internal string[] PinnedKeys {get{return pinned.Select(p=>p.Key).ToArray();}}
        internal bool MovePin(string source,string target,bool after){return shell.StartPins.Move(source,target,after);}
        internal void PreviewPin(string source,string target,bool after)
        {
            string signature=source+"|"+target+"|"+after;if(dragPreview==signature||!pinButtons.ContainsKey(source)||!pinButtons.ContainsKey(target))return;dragPreview=signature;
            var keys=pinned.Skip(pinPage*18).Take(18).Select(p=>p.Key).ToList();var slots=keys.ToDictionary(k=>k,k=>LayoutInformation.GetLayoutSlot(pinButtons[k]).TopLeft,StringComparer.OrdinalIgnoreCase);var original=keys.ToArray();keys.Remove(source);keys.Insert(keys.IndexOf(target)+(after?1:0),source);
            for(int i=0;i<keys.Count;i++){var key=keys[i];var from=slots[key];var to=slots[original[i]];AnimatePin(pinButtons[key],to.X-from.X,to.Y-from.Y);}
        }
        static void AnimatePin(Button button,double x,double y)
        {
            var shift=button.RenderTransform as TranslateTransform;if(shift==null){shift=new TranslateTransform();button.RenderTransform=shift;}double fromX=shift.X,fromY=shift.Y;shift.BeginAnimation(TranslateTransform.XProperty,null);shift.BeginAnimation(TranslateTransform.YProperty,null);shift.X=x;shift.Y=y;
            if(Motion.Enabled){shift.BeginAnimation(TranslateTransform.XProperty,Motion.Tween(fromX,x,170));shift.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(fromY,y,170));}
        }
        internal void FinishPinDrag()
        {
            if(closed||dragPreview==null)return;dragPreview=null;var positions=pinButtons.ToDictionary(p=>p.Key,p=>p.Value.TranslatePoint(new Point(),pins),StringComparer.OrdinalIgnoreCase);RefreshPins();pins.UpdateLayout();
            foreach(var pair in pinButtons){Point old;if(!positions.TryGetValue(pair.Key,out old))continue;var position=pair.Value.TranslatePoint(new Point(),pins);var shift=(TranslateTransform)pair.Value.RenderTransform;shift.X=old.X-position.X;shift.Y=old.Y-position.Y;AnimatePin(pair.Value,0,0);}
        }
        async void LoadRecent()
        {
            if(recentBusy||DateTime.UtcNow<recentUntil)return;recentBusy=true;int request=++load;
            try {var items=await BackgroundWork.Run(ReadRecent);if(closed||request!=load)return;cachedRecent=items;recentUntil=DateTime.UtcNow.AddSeconds(30);if(IsVisible)RenderRecent(items);}
            catch(Exception error){AppLog.Once("start-recent",error.Message);}finally{recentBusy=false;}
        }
        internal static List<StartRecent> ReadRecent()
        {
            string path=Environment.GetFolderPath(Environment.SpecialFolder.Recent);var result=new List<StartRecent>();if(!Directory.Exists(path))return result;
            foreach(var file in new DirectoryInfo(path).EnumerateFiles("*.lnk",SearchOption.TopDirectoryOnly).OrderByDescending(f=>f.LastWriteTimeUtc).Take(36)){
                string target=Native.ShortcutTarget(file.FullName);if(String.IsNullOrEmpty(target)||!File.Exists(target)&&!Directory.Exists(target)||result.Any(r=>String.Equals(r.App.Executable,target,StringComparison.OrdinalIgnoreCase)))continue;
                result.Add(new StartRecent {App=new AppEntry {Name=Path.GetFileNameWithoutExtension(file.Name),Target=file.FullName,Executable=target},Time=file.LastWriteTime});if(result.Count==6)break;
            }return result;
        }
        void RenderRecent(List<StartRecent> entries)
        {
            recent.Children.Clear();foreach(var value in entries){var item=value;var app=item.App;var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(41)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(AppIcons.Visual(app,32));
                var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};var name=Paint.TextBlock(app.Name,12,Paint.Text);name.TextTrimming=TextTrimming.CharacterEllipsis;words.Children.Add(name);words.Children.Add(Paint.TextBlock(item.Time.ToString("M월 d일  H:mm"),10,Paint.Muted));Grid.SetColumn(words,1);row.Children.Add(words);
                var button=Paint.Button(row,()=>Launch(app),app.Name);button.Background=Brushes.Transparent;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(1,1,1,2);button.Padding=new Thickness(9);button.Height=56;recent.Children.Add(button);
            }
            if(entries.Count==0){var empty=Paint.TextBlock("최근 사용한 파일이 여기에 표시됩니다.",12,Paint.Muted);empty.TextWrapping=TextWrapping.Wrap;empty.Margin=new Thickness(9,12,9,15);recent.Children.Add(empty);}
        }
        internal void FixtureRecent(List<StartRecent> entries){cachedRecent=entries;recentUntil=DateTime.UtcNow.AddHours(1);RenderRecent(entries);}
        async void Launch(AppEntry entry){if(await DismissAsync())entry.Launch();}
        async void OpenFolder(Environment.SpecialFolder folder){if(await DismissAsync())Native.Open(Environment.GetFolderPath(folder));}
        internal ContextMenu PowerMenu(FrameworkElement target)
        {
            var menu=new ContextMenu {PlacementTarget=target,Placement=PlacementMode.Top};foreach(var value in new[]{SystemPowerAction.Lock,SystemPowerAction.Sleep,SystemPowerAction.ShutDown,SystemPowerAction.Restart}){
                var action=value;var item=new MenuItem {Tag=action,Header=action==SystemPowerAction.Lock?"잠금":action==SystemPowerAction.Sleep?"절전":action==SystemPowerAction.ShutDown?"전원 끄기":"다시 시작",Icon=new Glyph(action==SystemPowerAction.Lock?"lock":action==SystemPowerAction.Sleep?"moon":action==SystemPowerAction.ShutDown?"power":"rotation",18){Color=Paint.Ink}};
                item.Click+=async(s,e)=> {if(!await DismissAsync())return;try{SystemPower.Execute(action);}catch(Exception error){shell.ShowWelcome();shell.Welcome.Feedback(error.Message);}};menu.Items.Add(item);
            }return menu;
        }
    }
    internal sealed class StartRecent {internal AppEntry App;internal DateTime Time;}
}
