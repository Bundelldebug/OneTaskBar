using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class SettingsVisibilityChecks
    {
        static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject {var value=root as T;if(value!=null)yield return value;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;IDisposable scope=null;int result=1;bool oldAnimations=Preferences.Current.Animations;
            application.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;scope=Motion.WithAnimationsForFixture();Preferences.Current.Animations=true;shell=new ShellController(true);var window=new WelcomeWindow(shell);shell.Welcome=window;window.Show();window.Pop();await Task.Delay(360);window.OpenSettings("데스크톱");await Task.Delay(180);var root=window.TransitionContent;if(root==null)throw new InvalidOperationException("Settings content missing");
                // SetDesktop previously hid settings before its toggle handler called Show alone.
                if(Children<Button>(window).Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="빠른 설정"))throw new InvalidOperationException("Settings retains the retired footer shortcut");
                if(window.ExitSettingsButton==null)throw new InvalidOperationException("Settings list has no exit entry");
                window.SettingsHome();await Task.Delay(300);
                foreach(var choice in UIVersions.Available)
                {
                    window.VersionPicker.IsDropDownOpen=true;window.VersionPicker.SelectedItem=choice;window.UpdateLayout();
                    if(window.VersionPreview.Version!=choice.Value||window.VersionDescription.Text!=choice.Description||String.IsNullOrWhiteSpace(choice.Description))
                        throw new InvalidOperationException("UI version illustration and description do not follow selection");
                }
                window.VersionPicker.SelectedValue=Preferences.Current.UIVersion;window.VersionPicker.IsDropDownOpen=false;
                window.OpenSettings("데스크톱");await Task.Delay(300);
                for(int i=0;i<4;i++)
                {
                    window.SettingsHome();if(!window.SettingsAnimating)throw new InvalidOperationException("Settings return has no motion");
                    await Task.Delay(25);window.OpenSettings(i%2==0?"화면":"데스크톱");
                    if(!window.SettingsAnimating)throw new InvalidOperationException("Settings category has no motion");
                    await Task.Delay(310);
                    if(window.SettingsAnimating||root.Opacity!=1||!root.RenderTransform.Value.IsIdentity)throw new InvalidOperationException("Settings navigation retains a transform after completion");
                }
                window.OpenSettings("데스크톱");
                for(int i=0;i<5;i++){window.Hide();await Task.Delay(20+i*15);window.Show();await Task.Delay(370);if(!window.IsVisible||window.IsDismissing||root.Opacity<.999||Math.Abs(root.RenderTransform.Value.M11-1)>.001||!Children<ToggleSwitch>(root).Any(t=>t.IsVisible&&System.Windows.Automation.AutomationProperties.GetName(t)=="Windows 작업표시줄 대체"))throw new InvalidOperationException("Settings becomes an empty surface after reopening during dismissal");}
                Preferences.Current.Animations=false;window.Hide();window.Show();if(!window.IsVisible||root.Opacity!=1)throw new InvalidOperationException("Settings reopen with animations disabled");Program.Save(window.Surface,(int)window.Width,(int)window.Height,System.IO.Path.Combine(AppLog.Data,"settings-reopen-visible.png"));AppLog.Json("settings-visibility-probe.json",new {passed=true,showAloneReversesPendingHide=true,repeatedReopenVisible=true,animationsDisabled=true,taskbarModeChanged=false,osInputSent=false});result=0;
            }catch(Exception error){AppLog.Json("settings-visibility-probe.json",new {passed=false,error=error.ToString()});}finally{if(shell!=null)shell.Dispose();Preferences.Current.Animations=oldAnimations;if(scope!=null)scope.Dispose();application.Shutdown(result);}}),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
