using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class DesktopOverviewChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Desktop overview: "+message);}
        internal static int Run(bool inspect=false)
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int exit=1;DesktopOverview view=null;var sources=new List<Window>();IDisposable motion=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;motion=Motion.WithAnimationsForFixture();
                var first=Guid.NewGuid();var second=Guid.NewGuid();
                Require(VirtualDesktops.Decode(first.ToByteArray().Concat(second.ToByteArray()).Concat(first.ToByteArray()).ToArray()).SequenceEqual(new[]{first,second}),"ordered desktop IDs deduplicate");
                Require(VirtualDesktops.Decode(new byte[17]).Count==0&&VirtualDesktops.Decode(new byte[16*257]).Count==0,"malformed registry values are bounded");
                int steps;Require(VirtualDesktops.Distance(new[]{second,first},second,first,out steps)&&steps==1&&!VirtualDesktops.Distance(new[]{second},second,first,out steps),"a reordered desktop list resolves the target ID and rejects a deleted target");
                var snapshot=new DesktopSnapshot {Current=first};var page=new DesktopPage {Id=first,Name="데스크톱 1"};snapshot.Pages.Add(page);snapshot.Pages.Add(new DesktopPage {Id=second,Name="데스크톱 2"});
                for(int i=0;i<3;i++){
                    var source=new Window {Title="검사 창 "+(i+1),Width=500,Height=320,Left=100+i*40,Top=130+i*30,Background=new SolidColorBrush(new[]{Colors.SteelBlue,Colors.DarkSeaGreen,Colors.IndianRed}[i]),Content=Paint.TextBlock("창 미리보기 "+(i+1),30,Brushes.White)};source.Show();sources.Add(source);
                    page.Windows.Add(new SwitchItem {Window=new WindowInteropHelper(source).Handle,Title=source.Title,App=new AppEntry {Name=source.Title,Target="fixture:"+i}});
                }
                var shapes=new[]{1.78,1.6,.6,2.4,1.33,1.0,1.78};var packed=DesktopPacking.Pack(shapes,1500,760);
                Require(packed.Length==shapes.Length&&packed.All(r=>r.Left>=-.5&&r.Right<=1500.5&&r.Top>=-.5&&r.Bottom<=760.5),"packed window cards stay inside the workspace");
                bool crossing=false;for(int a=0;a<packed.Length;a++)for(int b=a+1;b<packed.Length;b++)if(packed[a].IntersectsWith(packed[b])&&Rect.Intersect(packed[a],packed[b]).Width>.5&&Rect.Intersect(packed[a],packed[b]).Height>.5)crossing=true;
                Require(!crossing,"packed window cards never overlap");
                Require(Enumerable.Range(0,shapes.Length).All(i=>Math.Abs((packed[i].Width-DesktopPacking.SideChrome)/(packed[i].Height-DesktopPacking.VerticalChrome)-shapes[i])<.02),"each packed card keeps its window's shape");
                int commands=0;view=new DesktopOverview((id,window,operation)=>commands++,window=>{});view.Present(snapshot,System.Windows.Forms.Screen.PrimaryScreen.Bounds);await Task.Delay(320);
                Require(view.PreviewCount==3&&view.RegisteredThumbnails==3&&!view.EmptyVisible&&!view.TransitionActive,"three live previews finish their entry animation");
                var previews=app.Windows.OfType<SwitchThumbnail>().ToArray();Require(previews.All(p=>p.ClearBackground&&p.Background==Brushes.Transparent),"thumbnail hosts have no opaque rectangle behind their fading image");int moves=previews.Sum(p=>p.PositionChanges);
                int layoutPasses=view.LayoutPasses;await Task.Delay(120);Require(previews.Sum(p=>p.PositionChanges)==moves&&view.LayoutPasses==layoutPasses,"stationary and fading previews do not repeat layout or native moves");
                var chord=new TaskViewGesture();bool open;int opens=0;
                if(chord.Observe(9,true,0x5b,true,false,false,out open)&&open)opens++;
                Require(chord.Observe(9,true,0,true,false,false,out open)&&!open,"holding Tab consumes repeat without reopening");
                Require(chord.Observe(9,false,0,true,false,false,out open)&&chord.Observe(0x5b,false,0,true,false,false,out open)&&opens==1,"Win+Tab opens once and consumes both releases");
                Require(!chord.Observe(9,true,0x5b,true,true,false,out open),"other modifier chords pass through");
                Require(!new TaskViewGesture().Observe(9,true,0x5b,true,false,true,out open),"injected trackpad input stays with Windows");
                Require(Object.ReferenceEquals(view.Surface.Background,QuickPanel.Glass),"overview and quick settings use the exact same panel material");
                Program.Save(view.Surface,(int)view.Width,(int)view.Height,Path.Combine(AppLog.Data,"desktop-overview-windows.png"));
                view.SelectPage(1);await Task.Delay(60);Require(view.PreviewCount==0&&view.EmptyVisible&&view.RegisteredThumbnails==0,"an empty desktop releases all source previews");
                Program.Save(view.Surface,(int)view.Width,(int)view.Height,Path.Combine(AppLog.Data,"desktop-overview-empty.png"));
                for(int i=0;i<4;i++){view.SelectPage(0);await Task.Delay(45);view.SelectPage(1);await Task.Delay(25);}view.SelectPage(0);await Task.Delay(300);
                Require(view.RegisteredThumbnails==3&&app.Windows.OfType<SwitchThumbnail>().Count()==3,"rapid desktop selection retains only the visible previews");
                var actual=VirtualDesktops.Read(new AppEntry[0],new WindowInteropHelper(view).Handle);Require(actual.Pages.Count>0&&actual.CurrentIndex<actual.Pages.Count,"the read-only Windows desktop query returns a valid current page");
                if(inspect){AppLog.Json("desktop-overview-preview.json",new {ready=true});for(int i=0;i<240&&!File.Exists(Path.Combine(AppLog.Data,"preview-done"));i++)await Task.Delay(250);}
                view.Dismiss();
                await Task.Delay(250);
                Require(!app.Windows.OfType<SwitchThumbnail>().Any()&&LiveBackdrop.OrphanCount==0&&commands==0,"closing the overview releases previews without issuing OS shortcuts");
                view.Present(snapshot,System.Windows.Forms.Screen.PrimaryScreen.Bounds);await Task.Delay(290);Require(view.RegisteredThumbnails==3&&view.Surface.Opacity==1,"reopening reuses the overview shell and restores its full opacity");
                view.Dismiss();await Task.Delay(250);
                bool animationSetting=Preferences.Current.Animations;
                try
                {
                    Preferences.Set(p=>p.Animations=false);
                    view.Present(snapshot,System.Windows.Forms.Screen.PrimaryScreen.Bounds);
                    Require(view.Surface.Opacity==1&&!view.TransitionActive&&view.RegisteredThumbnails==3,"disabling animation after dismissal cannot leave the reopened overview transparent");
                }
                finally {Preferences.Set(p=>p.Animations=animationSetting);}
                view.HideImmediately();view.Close();
                for(int i=0;i<4;i++){
                    view=new DesktopOverview((id,window,operation)=>commands++,window=>{});view.Present(snapshot,System.Windows.Forms.Screen.PrimaryScreen.Bounds);await Task.Delay(35);
                    view.HideImmediately();await Task.Delay(35);Require(!app.Windows.OfType<SwitchThumbnail>().Any(),"hiding during entry cannot leave thumbnail windows behind");view.Close();
                }
                AppLog.Json("desktop-overview-probe.json",new {passed=true,liveThumbnails=true,emptyDesktop=true,rapidSelection=true,desktopMetadataReadOnly=true,osInputSent=false,realUserWindowsClosed=false});exit=0;
            }catch(Exception error){AppLog.Json("desktop-overview-probe.json",new {passed=false,error=error.ToString()});}finally{if(view!=null&&view.IsVisible)view.Close();foreach(var source in sources)source.Close();if(motion!=null)motion.Dispose();app.Shutdown(exit);}}),DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
    }
}
