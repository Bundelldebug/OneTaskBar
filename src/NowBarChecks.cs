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
using System.Threading;
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
        static bool LeftAligned(NowBar bar)
        {
            var grid=(Grid)bar.CompactButton.Content;var art=(FrameworkElement)grid.Children[0];var text=(FrameworkElement)grid.Children[1];
            double artX=art.TransformToAncestor(bar.CompactButton).Transform(new Point()).X,textX=text.TransformToAncestor(bar.CompactButton).Transform(new Point()).X;
            AppLog.Json("media-alignment-"+(int)bar.Shape.Width+".json",new {grid=grid.ActualWidth,left=grid.Margin.Left,right=grid.Margin.Right,button=bar.CompactButton.ActualWidth,art=artX,text=textX,column=grid.ColumnDefinitions[0].ActualWidth,hostWidth=bar.Width,surfaceWidth=bar.Surface.ActualWidth});
            return Math.Abs(grid.ActualWidth+grid.Margin.Left+grid.Margin.Right-bar.CompactButton.ActualWidth)<.1&&Math.Abs(artX-grid.Margin.Left)<.1&&Math.Abs(textX-grid.Margin.Left-grid.ColumnDefinitions[0].ActualWidth)<.1;
        }
        sealed class InputTarget : IDisposable
        {
            readonly Thread thread;readonly ManualResetEvent ready=new ManualResetEvent(false);Window window;internal IntPtr Handle;Exception failure;
            internal InputTarget(System.Drawing.Rectangle screen,double y){thread=new Thread(()=>{try{window=new Window {ShowInTaskbar=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Topmost=true,Background=Brushes.SlateGray,Width=400,Height=240};window.Show();Native.Place(window,screen,12,y-196,400,240);Handle=new WindowInteropHelper(window).Handle;ready.Set();Dispatcher.Run();}catch(Exception error){failure=error;ready.Set();}});thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();if(!ready.WaitOne(3000))throw new Exception("Input target did not start");if(failure!=null)throw failure;}
            public void Dispose(){if(window!=null)window.Dispatcher.BeginInvoke(new Action(()=>{window.Close();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}));thread.Join(2000);ready.Dispose();}
        }
        static bool PassesOutside(NowBar bar,IntPtr behind)
        {
            Native.RectI backing;if(!Native.GetWindowRect(behind,out backing))return false;Point first=bar.Surface.PointToScreen(new Point()),last=bar.Surface.PointToScreen(new Point(bar.Surface.ActualWidth,bar.Surface.ActualHeight));var visible=new Rect(first,last);visible.Inflate(2,2);int checkedPoints=0;
            foreach(var point in new[]{new Point(backing.Left+12,backing.Top+12),new Point(backing.Right-12,backing.Top+12),new Point(backing.Right-12,backing.Bottom-12),new Point(backing.Left+12,backing.Bottom-12)}){if(visible.Contains(point))continue;checkedPoints++;if(Native.GetAncestor(WindowFromPoint(new Native.PointI {X=(int)point.X,Y=(int)point.Y}),2)!=behind)return false;}return checkedPoints>0;
        }
        static async Task<bool> StableCollapse(NowBar bar,IntPtr behind)
        {
            Native.RectI host,background;var handle=new WindowInteropHelper(bar).Handle;var owner=Native.GetWindow(handle,4);Native.GetWindowRect(handle,out host);Native.GetWindowRect(owner,out background);bar.SetExpanded(false);bool stable=true;
            for(int i=0;i<26;i++){
                await Task.Delay(16);Native.RectI actual,blur;Native.GetWindowRect(handle,out actual);Native.GetWindowRect(owner,out blur);var clip=bar.BackdropClip.Value;var origin=bar.Surface.TransformToAncestor((FrameworkElement)bar.Content).Transform(new Point());
                stable&=actual.Equals(host)&&blur.Equals(background)&&bar.IsVisible&&bar.Surface.Opacity>.999&&Math.Abs(origin.Y-clip.Y)<.1&&Math.Abs(bar.Surface.ActualHeight-clip.Height)<.1;
            }
            return stable&&!bar.Expanded&&bar.Shape.Height==44&&bar.CompactButton.Opacity>.999&&PassesOutside(bar,behind);
        }
        internal static int Run(bool modern=false)
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;NowBar bar=null;InputTarget behind=null;int oldUI=Preferences.Current.UIVersion;double expectedHeight=modern?130:188;bool oldCover=Preferences.Current.NowBarLargeCover,oldAnimations=Preferences.Current.Animations;IDisposable animationFixture=null,pointerFixture=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try {
                    Program.Inspectable=true;animationFixture=Motion.WithAnimationsForFixture();pointerFixture=MenuDismiss.WithSyntheticPointerForFixture();Preferences.Current.Animations=true;Preferences.Current.UIVersion=modern?90:85;NowBarLayout.Check();shell=new ShellController(true);var screen=System.Windows.Forms.Screen.PrimaryScreen;var commands=new List<string>();var seeks=new List<double>();bar=new NowBar(shell,screen,c=>commands.Add(c),v=>{seeks.Add(v);return Task.FromResult(true);});
                    new WindowInteropHelper(bar).EnsureHandle();Native.SetWindowPos(new WindowInteropHelper(bar).Handle,IntPtr.Zero,screen.Bounds.Left+12,screen.Bounds.Top+12,0,0,0x1|0x4|0x10);double scale=Math.Max(96,Native.GetDpiForWindow(new WindowInteropHelper(bar).Handle))/96.0,y=screen.Bounds.Height/scale-56,start=340;
                    behind=new InputTarget(screen.Bounds,y);
                    var shortTrack=Track("곡",true);bar.Update(shortTrack,scale,start,y,true);await Task.Delay(400);var shortShape=bar.Shape;bool shortLeft=LeftAligned(bar);bool clickThrough=PassesOutside(bar,behind.Handle);Save(bar,"nowbar-short.png");
                    string longTitle=String.Join(" · ",Enumerable.Repeat("긴 미디어 제목의 캡슐과 자동 스크롤 확인",5));var longTrack=Track(longTitle,true);bar.Update(longTrack,scale,start,y,true);await Task.Delay(400);var longShape=bar.Shape;bool longLeft=LeftAligned(bar);await Task.Delay(1500);bool marquee=bar.MarqueeOverflow&&bar.TitleOffset>0;Save(bar,"nowbar-long.png");
                    bool synchronized=true,noBitmapCache=true;var synchronization=new List<object>();bool software=HwndSource.FromHwnd(new WindowInteropHelper(bar).Handle).CompositionTarget.RenderMode==RenderMode.SoftwareOnly;
                    Click(bar.CompactButton);var heights=new List<double>();for(int i=0;i<21;i++){await Task.Delay(18);heights.Add(bar.Surface.ActualHeight);var origin=bar.Surface.TransformToAncestor((FrameworkElement)bar.Content).Transform(new Point());var clip=bar.BackdropClip.Value;bool matches=Math.Abs(origin.Y-clip.Y)<.1&&Math.Abs(bar.Surface.ActualHeight-clip.Height)<.1&&Math.Abs(bar.Surface.ActualWidth-clip.Width)<.1;synchronized&=matches;if(!matches)synchronization.Add(new {frame=i,y=origin.Y,clipY=clip.Y,height=bar.Surface.ActualHeight,clipHeight=clip.Height,width=bar.Surface.ActualWidth,clipWidth=clip.Width});noBitmapCache&=bar.CompactButton.CacheMode==null;}AppLog.Json("media-synchronization.json",synchronization.ToArray());bool grew=bar.Expanded&&heights.Any(h=>h>45&&h<expectedHeight-1)&&Math.Abs(bar.Shape.Height-expectedHeight)<.1;
                    clickThrough&=PassesOutside(bar,behind.Handle);Save(bar,"nowbar-expanded.png");Click(bar.PlayButton);Click(bar.NextButton);Click(bar.PreviousButton);await Task.Delay(50);var peer=UIElementAutomationPeer.CreatePeerForElement(bar.SeekBar);((IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue)).SetValue(65);await Task.Delay(40);bool transport=commands.SequenceEqual(new[]{"play","next","previous"})&&seeks.SequenceEqual(new[]{65.0});
                    bar.Update(Track(longTitle,false),scale,start,y,true);bool paused=bar.PlayKind=="play";bar.SetExpanded(false);await Task.Delay(70);var before=bar.Shape;bar.SetExpanded(true);bool continuous=before==bar.Shape;await Task.Delay(400);bar.SetExpanded(false);var collapse=new List<double>();var gaps=new List<double>();for(int i=0;i<21;i++){await Task.Delay(16);collapse.Add(bar.Surface.ActualHeight);gaps.Add(bar.MaximumFrameGap);}int collapseFrames=bar.FrameCount;double placeTime=bar.MaximumPlaceMilliseconds;bool smoothCollapse=collapse.Zip(collapse.Skip(1),(a,b)=>b<=a+.01).All(v=>v)&&collapseFrames>=8;bar.Update(shortTrack,scale,start,y,true);await Task.Delay(400);bool shrank=bar.Shape.Height==44&&bar.Shape.Width<longShape.Width&&bar.Radius==22;
                    var bitmap=Capture(bar);var pixels=new byte[48*48*4];bitmap.CopyPixels(new Int32Rect(0,0,48,48),pixels,48*4,0);var alpha=new HashSet<byte>();for(int i=3;i<pixels.Length;i+=4)alpha.Add(pixels[i]);bool soft=alpha.Count>8&&pixels[3]==0;
                    bar.SetExpanded(true);await Task.Delay(350);bool stableCollapse=await StableCollapse(bar,behind.Handle);bar.SetExpanded(true);await Task.Delay(350);var original=bar.PointToScreen(new Point(22,bar.Height-22));MenuDismiss.PointerPressed(new Native.PointI {X=(int)original.X,Y=(int)original.Y});await Task.Delay(350);bool originCollapses=!bar.Expanded&&bar.Shape.Height==44;
                    Preferences.Current.NowBarLargeCover=true;bar.Update(shortTrack,scale,start,y,true);bar.SetExpanded(true);await Task.Delay(380);bool poster=bar.LargeCoverVisible&&bar.Shape.Height>300&&bar.Shape.Bottom<y;Save(bar,"nowbar-large-cover.png");bool posterCollapse=await StableCollapse(bar,behind.Handle);bar.SetExpanded(true);await Task.Delay(100);Preferences.Current.NowBarLargeCover=false;bar.Update(shortTrack,scale,start,y,true);await Task.Delay(380);bool styleSwitch=bar.Shape.Height==expectedHeight&&!bar.LargeCoverVisible;bar.SetExpanded(false);await Task.Delay(330);var missing=Track("커버 없는 미디어",true);missing.Artwork=null;Preferences.Current.NowBarLargeCover=true;bar.Update(missing,scale,start,y,true);bar.SetExpanded(true);await Task.Delay(350);bool missingCover=!bar.LargeCoverVisible&&bar.Shape.Height==expectedHeight;bar.SetExpanded(false);await Task.Delay(330);Preferences.Current.NowBarLargeCover=false;bar.Update(shortTrack,scale,start,y,true);
                    int style=Native.GetWindowLong(new WindowInteropHelper(bar).Handle,-20);bool tool=(style&0x80)!=0&&(style&0x40000)==0;await Task.Delay(950);bar.Update(new MediaState(),scale,start,y,true);await Task.Delay(220);bool gone=!bar.IsVisible&&!bar.HasRetainedArtwork;bar.Update(shortTrack,scale,start,y,true);await Task.Delay(400);bool reappears=bar.IsVisible&&bar.Shape.Width>44&&bar.Shape.Height==44;bool color=bar.Palette.Dominant.R>bar.Palette.Dominant.G+80&&bar.Palette.MinimumContrast>=5.4;
                    Preferences.Current.Animations=false;bar.SetExpanded(true);bool disabledInstant=bar.Shape.Height==expectedHeight;bar.SetExpanded(false);disabledInstant&=bar.Shape.Height==44;Preferences.Current.Animations=true;result=shortShape.Width<longShape.Width&&longShape.Width<=DockLayout.StatusSize(screen.Bounds.Width/scale)+.01&&shortLeft&&longLeft&&marquee&&grew&&transport&&paused&&continuous&&shrank&&soft&&tool&&gone&&reappears&&color&&clickThrough&&smoothCollapse&&software&&synchronized&&noBitmapCache&&stableCollapse&&originCollapses&&poster&&posterCollapse&&styleSwitch&&missingCover&&disabledInstant?0:1;
                    AppLog.Json(modern?"nowbar9-probe.json":"nowbar-probe.json",new {passed=result==0,collapseKeepsNativeHostsStable=stableCollapse,shortTitleLeftAligned=shortLeft,longTitleLeftAligned=longLeft,softwareLayeredSurface=software,contentAndBlurSynchronized=synchronized,noStaleBitmapCache=noBitmapCache,originalSlotClickCollapses=originCollapses,largeCoverStyle=poster,largeCoverCollapse=posterCollapse,switchStyleDuringAnimation=styleSwitch,missingCoverHasNoLargePlaceholder=missingCover,shortWidth=shortShape.Width,cappedWidth=longShape.Width,collapsedHeight=shortShape.Height,marqueeMoves=marquee,expandedHeight=expectedHeight,actualGrowthFrames=heights.ToArray(),actualCollapseFrames=collapse.ToArray(),collapseFrames=collapseFrames,maximumPlaceMilliseconds=placeTime,maximumCollapseGap=gaps.Max(),clicksPassThroughOutsideCapsule=clickThrough,smoothCollapse=smoothCollapse,singleWindowMorph=grew,transportDispatch=commands.ToArray(),seekDispatch=seeks.ToArray(),pausedIcon=paused,interruptedMorphContinuous=continuous,shrinks=shrank,edgeAlphaLevels=alpha.Count,softCorners=soft,excludedFromAltTab=tool,hidesAfterSessionEnds=gone,inactiveArtworkReleased=gone,reappearsAtFullWidth=reappears,artworkColor=color,titleContrast=bar.Palette.MinimumContrast,realPlayerChanged=false});
                }catch(Exception error){AppLog.Json(modern?"nowbar9-probe.json":"nowbar-probe.json",new {passed=false,error=error.ToString()});}
                finally {Preferences.Current.UIVersion=oldUI;Preferences.Current.NowBarLargeCover=oldCover;Preferences.Current.Animations=oldAnimations;if(animationFixture!=null)animationFixture.Dispose();if(pointerFixture!=null)pointerFixture.Dispose();if(bar!=null)bar.Close();if(behind!=null)behind.Dispose();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
