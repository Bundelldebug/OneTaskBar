using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Xml;

namespace OneTaskBar
{
    internal static class TaskbarLogo
    {
        static readonly Regex size=new Regex(@"(?:\.|_)targetsize-(\d+)(?:\.|_|$)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        internal static BitmapSource Read(string appId)
        {
            try{string file=Source(appId);if(file==null||new FileInfo(file).Length>8388608)return null;
                using(var stream=File.OpenRead(file)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=160;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}}
            catch{return null;}
        }
        internal static string Source(string appId){try{var app=Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(appId);return app==null||app.Package==null?null:Find(app.Package.InstalledLocation.Path,appId.Substring(appId.IndexOf('!')+1));}catch{return null;}}
        internal static string Find(string root,string application)
        {
            try{root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;string manifest=Path.Combine(root,"AppxManifest.xml");if(!File.Exists(manifest)||new FileInfo(manifest).Length>2097152)return null;
                var document=new XmlDocument {XmlResolver=null};using(var reader=XmlReader.Create(manifest,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))document.Load(reader);
                var node=document.SelectNodes("//*[local-name()='Application']").Cast<XmlNode>().FirstOrDefault(n=>n.Attributes["Id"]!=null&&String.Equals(n.Attributes["Id"].Value,application,StringComparison.OrdinalIgnoreCase));if(node==null)return null;
                var visual=node.ChildNodes.Cast<XmlNode>().FirstOrDefault(n=>n.LocalName=="VisualElements");var logo=visual==null?null:visual.Attributes["Square44x44Logo"];if(logo==null||String.IsNullOrWhiteSpace(logo.Value)||logo.Value.Contains(":"))return null;
                string basis=Path.GetFullPath(Path.Combine(root,logo.Value.Replace('/',Path.DirectorySeparatorChar)));if(!basis.StartsWith(root,StringComparison.OrdinalIgnoreCase))return null;string folder=Path.GetDirectoryName(basis),prefix=Path.GetFileNameWithoutExtension(basis)+".";if(!Directory.Exists(folder))return null;
                return Directory.EnumerateFiles(folder,"*.png").Take(256).Where(f=>Path.GetFileName(f).StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(f).IndexOf("altform-unplated",StringComparison.OrdinalIgnoreCase)>=0&&size.IsMatch(Path.GetFileName(f))).OrderBy(f=>Rank(f)).ThenBy(f=>f,StringComparer.OrdinalIgnoreCase).FirstOrDefault();}
            catch{return null;}
        }
        static int Rank(string file){int pixels;var match=size.Match(Path.GetFileName(file));if(!match.Success||!Int32.TryParse(match.Groups[1].Value,out pixels))return Int32.MaxValue;return pixels>=160?pixels-160:1000+160-pixels;}
    }
}
