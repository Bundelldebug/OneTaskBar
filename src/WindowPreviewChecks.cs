using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class WindowPreviewChecks
    {
        internal static int Run(bool inspect)
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;var sources=new List<Window>();WindowPreview preview=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try {
                    var entry=new AppEntry {Name="미리보기 테스트",Target="OneTaskBar.PreviewFixture"};
                    foreach(var color in new[]{Colors.Crimson,Colors.RoyalBlue}){var source=new Window {Title="미리보기 테스트 · "+(sources.Count+1),Width=420,Height=280,Background=new SolidColorBrush(color),Content=new TextBlock {Text="실시간 창 미리보기",Foreground=Brushes.White,FontSize=30,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};sources.Add(source);source.Show();entry.Windows.Add(new WindowInteropHelper(source).Handle);}
                    sources[1].Activate();preview=new WindowPreview(entry,new WindowInteropHelper(sources[1]).Handle);preview.Fit(740);preview.Left=80;preview.Top=80;preview.Show();await Task.Delay(450);bool registered=preview.RegisteredThumbnails==2;var actual=preview.ThumbnailSize;bool enlarged=actual.Width>=290&&actual.Height>=165;var handle=new WindowInteropHelper(preview).Handle;int styles=Native.GetWindowLong(handle,-20);bool excluded=(styles&0x80)!=0&&(styles&0x40000)==0,noActivate=(styles&0x08000000)!=0;
                    sources[0].Content=new TextBlock {Text="내용이 바뀌는 실제 창",FontSize=30,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};await Task.Delay(inspect?60000:200);
                    preview.Fit(300);await Task.Delay(100);bool narrow=preview.Width<=300&&preview.RegisteredThumbnails==1;var button=preview.FirstPreview;var peer=UIElementAutomationPeer.CreatePeerForElement(button);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();await Task.Delay(120);bool activated=Native.GetForegroundWindow()==new WindowInteropHelper(sources[0]).Handle,released=preview.RegisteredThumbnails==0;
                    result=registered&&enlarged&&excluded&&noActivate&&narrow&&activated&&released?0:1;AppLog.Json("window-preview-probe.json",new {passed=result==0,twoLiveDwmThumbnails=registered,actualThumbnailWidth=actual.Width,actualThumbnailHeight=actual.Height,contentEnlarged=enlarged,excludedFromAltTab=excluded,doesNotStealFocus=noActivate,narrowMonitorPagination=narrow,clickActivatesSource=activated,thumbnailResourcesReleased=released,onlyFixtureWindowsUsed=true});
                }catch(Exception error){AppLog.Json("window-preview-probe.json",new {passed=false,error=error.ToString()});}
                finally {if(preview!=null&&preview.IsVisible)preview.Close();foreach(var source in sources)source.Close();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
