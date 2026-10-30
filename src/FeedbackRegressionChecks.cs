using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OneTaskBar
{
    internal static class FeedbackRegressionChecks
    {
        static void Require(bool value,string name)
        {
            if(!value)throw new InvalidOperationException(name);
        }
        internal static void Validate(List<string> checks)
        {
            string directory=Path.Combine(AppLog.Data,"shortcut-folders-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string icon=Path.Combine(directory,"game.ico");
            using(var bitmap=new System.Drawing.Bitmap(32,32))
            {
                using(var drawing=System.Drawing.Graphics.FromImage(bitmap))drawing.Clear(System.Drawing.Color.OrangeRed);
                IntPtr handle=bitmap.GetHicon();
                try
                {
                    using(var source=System.Drawing.Icon.FromHandle(handle))
                    using(var output=File.Create(icon))source.Save(output);
                }
                finally {DestroyIcon(handle);}
            }
            string shortcut=Path.Combine(directory,"Garry's Mod.url");
            File.WriteAllText(shortcut,"[InternetShortcut]\r\nURL=steam://rungameid/4000\r\nIconFile=game.ico\r\nIconIndex=0\r\n");
            var game=new AppEntry {Name="Garry's Mod",AppID="steam://rungameid/4000",Target=shortcut};
            Require(InternetShortcuts.Identity(InternetShortcuts.Read(shortcut))=="com.valvesoftware.steam.4000","Steam AppsFolder identity was not indexed");
            var artwork=InternetShortcuts.Icon(game) as System.Windows.Media.Imaging.BitmapSource;
            Require(artwork!=null,"Steam URL shortcut lost its registered local icon");
            byte[] pixels=new byte[artwork.PixelWidth*artwork.PixelHeight*4];
            var rgba=new System.Windows.Media.Imaging.FormatConvertedBitmap(artwork,System.Windows.Media.PixelFormats.Bgra32,null,0);
            rgba.CopyPixels(pixels,artwork.PixelWidth*4,0);
            Require(pixels.Where((value,index)=>index%4==2).Any(value=>value>230),"Game icon pixels were replaced by a generic icon");
            File.WriteAllText(Path.Combine(directory,"invalid.url"),"URL=steam://rungameid/4000");
            Require(InternetShortcuts.Read(Path.Combine(directory,"invalid.url"))==null,"URL outside InternetShortcut section accepted");
            checks.Add("Steam URL shortcuts use their local IconFile, including relative paths; malformed shortcuts are ignored");

            var apps=new List<AppEntry>
            {
                new AppEntry {Name="Manual",AppID="manual"},
                new AppEntry {Name="Game A",AppID="game-a",SourceFolder="Games"},
                new AppEntry {Name="Game B",AppID="game-b",SourceFolder="Games"},
                new AppEntry {Name="Tool",AppID="tool",SourceFolder="Tools"}
            };
            string foldersFile=Path.Combine(directory,"apps.json");
            var folders=new StartPinStore(foldersFile,4096);
            folders.Replace(apps);
            string manual=folders.CreateFolder("manual");
            Require(manual!=null&&folders.ApplyWindowsFolders(apps,true),"App folder creation failed");
            var tiles=new StartPinStore(foldersFile,4096).Tiles(apps);
            Require(tiles.Any(t=>t.Key==manual)&&tiles.Any(t=>t.Name=="Games"&&t.Children.Count==2),"App folders were not persisted");
            DateTime written=File.GetLastWriteTimeUtc(foldersFile);
            Require(!folders.ApplyWindowsFolders(apps,true)&&written==File.GetLastWriteTimeUtc(foldersFile),"Unchanged app folders were written again");
            folders.ApplyWindowsFolders(apps,false);
            Require(folders.FolderCount==1&&folders.Resolve(apps).Select(a=>a.Key).Distinct().Count()==apps.Count,"Disabling automatic folders changed manual folders or lost apps");
            checks.Add("Windows app folders persist, preserve manual folders and every app, and avoid unchanged writes");

            var chrome=new AppEntry {Name="Chrome",AppID=@"C:\Apps\chrome.exe",Executable=@"C:\Apps\chrome.exe"};
            var first=new AppEntry {Name="Profile 1",AppID="Chrome.profile-one",Executable=chrome.Executable};
            var second=new AppEntry {Name="Profile 2",AppID="Chrome.profile-two",Executable=chrome.Executable};
            // ResolveInstalled used to replace an unknown profile's identity with the generic browser.
            first.Windows.Add(new IntPtr(123));
            Require(Catalog.ResolveInstalled(first,new[]{chrome}).AppID==first.AppID,"Chrome profile collapsed to generic browser metadata");
            Require(Catalog.Merge(new[]{first,second},new[]{first,second}).Count==2,"Chrome profiles merged by executable");
            first.Icon=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[]{255,0,0,255},4);
            var profile=first.Copy();profile.Icon=null;
            Require(Object.ReferenceEquals(Catalog.ResolveInstalled(first,new[]{profile}).Icon,first.Icon),"Profile window artwork was replaced by installed metadata");
            Require(Object.ReferenceEquals(Catalog.Merge(new[]{profile},new[]{first})[0].Icon,first.Icon),"Pinned Chrome profile did not refresh its running icon");
            checks.Add("Chrome profiles retain distinct identity and icon cache entries when they share one executable");

            using(var audio=new AudioService())
            {
                audio.Read();audio.Read();
                bool available=audio.Available;
                audio.RefreshDevice();
                audio.Read();
                if(available)Require(audio.Available,"Rebinding the output device disabled audio control");
                int reads=audio.ReadCount;
                for(int i=0;i<1000;i++)audio.Read();
                if(audio.Available&&audio.NotificationsRegistered)Require(audio.ReadCount==reads,"Unchanged volume caused repeated endpoint reads");
                audio.Dispose();
                Require(!audio.NotificationsRegistered,"Audio callback survived disposal");
            }
            checks.Add("Unchanged volume reads are coalesced and endpoint callbacks are released without changing volume");
            AudioMuteChecks.Validate(checks);
        }
        [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
    }
}
