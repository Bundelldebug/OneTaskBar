using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class SurfaceMaterialChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Surface material: "+message);}
        static Slider FindSlider(DependencyObject parent,string name)
        {
            var slider=parent as Slider;
            if(slider!=null&&AutomationProperties.GetName(slider)==name)return slider;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){
                var found=FindSlider(VisualTreeHelper.GetChild(parent,i),name);
                if(found!=null)return found;
            }
            return null;
        }
        static void ShowMaterialControls(WelcomeWindow settings,string name)
        {
            var slider=FindSlider(settings.Surface,name);
            Require(slider!=null&&slider.IsEnabled,"material controls are available on their settings page");
            var row=(FrameworkElement)slider.Parent;
            ((FrameworkElement)row.Parent).BringIntoView();
        }
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            ShellController shell=null;GlassWindow dock=null;int result=1;
            application.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    Program.Inspectable=true;Directory.CreateDirectory(AppLog.Data);
                    var defaults=new PreferenceData();Require(!defaults.DockMaterialCustom&&!defaults.StartMaterialCustom,"both overrides are opt-in");
                    var invalid=new PreferenceData {DockMaterialColor="invalid",StartMaterialColor=null,DockMaterialOpacity=-30,StartMaterialOpacity=140,DockMaterialBlur=-1,StartMaterialBlur=500};
                    Preferences.Normalize(invalid);
                    Require(invalid.DockMaterialOpacity==0&&invalid.StartMaterialOpacity==100&&invalid.DockMaterialBlur==0&&invalid.StartMaterialBlur==40,"out-of-range persisted values are clamped");
                    Require(invalid.DockMaterialColor==defaults.DockMaterialColor&&invalid.StartMaterialColor==defaults.StartMaterialColor,"invalid persisted colors have valid defaults");
                    shell=new ShellController(true);shell.StartMenu=new StartWindow(shell);shell.Quick=new QuickWindow(shell);
                    var screen=System.Windows.Forms.Screen.PrimaryScreen;
                    shell.StartMenu.ShowAt(screen.Bounds,20,20,940,720);shell.Quick.ShowAt(screen.Bounds,30,30,380,650);
                    dock=new GlassWindow {DockSurface=true};dock.Surface.Background=LightPanel.DockGlass;dock.ShowAt(screen.Bounds,40,40,500,60);
                    int hosts=LiveBackdrop.InstanceCount,fullChanges=0;
                    var quickBrush=shell.Quick.Surface.Background;
                    Action observe=()=>fullChanges++;Preferences.Changed+=observe;
                    try{
                        foreach(int ui in new[]{85,90,95})foreach(int amount in new[]{0,30,70,100}){
                            Preferences.Current.UIVersion=ui;
                            Preferences.SetAppearance(p=>{
                                p.DockMaterialCustom=p.StartMaterialCustom=true;
                                p.DockMaterialColor="#FF204060";p.StartMaterialColor="#FF603020";
                                p.DockMaterialOpacity=amount;p.StartMaterialOpacity=100-amount;
                                p.DockMaterialBlur=8;p.StartMaterialBlur=24;
                            });
                            var dockTint=(SolidColorBrush)dock.Surface.Background;
                            var startTint=(SolidColorBrush)shell.StartMenu.Surface.Background;
                            Require(dockTint.Color.R==32&&dockTint.Color.A==(byte)Math.Round(amount*2.55),"dock tint applies its own color and alpha");
                            Require(startTint.Color.R==96&&startTint.Color.A==(byte)Math.Round((100-amount)*2.55),"Start tint is independent");
                            Require(dockTint.IsFrozen&&startTint.IsFrozen,"surface brushes are reusable across refreshes");
                            Require(Object.ReferenceEquals(quickBrush,shell.Quick.Surface.Background),"quick settings are unaffected");
                            Require(Native.SurfaceBlur(dock)==8&&Native.SurfaceBlur(shell.StartMenu)==24,"each surface keeps its own blur radius");
                            Require(Native.CustomSurfaceBlur(dock)&&Native.CustomSurfaceBlur(shell.StartMenu),"native compositor accepts the blur effect");
                            Require(hosts==LiveBackdrop.InstanceCount&&fullChanges==0,"style edits do not rebuild the shell or allocate backdrop hosts");
                        }
                        var json=new JavaScriptSerializer();var roundTrip=json.Deserialize<PreferenceData>(json.Serialize(Preferences.Current));
                        Require(roundTrip.DockMaterialCustom&&roundTrip.StartMaterialCustom&&roundTrip.StartMaterialBlur==24,"independent style settings persist");
                        Preferences.SetAppearance(p=>{p.DockMaterialBlur=p.StartMaterialBlur=0;});
                        Require(Native.SurfaceBlur(dock)==-1&&Native.SurfaceBlur(shell.StartMenu)==-1,"zero disables the customized blur");
                        Preferences.SetAppearance(p=>{p.DockMaterialCustom=p.StartMaterialCustom=false;});
                        Require(dock.Surface.Background==LightPanel.DockGlass,"dock reset restores its default material");
                        Require(shell.StartMenu.Surface.Background==PanelMaterial.PanelSurface(Glass95.Dark,Glass95.Enabled),"Start reset restores its default material");
                    }finally{Preferences.Changed-=observe;}
                    Preferences.SetAppearance(p=>{p.DockMaterialCustom=p.StartMaterialCustom=true;p.DockMaterialBlur=p.StartMaterialBlur=16;});
                    var settings=new WelcomeWindow(shell);
                    try{
                        settings.Show();await Task.Delay(450);settings.OpenSettings("데스크톱");await Task.Delay(450);settings.UpdateLayout();
                        ShowMaterialControls(settings,"독 배경 불투명도");await Task.Delay(350);
                        Program.Save(settings.Surface,760,720,Path.Combine(AppLog.Data,"settings-dock-material.png"));
                        settings.OpenSettings("시작과 검색");await Task.Delay(450);settings.UpdateLayout();
                        ShowMaterialControls(settings,"시작 패널 배경 불투명도");await Task.Delay(350);
                        Program.Save(settings.Surface,760,720,Path.Combine(AppLog.Data,"settings-start-material.png"));
                    }finally{settings.ReleaseIdle();}
                    AppLog.Json("surface-material-probe.json",new {passed=true,independentSurfaces=2,uiVersions=3,alphaEndpoints=true,nativeBlur=true,fullShellRefreshes=fullChanges});result=0;
                }catch(Exception error){AppLog.Json("surface-material-probe.json",new {passed=false,error=error.ToString()});}
                finally{
                    if(dock!=null)dock.Close();
                    if(shell!=null){if(shell.StartMenu!=null)shell.StartMenu.Close();if(shell.Quick!=null)shell.Quick.Close();shell.Dispose();}
                    application.Shutdown(result);
                }
            }));application.Run();return result;
        }
    }
}
