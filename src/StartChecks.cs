using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
namespace OneTaskBar
{
    internal static class StartChecks
    {
        [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Native.PointI point);
        [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
        static async Task<bool> Drag(StartWindow window,Button source,Point destination)
        {
            var owner=new WindowInteropHelper(window).Handle;var origin=source.PointToScreen(new Point(source.ActualWidth/2,source.ActualHeight/2));var cursor=System.Windows.Forms.Cursor.Position;
            return await Task.Run(()=> {bool down=false;try{if(Native.GetAncestor(WindowFromPoint(new Native.PointI {X=(int)origin.X,Y=(int)origin.Y}),2)!=owner)return false;SetCursorPos((int)origin.X,(int)origin.Y);System.Threading.Thread.Sleep(40);mouse_event(2,0,0,0,UIntPtr.Zero);down=true;for(int i=1;i<=24;i++){double t=i/24.0;SetCursorPos((int)Math.Round(origin.X+(destination.X-origin.X)*t),(int)Math.Round(origin.Y+(destination.Y-origin.Y)*t));System.Threading.Thread.Sleep(20);}System.Threading.Thread.Sleep(110);return true;}finally{if(down){mouse_event(4,0,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(130);}SetCursorPos(cursor.X,cursor.Y);}});
        }
        static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject {if(root is T)yield return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;bool oldNative=Preferences.Current.NativeStartButton,oldSearch=Preferences.Current.NativeSearch;
            app.Dispatcher.BeginInvoke(new Action(async()=> {
                try {
                    Program.Inspectable=true;Preferences.Current.NativeStartButton=false;Preferences.Current.NativeSearch=false;shell=new ShellController(true);shell.Apps=await BackgroundWork.Run(()=>Catalog.Read(false));shell.Pins=shell.Apps.Take(18).Select(a=>a.Copy()).ToList();foreach(var pin in shell.Pins)pin.Pinned=true;shell.StartPins.Replace(shell.Apps.Take(18));
                    shell.StartMenu=new StartWindow(shell);shell.StartMenu.FixturePhone(StartDataChecks.Phone());shell.StartMenu.FixtureRecent(new List<StartRecent>{new StartRecent {App=new AppEntry {Name="프로젝트 메모",Target=Path.Combine(AppLog.Data,"notes.txt")},Time=DateTime.Now},new StartRecent {App=new AppEntry {Name="최근 작업",Target=AppLog.Data},Time=DateTime.Now.AddMinutes(-5)}});
                    shell.ToggleStartButton(null);await Task.Delay(430);var start=shell.StartMenu;bool opened=start.IsVisible&&start.PinnedCount==Math.Min(18,shell.Pins.Count)&&start.RecentCount==2;Program.Save(start.Surface,(int)start.Width,(int)start.Height,Path.Combine(AppLog.Data,"start-menu.png"));
                    bool phonePane=start.Phone.Visibility==Visibility.Visible&&start.Phone.HasPhone&&start.Phone.EnabledActions==4&&start.Phone.TranslatePoint(new Point(),start.Surface).X>start.Width*.65;string launched=null;var phone=new PhonePane(uri=>launched=uri,()=>{});phone.Update(StartDataChecks.Phone());phone.ActionButton("messages").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));bool phoneActions=launched!=null&&launched.StartsWith("ms-phone:messages?");phone.Update(new PhoneSnapshot());phoneActions&=phone.EnabledActions==0;double wide=start.Width;start.Configure(640,620);bool responsive=start.Phone.Visibility==Visibility.Collapsed;start.Configure(wide,620);
                    var power=start.PowerMenu(start.Surface);bool powerOptions=power.Items.OfType<MenuItem>().Count()==4;
                    var all=Children<Button>(start.Surface).First(b=>Children<TextBlock>(b).Any(t=>t.Text=="모든 앱  ›"));all.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(380);bool allApps=shell.Drawer!=null&&shell.Drawer.IsVisible&&!start.IsVisible;
                    shell.ToggleStart(null);await Task.Delay(350);start.Search.Text="메모";await Task.Delay(400);bool search=shell.Finder!=null&&shell.Finder.IsVisible&&!start.IsVisible&&shell.Finder.Query.Text=="메모";
                    shell.Finder.HideImmediately();shell.ToggleStart(null);await Task.Delay(350);bool exclusive=start.IsVisible&&!shell.Drawer.IsVisible&&!shell.Finder.IsVisible;
                    var original=start.PinnedKeys;start.PreviewPin(original[0],original[2],true);await Task.Delay(80);bool preview=Math.Abs(((TranslateTransform)start.PinButton(original[1]).RenderTransform).X)>1;start.FinishPinDrag();await Task.Delay(240);bool cancelled=start.PinnedKeys.SequenceEqual(original)&&Math.Abs(((TranslateTransform)start.PinButton(original[1]).RenderTransform).X)<.1;
                    int completed=DragReorder.CompletedMoves;bool input=await Drag(start,start.PinButton(original[0]),start.PinButton(original[2]).PointToScreen(new Point(start.PinButton(original[2]).ActualWidth*.8,35)));await Task.Delay(350);bool committed=DragReorder.CompletedMoves>completed,changed=!start.PinnedKeys.SequenceEqual(original);var storedOrder=new StartPinStore(Path.Combine(AppLog.Data,"start-pins.json")).Resolve(shell.Apps).Select(a=>a.Key).ToArray();bool reloadMatches=storedOrder.SequenceEqual(start.PinnedKeys),dockUnchanged=shell.Pins.Select(a=>a.Key).SequenceEqual(original);bool reordered=input&&committed&&changed&&!DragReorder.Active&&reloadMatches&&dockUnchanged;string firstDrop=start.LastPinDrop;
                    var beforeFolders=start.PinnedKeys;string moving=beforeFolders[0],anchor=beforeFolders[1];int folderMoves=DragReorder.CompletedMoves;bool folderInput=await Drag(start,start.PinButton(moving),start.PinButton(anchor).PointToScreen(new Point(start.PinButton(anchor).ActualWidth*.5,27)));await Task.Delay(330);
                    var folder=shell.StartPins.Tiles(shell.Apps).FirstOrDefault(t=>t.Folder);bool folders=folderInput&&DragReorder.CompletedMoves>folderMoves&&folder!=null&&start.FolderCount==1&&folder.Children.Count==2&&shell.Pins.Select(a=>a.Key).SequenceEqual(original);
                    bool folderOrder=false,folderPersist=false,extract=false,nameTyped=false;if(folder!=null){string folderKey=folder.Key;start.OpenPinFolder(folderKey);await Task.Delay(180);folders&=start.OpenFolderKey==folderKey&&start.PinnedCount==2;var editor=start.FolderNameEditor;start.Activate();editor.Focus();editor.SelectAll();if(!editor.IsKeyboardFocusWithin||Native.GetForegroundWindow()!=new WindowInteropHelper(start).Handle)throw new InvalidOperationException("Folder-name fixture did not receive focus; no keys sent");bool sent=SystemPanels.ProbeText("작업 도구");await Task.Delay(120);nameTyped=sent&&editor.Text=="작업 도구"&&start.IsVisible&&(shell.Finder==null||!shell.Finder.IsVisible);editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(editor),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});await Task.Delay(100);Program.Save(start.Surface,(int)start.Width,(int)start.Height,Path.Combine(AppLog.Data,"start-folder.png"));
                        var children=start.PinnedKeys;int beforeMove=DragReorder.CompletedMoves;bool childInput=await Drag(start,start.PinButton(children[0]),start.PinButton(children[1]).PointToScreen(new Point(start.PinButton(children[1]).ActualWidth*.84,32)));await Task.Delay(300);folderOrder=childInput&&DragReorder.CompletedMoves>beforeMove&&start.PinnedKeys.SequenceEqual(children.Reverse());
                        var savedFolder=new StartPinStore(Path.Combine(AppLog.Data,"start-pins.json")).Tiles(shell.Apps).FirstOrDefault(t=>t.Key==folderKey);folderPersist=savedFolder!=null&&savedFolder.Name=="작업 도구"&&savedFolder.Children.Select(c=>c.Key).SequenceEqual(start.PinnedKeys);
                        var outside=start.PinButton(start.PinnedKeys[0]).ContextMenu.Items.OfType<MenuItem>().First(m=>(string)m.Header=="폴더 밖으로 꺼내기");outside.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));await Task.Delay(120);extract=start.OpenFolderKey==null&&start.PinnedKeys.Length==beforeFolders.Length&&shell.StartPins.Resolve(shell.Apps).Count==beforeFolders.Length;shell.StartPins.Dissolve(folderKey);start.RefreshPins();extract&=start.FolderCount==0&&start.PinnedKeys.Length==beforeFolders.Length;
                    }
                    shell.ToggleStart(null);await Task.Delay(240);bool togglesClosed=!start.IsVisible;
                    var pinnedItem=shell.Pins[0];int dockCount=shell.Pins.Count;bool dockState=pinnedItem.Pinned;shell.PinStart(pinnedItem,false);bool independent=!shell.StartPins.Contains(pinnedItem)&&shell.Pins.Count==dockCount&&pinnedItem.Pinned==dockState;shell.PinStart(pinnedItem,true);var reloaded=new StartPinStore(Path.Combine(AppLog.Data,"start-pins.json"));independent&=reloaded.Contains(pinnedItem)&&shell.Pins.Count==dockCount;
                    bool passed=opened&&allApps&&search&&powerOptions&&exclusive&&togglesClosed&&independent&&preview&&cancelled&&reordered&&folders&&folderOrder&&folderPersist&&extract&&phonePane&&phoneActions&&responsive&&nameTyped;AppLog.Json("start-menu-probe.json",new {passed=passed,pinnedAndRecent=opened,allApps=allApps,search=search,powerMenu=powerOptions,exclusive=exclusive,togglesClosed=togglesClosed,startAndDockPinsIndependent=independent,dragAnimation=preview,cancelRestoresOrder=cancelled,actualMouseDragAndPersistedOrder=reordered,dragInput=input,firstDrop=firstDrop,dragCommitted=committed,dragOrderChanged=changed,dragReloadMatches=reloadMatches,dragDockUnchanged=dockUnchanged,actualFolderDragAndOpen=folders,folderChildDrag=folderOrder,folderNameKeyboardEditing=nameTyped,folderNameAndOrderPersisted=folderPersist,extractAndDissolvePreserveApps=extract,phonePaneAndNativeHeader=phonePane,phoneActionAndUnavailableState=phoneActions,narrowScreenLayout=responsive,backgroundRefreshSeconds=30,systemPowerExecuted=false,phoneCommandsExecuted=false});result=passed?0:1;
                }catch(Exception error){AppLog.Json("start-menu-probe.json",new {passed=false,error=error.ToString()});}
                finally {Preferences.Current.NativeStartButton=oldNative;Preferences.Current.NativeSearch=oldSearch;if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
