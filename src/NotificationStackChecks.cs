using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class NotificationStackChecks
    {
        static NotificationEntry Entry(uint id,string app,string title,int age=0){return new NotificationEntry {Id=id,AppId=app,App=app=="browser"?"브라우저":"One TaskBar",Title=title,Body="같은 앱의 알림을 펼쳐 확인할 수 있습니다.",Created=DateTimeOffset.Now.AddMinutes(-age)};}
        static void Require(bool value,string label){if(!value)throw new InvalidOperationException("Notification stack: "+label);}
        internal static void Validate(List<string> checks)
        {
            var first=Entry(1,"one","오래된 알림",5);var latest=Entry(2,"ONE","최신 알림");var sameName=Entry(1,"two","다른 앱");var groups=NotificationStack.Group(new[]{first,latest,sameName,first});
            Require(groups.Count==2&&groups.Sum(g=>g.Items.Count)==3&&groups.First(g=>g.Items.Count==2).Latest.Id==2,"app identity, latest ordering and duplicate IDs");
            var unknown=Entry(9,null,"이름만 있는 앱");var other=Entry(10,null,"별도 앱");other.App="별도 앱";Require(NotificationStack.Group(new[]{unknown,other}).Count==2,"no app ID fallback");
            checks.Add("Notification stacks use app IDs, preserve distinct apps with the same display name, sort latest first and deduplicate IDs within each app");
        }
        static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T)yield return (T)child;foreach(var nested in Children<T>(child))yield return nested;}}
        static void Click(Button button){((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke)).Invoke();}
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;GlassWindow fixture=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    Directory.CreateDirectory(AppLog.Data);Program.Inspectable=true;Validate(new List<string>());shell=new ShellController(true);shell.Notifications.Allowed=true;
                    var records=new List<NotificationEntry>{Entry(1,"browser","댓글이 달렸습니다.",4),Entry(2,"browser","새로운 답글이 있습니다.",2),Entry(3,"browser","새 글을 확인해 보세요."),Entry(4,"one","업데이트를 적용했습니다.",1)};shell.Notifications.Items=records;shell.Center=new NotificationWindow(shell);var center=shell.Center;center.Refresh();var screen=System.Windows.Forms.Screen.PrimaryScreen;center.ShowAt(screen.Bounds,30,30,394,584);await Task.Delay(280);
                    Program.Save(center.Surface,394,584,Path.Combine(AppLog.Data,"notifications-stacked.png"));var stack=center.Stacks.First(g=>g.Count==3);bool compact=center.CardCount==2&&!stack.Expanded&&stack.RetainedCards.Count()==1;
                    Click(stack.ExpandButton);await Task.Delay(280);bool expanded=stack.Expanded&&stack.RetainedCards.Count()==3;var retained=stack.RetainedCards.ToArray();records.Add(Entry(5,"browser","추가 알림도 같은 스택에 쌓입니다."));center.Refresh();await Task.Delay(280);bool stable=stack.Expanded&&stack.Count==4&&retained.All(b=>stack.RetainedCards.Contains(b));Program.Save(center.Surface,394,584,Path.Combine(AppLog.Data,"notifications-expanded.png"));
                    records.RemoveAll(n=>n.Id!=1&&n.AppId=="browser");center.Refresh();await Task.Delay(280);bool single=!stack.Expanded&&stack.Count==1&&stack.RetainedCards.Count()==1;records.RemoveAll(n=>n.AppId=="browser");center.Refresh();await Task.Delay(280);bool removed=center.CardCount==1;
                    var opened=new List<uint>();var dismissed=new List<uint>();int cleared=0;var group=NotificationStack.Group(new[]{Entry(10,"test","알림 열기"),Entry(11,"test","다른 알림",1)}).Single();var view=new NotificationStackView(group,n=>opened.Add(n.Id),n=>dismissed.Add(n.Id),g=>cleared=g.Items.Count);fixture=new GlassWindow();fixture.Surface.Child=view;fixture.ShowAt(screen.Bounds,440,30,354,400);await Task.Delay(120);view.SetExpanded(true);await Task.Delay(280);Click(Children<Button>(view).First(b=>b.Tag is uint&&(uint)b.Tag==11));await Task.Delay(50);Click(Children<Button>(view).First(b=>(b.ToolTip as string)=="이 앱의 알림 모두 지우기"));await Task.Delay(50);bool exact=opened.SequenceEqual(new uint[]{11})&&cleared==2;
                    shell.Banners.Show(Entry(21,"same","첫 알림",1));shell.Banners.Show(Entry(22,"same","두 번째 알림"));bool banners=shell.Banners.VisibleCount==1&&shell.Banners.Last.StackCount==2&&shell.Banners.Last.Entry.Id==22;shell.Banners.Clear();fixture.Close();fixture=null;center.HideImmediately();bool clean=LiveBackdrop.OrphanCount==0;
                    bool passed=compact&&expanded&&stable&&single&&removed&&exact&&banners&&clean;AppLog.Json("notification-stack-probe.json",new {passed=passed,groupedByApp=compact,expandsAll=expanded,expandedStateAndCardsRetained=stable,singleCardCollapses=single,emptyGroupRemoved=removed,exactItemActionAndGroupClear=exact,sameAppPopupsGrouped=banners,noOrphanBackdrop=clean,realNotificationsChanged=false});result=passed?0:1;
                }catch(Exception error){AppLog.Json("notification-stack-probe.json",new {passed=false,error=error.ToString()});}finally{if(fixture!=null)fixture.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
