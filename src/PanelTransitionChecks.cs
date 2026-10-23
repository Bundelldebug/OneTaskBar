using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class PanelTransitionChecks
    {
        static IEnumerable<T> Children<T>(DependencyObject parent) where T:DependencyObject
        {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T)yield return (T)child;foreach(var nested in Children<T>(child))yield return nested;}
        }
        static NotificationEntry Entry(uint id,string title){return new NotificationEntry {Id=id,AppId="OneTaskBar.Fixture",App="One TaskBar",Title=title,Body="알림 카드의 가독성과 부드러운 전환을 확인하는 예시입니다.",Created=DateTimeOffset.Now};}
        static bool LowSliders()
        {
            bool passed=true;var preview=new StackPanel {Background=QuickPanel.Glass};
            foreach(string kind in new[]{"brightness","volume"})foreach(double value in new[]{0.0,1,3,10,100}){
                var slider=new PillSlider(kind) {Width=260,Height=44,Value=value,Light=true,DarkTrack=true,AnimateValue=false};slider.Measure(new Size(260,44));slider.Arrange(new Rect(0,0,260,44));slider.UpdateLayout();var bitmap=new RenderTargetBitmap(520,88,192,192,PixelFormats.Pbgra32);bitmap.Render(slider);var row=new byte[520*4];bitmap.CopyPixels(new Int32Rect(0,44,520,1),row,520*4,0);var white=Enumerable.Range(0,520).Where(x=>row[x*4+3]>240&&row[x*4]>220&&row[x*4+1]>220&&row[x*4+2]>220).ToArray();var bounds=PillSlider.CapsuleFillBounds(260,44,value);passed&=(value==0?row[20*4+3]<100:value<10?bounds.Width==bounds.Height&&bounds.Height<44:white.Length>0&&white.Last()-white.First()>=84)&&slider.Value==value;
                var label=Paint.TextBlock(kind+" · "+value,11,Paint.Text);label.Margin=new Thickness(12,10,12,4);preview.Children.Add(label);preview.Children.Add(new PillSlider(kind) {Width=260,Height=44,Value=value,Light=true,DarkTrack=true,AnimateValue=false,Margin=new Thickness(12,0,12,4)});
            }
            Program.Save(preview,284,760,Path.Combine(AppLog.Data,"slider-low-levels.png"));return passed;
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;bool nativeQuick=Preferences.Current.NativeQuickSettings,nativeCenter=Preferences.Current.NativeNotifications,animations=Preferences.Current.Animations;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try {
                    Program.Inspectable=true;Preferences.Current.NativeQuickSettings=Preferences.Current.NativeNotifications=false;Preferences.Current.Animations=true;bool sliders=LowSliders();shell=new ShellController(true);shell.Center=new NotificationWindow(shell);var center=shell.Center;var screen=System.Windows.Forms.Screen.PrimaryScreen;
                    shell.Notifications.Allowed=true;shell.Notifications.Items=new List<NotificationEntry>{Entry(1,"일정 없이 알림만 표시"),Entry(2,"기존 카드는 그대로 유지"),Entry(3,"터치와 트랙패드로 스크롤")};center.Refresh();center.ShowAt(screen.Bounds,24,24,394,584);center.Pop();await Task.Delay(380);
                    bool noCalendar=!Children<UniformGrid>(center.Surface).Any()&&center.Height==584;var kept=center.Stacks.Single();kept.SetExpanded(true);await Task.Delay(260);var keptCard=kept.RetainedCards.First(b=>(b.Tag as string??"").Contains("|기존 카드는 그대로 유지|"));
                    shell.Notifications.Items.RemoveAt(0);shell.Notifications.Items.Add(Entry(4,"새 알림이 부드럽게 들어옵니다"));center.Refresh();await Task.Delay(260);bool retained=center.Stacks.Contains(kept)&&kept.RetainedCards.Contains(keptCard)&&kept.Expanded&&center.CardCount==1&&kept.Count==3;Program.Save(center.Surface,394,584,Path.Combine(AppLog.Data,"notifications-redesigned.png"));
                    bool exclusive=true;int samples=0;Func<bool> overlap=()=>shell.Quick!=null&&shell.Quick.IsVisible&&center.IsVisible;
                    shell.ToggleQuick(null);for(int i=0;i<35;i++){await Task.Delay(10);exclusive&=!overlap();samples++;}bool quickOnly=shell.Quick!=null&&shell.Quick.IsVisible&&!center.IsVisible;
                    shell.ToggleNotifications(null);for(int i=0;i<35;i++){await Task.Delay(10);exclusive&=!overlap();samples++;}bool centerOnly=center.IsVisible&&!shell.Quick.IsVisible;
                    shell.ToggleQuick(null);await Task.Delay(25);exclusive&=!overlap();shell.ToggleNotifications(null);await Task.Delay(25);exclusive&=!overlap();shell.ToggleQuick(null);
                    for(int i=0;i<40;i++){await Task.Delay(10);exclusive&=!overlap();samples++;}bool reversal=shell.Quick.IsVisible&&!center.IsVisible;
                    result=sliders&&noCalendar&&retained&&exclusive&&quickOnly&&centerOnly&&reversal?0:1;AppLog.Json("panel-transition-probe.json",new {passed=result==0,circularLowSliderCaps=sliders,calendarRemoved=noCalendar,notificationCardsRetained=retained,mutuallyExclusive=exclusive,samples=samples,quickOnly=quickOnly,notificationsOnly=centerOnly,rapidReversal=reversal});
                }catch(Exception error){AppLog.Json("panel-transition-probe.json",new {passed=false,error=error.ToString()});}
                finally {Preferences.Current.NativeQuickSettings=nativeQuick;Preferences.Current.NativeNotifications=nativeCenter;Preferences.Current.Animations=animations;if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
