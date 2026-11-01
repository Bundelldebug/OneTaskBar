using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class BackdropInkChecks
    {
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        internal static int Run()
        {
            Program.Inspectable=true;
            var app=new Application();
            int result=1;
            GlassWindow window=null;
            BackdropInk ink=null;
            IDisposable motion=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try
                {
                    motion=Motion.WithAnimationsForFixture();
                    var palette=new BackdropInkPalette();
                    palette.Set(true,true);
                    Require(palette.Primary.HasAnimatedProperties,"contrast transitions run with animations enabled");
                    using(var dc=new DrawingVisual().RenderOpen())DrawAdaptiveSymbol(dc,palette.Primary);
                    palette.Set(false,true);
                    palette.Stop();
                    Require(!palette.Primary.HasAnimatedProperties&&!palette.Primary.IsFrozen,"drawing and interrupted color transitions release their clocks safely");
                    Require(BackdropInk.ChooseDark(.8,null)&&!BackdropInk.ChooseDark(.05,null),"bright and dark backdrops select opposite ink");
                    Require(BackdropInk.ChooseDark(.20,true)&&!BackdropInk.ChooseDark(.20,false),"small changes near the boundary cannot flicker");
                    var tone=new BackdropInkTone();
                    Require(tone.Observe(.8,0)&&tone.Dark,"initial backdrop sets the palette");
                    Require(!tone.Observe(.05,100)&&tone.Dark&&tone.Pending,"one dark video frame cannot reverse light-background ink");
                    Require(!tone.Observe(.8,300)&&tone.Dark&&!tone.Pending,"returning backdrop cancels a transient reversal");
                    tone.Observe(.05,400);
                    Require(!tone.Observe(.05,500)&&tone.Dark,"confirmation has a minimum settling interval");
                    Require(tone.Observe(.05,600)&&!tone.Dark,"a sustained backdrop change switches once");
                    Require(!tone.Observe(.22,800)&&!tone.Dark,"near-boundary brightness preserves the palette");
                    Require(!tone.Observe(Double.NaN,1000)&&!tone.Dark,"invalid samples preserve the last valid palette");
                    var symbol=new DeviceSymbol();
                    symbol.Set(ArtworkPalette.Sample(Colors.White));
                    var borrowed=new SolidColorBrush(Colors.White);
                    var drawing=new DrawingVisual();
                    using(var dc=drawing.RenderOpen())symbol.Draw(dc,borrowed,new Point(16,16),24,false);
                    Require(!borrowed.IsFrozen,"a device icon must not freeze a borrowed foreground brush");
                    borrowed.Color=Colors.Black;
                    double luminance=.8;
                    window=new GlassWindow {Width=420,Height=260};
                    var layout=new StackPanel {Margin=new Thickness(24)};
                    var label=Paint.TextBlock("밝은 배경과 어두운 배경",20,Paint.Text);
                    var selected=new Glyph("wifi",32) {Color=Paint.OnLight};
                    var icon=new Glyph("bluetooth",32) {Color=Paint.Text};
                    icon.SetDeviceIcon(ArtworkPalette.Sample(Colors.White));
                    var media=new MediaPlayerView(c=>{},p=>Task.FromResult(true),()=>{});
                    layout.Children.Add(label);layout.Children.Add(icon);layout.Children.Add(selected);layout.Children.Add(media);
                    window.Surface.Child=layout;
                    window.Show();
                    ink=new BackdropInk(window,()=>Task.FromResult((double?)luminance));
                    await Task.Delay(260);
                    Require(ink.DarkInk&&label.Foreground==ink.Primary&&icon.Color==ink.Primary,"semantic labels and icons use the window's contrast brushes");
                    Require(!ink.Primary.CanFreeze&&!ink.Secondary.CanFreeze,"live palette bindings cannot be frozen by cached drawings");
                    Require(selected.Color==Paint.OnLight,"selected white-circle icons retain their fixed dark ink");
                    Brush sameBrush=label.Foreground;
                    Program.Save(window.Surface,420,260,Path.Combine(AppLog.Data,"adaptive-ink-bright.png"));
                    luminance=.05;
                    await Task.Delay(1700);
                    Require(!ink.DarkInk&&label.Foreground==sameBrush,"backdrop changes reuse the brushes rather than rebuilding controls");
                    Glass95.RefreshColors(window.Surface);
                    Require(label.Foreground==sameBrush,"theme refresh preserves a live adaptive brush even when its color matches the theme");
                    Program.Save(window.Surface,420,260,Path.Combine(AppLog.Data,"adaptive-ink.png"));
                    Require(!ink.Primary.IsFrozen&&!ink.Secondary.IsFrozen,"rendering a device icon leaves both ink brushes mutable");
                    window.HideImmediately();
                    int samples=ink.SampleCount;
                    await Task.Delay(1400);
                    Require(!ink.Running&&ink.SampleCount==samples,"hidden panels stop sampling");
                    window.Show();await Task.Delay(250);
                    Require(ink.Running&&ink.SampleCount>samples,"reopening resumes the local palette");
                    for(int i=0;i<8;i++)
                    {
                        window.HideImmediately();
                        window.Show();
                        await Task.Delay(30);
                    }
                    ink.Dispose();
                    Require(!ink.Running,"closing releases the sampling timer");
                    AppLog.Json("adaptive-ink-probe.json",new {passed=true,hysteresis=true,transientReversalSuppressed=true,deviceIconDoesNotFreezeInk=true,themePreservesLiveInk=true,fixedControlInk=true,reusedBrushes=true,hiddenSamplingStopped=true,repeatedHideAndClose=true});
                    result=0;
                }
                catch(Exception error){AppLog.Json("adaptive-ink-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(ink!=null)ink.Dispose();if(window!=null)window.Close();if(motion!=null)motion.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
        static void DrawAdaptiveSymbol(DrawingContext context,Brush brush)
        {
            var symbol=new DeviceSymbol();
            symbol.Set(ArtworkPalette.Sample(Colors.White));
            symbol.Draw(context,brush,new Point(16,16),24,false);
        }
    }
}
