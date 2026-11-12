using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    internal static class AppMetadataChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("App metadata: "+message);}
        internal static void Validate(List<string> checks)
        {
            CheckSteam(checks);
            var icon=new DrawingImage();icon.Freeze();var old=new AppEntry {Name="development-name",Target=@"C:\Apps\Files\electron.exe",Executable=@"C:\Apps\Files\electron.exe"};var native=new AppEntry {Name="Native file manager",AppID=@"C:\Apps\Files\Files.exe",Target=@"C:\Menu\Files.lnk",Executable=@"C:\Apps\Files\Files.exe",Icon=icon};
            var rebound=Catalog.ResolveInstalled(old,new[]{native});Require(rebound.Key==old.Key&&rebound.Name==native.Name&&rebound.Target==native.Target&&rebound.Executable==native.Executable&&rebound.Icon==icon,"unique legacy Electron launcher adopts the native shortcut metadata while preserving pin identity");
            Require(Catalog.PresentationChanged(new[]{old},new[]{rebound})&&!Catalog.PresentationChanged(new[]{rebound},new[]{rebound.Copy()}),"dock refresh detects changed names/icons even when pin identities stay the same");var running=new AppEntry {Name="Document title",AppID="example.running",Executable=native.Executable,StartedUtcTicks=123};running.Windows.Add(new IntPtr(42));var canonical=Catalog.ResolveInstalled(running,new[]{native});Require(canonical.Name==native.Name&&canonical.AppID==native.AppID&&canonical.Windows.SequenceEqual(running.Windows)&&canonical.StartedUtcTicks==123,"native running-app metadata preserves activation handles and launch order");
            var other=new AppEntry {Name="Other app",AppID=@"C:\Apps\Files\Other.exe",Executable=@"C:\Apps\Files\Other.exe"};Require(Catalog.ResolveInstalled(old,new[]{native,other}).Target==old.Target,"ambiguous install folders are not guessed");Require(Catalog.ResolveInstalled(new AppEntry {Name="Old title",AppID=native.AppID},new[]{native}).Name==native.Name,"an exact identity uses the current native display name");var malformed=new AppEntry {Name="invalid",Target="C:\\invalid\0\\electron.exe"};Require(Catalog.ResolveInstalled(malformed,new[]{native}).Target==malformed.Target,"invalid stored paths leave other pinned apps usable");
            string folder=Path.Combine(Path.GetTempPath(),"OneTaskBar-metadata-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{string file=Path.Combine(folder,"pins.json");var pins=new StartPinStore(file);pins.Replace(new[]{old});var tile=pins.Tiles(new List<AppEntry>{native}).Single();Require(tile.Key==old.Key&&tile.App.Key==old.Key&&tile.Name==native.Name&&tile.App.Icon==icon,"Start displays current native names/icons without moving or renaming saved pins");pins.Replace(new[]{tile.App});var reopened=new StartPinStore(file).Tiles(new List<AppEntry>{native}).Single();Require(reopened.Key==old.Key&&reopened.App.Key==old.Key,"canonical app metadata retains the original pin identity after saving and reopening");}finally{foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
            checks.Add("Native app display names and shortcut icons replace stale pin metadata; unique legacy Electron launcher aliases retain pin identities and ambiguous folders remain untouched");
        }
        static void CheckSteam(List<string> checks)
        {
            string folder=Path.Combine(AppLog.Data,"steam-client-fixture-"+Guid.NewGuid().ToString("N"));
            string helperFolder=Path.Combine(folder,"bin","cef","cef.win7x64");
            Directory.CreateDirectory(helperFolder);
            string executable=Path.Combine(folder,"steam.exe");
            string helper=Path.Combine(helperFolder,"steamwebhelper.exe");
            File.WriteAllText(executable,"not launched by this fixture");
            File.WriteAllText(helper,"not launched by this fixture");
            var icon=new DrawingImage();icon.Freeze();
            var native=new AppEntry {Name="Steam",AppID="Valve.Steam",Target=executable,Executable=executable,Icon=icon};
            var saved=new AppEntry {Name="Old Steam title",AppID="Steam",Executable=helper,StoredKey="original-pin-slot",Pinned=true};
            var fixedPin=Catalog.ResolveInstalled(saved,new[]{native});
            Require(fixedPin.Key==saved.Key&&fixedPin.Target==executable&&fixedPin.Executable==executable&&Object.ReferenceEquals(fixedPin.Icon,icon),
                "Steam pin keeps its slot and uses the client executable/artwork after its window closes");
            var standalone=SteamLauncher.Resolve(saved,new AppEntry[0]);
            var launch=SteamLauncher.Prepare(standalone);
            Require(launch!=null&&launch.FileName==executable&&launch.Arguments==""&&launch.UseShellExecute,
                "Steam relaunch uses steam.exe rather than steamwebhelper or an unregistered AppsFolder ID");
            var preference=new Catalog.PinPreference {Key=fixedPin.Key,Name=fixedPin.Name,Target=fixedPin.Target,AppID=fixedPin.AppID,Executable=fixedPin.Executable};
            var json=new System.Web.Script.Serialization.JavaScriptSerializer();
            var reopened=json.Deserialize<Catalog.PinPreference>(json.Serialize(preference));
            var cold=Catalog.ResolveInstalled(new AppEntry {Name=reopened.Name,Target=reopened.Target,AppID=reopened.AppID,Executable=reopened.Executable,StoredKey=reopened.Key},new[]{native});
            Require(cold.Key==saved.Key&&SteamLauncher.Prepare(cold).FileName==executable,
                "Steam launcher survives saved-pin serialization without a running window");
            var game=new AppEntry {AppID="com.valvesoftware.steam.4000",Target="steam://rungameid/4000",Executable=executable};
            var browser=new AppEntry {AppID="Chrome.profile-one",Executable=helper};
            Require(!SteamLauncher.IsClient(game)&&!SteamLauncher.IsClient(browser)&&
                Object.ReferenceEquals(SteamLauncher.Resolve(game,new[]{native}),game)&&!Catalog.Same(game,native),
                "Steam games and browser profiles keep their own launch targets and identities");
            Require(Catalog.Merge(new[]{fixedPin},new[]{standalone}).Count==1,
                "Steam client aliases do not create duplicate dock icons");
            var pathPin=new AppEntry {Name="Steam",Target=helper,Executable=helper};
            Require(Catalog.ResolveInstalled(pathPin,new[]{native}).Key==helper,
                "A helper-path pin without an AppID retains its original position key");
            var unresolved=new AppEntry {Name="Steam",AppID="Steam",Icon=icon};
            var warm=Catalog.ResolveInstalled(saved,new[]{unresolved});
            Require(warm.Key==saved.Key&&warm.Executable==executable&&warm.Target==executable,
                "A cold Start-app cache cannot overwrite the repaired Steam launch path");
            checks.Add("Steam client pins retain their client launcher, icon and saved slot after window closure; Chromium helpers, Steam games and Chrome profiles stay distinct; no app was launched");
        }
        static string Pixels(ImageSource source){var bitmap=source as BitmapSource;if(bitmap==null)return null;var image=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);int stride=image.PixelWidth*4;var bytes=new byte[stride*image.PixelHeight];image.CopyPixels(bytes,stride,0);using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes));}
        internal static async Task CheckDock(ShellController shell,System.Windows.Forms.Screen screen)
        {
            var pins=shell.Pins;
            var running=shell.Running;
            DockHost dock=null;
            try {
                var old=new AppEntry {Name="Old development name",Target=@"C:\Fixture\Files.exe",Pinned=true};
                shell.Pins=new List<AppEntry>{old};shell.Running=new List<AppEntry>();
                dock=new DockHost(shell,screen);dock.Show();await Task.Delay(80);
                var button=dock.AppButton(old.Key);
                Require(button!=null,"fixture dock creates the pinned cell");
                var host=((Grid)button.Content).Children.OfType<System.Windows.Controls.Border>().First();
                var current=old.Copy();current.Name="Native file manager";current.Icon=ArtworkPalette.Sample(Colors.Coral);
                shell.Pins=new List<AppEntry>{current};dock.Rebuild();await Task.Delay(30);
                var presenter=host.Child as Decorator;
                var image=presenter==null?host.Child as Image:presenter.Child as Image;
                Require(Object.ReferenceEquals(button,dock.AppButton(old.Key))&&image!=null&&Object.ReferenceEquals(image.Source,AppIconTheme.Resolve(current,current.Icon))&&(string)button.ToolTip==current.Name,"existing dock cell updates its icon and label without replacing the animated button");
            }
            finally {
                if(dock!=null)dock.Close();
                shell.Pins=pins;shell.Running=running;
            }
        }
        internal static int Live()
        {
            try{var apps=Catalog.Read(false);var native=apps.FirstOrDefault(a=>a.Name=="One UI 내 파일");if(native==null)throw new InvalidOperationException("Reported application is not installed");string exe=native.Executable??native.AppID;string legacy=Path.Combine(Path.GetDirectoryName(exe),"electron.exe");var saved=new AppEntry {Name="one-ui-files",Target=legacy,Executable=legacy};var rebound=Catalog.ResolveInstalled(saved,apps);var shortcut=WebAppIcons.LaunchShortcut(native.AppID,native.Name);Require(shortcut!=null,"native shortcut exists");var actual=AppIcons.Get(rebound);var expected=AppIcons.Normalize(Native.ShortcutIcon(shortcut.File) as BitmapSource);bool same=Pixels(actual)!=null&&Pixels(actual)==Pixels(expected);Require(rebound.Name==native.Name&&rebound.Key==saved.Key&&same,"reported legacy pin resolves to the exact native name/icon");AppLog.Json("app-metadata-live.json",new {passed=true,nativeName=native.Name,nameMatches=true,iconPixelsMatchNativeShortcut=true,pinIdentityPreserved=true,applicationLaunched=false});return 0;}catch(Exception e){AppLog.Json("app-metadata-live.json",new {passed=false,error=e.ToString()});return 1;}
        }
    }
}
