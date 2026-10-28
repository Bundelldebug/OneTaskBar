using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class NotificationSwipeChecks
    {
        internal static int Run(bool inspect)
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int code=1;GlassWindow window=null;IDisposable motion=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try
                {
                    Program.Inspectable=true;motion=Motion.WithAnimationsForFixture();
                    var entries=new List<NotificationEntry>();
                    for(uint i=1;i<=2;i++)entries.Add(new NotificationEntry {Id=i,AppId="swipe-fixture-"+i,App="알림 검사",Title="밀어서 지우기 "+i,Body="좌우로 밀어 삭제 · 작은 드래그는 원위치",Created=DateTimeOffset.Now});
                    var body=new StackPanel {Margin=new Thickness(18)};int dismissed=0,opened=0;
                    var status=Paint.TextBlock("검사 알림 2개",16,Paint.Text);body.Children.Add(status);
                    foreach(var entry in entries)
                    {
                        NotificationStackView view=null;
                        view=new NotificationStackView(NotificationStack.Group(new[]{entry}).Single(),n=>opened++,n=>{dismissed++;body.Children.Remove(view);status.Text="삭제 "+dismissed+" · 열기 "+opened;},g=>{});
                        body.Children.Add(view);
                    }
                    window=new GlassWindow {Title="One TaskBar · 알림 밀기 검사"};window.Surface.Child=body;
                    window.ShowAt(System.Windows.Forms.Screen.PrimaryScreen.Bounds,40,40,400,400);await Task.Delay(250);
                    if(inspect)
                    {
                        AppLog.Json("swipe-ready.json",new {ready=true});
                        for(int i=0;i<240&&dismissed<2;i++)await Task.Delay(250);
                    }
                    else
                    {
                        foreach(var view in body.Children.OfType<NotificationStackView>().ToArray()){view.Swipe.Dismiss(dismissed==0?1:-1);await Task.Delay(250);}
                    }
                    bool passed=dismissed==2&&opened==0&&!body.Children.OfType<NotificationStackView>().Any();
                    AppLog.Json("notification-swipe-probe.json",new {passed=passed,dismissed=dismissed,opened=opened,mouseGesture=inspect,realNotificationsChanged=false});code=passed?0:1;
                }
                catch(Exception error){AppLog.Json("notification-swipe-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(window!=null)window.Close();if(motion!=null)motion.Dispose();app.Shutdown(code);}
            }),DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
    }
}
