using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace OneTaskBar
{
    // Compatible leaf fields preserve the existing flat file and pin positions.
    internal sealed class StartPinNode
    {
        public string Key,Name,AppID,Target,Executable;
        public List<StartPinNode> Children;
        internal bool Folder {get{return Children!=null;}}
        internal StartPinNode Copy(){return new StartPinNode {Key=Key,Name=Name,AppID=AppID,Target=Target,Executable=Executable,Children=Children==null?null:Children.Select(n=>n.Copy()).ToList()};}
    }
    internal sealed class StartTile
    {
        internal string Key,Name;internal AppEntry App;internal List<StartTile> Children;
        internal bool Folder {get{return Children!=null;}}
    }
    internal sealed class StartPinStore
    {
        readonly string file;readonly int capacity;List<StartPinNode> items=new List<StartPinNode>();bool initialized;
        internal StartPinStore(string path,int limit=120)
        {
            file=path;capacity=Math.Max(1,Math.Min(4096,limit));try {if(File.Exists(file)&&new FileInfo(file).Length<=4194304){var data=Serializer().Deserialize<List<StartPinNode>>(File.ReadAllText(file));if(data!=null){items=Normalize(data);initialized=true;}}}catch(Exception error){AppLog.Once("start-pins-read",error.Message);}
        }
        static JavaScriptSerializer Serializer(){return new JavaScriptSerializer {MaxJsonLength=4194304,RecursionLimit=12};}
        List<StartPinNode> Normalize(List<StartPinNode> source)
        {
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var folders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var result=new List<StartPinNode>();int count=0;
            foreach(var node in source.Where(n=>n!=null).Take(capacity)){
                if(node.Folder){var children=node.Children.Where(n=>n!=null&&!n.Folder&&!String.IsNullOrEmpty(n.Key)&&seen.Add(n.Key)).Take(Math.Max(0,capacity-count)).ToList();if(children.Count==0)continue;count+=children.Count;node.Children=children;if(String.IsNullOrEmpty(node.Key)||!node.Key.StartsWith("folder:",StringComparison.Ordinal)||!folders.Add(node.Key))node.Key="folder:"+Guid.NewGuid().ToString("N");node.Name=Name(node.Name);result.Add(node);}
                else if(count<capacity&&!String.IsNullOrEmpty(node.Key)&&seen.Add(node.Key)){result.Add(node);count++;}
            }return result;
        }
        static IEnumerable<StartPinNode> Leaves(IEnumerable<StartPinNode> nodes){return nodes.SelectMany(n=>n.Folder?n.Children:new[]{n}.AsEnumerable());}
        internal bool Contains(AppEntry app){return Leaves(items).Any(p=>Same(p.Key,app.Key));}
        internal int FolderCount {get{return items.Count(n=>n.Folder);}}
        internal void Seed(List<AppEntry> apps)
        {
            if(initialized||apps.Count==0)return;
            string[] names={"파일 탐색기","File Explorer","삼성 브라우저","Samsung Browser","Microsoft Edge","Samsung Notes","삼성 노트","Microsoft Store","메모장","Notepad","설정","Settings","계산기","Calculator","사진","Photos","Quick Share","Spotify","ChatGPT","Codex"};
            Replace(apps.Where(a=>names.Any(n=>Same(a.Name,n))).Take(12));
        }
        internal List<AppEntry> Resolve(List<AppEntry> apps)
        {
            return Tiles(apps).SelectMany(t=>t.Folder?t.Children.Select(c=>c.App):new[]{t.App}).ToList();
        }
        internal List<StartTile> Tiles(List<AppEntry> apps)
        {
            Seed(apps);var known=apps.Where(a=>!String.IsNullOrEmpty(a.Key)).GroupBy(a=>a.Key,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
            Func<StartPinNode,StartTile> leaf=node=> {AppEntry app;if(known.TryGetValue(node.Key,out app))app=app.Copy();else app=Catalog.ResolveInstalled(new AppEntry {Name=node.Name,AppID=node.AppID,Target=node.Target,Executable=node.Executable,StoredKey=node.Key},apps);return new StartTile {Key=node.Key,Name=app.Name??node.Name,App=app};};
            return items.Select(n=>n.Folder?new StartTile {Key=n.Key,Name=n.Name,Children=n.Children.Select(leaf).ToList()}:leaf(n)).ToList();
        }
        internal void Set(AppEntry app,bool pin)
        {
            Change(list=> {if(pin&&Leaves(list).Any(n=>Same(n.Key,app.Key)))return false;Remove(list,app.Key);if(pin&&Leaves(list).Count()<capacity)list.Add(Preference(app));return true;});
        }
        internal void Replace(IEnumerable<AppEntry> apps){var values=apps.Where(a=>!String.IsNullOrEmpty(a.Key)).GroupBy(a=>a.Key,StringComparer.OrdinalIgnoreCase).Select(g=>Preference(g.First())).Take(capacity).ToList();Change(list=> {list.Clear();list.AddRange(values);return true;});}
        // The all-apps screen owns a separate store; new installs append without disturbing folders.
        internal void Reconcile(List<AppEntry> apps)
        {
            var catalog=apps.Where(a=>!String.IsNullOrEmpty(a.Key)).GroupBy(a=>a.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).Take(capacity).ToList();if(catalog.Count==0)return;
            var known=new HashSet<string>(catalog.Select(a=>a.Key),StringComparer.OrdinalIgnoreCase);
            Change(list=> {bool changed=false;foreach(var key in Leaves(list).Where(n=>!known.Contains(n.Key)).Select(n=>n.Key).ToArray()){Remove(list,key);changed=true;}var present=new HashSet<string>(Leaves(list).Select(n=>n.Key),StringComparer.OrdinalIgnoreCase);foreach(var app in catalog)if(present.Add(app.Key)){list.Add(Preference(app));changed=true;}return changed;});
        }
        internal bool Move(string source,string target,bool after)
        {
            return Change(list=> {var moving=list.FirstOrDefault(p=>Same(p.Key,source));var anchor=list.FirstOrDefault(p=>Same(p.Key,target));if(moving==null||anchor==null||moving==anchor)return false;int old=list.IndexOf(moving);list.Remove(moving);int index=list.IndexOf(anchor)+(after?1:0);list.Insert(index,moving);return old!=index;});
        }
        internal bool MoveWithin(string folder,string source,string target,bool after)
        {
            return Change(list=> {var group=list.FirstOrDefault(n=>n.Folder&&Same(n.Key,folder));if(group==null)return false;var moving=group.Children.FirstOrDefault(n=>Same(n.Key,source));var anchor=group.Children.FirstOrDefault(n=>Same(n.Key,target));if(moving==null||anchor==null||moving==anchor)return false;int old=group.Children.IndexOf(moving);group.Children.Remove(moving);int index=group.Children.IndexOf(anchor)+(after?1:0);group.Children.Insert(index,moving);return old!=index;});
        }
        internal bool Combine(string source,string target)
        {
            return Change(list=> {var moving=list.FirstOrDefault(n=>Same(n.Key,source));var anchor=list.FirstOrDefault(n=>Same(n.Key,target));if(moving==null||moving.Folder||anchor==null||moving==anchor)return false;list.Remove(moving);if(anchor.Folder)anchor.Children.Add(moving);else {int index=list.IndexOf(anchor);list[index]=new StartPinNode {Key="folder:"+Guid.NewGuid().ToString("N"),Name="폴더",Children=new List<StartPinNode>{anchor,moving}};}return true;});
        }
        internal bool Rename(string folder,string name)
        {
            if(String.IsNullOrWhiteSpace(name))return false;return Change(list=> {var node=list.FirstOrDefault(n=>n.Folder&&Same(n.Key,folder));if(node==null||node.Name==Name(name))return false;node.Name=Name(name);return true;});
        }
        internal bool Extract(string key)
        {
            return Change(list=> {var folder=list.FirstOrDefault(n=>n.Folder&&n.Children.Any(c=>Same(c.Key,key)));if(folder==null)return false;var app=folder.Children.First(c=>Same(c.Key,key));folder.Children.Remove(app);int position=list.IndexOf(folder)+1;list.Insert(position,app);list.RemoveAll(n=>n.Folder&&n.Children.Count==0);return true;});
        }
        internal bool Dissolve(string key)
        {
            return Change(list=> {var folder=list.FirstOrDefault(n=>n.Folder&&Same(n.Key,key));if(folder==null)return false;int index=list.IndexOf(folder);list.RemoveAt(index);list.InsertRange(index,folder.Children);return true;});
        }
        static void Remove(List<StartPinNode> list,string key){list.RemoveAll(n=>!n.Folder&&Same(n.Key,key));foreach(var folder in list.Where(n=>n.Folder))folder.Children.RemoveAll(n=>Same(n.Key,key));list.RemoveAll(n=>n.Folder&&n.Children.Count==0);}
        static bool Same(string first,string second){return String.Equals(first,second,StringComparison.OrdinalIgnoreCase);}
        static string Name(string value){var name=new string((value??"").Where(c=>!Char.IsControl(c)).Take(48).ToArray()).Trim();return name.Length==0?"폴더":name;}
        static StartPinNode Preference(AppEntry app){return new StartPinNode {Key=app.Key,Name=app.Name,AppID=app.AppID,Target=app.Target,Executable=app.Executable};}
        bool Change(Func<List<StartPinNode>,bool> edit){var previous=items;items=items.Select(n=>n.Copy()).ToList();if(!edit(items)){items=previous;return false;}if(Save()){initialized=true;return true;}items=previous;return false;}
        bool Save(){try {Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".tmp";File.WriteAllText(temp,Serializer().Serialize(items));if(File.Exists(file)){try{File.Replace(temp,file,null);}catch(IOException){File.Copy(temp,file,true);try{File.Delete(temp);}catch(IOException){}}}else File.Move(temp,file);return true;}catch(Exception error){AppLog.Write("start-pins-save",error.Message);return false;}}
    }
}
