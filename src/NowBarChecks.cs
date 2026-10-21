using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class NowBarChecks
    {
        [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Native.PointI point);
        static MediaState Track(string title,bool playing){return new MediaState {Available=true,Active=true,Playing=playing,CanPlay=true,CanPrevious=true,CanNext=true,CanSeek=true,Title=title,Artist="One TaskBar test fixture",Duration=210,Position=42,SampledUtc=DateTime.UtcNow,Artwork=ArtworkPalette.Sample(Color.FromRgb(224,43,66))};}
        static void Click(Button button){var peer=UIElementAutomationPeer.CreatePeerForElement(button);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();}
        static CroppedBitmap Capture(NowBar bar)
        {
            bar.UpdateLayout();var root=(FrameworkElement)bar.Content;var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*2),(int)Math.Ceiling(root.ActualHeight*2),192,192,PixelFormats.Pbgra32);bitmap.Render(root);var origin=bar.Surface.TransformToAncestor(root).Transform(new Point());return new CroppedBitmap(bitmap,new Int32Rect((int)Math.Round(origin.X*2),(int)Math.Round(origin.Y*2),(int)Math.Round(bar.Surface.ActualWidth*2),(int)Math.Round(bar.Surface.ActualHeight*2)));
        }
        static void Save(NowBar bar,string name){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Capture(bar)));using(var stream=File.Create(Path.Combine(AppLog.Data,name)))encoder.Save(stream);}
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;NowBar bar=null;Window behind=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try {
                    Program.Inspectable=true;NowBarLayout.Check();shell=new ShellController(true);var screen=System.Windows.Forms.Screen.PrimaryScreen;var commands=new List<string>();var seeks=new List<double>();bar=new NowBar(shell,screen,c=>commands.Add(c),v=>{seeks.Add(v);return Task.FromResult(true);});
                    new WindowInteropHelper(bar).EnsureHandle();Native.SetWindowPos(new WindowInteropHelper(bar).Handle,IntPtr.Zero,screen.Bounds.Left+12,screen.Bounds.Top+12,0,0,0x1|0x4|0x10);double scale=Math.Max(96,Native.GetDpiForWindow(new WindowInteropHelper(bar).Handle))/96.0,y=screen.Bounds.Height/scale-56,start=340;
                    behind=new Window {ShowInTaskbar=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Topmost=true,Background=Brushes.SlateGray,Width=400,Height=240};behind.Show();Native.Place(behind,screen.Bounds,12,y-196,400,240);
                    var shortTrack=Track("테스트 음악",true);bar.Update(shortTrack,scale,start,y,true);await Task.Delay(400);var shortShape=bar.Shape;var outer=bar.PointToScreen(new Point(350,25));bool clickThrough=Native.GetAncestor(WindowFromPoint(new Native.PointI {X=(int)outer.X,Y=(int)outer.Y}),2)==new WindowInteropHelper(behind).Handle;Save(bar,"nowbar-short.png");
                    string longTitle=String.Join(" · ",Enumerable.Repeat("긴 미디어 제목의 캡슐과 자동 스크롤 확인",5));var longTrack=Track(longTitle,true);bar.Update(longTrack,scale,start,y,true);await Task.Delay(400);var longShape=bar.Shape;await Task.Delay(1500);bool marquee=bar.MarqueeOverflow&&bar.TitleOffset>0;Save(bar,"nowbar-long.png");
                    Click(bar.CompactButton);var heights=new List<double>();for(int i=0;i<21;i++){await Task.Delay(18);Native.RectI rect;Native.GetWindowRect(new WindowInteropHelper(bar).Handle,out rect);heights.Add(bar.Surface.ActualHeight);}bool grew=bar.Expanded&&heights.Any(h=>h>45&&h<187)&&Math.Abs(bar.Shape.Height-188)<.1;
                    Save(bar,"nowbar-expanded.png");Click(bar.PlayButton);Click(bar.NextButton);Click(bar.PreviousButton);await Task.Delay(50);var peer=UIElementAutomationPeer.CreatePeerForElement(bar.SeekBar);((IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue)).SetValue(65);await Task.Delay(40);bool transport=commands.SequenceEqual(new[]{"play","next","previous"})&&seeks.SequenceEqual(new[]{65.0});
                    bar.Update(Track(longTitle,false),scale,start,y,true);bool paused=bar.PlayKind=="play";bar.SetExpanded(false);await Task.Delay(70);var before=bar.Shape;bar.SetExpanded(true);bool continuous=before==bar.Shape;await Task.Delay(400);bar.SetExpanded(false);var collapse=new List<double>();var gaps=new List<double>();for(int i=0;i<21;i++){await Task.Delay(16);collapse.Add(bar.Surface.ActualHeight);gaps.Add(bar.MaximumFrameGap);}bool smoothCollapse=collapse.Zip(collapse.Skip(1),(a,b)=>b<=a+.01).All(v=>v)&&bar.FrameCount>=8;bar.Update(shortTrack,scale,start,y,true);await Task.Delay(400);bool shrank=bar.Shape.Height==44&&bar.Shape.Width<longShape.Width&&bar.Radius==22;
                    var bitmap=Capture(bar);var pixels=new byte[48*48*4];bitmap.CopyPixels(new Int32Rect(0,0,48,48),pixels,48*4,0);var alpha=new HashSet<byte>();for(int i=3;i<pixels.Length;i+=4)alpha.Add(pixels[i]);bool soft=alpha.Count>8&&pixels[3]==0;
                    int style=Native.GetWindowLong(new WindowInteropHelper(bar).Handle,-20);bool tool=(style&0x80)!=0&&(style&0x40000)==0;await Task.Delay(950);bar.Update(new MediaState(),scale,start,y,true);await Task.Delay(220);bool gone=!bar.IsVisible;bar.Update(shortTrack,scale,start,y,true);await Task.Delay(400);bool reappears=bar.IsVisible&&bar.Shape.Width>44&&bar.Shape.Height==44;bool color=bar.Palette.Dominant.R>bar.Palette.Dominant.G+80&&bar.Palette.MinimumContrast>=5.4;
                    result=shortShape.Width<longShape.Width&&longShape.Width<=316.01&&marquee&&grew&&transport&&paused&&continuous&&shrank&&soft&&tool&&gone&&reappears&&color&&clickThrough&&smoothCollapse?0:1;
                    AppLog.Json("nowbar-probe.json",new {passed=result==0,shortWidth=shortShape.Width,cappedWidth=longShape.Width,collapsedHeight=shortShape.Height,marqueeMoves=marquee,expandedHeight=188,actualGrowthFrames=heights.ToArray(),actualCollapseFrames=collapse.ToArray(),collapseFrames=bar.FrameCount,maximumCollapseGap=gaps.Max(),clicksPassThroughOutsideCapsule=clickThrough,smoothCollapse=smoothCollapse,singleWindowMorph=grew,transportDispatch=commands.ToArray(),seekDispatch=seeks.ToArray(),pausedIcon=paused,interruptedMorphContinuous=continuous,shrinks=shrank,edgeAlphaLevels=alpha.Count,softCorners=soft,excludedFromAltTab=tool,hidesAfterSessionEnds=gone,reappearsAtFullWidth=reappears,artworkColor=color,titleContrast=bar.Palette.MinimumContrast,realPlayerChanged=false});
                }catch(Exception error){AppLog.Json("nowbar-probe.json",new {passed=false,error=error.ToString()});}
                finally {if(bar!=null)bar.Close();if(behind!=null)behind.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
