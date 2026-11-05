using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OneTaskBar
{
    // A manual diagnostic, not a fixture: it opens a real Character Map window, minimizes it, then runs the same
    // restore flight a media app gets and saves what is on screen part-way through.
    // Run with --media-foreign-diag. It never touches saved settings or other windows.
    internal static class MediaForeignDiag
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;
            Process notepad=null;
            application.Dispatcher.BeginInvoke(new Action(async()=>
            {
                try
                {
                    Program.Inspectable=true;
                    notepad=Process.Start("charmap.exe");
                    IntPtr window=IntPtr.Zero;
                    for(int i=0;i<60&&window==IntPtr.Zero;i++)
                    {
                        await Task.Delay(100);
                        notepad.Refresh();
                        window=notepad.MainWindowHandle;
                    }
                    if(window==IntPtr.Zero)throw new Exception("The window did not open");
                    Native.SetWindowPos(window,IntPtr.Zero,300,160,0,0,0x15);
                    await Task.Delay(500);
                    Native.RectI rect;
                    Native.GetWindowRect(window,out rect);
                    var bounds=new Rect(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
                    Native.Activate(window);await Task.Delay(500);var image=MediaWindowSnapshot.Capture(window,bounds,()=>true);
                    var screen=System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                    var capsule=new Rect(screen.Left+screen.Width/2-170,screen.Bottom-150,340,56);
                    using(var enter=new MediaWindowMotion(null,null))
                    {
                        enter.Begin(window,null,IntPtr.Zero,bounds,capsule,true,false,image,null);
                        Native.ShowWindow(window,6);
                        var log=new List<string>();
                        var clock=System.Diagnostics.Stopwatch.StartNew();
                        for(int step=0;step<9;step++)
                        {
                            await Task.Delay(step==0?20:42);
                            var plane=enter.PreviewHandle;
                            Native.RectI pr;bool got=plane!=IntPtr.Zero&&Native.GetWindowRect(plane,out pr);
                            pr=new Native.RectI();if(got)Native.GetWindowRect(plane,out pr);
                            log.Add(clock.ElapsedMilliseconds+"ms visible="+(plane!=IntPtr.Zero&&Native.IsWindowVisible(plane))+" rect="+pr.Left+","+pr.Top+","+pr.Right+","+pr.Bottom+" frames="+enter.FrameCount+" sourceVisible="+Native.IsWindowVisible(window));
                            if(got&&pr.Right>pr.Left)
                            {
                                using(var bitmap=new System.Drawing.Bitmap(pr.Right-pr.Left,pr.Bottom-pr.Top))
                                using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
                                {
                                    graphics.Clear(System.Drawing.Color.FromArgb(255,60,60,70));
                                    var dc=graphics.GetHdc();
                                    try{PrintWindow(plane,dc,2);}finally{graphics.ReleaseHdc(dc);}
                                    bitmap.Save(Path.Combine(AppLog.Data,"foreign-enter-"+step+".png"));
                                }
                            }
                        }
                        File.WriteAllLines(Path.Combine(AppLog.Data,"foreign-enter.txt"),log);
                        await Task.Delay(700);
                    }
                    Native.ShowWindow(window,6);
                    await Task.Delay(700);
                    var shots=new List<string>();var states=new List<string>();bool activeAtStart=false;long planeWindow=0;int frames=0;
                    using(var motion=new MediaWindowMotion(null,null))
                    {
                        motion.Begin(window,null,IntPtr.Zero,capsule,bounds,true,true,image,null);
                        activeAtStart=motion.Active;planeWindow=motion.PreviewHandle.ToInt64();
                        Native.ShowWindow(window,9);
                        foreach(int wait in new[]{70,90,100,110,150})
                        {
                            await Task.Delay(wait);
                            string file=Path.Combine(AppLog.Data,"foreign-restore-"+shots.Count+".png");
                            using(var bitmap=new System.Drawing.Bitmap(screen.Width,screen.Height))
                            using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
                            {
                                graphics.CopyFromScreen(screen.Left,screen.Top,0,0,bitmap.Size);
                                bitmap.Save(file);
                            }
                            shots.Add(file);Native.RectI pr;bool got=Native.GetWindowRect(new IntPtr(planeWindow),out pr);states.Add("plane visible="+Native.IsWindowVisible(new IntPtr(planeWindow))+" rect="+(got?pr.Left+","+pr.Top+","+pr.Right+","+pr.Bottom:"none")+" frames="+motion.FrameCount+" active="+motion.Active+" targetIconic="+Native.IsIconic(window));
                        }
                        await Task.Delay(900);
                    }
                    AppLog.Json("media-foreign-diag.json",new {passed=true,imageNull=image==null,activeAtStart=activeAtStart,planeWindow=planeWindow,frames=frames,shots=shots.ToArray(),states=states.ToArray(),bounds=new {bounds.X,bounds.Y,bounds.Width,bounds.Height}});
                    result=0;
                }
                catch(Exception error)
                {
                    AppLog.Json("media-foreign-diag.json",new {passed=false,error=error.ToString()});
                }
                finally
                {
                    try{if(notepad!=null&&!notepad.HasExited)notepad.Kill();}catch(Exception){}
                    application.Shutdown(result);
                }
            }),DispatcherPriority.ApplicationIdle);
            return application.Run();
        }
    }
}
