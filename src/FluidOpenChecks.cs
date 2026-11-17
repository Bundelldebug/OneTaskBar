using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // The start menu and the search window open through the Fluid unfold and end as their plain windows.
    internal static class FluidOpenChecks
    {
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Shot(Rect area,string path)
        {
            using(var bitmap=new System.Drawing.Bitmap((int)area.Width,(int)area.Height))
            {
                using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen((int)area.X,(int)area.Y,0,0,bitmap.Size);
                bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;IDisposable motion=null;ShellController shell=null;Window backdrop=null;
            int old=Preferences.Current.UIVersion;bool oldStart=Preferences.Current.NativeStartButton,oldSearch=Preferences.Current.NativeSearch;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try
                {
                    Program.Inspectable=true;
                    if(!Fluid.Available){AppLog.Json("fluid-open-probe.json",new {passed=true,fluidAvailable=false});result=0;return;}
                    Preferences.Set(p=>p.UIVersion=Fluid.Version);Preferences.Current.NativeStartButton=false;Preferences.Current.NativeSearch=false;
                    motion=Motion.WithAnimationsForFixture();
                    shell=new ShellController(true);shell.Apps=await BackgroundWork.Run(()=>Catalog.Read(false));shell.Pins=shell.Apps.Take(18).Select(a=>a.Copy()).ToList();foreach(var pin in shell.Pins)pin.Pinned=true;shell.StartPins.Replace(shell.Apps.Take(18));
                    shell.StartMenu=new StartWindow(shell);
                    var work=SystemParameters.WorkArea;
                    var group=new DrawingGroup();
                    for(int i=0;i<80;i++)group.Children.Add(new GeometryDrawing(i%2==0?Brushes.OrangeRed:Brushes.DeepSkyBlue,null,new RectangleGeometry(new Rect(i*24,0,24,1200))));
                    backdrop=new Window {WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,ShowActivated=false,Left=work.Left,Top=work.Top,Width=work.Width,Height=work.Height,Background=new DrawingBrush(group){Stretch=Stretch.None,TileMode=TileMode.None}};
                    backdrop.Show();await Task.Delay(300);
                    var report=new List<string>();
                    foreach(string which in new[]{"start","search"})
                    {
                        if(which=="start")shell.ToggleStart(null);else shell.ToggleSearch(null);
                        GlassWindow panel=which=="start"?(GlassWindow)shell.StartMenu:shell.Finder;
                        Require(panel!=null&&panel.IsVisible,which+": opens");
                        bool bud=panel.BudActive;
                        var area=new Rect(0,0,Forms().Width,Forms().Height);
                        var states=new List<string>();int waited=0;
                        foreach(int at in new[]{30,90,150,220,300,400,520,700})
                        {
                            await Task.Delay(at-waited);waited=at;
                            states.Add(at+":bud="+panel.BudActive+" clip="+(panel.BackdropClip.HasValue?panel.BackdropClip.Value.ToString():"-")+" op="+panel.Surface.Opacity.ToString("0.00")+" child="+(panel.Surface.Child!=null?panel.Surface.Child.Opacity.ToString("0.00"):"-"));
                            Shot(area,Path.Combine(AppLog.Data,"open-"+which+"-"+at+".png"));
                        }
                        await Task.Delay(500);
                        report.Add(which+": budAtOpen="+bud+" | "+string.Join(" ; ",states));
                        Require(bud,which+": the Fluid unfold runs when it opens");
                        Require(!panel.BudActive&&!panel.BackdropClip.HasValue,which+": the unfold ends as the plain window");
                        panel.HideImmediately();await Task.Delay(300);
                    }
                    AppLog.Json("fluid-open-probe.json",new {passed=true,report=report.ToArray()});
                    result=0;
                }
                catch(Exception error){AppLog.Json("fluid-open-probe.json",new {passed=false,error=error.ToString()});}
                finally
                {
                    Preferences.Set(p=>p.UIVersion=old);Preferences.Current.NativeStartButton=oldStart;Preferences.Current.NativeSearch=oldSearch;
                    if(backdrop!=null)backdrop.Close();if(shell!=null)shell.Dispose();if(motion!=null)motion.Dispose();app.Shutdown(result);
                }
            }),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
        static System.Drawing.Rectangle Forms(){return System.Windows.Forms.Screen.PrimaryScreen.Bounds;}
    }
}