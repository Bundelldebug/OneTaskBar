using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    internal static class WebAppIcons
    {
        static readonly object gate=new object();static Dictionary<string,AppShortcut> shortcuts,names,targets;static DateTime indexed;
        internal static bool IsWebApp(string id){return !String.IsNullOrEmpty(id)&&id.IndexOf("._crx_",StringComparison.OrdinalIgnoreCase)>=0;}
        internal static ImageSource Read(AppEntry entry)
        {
            if(!IsWebApp(entry.AppID))return null;
            foreach(var window in entry.Windows){var image=Native.WindowRelaunchIcon(window);if(image!=null)return image;}
            string file=Shortcut(entry.AppID);if(file!=null){var image=Native.ShortcutIcon(file);if(image!=null)return image;}
            foreach(var window in entry.Windows){var image=Native.WindowIcon(window);if(image!=null)return image;}return null;
        }
        internal static string Shortcut(string id){var item=LaunchShortcut(id,null);return item!=null?item.File:null;}
        internal static AppShortcut LaunchShortcut(string id,string name)
        {
            lock(gate){if(shortcuts==null||(DateTime.UtcNow-indexed).TotalMinutes>=5){shortcuts=Index();indexed=DateTime.UtcNow;}AppShortcut item;if(!String.IsNullOrEmpty(id)&&(shortcuts.TryGetValue(id,out item)||targets.TryGetValue(id,out item)))return item;return !String.IsNullOrEmpty(name)&&names.TryGetValue(name,out item)?item:null;}
        }
        static Dictionary<string,AppShortcut> Index()
        {
            var result=new Dictionary<string,AppShortcut>(StringComparer.OrdinalIgnoreCase);names=new Dictionary<string,AppShortcut>(StringComparer.CurrentCultureIgnoreCase);targets=new Dictionary<string,AppShortcut>(StringComparer.OrdinalIgnoreCase);int remaining=768;
            foreach(var folder in new[]{Environment.SpecialFolder.StartMenu,Environment.SpecialFolder.CommonStartMenu,Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.CommonDesktopDirectory}){
                try{string path=Environment.GetFolderPath(folder);if(!Directory.Exists(path))continue;bool desktop=folder==Environment.SpecialFolder.DesktopDirectory||folder==Environment.SpecialFolder.CommonDesktopDirectory;foreach(string file in Directory.EnumerateFiles(path,"*.lnk",desktop?SearchOption.TopDirectoryOnly:SearchOption.AllDirectories).Take(remaining)){remaining--;var info=Native.ShortcutInfo(file);if(info==null)continue;if(!String.IsNullOrEmpty(info.Id)&&!result.ContainsKey(info.Id))result[info.Id]=info;if(!String.IsNullOrEmpty(info.Target)&&String.IsNullOrWhiteSpace(info.Arguments)&&!targets.ContainsKey(info.Target))targets[info.Target]=info;string name=Path.GetFileNameWithoutExtension(file);AppShortcut previous;if(!names.TryGetValue(name,out previous))names[name]=info;else if(previous!=null&&(!String.Equals(previous.Target,info.Target,StringComparison.OrdinalIgnoreCase)||previous.Arguments!=info.Arguments))names[name]=null;}if(remaining<=0)break;}catch{}
            }return result;
        }
        internal static ImageSource ReadResource(string resource)
        {
            if(String.IsNullOrWhiteSpace(resource))return null;int comma=resource.LastIndexOf(',');string path=Environment.ExpandEnvironmentVariables(comma>=0?resource.Substring(0,comma):resource).Trim().Trim('"');int index=0;if(comma>=0&&!Int32.TryParse(resource.Substring(comma+1).Trim(),out index))return null;
            // Do not resolve network resources or fetch site favicons. These are the
            // same local icon resources registered with Explorer by the installed app.
            if(!Path.IsPathRooted(path)||path.StartsWith(@"\\",StringComparison.Ordinal)||!File.Exists(path))return null;
            return Native.ResourceIcon(path,index);
        }
    }
}
