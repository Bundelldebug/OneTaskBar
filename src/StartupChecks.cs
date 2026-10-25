using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class StartupChecks
    {
        static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};GlassWindow window=null,cancelled=null;StartupWork work=null,pending=null;int result=1;
            application.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;var pins=Enumerable.Range(1,5).Select(i=>new AppEntry {Name="저장된 앱 "+i,AppID="fixture:startup:"+i,Target=Path.Combine(AppLog.Root,"assets","OneTaskBar.ico"),Executable="fixture.exe",StoredKey="original-key:"+i,Pinned=true}).ToList();
                string executable=@"C:\Fixture & Test\OneTaskBar.exe",sid="S-1-5-21-1234";string definition=LogonStartup.Definition(executable,sid);Require(LogonStartup.Owned(definition,executable,sid)&&LogonStartup.Owned(definition.Replace("<RunLevel>LeastPrivilege</RunLevel>",""),executable,sid)&&!LogonStartup.Owned(definition,executable,"another-user")&&!LogonStartup.Owned(definition,@"C:\Other\OneTaskBar.exe",sid)&&!LogonStartup.Owned(definition.Replace("LeastPrivilege","HighestAvailable"),executable,sid)&&!LogonStartup.Owned(definition.Replace("--desktop --logon","--settings"),executable,sid),"Logon task XML retains exact owner, executable, arguments, interactive token and least privilege, including the normalized default");
                AppLog.Json("apps.json",pins.Select(p=>new {Name=p.Name,AppID=p.AppID,Target=p.Target,Executable=p.Executable}).ToArray());Require(Catalog.ReadCached().Count==5,"Existing catalog is available without Get-StartApps or shortcut resolution");
                StartupDockCache.Save(pins);var cached=StartupDockCache.Read();Require(cached!=null&&cached.Select(p=>p.Key).SequenceEqual(pins.Select(p=>p.Key))&&cached.Select(p=>p.Name).SequenceEqual(pins.Select(p=>p.Name))&&cached.All(p=>p.Pinned&&p.Icon==null&&p.Windows.Count==0),"Cached pin identities and original labels persist without image/window serialization");
                AppLog.Json("dock-pins.json",new[]{new {Key="changed-pin",Hidden=true}});Require(StartupDockCache.Read()==null,"Pin changes invalidate the startup cache");StartupDockCache.Save(new AppEntry[0]);Require(StartupDockCache.Read()!=null&&StartupDockCache.Read().Count==0,"An intentionally empty pin list stays empty");
                File.WriteAllText(Path.Combine(AppLog.Data,"startup-dock-cache.json"),"{");Require(StartupDockCache.Read()==null,"Malformed startup snapshots fall back safely");File.WriteAllText(Path.Combine(AppLog.Data,"apps.json"),"{");Require(Catalog.ReadCached().Count==0,"Malformed catalog does not seed fake default pins");
                window=new GlassWindow {ShowActivated=false,Width=480,Height=90};var content=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(12)};foreach(var pin in cached.Take(4)){pin.Icon=ArtworkPalette.Sample(Colors.CornflowerBlue);content.Children.Add(AppIcons.Visual(pin,44));}window.Surface.Child=content;
                int rendered=0,calls=0;var clock=Stopwatch.StartNew();double paintMs=0;window.ContentRendered+=(s,e)=>{rendered++;paintMs=clock.Elapsed.TotalMilliseconds;};var completed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);work=new StartupWork(window,()=>{calls++;completed.TrySetResult(rendered>0);});window.Show();
                if(await Task.WhenAny(completed.Task,Task.Delay(5000))!=completed.Task)throw new TimeoutException("First frame startup work was not released");Require(await completed.Task&&calls==1&&!work.Pending,"Discovery begins once, after actual first-frame rendering");window.HideImmediately();window.Show();await Task.Delay(120);Require(calls==1,"Further renders do not restart discovery");window.Close();window=null;
                cancelled=new GlassWindow {ShowActivated=false,Width=200,Height=60};int unexpected=0;pending=new StartupWork(cancelled,()=>unexpected++);pending.Dispose();cancelled.Show();await Task.Delay(180);Require(unexpected==0&&!pending.Pending,"Shutting down before first paint cancels all startup work");cancelled.Close();cancelled=null;
                var noWindow=new TaskCompletionSource<bool>();using(var idle=new StartupWork(null,()=>noWindow.TrySetResult(true))){Require(await Task.WhenAny(noWindow.Task,Task.Delay(1000))==noWindow.Task,"Startup without a window completes through the dispatcher");}
                AppLog.Json("startup-probe.json",new {passed=true,cachedCatalogWithoutExternalProcess=true,cachedPinIdentityAndLabels=true,staleAndMalformedCachesRejected=true,emptyPinsPreserved=true,initializationAfterActualFrame=true,initializationRunsOnce=true,shutdownCancelsStartup=true,fixtureFirstFrameMilliseconds=Math.Round(paintMs,1),bootWasNotSimulated=true,osInputSent=false,realTaskbarChanged=false});result=0;
            }catch(Exception error){AppLog.Json("startup-probe.json",new {passed=false,error=error.ToString()});}finally{if(work!=null)work.Dispose();if(pending!=null)pending.Dispose();if(window!=null)window.Close();if(cancelled!=null)cancelled.Close();application.Shutdown(result);}}),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
