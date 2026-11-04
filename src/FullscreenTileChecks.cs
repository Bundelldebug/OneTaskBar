using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class FullscreenTileChecks
    {
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        const string Forecast="{\"current\":{\"temperature_2m\":8.4,\"weather_code\":3,\"is_day\":0},\"hourly\":{\"time\":[\"2026-11-05T01:00\",\"2026-11-05T02:00\",\"2026-11-05T03:00\",\"2026-11-05T04:00\",\"2026-11-05T05:00\"],\"precipitation_probability\":[10,20,null,70,40]}}";
        static void Pure()
        {
            Require(Math.Abs(MoonTile.Phase(new DateTime(2026,8,12,17,37,0,DateTimeKind.Utc))-0)<.03||MoonTile.Phase(new DateTime(2026,8,12,17,37,0,DateTimeKind.Utc))>.97,"a known new moon has phase near zero");
            Require(Math.Abs(MoonTile.Phase(new DateTime(2026,3,3,11,38,0,DateTimeKind.Utc))-.5)<.03,"a known full moon has phase near one half");
            foreach(double phase in new[]{0,.1,.25,.4,.5,.6,.75,.9})Require(MoonTile.Shape(phase,32).IsFrozen,"moon shapes are frozen");
            Require(CalendarTile.Offset(new DateTime(2026,11,1))==0&&CalendarTile.Offset(new DateTime(2026,12,1))==2,"calendar weeks start on Sunday");
            var report=WeatherService.Parse(Forecast,new DateTime(2026,11,5,1,30,0));
            Require(report.Temperature==8.4&&report.Code==3&&!report.Day,"current weather fields are read");
            Require(report.RainPercent==70&&report.Summary.Contains("4:00"),"a null hour is skipped and the first likely rain hour is named");
            var past=WeatherService.Parse(Forecast,new DateTime(2026,11,5,4,30,0));
            Require(past.RainPercent==70,"the hour in progress still counts");
            string name;
            double latitude,longitude;
            Require(WeatherService.ParsePlace("{\"results\":[{\"name\":\"조치원읍\",\"latitude\":36.6,\"longitude\":127.3}]}",out name,out latitude,out longitude)&&name=="조치원읍"&&latitude==36.6,"a place result is read");
            Require(!WeatherService.ParsePlace("{\"generationtime_ms\":1}",out name,out latitude,out longitude),"an unknown place is rejected");
            var cells=FullscreenTileStrip.Place(new[]{2,1,1,2,2,2},new[]{2,1,1,1,2,2},8);
            Require(cells[0]==new Point(0,0)&&cells[1]==new Point(2,0)&&cells[2]==new Point(3,0)&&cells[3]==new Point(2,1)&&cells[4]==new Point(4,0)&&cells[5]==new Point(6,0),"tiles follow the reference arrangement");
            var narrow=FullscreenTileStrip.Place(new[]{2,2,1},new[]{2,2,1},3);
            Require(narrow[1].X<0&&narrow[2]==new Point(2,.5),"a tile that does not fit is left out, and a lone tile centers vertically");
            Require(CalendarTile.Weeks(new DateTime(2026,11,1))==5&&CalendarTile.Weeks(new DateTime(2026,8,1))==6&&CalendarTile.Weeks(new DateTime(2026,2,1))==4,"calendar rows match the month");
            Require(BudsInfo.AppBattery("L 80% R 75% 케이스 50%")==75,"the lower earbud level is shown, not the case");
            Require(BudsInfo.AppBattery("왼쪽 90 · 오른쪽 88")==88&&BudsInfo.AppBattery("배터리 64%")==64&&BudsInfo.AppBattery("연결됨")==null&&BudsInfo.AppBattery("L 300%")==null,"app battery text is read safely");
            string todo=Path.Combine(AppLog.Data,"todo-fixture.json");
            File.WriteAllText(todo,"[\"  하나  \",\"\",null,\""+new string('x',500)+"\"]");
            var items=TodoStore.Load(todo);
            Require(items.Count==2&&items[0]=="하나"&&items[1].Length==TodoStore.TextLimit,"saved to-do text is trimmed and bounded");
            File.WriteAllText(todo,"not json");
            Require(TodoStore.Load(todo).Count==0,"a damaged to-do file is ignored");
        }
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;
            ShellController shell=null;
            FullscreenMedia view=null;
            application.Dispatcher.BeginInvoke(new Action(async()=>
            {
                try
                {
                    Program.Inspectable=true;
                    Preferences.Current.UIVersion=95;
                    Pure();
                    Require(new PreferenceData().FullscreenWidgets&&new PreferenceData().FullscreenAppLeft==null&&new PreferenceData().WeatherPlace==null,"tiles default on, with no app shortcut and no weather location");
                    Require(!WeatherService.Configured,"no location means no weather requests");
                    WeatherService.Request();
                    Require(WeatherService.Requests==0,"an unset location sends nothing");
                    shell=new ShellController(true);
                    shell.Apps.Add(new AppEntry {Name="메모 앱",AppID="OneTaskBar.Fixture!Notes"});
                    shell.Apps.Add(new AppEntry {Name="카메라 앱",AppID="OneTaskBar.Fixture!Camera"});
                    AppEntry launched=null;
                    shell.LaunchDispatch=a=>launched=a;
                    shell.Power=new PowerState {Known=true,Level=82,Connected=false};
                    view=new FullscreenMedia(c=>{},p=>Task.FromResult(true),()=>{},()=>{},null,shell);
                    view.Width=1600;
                    view.Height=1000;
                    view.Show();
                    var media=new MediaState {Available=true,Active=true,Playing=true,CanPlay=true,CanNext=true,Duration=210,Position=50,Title="깊은 밤이에요",Artist="테스트",Artwork=ArtworkPalette.Sample(Colors.MediumPurple)};
                    view.Update(media,"Fixture");
                    await Task.Delay(60);
                    Require(view.LargeCover&&(view.Tiles==null||view.Tiles.Visibility!=Visibility.Visible)&&(view.Corners==null||view.Corners.Left.Visibility!=Visibility.Visible),"the expanded cover shows no tiles or corner apps");
                    view.ToggleArtwork();
                    await Task.Delay(450);
                    view.Update(media,"Fixture");
                    for(int n=0;n<100&&view.WallpaperBrush==null;n++)await Task.Delay(50);
                    await Task.Delay(60);
                    var warm=TileScheme.Tinted(Color.FromRgb(230,200,150),true);
                    var night=TileScheme.Tinted(Color.FromRgb(20,30,50),false);
                    Require(TileScheme.Luminance(((SolidColorBrush)warm.Ink).Color)<.3,"a bright wallpaper gets dark ink");
                    Require(TileScheme.Luminance(((SolidColorBrush)night.Ink).Color)>.7,"a dark wallpaper gets pale ink");
                    Require(warm.Monochrome&&warm.Level(5)==warm.Ink&&TileScheme.Light.Level(5)!=TileScheme.Light.Ink,"wallpaper tiles stay one color; desktop cards keep level colors");
                    var strip=view.Tiles;
                    Require(strip!=null&&strip.Visibility==Visibility.Visible&&strip.VisibleCount>=4,"collapsing shows tiles");
                    Require(strip.Find("weather:2x2")==null&&strip.Find("rain:1x1")==null&&strip.Find("calendar:2x2")!=null,"weather stays out until a location is chosen");
                    foreach(string token in InfoTileCatalog.Tokens)
                    {
                        var sample=InfoTileCatalog.Create(token,true,shell,TileScheme.Light);
                        Require(sample!=null,"every catalog token creates a tile");
                        sample.Refresh(shell,new DateTime(2026,11,5,1,22,55));
                        sample.Refresh(shell,new DateTime(2026,11,5,1,22,56));
                        int spanColumns=InfoTileCatalog.Columns(token),spanRows=InfoTileCatalog.Rows(token);
                        Require(InfoTile.Pill(spanColumns,spanRows)==(spanRows==1&&spanColumns>1),"one-row wide tiles are pills");
                        var preview=new Border {Background=TileScheme.Light.Surface,CornerRadius=new CornerRadius(InfoTile.Radius(spanColumns,spanRows)),Child=sample};
                        var design=InfoTileCatalog.Design(token);
                        Program.Save(preview,(int)design.Width,(int)design.Height,Path.Combine(AppLog.Data,"tile-"+token.Replace(':','-')+".png"));
                        sample.Retire();
                    }
                    var box=view.TilesBounds;
                    Require(box.Top>=view.ClockBottom&&box.Bottom<=view.CardBounds.Top,"tiles sit between the clock and the media pill");
                    Require(box.Left>=0&&box.Right<=view.ActualWidth&&Math.Abs(box.Left+box.Width/2-view.ActualWidth/2)<2,"tiles are centered inside the screen");
                    var corners=view.Corners;
                    Require(corners.Left.Visibility==Visibility.Visible&&Canvas.GetLeft(corners.Left)+FullscreenAppCorners.Size<view.CardBounds.Left&&Canvas.GetLeft(corners.Right)>view.CardBounds.Right,"corner apps stay beside the pill");
                    Program.Save(view.Surface,1600,1000,Path.Combine(AppLog.Data,"fullscreen-tiles.png"));
                    corners.Left.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(corners.PickerOpen&&launched==null,"an empty corner opens the app picker");
                    await Task.Delay(60);
                    Program.Save(view.Surface,1600,1000,Path.Combine(AppLog.Data,"fullscreen-app-picker.png"));
                    corners.Choose(shell.Apps[0].Key);
                    Require(!corners.PickerOpen&&Preferences.Current.FullscreenAppLeft==shell.Apps[0].Key,"the chosen app persists");
                    Require(Preferences.Current.FullscreenAppRight==null,"choosing the left app leaves the right one alone");
                    corners.Left.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(launched==shell.Apps[0],"a configured corner launches its app");
                    Require(!view.IsVisible,"launching dismisses the fullscreen surface");
                    view.Show();
                    view.Update(media,"Fixture");
                    await Task.Delay(60);
                    int passes=view.LayoutPasses;
                    var first=strip.Find("calendar:2x2");
                    for(int n=0;n<20;n++)view.Update(media,"Fixture");
                    Require(view.LayoutPasses==passes&&strip.Find("calendar:2x2")==first,"refreshes neither restart layout nor rebuild tiles");
                    var todo=(TodoTile)strip.Find("todo:2x2");
                    int rows=todo.RowCount;
                    TodoStore.Add("픽스처 할 일");
                    Require(todo.RowCount>=1&&TodoStore.Items.Contains("픽스처 할 일"),"a new to-do appears");
                    TodoStore.Remove("픽스처 할 일");
                    Require(!TodoStore.Items.Contains("픽스처 할 일")&&todo.RowCount==rows,"completing a to-do removes it");
                    int before=strip.TileCount;
                    Preferences.SetMediaAppearance(p=>p.FullscreenWidgets=false);
                    Require(strip.Visibility!=Visibility.Visible,"tiles can be turned off");
                    Preferences.SetMediaAppearance(p=>p.FullscreenWidgets=true);
                    Require(strip.Visibility==Visibility.Visible&&strip.TileCount==before&&strip.Find("calendar:2x2")==first,"turning tiles back on keeps the same tiles");
                    foreach(var size in new[]{new Size(640,480),new Size(1280,720),new Size(2160,1440)})
                    {
                        view.Width=size.Width;
                        view.Height=size.Height;
                        await Task.Delay(40);
                        view.Update(media,"Fixture");
                        if(strip.Visibility==Visibility.Visible)
                        {
                            var fit=view.TilesBounds;
                            Require(fit.Left>=0&&fit.Right<=size.Width&&fit.Top>=view.ClockBottom&&fit.Bottom<=view.CardBounds.Top,"tiles stay clear at every size");
                        }
                    }
                    view.ToggleArtwork();
                    await Task.Delay(450);
                    Require(strip.Visibility!=Visibility.Visible&&corners.Left.Visibility!=Visibility.Visible,"expanding hides tiles and corner apps again");
                    AppLog.Json("fullscreen-tiles-probe.json",new {passed=true,tiles=before,realAppsLaunched=false,weatherRequests=WeatherService.Requests});
                    result=0;
                }
                catch(Exception e)
                {
                    AppLog.Json("fullscreen-tiles-probe.json",new {passed=false,error=e.ToString()});
                }
                finally
                {
                    if(view!=null)view.Close();
                    if(shell!=null)shell.Dispose();
                    application.Shutdown(result);
                }
            }),DispatcherPriority.ApplicationIdle);
            return application.Run();
        }
    }
}
