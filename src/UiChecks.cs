using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class UiChecks
    {
        static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
        {var item=root as T;if(item!=null)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
        internal static int Run(string label)
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;DrawerWindow drawer=null;int result=1;
            app.Dispatcher.BeginInvoke(new Action(async()=> {
                try {
                    shell=new ShellController(true);shell.Apps=await Task.Run(()=>Catalog.Read());drawer=new DrawerWindow(shell);drawer.Configure(720,680);drawer.Reload();var screen=System.Windows.Forms.Screen.PrimaryScreen;drawer.ShowAt(screen.Bounds,24,24,720,680);drawer.Pop();drawer.Activate();
                    var tint=drawer.Surface.Background;bool stable=true;var frames=new List<object>();string output=Path.Combine(AppLog.Data,"ui-frames");Directory.CreateDirectory(output);
                    var watch=Stopwatch.StartNew();for(int i=0;i<9;i++){await Task.Delay(32);bool current=drawer.Surface.Opacity==1&&drawer.Surface.RenderTransform.Value.IsIdentity&&drawer.Surface.Background==tint;stable&=current;frames.Add(new {milliseconds=watch.ElapsedMilliseconds,stable=current,contentOpacity=drawer.TransitionContent.Opacity});if(i==1||i==7)Program.Save(drawer.Surface,720,680,Path.Combine(output,"apps-"+i+".png"));}
                    var bitmap=new RenderTargetBitmap(1440,1360,192,192,PixelFormats.Pbgra32);bitmap.Render(drawer.Surface);byte[] pixels=new byte[80*80*4];bitmap.CopyPixels(new Int32Rect(0,0,80,80),pixels,80*4,0);var alpha=new HashSet<byte>();for(int i=3;i<pixels.Length;i+=4)alpha.Add(pixels[i]);bool soft=alpha.Count>8&&pixels[3]==0&&drawer.SoftCorners&&drawer.Radius==32;
                    var scroll=Descendants<SmoothScrollViewer>(drawer.Surface).First();scroll.ScrollToTop();await Task.Delay(50);for(int i=0;i<12;i++){scroll.CheckWheel(-10);await Task.Delay(12);}await Task.Delay(80);double expected=Math.Min(scroll.ScrollableHeight,Math.Max(0,SmoothScrollViewer.Distance(-120,SystemParameters.WheelScrollLines,scroll.ViewportHeight)));bool precise=Math.Abs(scroll.VerticalOffset-expected)<1;
                    bool taskManager=WindowPicker.Menu(new AppEntry {Name="Example app"},shell).Items.OfType<MenuItem>().Any(i=>(i.Header as string)=="작업 관리자 열기");
                    for(int i=0;i<3;i++){await drawer.DismissAsync();drawer.ShowAt(screen.Bounds,24,24,720,680);drawer.Pop();await Task.Delay(230);}await drawer.DismissAsync();
                    var process=Process.GetCurrentProcess();process.Refresh();var memory=new {privateBytes=process.PrivateMemorySize64,workingSet=process.WorkingSet64,managedBytes=GC.GetTotalMemory(false),apps=shell.Apps.Count};
                    result=stable&&soft&&precise&&taskManager?0:1;AppLog.Json("ui-"+label+".json",new {passed=result==0,stableBackdrop=stable,roundedSoftCorners=soft,edgeAlphaLevels=alpha.Count,precisionScrolling=precise,scrollExpected=expected,scrollActual=scroll.VerticalOffset,taskManagerMenu=taskManager,frames=frames.ToArray(),memory=memory});
                }catch(Exception error){AppLog.Json("ui-"+label+".json",new {passed=false,error=error.ToString()});}
                finally {if(drawer!=null)drawer.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
