using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace OneTaskBar
{
    internal sealed class StartPinStore
    {
        readonly string file;List<Catalog.PinPreference> items=new List<Catalog.PinPreference>();bool initialized;
        internal StartPinStore(string path)
        {
            file=path;try {if(File.Exists(file)){items=new JavaScriptSerializer().Deserialize<List<Catalog.PinPreference>>(File.ReadAllText(file))??items;initialized=true;}}catch(Exception error){AppLog.Once("start-pins-read",error.Message);}
        }
        internal bool Contains(AppEntry app){return items.Any(p=>String.Equals(p.Key,app.Key,StringComparison.OrdinalIgnoreCase));}
        internal void Seed(List<AppEntry> apps)
        {
            if(initialized||apps.Count==0)return;
            string[] names={"파일 탐색기","File Explorer","삼성 브라우저","Samsung Browser","Microsoft Edge","Samsung Notes","삼성 노트","Microsoft Store","메모장","Notepad","설정","Settings","계산기","Calculator","사진","Photos","Quick Share","Spotify","ChatGPT","Codex"};
            Replace(apps.Where(a=>names.Any(n=>String.Equals(a.Name,n,StringComparison.OrdinalIgnoreCase))).Take(12));
        }
        internal List<AppEntry> Resolve(List<AppEntry> apps)
        {
            Seed(apps);return items.Select(p=> {var known=apps.FirstOrDefault(a=>String.Equals(a.Key,p.Key,StringComparison.OrdinalIgnoreCase));return known!=null?known.Copy():new AppEntry {Name=p.Name,AppID=p.AppID,Target=p.Target,Executable=p.Executable};}).ToList();
        }
        internal void Set(AppEntry app,bool pin)
        {
            initialized=true;items.RemoveAll(p=>String.Equals(p.Key,app.Key,StringComparison.OrdinalIgnoreCase));if(pin&&items.Count<120)items.Add(Preference(app));Save();
        }
        internal void Replace(IEnumerable<AppEntry> apps){initialized=true;items=apps.Where(a=>!String.IsNullOrEmpty(a.Key)).GroupBy(a=>a.Key,StringComparer.OrdinalIgnoreCase).Select(g=>Preference(g.First())).Take(120).ToList();Save();}
        internal bool Move(string source,string target,bool after)
        {
            var next=items.ToList();var moving=next.FirstOrDefault(p=>String.Equals(p.Key,source,StringComparison.OrdinalIgnoreCase));var anchor=next.FirstOrDefault(p=>String.Equals(p.Key,target,StringComparison.OrdinalIgnoreCase));if(moving==null||anchor==null||moving==anchor)return false;
            next.Remove(moving);next.Insert(next.IndexOf(anchor)+(after?1:0),moving);if(items.SequenceEqual(next))return false;var old=items;items=next;if(Save())return true;items=old;return false;
        }
        static Catalog.PinPreference Preference(AppEntry app){return new Catalog.PinPreference {Key=app.Key,Name=app.Name,AppID=app.AppID,Target=app.Target,Executable=app.Executable};}
        bool Save(){try {Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(items));if(File.Exists(file)){try{File.Replace(temp,file,null);}catch(IOException){File.Copy(temp,file,true);try{File.Delete(temp);}catch(IOException){}}}else File.Move(temp,file);return true;}catch(Exception error){AppLog.Write("start-pins-save",error.Message);return false;}}
    }
}
