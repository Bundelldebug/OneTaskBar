using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // The refraction option: default off, the glass profile math, and a banner that really draws from the captured backdrop.
    internal static class BannerRefractionChecks
    {
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Math()
        {
            Require(!new PreferenceData().LiquidRefraction,"The refraction option is off by default");
            Require(RefractionGlass.Bevel(0,26)>.99&&RefractionGlass.Bevel(26,26)<.001&&RefractionGlass.Bevel(40,26)<.001,"The bevel is full at the edge and gone past its width");
            double last=2;
            for(double depth=0;depth<=26;depth+=2){double v=RefractionGlass.Bevel(depth,26);Require(v<=last+1e-9,"The bevel only falls inward");last=v;}
            double nx,ny,d=RefractionGlass.Distance(100,0,100,50,20,out nx,out ny);
            Require(System.Math.Abs(d)<1e-6&&nx==1&&ny==0,"A point on the right edge has distance 0 and an outward normal");
            d=RefractionGlass.Distance(0,0,100,50,20,out nx,out ny);
            Require(d<-49&&d>-51,"The centre is half the short side inside");
            d=RefractionGlass.Distance(100,50,100,50,20,out nx,out ny);
            Require(d>5&&nx>.6&&ny>.6,"Past a rounded corner the normal points diagonally outward");
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;IDisposable motion=null;Window backdrop=null;
            bool oldRefraction=Preferences.Current.LiquidRefraction,oldLiquid=Preferences.Current.LiquidGlass;int oldVersion=Preferences.Current.UIVersion;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try
                {
                    Program.Inspectable=true;motion=Motion.WithAnimationsForFixture();Preferences.Current.Animations=true;
                    Math();
                    Preferences.Current.UIVersion=Fluid.Available?100:95;Preferences.Current.LiquidGlass=true;Preferences.Current.Blur=true;
                    shell=new ShellController(true);
                    // Stripes behind the banner make the bend visible in the screenshot.
                    var screen=System.Windows.Forms.Screen.PrimaryScreen;
                    var stripes=new DrawingBrush();var group=new DrawingGroup();
                    for(int i=0;i<40;i++)group.Children.Add(new GeometryDrawing(i%2==0?Brushes.OrangeRed:Brushes.DeepSkyBlue,null,new RectangleGeometry(new Rect(i*20,0,20,800))));
                    stripes.Drawing=group;stripes.Stretch=Stretch.None;stripes.TileMode=TileMode.None;
                    backdrop=new Window {WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Left=SystemParameters.WorkArea.Right-460,Top=SystemParameters.WorkArea.Bottom-330,Width=460,Height=330,Background=stripes};
                    backdrop.Show();await Task.Delay(300);
                    Preferences.Current.LiquidRefraction=false;
                    shell.Banners.Show(BannerEntry(1));var plain=shell.Banners.Last;await Task.Delay(700);
                    Require(!plain.Refracting,"The banner does not refract while the option is off");
                    var plainShot=Capture(plain);shell.Banners.Clear();await Task.Delay(300);
                    Preferences.Current.LiquidRefraction=true;
                    shell.Banners.Show(BannerEntry(2));var banner=shell.Banners.Last;
                    var steps=new System.Collections.Generic.List<System.Drawing.Bitmap>();
                    foreach(int wait in new[]{40,60,80,120,160,200}){await Task.Delay(wait);steps.Add(Capture(banner));}
                    var mid=steps[1];
                    await Task.Delay(500);
                    Require(banner.Refracting==RefractionGlass.Enabled,"The banner refracts when the option and the liquid material are on");
                    if(RefractionGlass.Enabled)
                    {
                        var shot=Capture(banner);
                        Require(shot!=null&&plainShot!=null,"The banner could be captured");
                        Save(shot,"banner-refraction.png");Save(plainShot,"banner-plain.png");for(int i=0;i<steps.Count;i++)if(steps[i]!=null)Save(steps[i],"banner-step"+i+".png");
                    }
                    // The dock glow before the card rises: a dock is added to the shell and the area around both is captured over time.
                    shell.Banners.Clear();await Task.Delay(300);
                    // The card leaves a bar: a pill window stands in for the dock piece and the area around both is captured over time.
                    double dip=Native.PrepareMonitor(backdrop,screen.Bounds);
                    var bar=new Window {WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,Topmost=true,ResizeMode=ResizeMode.NoResize,ShowActivated=false,Width=300,Height=56,
                        Left=SystemParameters.WorkArea.Right-760/dip+120/dip,Top=SystemParameters.WorkArea.Bottom+8};
                    bar.Content=new System.Windows.Controls.Border {CornerRadius=new CornerRadius(28),Background=new SolidColorBrush(Color.FromRgb(60,70,96)),BorderBrush=Brushes.White,BorderThickness=new Thickness(1)};
                    bar.Show();await Task.Delay(300);
                    Native.RectI barRect;Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(bar).Handle,out barRect);
                    NotificationBanners.BarForFixture=()=>barRect;
                    var region=new System.Drawing.Rectangle(barRect.Left-300,barRect.Bottom-440,760,440);
                    shell.Banners.Clear();await Task.Delay(300);
                    shell.Banners.Show(BannerEntry(3));var cueFrames=new System.Collections.Generic.List<System.Drawing.Bitmap>();var glowStates=new System.Collections.Generic.List<string>();
                    foreach(int wait in new[]{10,45,45,45,45,45,45,45,60,100,150}){await Task.Delay(wait);glowStates.Add("glow="+GlowCue.Showing);var shotBitmap=new System.Drawing.Bitmap(region.Width,region.Height);using(var graphics=System.Drawing.Graphics.FromImage(shotBitmap))graphics.CopyFromScreen(region.Location,System.Drawing.Point.Empty,region.Size);cueFrames.Add(shotBitmap);}
                    for(int i=0;i<cueFrames.Count;i++)Save(cueFrames[i],"dock-cue"+i+".png");
                    NotificationBanners.BarForFixture=null;bar.Close();
                    // Other Fluid panels share the flare: opening one lights its glass at the point it grows from.
                    var panel=new GlassWindow();panel.Surface.Child=new System.Windows.Controls.TextBlock {Text="패널",Margin=new Thickness(30)};
                    panel.ShowAt(screen.Bounds,40,40,300,200);await Task.Delay(400);panel.Pop();await Task.Delay(60);
                    string flareState=panel.FlareState;panel.Close();
                    Require(!Fluid.Enabled||flareState.Contains("flaring=True"),"A Fluid panel flares when it opens ("+flareState+")");
                    Require(!Fluid.Enabled||glowStates.Contains("glow=True"),"A glow plays on the bar before the card leaves it");                    AppLog.Json("banner-refraction-probe.json",new {passed=true,flareState=flareState,glowStates=glowStates.ToArray(),enabled=RefractionGlass.Enabled,fluid=Fluid.Enabled,refracting=banner.Refracting});
                    result=0;
                }
                catch(Exception error){AppLog.Json("banner-refraction-probe.json",new {passed=false,error=error.ToString()});}
                finally
                {
                    Preferences.Current.LiquidRefraction=oldRefraction;Preferences.Current.LiquidGlass=oldLiquid;Preferences.Current.UIVersion=oldVersion;
                    if(shell!=null)shell.Dispose();if(backdrop!=null)backdrop.Close();if(motion!=null)motion.Dispose();app.Shutdown(result);
                }
            }),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
        static NotificationEntry BannerEntry(uint id){return new NotificationEntry {Id=id,AppId="OneTaskBar.QA",App="One TaskBar",Title="새로운 알림 "+id,Body="굴절 옵션으로 가장자리에서 뒤 화면이 휘어 보입니다.",Created=DateTimeOffset.Now};}
        static System.Drawing.Bitmap Capture(NotificationBanner banner)
        {
            Native.RectI rect;
            if(!Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(banner).Handle,out rect)||rect.Right-rect.Left<4)return null;
            var bitmap=new System.Drawing.Bitmap(rect.Right-rect.Left,rect.Bottom-rect.Top);
            using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen(rect.Left,rect.Top,0,0,bitmap.Size);
            return bitmap;
        }
        static void Save(System.Drawing.Bitmap bitmap,string name){bitmap.Save(Path.Combine(AppLog.Data,name),System.Drawing.Imaging.ImageFormat.Png);}
    }
}
