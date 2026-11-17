using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace OneTaskBar
{
    // A Fluid panel unfolds from the dock piece it belongs to: the piece itself is the first shape, a piece that is
    // the panel (start, search) is hidden while it is open, and one that stays (quick settings) gives up width.
    internal static class FluidExpandChecks
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
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;IDisposable motion=null;int old=Preferences.Current.UIVersion;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                GlassWindow pill=null,panel=null;Window backdrop=null;
                try
                {
                    Program.Inspectable=true;
                    if(!Fluid.Available){AppLog.Json("fluid-expand-probe.json",new {passed=true,fluidAvailable=false});result=0;return;}
                    Preferences.Set(p=>p.UIVersion=Fluid.Version);
                    motion=Motion.WithAnimationsForFixture();
                    var screen=Forms.Screen.PrimaryScreen;var work=SystemParameters.WorkArea;
                    var report=new System.Collections.Generic.List<string>();
                    foreach(bool detach in new[]{false,true})
                    {
                        string name=detach?"detach":"expand";
                        var group=new DrawingGroup();
                        for(int i=0;i<40;i++)group.Children.Add(new GeometryDrawing(i%2==0?Brushes.OrangeRed:Brushes.DeepSkyBlue,null,new RectangleGeometry(new Rect(i*20,0,20,700))));
                        backdrop=new Window {WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,ShowActivated=false,Left=work.Right-460,Top=work.Bottom-560,Width=460,Height=560,Background=new DrawingBrush(group){Stretch=Stretch.None,TileMode=TileMode.None}};
                        backdrop.Show();                        pill=new GlassWindow {Radius=20,ShowActivated=false};
                        var grid=new Grid();grid.Children.Add(new TextBlock {Text="상태 · 12:34 · 80%",Foreground=Brushes.White,FontSize=14,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16,0,16,0)});
                        pill.Surface.Child=grid;
                        pill.ShowAt(screen.Bounds,work.Right-380,work.Bottom-48,360,40);
                        await Task.Delay(200);
                        Native.RectI rect;Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(pill).Handle,out rect);
                        panel=new GlassWindow {Width=380,Height=300,ShowActivated=false};
                        panel.Surface.Child=new TextBlock {Text="패널",Foreground=Brushes.White,Margin=new Thickness(24),FontSize=18};
                        panel.MorphFixed=true;panel.MorphSource=rect;panel.MorphPiece=pill;panel.MorphDetach=detach;
                        panel.ShowAt(screen.Bounds,work.Right-400,work.Bottom-48-12-300,380,300);
                        panel.Pop();
                        Require(panel.BudActive,name+": the panel opens through the morph");
                        var area=new Rect(screen.Bounds.Right-460,screen.Bounds.Bottom-420,460,420);
                        int waited=0;
                        foreach(int at in new[]{20,70,130,200,290,420,560})
                        {
                            await Task.Delay(at-waited);waited=at;
                            Shot(area,Path.Combine(AppLog.Data,"expand-"+name+"-"+at+".png"));
                            if(at==20)Require(panel.BackdropClip.HasValue&&panel.BackdropClip.Value.Bottom>300+8,name+": the first shape sits down in the piece: "+(panel.BackdropClip.HasValue?panel.BackdropClip.Value.ToString():"none"));
                        }
                        await Task.Delay(500);
                        Require(!panel.BudActive&&!panel.BackdropClip.HasValue,name+": the unfold ends as the plain panel");
                        if(detach)Require(pill.BackdropClip.HasValue&&pill.BackdropClip.Value.Width<pill.Surface.ActualWidth-20&&pill.Opacity>.99,name+": the piece that stays has shrunk: "+(pill.BackdropClip.HasValue?pill.BackdropClip.Value.Width.ToString("0"):"none"));
                        else Require(pill.Opacity<.01,name+": the piece that became the panel is hidden");
                        Shot(area,Path.Combine(AppLog.Data,"expand-"+name+"-open.png"));
                        panel.Hide();
                        foreach(int at in new[]{60,140,230})
                        {
                            await Task.Delay(at-(at==60?0:at==140?60:140));
                            Shot(area,Path.Combine(AppLog.Data,"expand-"+name+"-close-"+at+".png"));
                        }
                        await Task.Delay(500);
                        Require(!panel.IsVisible,name+": the panel is hidden after it folds back");
                        Require(pill.Opacity>.99&&!pill.BackdropClip.HasValue,name+": the piece is whole again");
                        report.Add(name+" ok");
                        panel.Close();pill.Close();backdrop.Close();panel=null;pill=null;backdrop=null;
                        await Task.Delay(200);
                    }
                    AppLog.Json("fluid-expand-probe.json",new {passed=true,steps=report.ToArray()});
                    result=0;
                }
                catch(Exception error){AppLog.Json("fluid-expand-probe.json",new {passed=false,error=error.ToString()});}
                finally
                {
                    Preferences.Set(p=>p.UIVersion=old);
                    if(panel!=null)panel.Close();if(pill!=null)pill.Close();if(backdrop!=null)backdrop.Close();if(motion!=null)motion.Dispose();app.Shutdown(result);
                }
            }),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
    }
}