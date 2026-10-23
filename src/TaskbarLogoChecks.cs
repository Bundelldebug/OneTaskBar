using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    internal static class TaskbarLogoChecks
    {
        internal static void Validate(List<string> checks)
        {
            string root=Path.Combine(AppLog.Data,"taskbar-icon-fixture-"+Guid.NewGuid().ToString("N")),assets=Path.Combine(root,"Assets");Directory.CreateDirectory(assets);
            string manifest=Path.Combine(root,"AppxManifest.xml");File.WriteAllText(manifest,"<Package><Applications><Application Id='Other'><VisualElements Square44x44Logo='Assets/Other.png'/></Application><Application Id='Player'><VisualElements Square44x44Logo='Assets/Icon.png'/></Application></Applications></Package>");
            foreach(string name in new[]{"Icon.png","Icon.scale-200.png","Icon.targetsize-48_altform-unplated.png","Icon.targetsize-256_altform-unplated.png","Other.targetsize-256_altform-unplated.png"})File.WriteAllBytes(Path.Combine(assets,name),new byte[]{0});
            if(TaskbarLogo.Find(root,"Player")!=Path.Combine(assets,"Icon.targetsize-256_altform-unplated.png")||TaskbarLogo.Find(root,"Absent")!=null)throw new Exception("Taskbar asset identity or unplated/resolution selection");
            File.WriteAllText(manifest,"<Package><Applications><Application Id='Player'><VisualElements Square44x44Logo='../outside.png'/></Application></Applications></Package>");if(TaskbarLogo.Find(root,"Player")!=null)throw new Exception("Taskbar asset escaped its package");
            File.WriteAllText(manifest,"<!DOCTYPE Package [<!ENTITY outside SYSTEM 'file:///outside'>]><Package/>");if(TaskbarLogo.Find(root,"Player")!=null)throw new Exception("External XML entity accepted");
            checks.Add("Packaged taskbar icons use the matching app's unplated target-size asset before tile logos, with bounded decoding and package path/XML limits");
        }
        internal static int Probe()
        {
            try{var rows=new List<object>();var panel=new StackPanel {Background=LightPanel.DockGlass};var apps=Catalog.Read(false);foreach(var app in apps.Where(a=>!String.IsNullOrEmpty(a.AppID)&&a.AppID.Contains("!")).Take(8)){string file=TaskbarLogo.Source(app.AppID);var icon=TaskbarLogo.Read(app.AppID);if(file==null)continue;if(icon==null)throw new Exception("Installed taskbar asset failed to decode");var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(10)};row.Children.Add(AppIcons.Visual(icon,48));row.Children.Add(Paint.TextBlock(app.Name,14,Paint.Ink));panel.Children.Add(row);rows.Add(new {app=app.Name,asset=Path.GetFileName(file),decoded=true});}if(rows.Count==0)throw new Exception("No installed unplated taskbar assets available for verification");var webApps=new List<object>();foreach(var app in apps.Where(a=>WebAppIcons.IsWebApp(a.AppID))){string shortcut=WebAppIcons.Shortcut(app.AppID);var icon=AppIcons.Get(app);if(shortcut!=null&&WebAppIcons.Read(app)==null)throw new Exception("Registered web app shortcut icon failed to decode");var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(10)};if(icon!=null)row.Children.Add(AppIcons.Visual(icon,48));row.Children.Add(Paint.TextBlock(app.Name+" · 웹앱",14,Paint.Ink));panel.Children.Add(row);webApps.Add(new {app=app.Name,registeredShortcut=shortcut!=null,decoded=icon!=null});}Program.Save(panel,400,(rows.Count+webApps.Count)*68,Path.Combine(AppLog.Data,"taskbar-native-icons.png"));AppLog.Json("taskbar-logo-probe.json",new {passed=true,installed=rows.ToArray(),webApps=webApps.ToArray(),storeSpotifyInstalled=apps.Any(a=>a.Name=="Spotify"&&(a.AppID??"").Contains("!")),thirdPartyAssetsRedistributed=false});return 0;}
            catch(Exception error){AppLog.Json("taskbar-logo-probe.json",new {passed=false,error=error.ToString()});return 1;}
        }
    }
}
