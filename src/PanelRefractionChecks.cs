using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // The edge of an ordinary liquid panel bends the screen behind it when the refraction option is on.
    internal static class PanelRefractionChecks
    {
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Capture(Window window,string path)
        {
            Native.RectI rect;
            Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(window).Handle,out rect);
            int pad=40;
            using(var bitmap=new System.Drawing.Bitmap(rect.Right-rect.Left+pad*2,rect.Bottom-rect.Top+pad*2))
            {
                using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen(rect.Left-pad,rect.Top-pad,0,0,bitmap.Size);
                bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;IDisposable motion=null;Window stripes=null;GlassWindow off=null,on=null;
            bool oldRefraction=Preferences.Current.LiquidRefraction,oldLiquid=Preferences.Current.LiquidGlass;int oldVersion=Preferences.Current.UIVersion;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try
                {
                    Program.Inspectable=true;motion=Motion.WithAnimationsForFixture();Preferences.Current.Animations=true;
                    Preferences.Current.UIVersion=95;Preferences.Current.LiquidGlass=true;Preferences.Current.Blur=true;
                    var screen=System.Windows.Forms.Screen.PrimaryScreen;
                    var group=new DrawingGroup();
                    for(int i=0;i<60;i++)group.Children.Add(new GeometryDrawing(i%2==0?Brushes.OrangeRed:Brushes.DeepSkyBlue,null,new RectangleGeometry(new Rect(i*16,0,16,900))));
                    stripes=new Window {WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Left=SystemParameters.WorkArea.Left+100,Top=SystemParameters.WorkArea.Top+80,Width=720,Height=520,Background=new DrawingBrush(group){Stretch=Stretch.None,TileMode=TileMode.None}};
                    stripes.Show();await Task.Delay(300);
                    Func<GlassWindow> make=()=>{var w=new GlassWindow();w.Surface.Child=new System.Windows.Controls.TextBlock {Text="리퀴드 글래스 패널",Foreground=Brushes.White,FontSize=20,Margin=new Thickness(40)};return w;};
                    Preferences.Current.LiquidRefraction=false;
                    off=make();off.ShowAt(screen.Bounds,SystemParameters.WorkArea.Left+260,SystemParameters.WorkArea.Top+160,380,240);await Task.Delay(700);
                    Capture(off,Path.Combine(AppLog.Data,"panel-off.png"));bool offState=off.RimRefracting;off.Close();off=null;await Task.Delay(300);
                    Preferences.Current.LiquidRefraction=true;
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    on=make();on.ShowAt(screen.Bounds,SystemParameters.WorkArea.Left+260,SystemParameters.WorkArea.Top+160,380,240);watch.Stop();await Task.Delay(700);
                    Capture(on,Path.Combine(AppLog.Data,"panel-on.png"));bool onState=on.RimRefracting;
                    AppLog.Json("panel-refraction-probe.json",new {passed=true,refractingWhenOff=offState,refractingWhenOn=onState,showMs=watch.ElapsedMilliseconds});
                    Require(!offState,"The option off leaves the panel edge alone");
                    Require(onState,"The option on bends the panel edge");
                    result=0;
                }
                catch(Exception error){AppLog.Json("panel-refraction-probe.json",new {passed=false,error=error.ToString()});}
                finally
                {
                    Preferences.Current.LiquidRefraction=oldRefraction;Preferences.Current.LiquidGlass=oldLiquid;Preferences.Current.UIVersion=oldVersion;
                    if(off!=null)off.Close();if(on!=null)on.Close();if(stripes!=null)stripes.Close();if(motion!=null)motion.Dispose();app.Shutdown(result);
                }
            }),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
    }
}