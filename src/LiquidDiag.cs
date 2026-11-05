using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    // A manual diagnostic, run with --liquid-diag and an isolated ONE_TASKBAR_DATA: a striped, colored scene
    // behind the quick panel, captured with the liquid material off and then on, so the refraction at the
    // panel's edges can be judged by eye. It never touches saved settings outside that data folder.
    internal static class LiquidDiag
    {
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;
            application.Dispatcher.BeginInvoke(new Action(async()=>
            {
                Window scene=null;QuickWindow quick=null;
                try
                {
                    Program.Inspectable=true;
                    Preferences.Current.UIVersion=95;Preferences.Current.Blur=true;
                    var shell=new ShellController(true);
                    var screen=System.Windows.Forms.Screen.PrimaryScreen;
                    var grid=new Grid {Background=Brushes.White};
                    var colors=new[]{Colors.OrangeRed,Colors.Gold,Colors.LimeGreen,Colors.DeepSkyBlue,Colors.MediumPurple,Colors.HotPink};
                    for(int i=0;i<30;i++)
                    {
                        grid.Children.Add(new Border {Width=22,Height=900,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(i*44,0,0,0),Background=new SolidColorBrush(colors[i%colors.Length])});
                    }
                    for(int i=0;i<18;i++)
                    {
                        grid.Children.Add(new Border {Width=1500,Height=3,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,i*50,0,0),Background=Brushes.Black});
                    }
                    scene=new Window {Title="liquid diag scene",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,ShowActivated=false,Width=1100,Height=860,Left=10,Top=10,Content=grid};
                    scene.Show();
                    foreach(bool liquid in new[]{false,true})
                    {
                        Preferences.Current.LiquidGlass=liquid;
                        quick=new QuickWindow(shell);shell.Quick=quick;
                        quick.ShowAt(screen.Bounds,120,60,380,720);
                        quick.Refresh(shell.Power,64,35,false,true,true);
                        for(int i=0;i<12;i++){await Task.Delay(60);}
                        var r=new System.Drawing.Rectangle((int)(quick.Left-24),(int)(quick.Top-24),(int)(quick.Width+48),(int)(quick.Height+48));
                        using(var shot=new System.Drawing.Bitmap(r.Width,r.Height))
                        using(var g=System.Drawing.Graphics.FromImage(shot))
                        {
                            g.CopyFromScreen(r.Left,r.Top,0,0,r.Size);
                            shot.Save(Path.Combine(AppLog.Data,liquid?"liquid-diag-on.png":"liquid-diag-off.png"));
                        }
                        quick.Close();
                        await Task.Delay(300);
                    }
                    result=0;
                }
                catch(Exception error)
                {
                    File.WriteAllText(Path.Combine(AppLog.Data,"liquid-diag-error.txt"),error.ToString());
                }
                finally
                {
                    try{if(scene!=null)scene.Close();}catch(Exception){}
                    application.Shutdown(result);
                }
            }),DispatcherPriority.ApplicationIdle);
            return application.Run();
        }
    }
}
