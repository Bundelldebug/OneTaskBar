using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class AppScreenChecks
    {
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;DrawerWindow drawer=null;
            application.Dispatcher.BeginInvoke(new Action(async()=> {
                try{
                    Program.Inspectable=true;shell=new ShellController(true);shell.Apps=Enumerable.Range(1,30).Select(i=>new AppEntry {Name="테스트 앱 "+i,Target="fixture:"+i}).ToList();shell.StartPins.Replace(shell.Apps.Take(3));shell.Pins=shell.Apps.Take(4).Select(a=>a.Copy()).ToList();var start=shell.StartPins.Resolve(shell.Apps).Select(a=>a.Key).ToArray();var dock=shell.Pins.Select(a=>a.Key).ToArray();
                    drawer=new DrawerWindow(shell);drawer.Configure(720,650);drawer.Reload();drawer.ShowAt(System.Windows.Forms.Screen.PrimaryScreen.Bounds,40,40,720,650);await Task.Delay(300);drawer.Surface.UpdateLayout();var original=drawer.AppKeys;
                    drawer.PreviewMove(original[0],original[3],true);await Task.Delay(80);bool animated=Math.Abs(((TranslateTransform)drawer.AppButton(original[1]).RenderTransform).X)>1;drawer.FinishDrag();await Task.Delay(220);bool cancel=drawer.AppKeys.SequenceEqual(original);
                    var anchor=drawer.AppButton(original[3]);var slot=LayoutInformation.GetLayoutSlot(anchor);bool moved=drawer.DropApp(original[0],new Point(slot.Right-10,slot.Top+30));drawer.RefreshApps();drawer.Surface.UpdateLayout();bool reorder=moved&&drawer.AppKeys[3]==original[0];
                    var target=drawer.AppButton(original[2]);slot=LayoutInformation.GetLayoutSlot(target);bool grouped=drawer.DropApp(original[1],new Point(slot.Left+slot.Width/2,slot.Top+30));drawer.RefreshApps();var folder=shell.AppScreen.Tiles(shell.Apps).FirstOrDefault(t=>t.Folder);bool folderCreated=grouped&&folder!=null&&folder.Children.Count==2;
                    bool child=false,renamed=false,extracted=false,dissolved=false,folderTop=false;
                    if(folder!=null){drawer.OpenFolder(folder.Key);drawer.FolderNameEditor.Text="게임 및 도구";drawer.FolderNameEditor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(drawer),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});var children=drawer.AppKeys;child=drawer.MoveApp(children[0],children[1],true);drawer.RefreshApps();child&=drawer.AppKeys.SequenceEqual(children.Reverse());var saved=new StartPinStore(Path.Combine(AppLog.Data,"apps-layout.json"),4096).Tiles(shell.Apps).First(t=>t.Key==folder.Key);renamed=saved.Name=="게임 및 도구";
                        drawer.Surface.UpdateLayout();folderTop=drawer.AppButton(drawer.AppKeys[0]).TranslatePoint(new Point(),drawer.Surface).Y<170;Program.Save(drawer.Surface,720,650,Path.Combine(AppLog.Data,"apps-folder.png"));var extract=drawer.AppButton(drawer.AppKeys[0]).ContextMenu.Items.OfType<MenuItem>().First(m=>Convert.ToString(m.Header)=="폴더 밖으로 꺼내기");extract.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));extracted=drawer.OpenFolderKey==null&&shell.AppScreen.Resolve(shell.Apps).Count==30;dissolved=shell.AppScreen.Dissolve(folder.Key);drawer.RefreshApps();dissolved&=drawer.AppKeys.Length==30;
                    }
                    bool independent=start.SequenceEqual(shell.StartPins.Resolve(shell.Apps).Select(a=>a.Key))&&dock.SequenceEqual(shell.Pins.Select(a=>a.Key));drawer.Close();drawer=new DrawerWindow(shell);drawer.Configure(720,650);drawer.Reload();bool persisted=drawer.AppKeys.SequenceEqual(new StartPinStore(Path.Combine(AppLog.Data,"apps-layout.json"),4096).Tiles(shell.Apps).Select(t=>t.Key));
                    bool passed=animated&&cancel&&reorder&&folderCreated&&child&&renamed&&extracted&&dissolved&&independent&&persisted&&folderTop;AppLog.Json("app-screen-probe.json",new {passed=passed,animatedReorder=animated,cancelPreservesOrder=cancel,edgeDropReorders=reorder,centerDropCreatesFolder=folderCreated,childOrder=child,renamePersists=renamed,folderIconsBelowHeading=folderTop,extractPreservesApps=extracted,dissolvePreservesApps=dissolved,startAndDockUnchanged=independent,reopenPreservesLayout=persisted,externalAppsLaunched=false});result=passed?0:1;
                }catch(Exception error){AppLog.Json("app-screen-probe.json",new {passed=false,error=error.ToString()});}finally{if(drawer!=null)drawer.Close();if(shell!=null)shell.Dispose();application.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
