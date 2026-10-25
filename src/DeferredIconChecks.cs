using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class DeferredIconChecks
    {
        static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject {var current=root as T;if(current!=null)yield return current;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};GlassWindow window=null;IDisposable scope=null;int result=1;bool oldAnimations=Preferences.Current.Animations;
            application.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;scope=Motion.WithAnimationsForFixture();Preferences.Current.Animations=true;string file=Path.Combine(AppLog.Root,"assets","OneTaskBar.ico");if(!File.Exists(file))throw new InvalidOperationException("Native icon fixture missing");
                var entries=Enumerable.Range(0,9).Select(i=>new AppEntry {Name="지연 아이콘 "+i,AppID="fixture.deferred."+Guid.NewGuid().ToString("N"),Target=file}).ToArray();var grid=new System.Windows.Controls.Primitives.UniformGrid {Columns=3};foreach(var entry in entries)grid.Children.Add(AppIcons.Visual(entry,40));window=new GlassWindow {SmoothLauncher=true,Width=360,Height=240};window.Surface.Child=grid;window.TransitionContent=grid;window.ShowAt(System.Windows.Forms.Screen.PrimaryScreen.Bounds,20,20,360,240);window.Pop();
                // A replaced/missing WPF completion must not gate independent icon loading forever.
                var transform=grid.RenderTransform as TransformGroup;if(transform!=null){var scale=transform.Children.OfType<ScaleTransform>().FirstOrDefault();if(scale!=null)scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);}
                await Task.Delay(750);int sourceReady=await BackgroundWork.Run(()=>entries.Count(a=>AppIcons.Get(a)!=null));int displayed=Children<Image>(grid).Count(i=>i.Source!=null);bool settled=!LauncherMotion.Active(grid)&&grid.CacheMode==null;var report=new {passed=sourceReady==entries.Length&&displayed==entries.Length&&settled,sourceIconsReady=sourceReady,displayedIcons=displayed,expected=entries.Length,completionReplaced=true,motionSettled=settled,osInputSent=false};AppLog.Json("deferred-icon-probe.json",report);if(!report.passed)return;
                window.HideImmediately();window.ShowAt(System.Windows.Forms.Screen.PrimaryScreen.Bounds,20,20,360,240);window.Pop();await Task.Delay(350);if(Children<Image>(grid).Count(i=>i.Source!=null)!=entries.Length||LauncherMotion.Active(grid))throw new InvalidOperationException("Reopen loses loaded icons");result=0;
            }catch(Exception error){AppLog.Json("deferred-icon-probe.json",new {passed=false,error=error.ToString()});}finally{if(window!=null)window.Close();if(scope!=null)scope.Dispose();Preferences.Current.Animations=oldAnimations;application.Shutdown(result);}}),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
