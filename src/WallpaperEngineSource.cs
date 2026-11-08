using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace OneTaskBar
{
    // With Wallpaper Engine running, Windows still reports its own wallpaper file, so glass that samples the desktop
    // showed the default picture. The engine's own configuration names the wallpaper it is playing; its preview
    // image is a faithful enough source for a blurred backdrop.
    internal static class WallpaperEngineSource
    {
        static readonly string[] pictures={".jpg",".jpeg",".png",".bmp",".gif"};
        internal static string Current(System.Drawing.Rectangle screen)
        {
            try
            {
                string folder=EngineFolder();
                if(folder==null)return null;
                string config=Path.Combine(folder,"config.json");
                if(!File.Exists(config)||new FileInfo(config).Length>8388608)return null;
                var root=new JavaScriptSerializer {MaxJsonLength=int.MaxValue}.DeserializeObject(File.ReadAllText(config));
                var selected=Find(root,"selectedwallpapers",0) as IDictionary;
                if(selected==null||selected.Count==0)return null;
                int index=Array.FindIndex(System.Windows.Forms.Screen.AllScreens,s=>s.Bounds==screen);
                IDictionary entry=null;
                foreach(DictionaryEntry pair in selected)
                {
                    var candidate=pair.Value as IDictionary;
                    if(candidate==null)continue;
                    if(entry==null)entry=candidate;
                    if(index>=0&&String.Equals(Convert.ToString(pair.Key),"Monitor"+index,StringComparison.OrdinalIgnoreCase)){entry=candidate;break;}
                }
                string file=entry==null?null:entry["file"] as string;
                return Resolve(file,folder);
            }
            catch(Exception error){AppLog.Once("wallpaper-engine",error.GetType().Name);return null;}
        }
        static string EngineFolder()
        {
            foreach(string name in new[]{"wallpaper64","wallpaper32"})
            {
                foreach(var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        string exe=process.MainModule.FileName;
                        if(!String.IsNullOrEmpty(exe))return Path.GetDirectoryName(exe);
                    }
                    catch(Exception){}
                    finally{process.Dispose();}
                }
            }
            return null;
        }
        static object Find(object node,string key,int depth)
        {
            if(depth>8||node==null)return null;
            var map=node as IDictionary;
            if(map!=null)
            {
                foreach(DictionaryEntry pair in map)
                {
                    if(String.Equals(Convert.ToString(pair.Key),key,StringComparison.OrdinalIgnoreCase))return pair.Value;
                    var inner=Find(pair.Value,key,depth+1);
                    if(inner!=null)return inner;
                }
            }
            return null;
        }
        static string Resolve(string file,string folder)
        {
            if(String.IsNullOrWhiteSpace(file))return null;
            if(!Path.IsPathRooted(file))file=Path.Combine(folder,file);
            if(!File.Exists(file))return null;
            if(pictures.Contains(Path.GetExtension(file).ToLowerInvariant()))return file;
            string dir=Path.GetDirectoryName(file);
            string project=Path.Combine(dir,"project.json");
            if(File.Exists(project)&&new FileInfo(project).Length<1048576)
            {
                var data=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(project)) as IDictionary;
                string preview=data==null?null:data["preview"] as string;
                if(!String.IsNullOrWhiteSpace(preview))
                {
                    string path=Path.Combine(dir,preview);
                    if(File.Exists(path)&&pictures.Contains(Path.GetExtension(path).ToLowerInvariant()))return path;
                }
            }
            foreach(string name in new[]{"preview.jpg","preview.png","preview.gif"})
            {
                string path=Path.Combine(dir,name);
                if(File.Exists(path))return path;
            }
            return null;
        }
    }
}
