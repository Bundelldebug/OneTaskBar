using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    internal static class AppMetadataChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("App metadata: "+message);}
        internal static void Validate(List<string> checks)
        {
            var icon=new DrawingImage();icon.Freeze();var old=new AppEntry {Name="development-name",Target=@"C:\Apps\Files\electron.exe",Executable=@"C:\Apps\Files\electron.exe"};var native=new AppEntry {Name="Native file manager",AppID=@"C:\Apps\Files\Files.exe",Target=@"C:\Menu\Files.lnk",Executable=@"C:\Apps\Files\Files.exe",Icon=icon};
            var rebound=Catalog.ResolveInstalled(old,new[]{native});Require(rebound.Key==old.Key&&rebound.Name==native.Name&&rebound.Target==native.Target&&rebound.Executable==native.Executable&&rebound.Icon==icon,"unique legacy Electron launcher adopts the native shortcut metadata while preserving pin identity");
            var other=new AppEntry {Name="Other app",AppID=@"C:\Apps\Files\Other.exe",Executable=@"C:\Apps\Files\Other.exe"};Require(Catalog.ResolveInstalled(old,new[]{native,other}).Target==old.Target,"ambiguous install folders are not guessed");Require(Catalog.ResolveInstalled(new AppEntry {Name="Old title",AppID=native.AppID},new[]{native}).Name==native.Name,"an exact identity uses the current native display name");var malformed=new AppEntry {Name="invalid",Target="C:\\invalid\0\\electron.exe"};Require(Catalog.ResolveInstalled(malformed,new[]{native}).Target==malformed.Target,"invalid stored paths leave other pinned apps usable");
            string folder=Path.Combine(Path.GetTempPath(),"OneTaskBar-metadata-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{var pins=new StartPinStore(Path.Combine(folder,"pins.json"));pins.Replace(new[]{old});var tile=pins.Tiles(new List<AppEntry>{native}).Single();Require(tile.Key==old.Key&&tile.App.Key==old.Key&&tile.Name==native.Name&&tile.App.Icon==icon,"Start displays current native names/icons without moving or renaming saved pins");}finally{foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
            checks.Add("Native app display names and shortcut icons replace stale pin metadata; unique legacy Electron launcher aliases retain pin identities and ambiguous folders remain untouched");
        }
        static string Pixels(ImageSource source){var bitmap=source as BitmapSource;if(bitmap==null)return null;var image=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);int stride=image.PixelWidth*4;var bytes=new byte[stride*image.PixelHeight];image.CopyPixels(bytes,stride,0);using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes));}
        internal static int Live()
        {
            try{var apps=Catalog.Read(false);var native=apps.FirstOrDefault(a=>a.Name=="One UI 내 파일");if(native==null)throw new InvalidOperationException("Reported application is not installed");string exe=native.Executable??native.AppID;string legacy=Path.Combine(Path.GetDirectoryName(exe),"electron.exe");var saved=new AppEntry {Name="one-ui-files",Target=legacy,Executable=legacy};var rebound=Catalog.ResolveInstalled(saved,apps);var shortcut=WebAppIcons.LaunchShortcut(native.AppID,native.Name);Require(shortcut!=null,"native shortcut exists");var actual=AppIcons.Get(rebound);var expected=AppIcons.Normalize(Native.ShortcutIcon(shortcut.File) as BitmapSource);bool same=Pixels(actual)!=null&&Pixels(actual)==Pixels(expected);Require(rebound.Name==native.Name&&rebound.Key==saved.Key&&same,"reported legacy pin resolves to the exact native name/icon");AppLog.Json("app-metadata-live.json",new {passed=true,nativeName=native.Name,nameMatches=true,iconPixelsMatchNativeShortcut=true,pinIdentityPreserved=true,applicationLaunched=false});return 0;}catch(Exception e){AppLog.Json("app-metadata-live.json",new {passed=false,error=e.ToString()});return 1;}
        }
    }
}
