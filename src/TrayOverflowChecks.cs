using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class TrayOverflowChecks
    {
        sealed class SourceHost:System.Windows.Forms.NativeWindow,IDisposable
        {
            internal SourceHost(){CreateHandle(new System.Windows.Forms.CreateParams {ClassName="STATIC",Caption="트레이 복원 검사",Style=unchecked((int)0x90000000),Width=80,Height=80});}
            public void Dispose(){DestroyHandle();}
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Tray overflow: "+message);}
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int exit=1;
            TrayOverflowWindow view=null;SourceHost source=null;TrayIconSession recovery=null;bool originalMask=TaskbarMask.Active;
            app.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;
                foreach(double dpi in new[]{1.0,1.5,2.0})foreach(double zoom in new[]{.6,1,1.4})foreach(int edge in new[]{0,1,2,3})
                {
                    var monitor=new System.Drawing.Rectangle(-2560,-600,(int)(1600*dpi),(int)(900*dpi));
                    var anchor=new Point(monitor.Left+monitor.Width/2,monitor.Top+monitor.Height/2);
                    var position=new TrayPlacement(anchor,monitor,dpi,edge,22*zoom);
                    var target=position.Target((int)(240*dpi),(int)(260*dpi));
                    double distance=edge==0?target.X-anchor.X:edge==2?anchor.X-target.X-240*dpi:edge==1?target.Y-anchor.Y:anchor.Y-target.Y-260*dpi;
                    double expected=edge==3?8*dpi:(22*zoom+8)*dpi;
                    Require(position.Scale==dpi&&Math.Abs(distance-expected)<=1,"dock zoom changes the anchor gap, not the popup DPI");
                }
                Require(TrayIcons.Decode(new byte[6])==null&&TrayIcons.Decode(new byte[1024*1024+1])==null,"invalid artwork is bounded");
                var bitmap=ArtworkPalette.Sample(Colors.OrangeRed);
                using(var stream=new MemoryStream()){
                    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));png.Save(stream);
                    var image=TrayIcons.Decode(stream.ToArray()) as BitmapSource;
                    Require(image!=null&&image.IsFrozen&&image.PixelWidth<=64,"native PNG snapshots decode at a bounded size and freeze");
                }
                var mono=new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32);
                var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawEllipse(Brushes.Black,null,new Point(16,16),12,12);mono.Render(drawing);mono.Freeze();
                Require(TrayIconVisual.IsMonochrome(mono)&&!TrayIconVisual.IsMonochrome(bitmap),"only uniform monochrome ink follows popup text");
                var detailed=new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32);var logo=new DrawingVisual();using(var dc=logo.RenderOpen()){dc.DrawEllipse(Brushes.Black,null,new Point(16,16),12,12);dc.DrawEllipse(Brushes.White,null,new Point(16,16),6,6);}detailed.Render(logo);detailed.Freeze();
                Require(!TrayIconVisual.IsMonochrome(detailed),"grayscale logos retain internal white and black details");var visual=new TrayIconVisual(detailed,Brushes.White);visual.Measure(new Size(26,26));visual.Arrange(new Rect(0,0,26,26));var rendered=new RenderTargetBitmap(26,26,96,96,PixelFormats.Pbgra32);rendered.Render(visual);var centerPixel=new byte[4];rendered.CopyPixels(new Int32Rect(13,13,1,1),centerPixel,4,0);Require(centerPixel[0]>230&&centerPixel[1]>230&&centerPixel[2]>230,"original white logo center survives popup rendering");
                int exact=TrayIcons.Score("spotify premium","SpotifyGuid","SpotifyGuid","Spotify","spotify.exe");
                Require(exact>TrayIcons.Score("spotify premium","","Other","Spotify","spotify.exe")&&TrayIcons.Score("","","","","")==0,"identity matching prefers the native GUID and rejects empty labels");
                var items=Enumerable.Range(0,21).Select(i=>new TrayIconItem {Name="검사 아이콘 "+i,Icon=i%2==0?(ImageSource)mono:bitmap}).ToList();
                var session=new TrayIconSession(IntPtr.Zero,items);int invoked=0;
                view=new TrayOverflowWindow(session,(item,context)=>invoked++);
                var screen=System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                var placement=new TrayPlacement(new Point(screen.Right-140,screen.Bottom-65),screen,Native.PrepareMonitor(view,screen));
                view.Present(placement,false);await Task.Delay(160);
                Require(view.IconCount==21&&!view.Watching&&view.DockPalette&&!view.DockSurface,"all icons use a five-column themed popup without changing the dock material");
                Native.RectI bounds;Native.GetWindowRect(new WindowInteropHelper(view).Handle,out bounds);
                Require(bounds.Left>=screen.Left&&bounds.Right<=screen.Right&&bounds.Top>=screen.Top&&bounds.Bottom<=screen.Bottom,"popup stays on its selected monitor");
                var button=((Grid)view.Surface.Child).Children.OfType<Button>().First();button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(150);
                Require(invoked==1&&items.Count==0&&!app.Windows.OfType<TrayOverflowWindow>().Any(),"invocation disposes the source session and closes the popup");
                source=new SourceHost();var handle=source.Handle;
                recovery=new TrayIconSession(handle,new List<TrayIconItem>());
                TaskbarMask.Active=true;bool masked=recovery.Mask();Require(masked&&TaskbarMask.Protected(handle),"native host is represented without a second visible tray");
                TrayIconSession.Restore(handle);recovery.LeaveNative();recovery.Dispose();recovery=null;TaskbarMask.Active=originalMask;
                Require(Native.IsWindowVisible(handle)&&!TaskbarMask.Protected(handle),"recovery and unsupported-provider fallback restore the original host");
                for(int i=0;i<3;i++){
                    view=new TrayOverflowWindow(new TrayIconSession(IntPtr.Zero,new List<TrayIconItem> {new TrayIconItem {Name="검사"}}),(item,context)=>{});
                    view.Present(placement,false);view.HideImmediately();await Task.Delay(30);
                }
                Require(!app.Windows.OfType<TrayOverflowWindow>().Any()&&LiveBackdrop.OrphanCount==0,"interrupted popups leave no windows or backdrop hosts");
                for(int i=0;i<2;i++)
                {
                    view=new TrayOverflowWindow(new TrayIconSession(IntPtr.Zero,new List<TrayIconItem> {new TrayIconItem {Name="검사"}}),(item,context)=>{});
                    view.Present(placement,false);
                    if(i==1)view.Dismiss();
                    view.Close();await Task.Delay(320);
                }
                Require(!app.Windows.OfType<TrayOverflowWindow>().Any()&&LiveBackdrop.OrphanCount==0,"direct close and animation interruption release the tray exactly once");
                AppLog.Json("tray-overflow-probe.json",new {passed=true,icons=21,nativePng=true,monochromeInk=true,nativeHostRecovery=true,realTrayInvoked=false});exit=0;
            }catch(Exception error){AppLog.Json("tray-overflow-probe.json",new {passed=false,error=error.ToString()});}
            finally{if(view!=null&&view.IsVisible)view.Close();if(recovery!=null)recovery.Dispose();TaskbarMask.Active=originalMask;if(source!=null)source.Dispose();app.Shutdown(exit);}}),DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
    }
}
