using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OneTaskBar
{
    // Manual: shows the two top status pieces on the main monitor over the real desktop and saves the top strip.
    internal static class TopBarDiag
    {
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;
            application.Dispatcher.BeginInvoke(new Action(async()=>
            {
                TopStatusBar left=null,right=null;
                try
                {
                    Program.Inspectable=true;
                    Preferences.Current.UIVersion=95;
                    var shell=new ShellController(true);
                    var screen=System.Windows.Forms.Screen.PrimaryScreen;
                    double dpi=System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width/SystemParameters.PrimaryScreenWidth;
                    shell.Services.Media=new MediaState {Available=true,Active=true,Playing=true,CanPlay=true,CanPrevious=true,CanNext=true,CanSeek=true,CanShuffle=true,Title="Sunflower -Part 1-",Artist="Kazuki Yanagawa",Duration=214,Position=80,Artwork=ArtworkPalette.Sample(System.Windows.Media.Color.FromRgb(210,120,60))};
                    left=new TopStatusBar(shell,TopStatusBar.Side.Left);right=new TopStatusBar(shell,TopStatusBar.Side.Right);
                    left.Present(screen,dpi,false);right.Present(screen,dpi,false);
                    await Task.Delay(900);
                    left.Present(screen,dpi,false);right.Present(screen,dpi,false);
                    await Task.Delay(600);
                    left.ToggleMediaCard();
                    int shotIndex=0;
                    foreach(int wait in new[]{60,70,90,160})
                    {
                        await Task.Delay(wait);
                        using(var step=new System.Drawing.Bitmap((int)(560*dpi),(int)(260*dpi)))
                        using(var sg=System.Drawing.Graphics.FromImage(step))
                        {
                            sg.CopyFromScreen(screen.Bounds.Left,screen.Bounds.Top,0,0,step.Size);
                            step.Save(Path.Combine(AppLog.Data,"topbar-morph-"+(shotIndex++)+".png"));
                        }
                    }
                    await Task.Delay(600);
                    using(var card=new System.Drawing.Bitmap((int)(560*dpi),(int)(260*dpi)))
                    using(var cg=System.Drawing.Graphics.FromImage(card))
                    {
                        cg.CopyFromScreen(screen.Bounds.Left,screen.Bounds.Top,0,0,card.Size);
                        card.Save(Path.Combine(AppLog.Data,"topbar-card.png"));
                    }
                    using(var shot=new System.Drawing.Bitmap(screen.Bounds.Width,Math.Max(60,(int)(34*dpi))))
                    using(var g=System.Drawing.Graphics.FromImage(shot))
                    {
                        g.CopyFromScreen(screen.Bounds.Left,screen.Bounds.Top,0,0,shot.Size);
                        shot.Save(Path.Combine(AppLog.Data,"topbar-diag.png"));
                    }
                    File.WriteAllText(Path.Combine(AppLog.Data,"topbar-diag.txt"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new[]{left.Inspect(),right.Inspect()}));
                    result=0;
                }
                catch(Exception error){File.WriteAllText(Path.Combine(AppLog.Data,"topbar-diag-error.txt"),error.ToString());}
                finally{try{if(left!=null)left.Close();if(right!=null)right.Close();}catch(Exception){}application.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);
            return application.Run();
        }
    }
}
