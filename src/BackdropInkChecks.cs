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
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try
                {
                    Require(BackdropInk.ChooseDark(.8,null)&&!BackdropInk.ChooseDark(.05,null),"bright and dark backdrops select opposite ink");
                    Require(BackdropInk.ChooseDark(.20,true)&&!BackdropInk.ChooseDark(.20,false),"small changes near the boundary cannot flicker");
                    double luminance=.8;
                    window=new GlassWindow {Width=420,Height=260};
                    var layout=new StackPanel {Margin=new Thickness(24)};
                    var label=Paint.TextBlock("밝은 배경과 어두운 배경",20,Paint.Text);
                    var selected=new Glyph("wifi",32) {Color=Paint.OnLight};
                    var icon=new Glyph("bluetooth",32) {Color=Paint.Text};
                    var media=new MediaPlayerView(c=>{},p=>Task.FromResult(true),()=>{});
                    layout.Children.Add(label);layout.Children.Add(icon);layout.Children.Add(selected);layout.Children.Add(media);
                    window.Surface.Child=layout;
                    window.Show();
                    ink=new BackdropInk(window,()=>Task.FromResult((double?)luminance));
                    await Task.Delay(260);
                    Require(ink.DarkInk&&label.Foreground==ink.Primary&&icon.Color==ink.Primary,"semantic labels and icons use the window's contrast brushes");
                    Require(selected.Color==Paint.OnLight,"selected white-circle icons retain their fixed dark ink");
                    Brush sameBrush=label.Foreground;
                    luminance=.05;
                    await Task.Delay(1500);
                    Require(!ink.DarkInk&&label.Foreground==sameBrush,"backdrop changes reuse the brushes rather than rebuilding controls");
                    Program.Save(window.Surface,420,260,Path.Combine(AppLog.Data,"adaptive-ink.png"));
                    window.HideImmediately();
                    int samples=ink.SampleCount;
                    await Task.Delay(1400);
                    Require(!ink.Running&&ink.SampleCount==samples,"hidden panels stop sampling");
                    window.Show();await Task.Delay(250);
                    Require(ink.Running&&ink.SampleCount>samples,"reopening resumes the local palette");
                    ink.Dispose();
                    Require(!ink.Running,"closing releases the sampling timer");
                    AppLog.Json("adaptive-ink-probe.json",new {passed=true,hysteresis=true,fixedControlInk=true,reusedBrushes=true,hiddenSamplingStopped=true});
                    result=0;
                }
                catch(Exception error){AppLog.Json("adaptive-ink-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(ink!=null)ink.Dispose();if(window!=null)window.Close();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
    }
}
