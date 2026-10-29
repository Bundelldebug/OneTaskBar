using System;
using System.IO;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class InternetShortcut
    {
        internal string File,Target,IconFile;
        internal int IconIndex;
    }
    internal static class InternetShortcuts
    {
        internal static InternetShortcut Read(string file)
        {
            if(String.IsNullOrWhiteSpace(file)||file.StartsWith(@"\\",StringComparison.Ordinal)||!file.EndsWith(".url",StringComparison.OrdinalIgnoreCase)||!System.IO.File.Exists(file))return null;
            try
            {
                if(new FileInfo(file).Length>131072)return null;
                var result=new InternetShortcut {File=file};
                bool section=false;
                foreach(string line in System.IO.File.ReadLines(file))
                {
                    string value=line.Trim();
                    if(value.StartsWith("[",StringComparison.Ordinal))
                    {
                        section=String.Equals(value,"[InternetShortcut]",StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if(!section)continue;
                    int separator=value.IndexOf('=');
                    if(separator<=0)continue;
                    string name=value.Substring(0,separator).Trim();
                    value=value.Substring(separator+1).Trim().Trim('"');
                    if(String.Equals(name,"URL",StringComparison.OrdinalIgnoreCase))result.Target=value;
                    else if(String.Equals(name,"IconFile",StringComparison.OrdinalIgnoreCase))result.IconFile=value;
                    else if(String.Equals(name,"IconIndex",StringComparison.OrdinalIgnoreCase))Int32.TryParse(value,out result.IconIndex);
                }
                return String.IsNullOrWhiteSpace(result.Target)?null:result;
            }
            catch(IOException) {return null;}
            catch(UnauthorizedAccessException) {return null;}
        }
        internal static ImageSource Icon(AppEntry app)
        {
            string file=app.Target;
            if(!String.IsNullOrEmpty(file)&&file.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase))file=Native.ShortcutTarget(file);
            if(String.IsNullOrEmpty(file)||!file.EndsWith(".url",StringComparison.OrdinalIgnoreCase))
            {
                var shortcut=WebAppIcons.LaunchShortcut(app.AppID,app.Name);
                file=shortcut==null?app.AppID:shortcut.File;
            }
            var info=Read(file);
            if(info==null||String.IsNullOrWhiteSpace(info.IconFile))return null;
            string path=Environment.ExpandEnvironmentVariables(info.IconFile);
            if(!Path.IsPathRooted(path))path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)),path);
            if(path.StartsWith(@"\\",StringComparison.Ordinal)||!System.IO.File.Exists(path))return null;
            return Native.ResourceIcon(path,info.IconIndex)??Native.AppIcon(path);
        }
        internal static string Identity(InternetShortcut shortcut)
        {
            Uri address;
            if(Uri.TryCreate(shortcut.Target,UriKind.Absolute,out address)&&address.Scheme=="steam"&&(address.Host=="rungameid"||address.Host=="run"))
            {
                string game=address.AbsolutePath.Trim('/').Split('/')[0];
                ulong number;
                if(UInt64.TryParse(game,out number))return "com.valvesoftware.steam."+number;
            }
            return shortcut.Target;
        }
        internal static string Folder(string shortcut)
        {
            if(String.IsNullOrEmpty(shortcut))return null;
            foreach(var special in new[]{Environment.SpecialFolder.Programs,Environment.SpecialFolder.CommonPrograms})
            {
                string root=Environment.GetFolderPath(special).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(!shortcut.StartsWith(root,StringComparison.OrdinalIgnoreCase))continue;
                string relative=shortcut.Substring(root.Length);
                int separator=relative.IndexOf(Path.DirectorySeparatorChar);
                return separator>0?relative.Substring(0,separator):null;
            }
            return null;
        }
    }
}
