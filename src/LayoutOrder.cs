using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace OneTaskBar
{
    // Stable identities keep positions when an app closes, its title changes, or a new tile is added.
    internal sealed class LayoutStore
    {
        readonly string file;
        Dictionary<string,string[]> orders=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
        internal LayoutStore(string path)
        {
            file=path;
            try {if(File.Exists(file)){var saved=new JavaScriptSerializer().Deserialize<Dictionary<string,string[]>>(File.ReadAllText(file));if(saved!=null)foreach(var pair in saved)orders[pair.Key]=Clean(pair.Value).ToArray();}}
            catch(Exception e){AppLog.Once("layout-read",e.Message);}
        }
        static List<string> Clean(IEnumerable<string> keys) {return (keys??Enumerable.Empty<string>()).Where(k=>!String.IsNullOrEmpty(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();}
        internal List<T> Arrange<T>(string scope,IEnumerable<T> items,Func<T,string> key)
        {
            string[] saved;if(!orders.TryGetValue(scope,out saved))return items.ToList();
            var ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);for(int i=0;i<saved.Length;i++)ranks[saved[i]]=i;
            return items.OrderBy(item=> {int rank;string id=key(item);return id!=null&&ranks.TryGetValue(id,out rank)?rank:Int32.MaxValue;}).ToList();
        }
        internal bool Move(string scope,IEnumerable<string> visible,string source,string target,bool after)
        {
            var before=Clean(visible);var next=before.ToList();
            int from=next.FindIndex(k=>String.Equals(k,source,StringComparison.OrdinalIgnoreCase));
            if(from<0||!next.Any(k=>String.Equals(k,target,StringComparison.OrdinalIgnoreCase))||String.Equals(source,target,StringComparison.OrdinalIgnoreCase))return false;
            string moving=next[from];next.RemoveAt(from);int to=next.FindIndex(k=>String.Equals(k,target,StringComparison.OrdinalIgnoreCase));next.Insert(to+(after?1:0),moving);
            if(before.SequenceEqual(next,StringComparer.OrdinalIgnoreCase))return false;
            string[] saved;orders.TryGetValue(scope,out saved);var all=Clean(saved);foreach(string key in before)if(!all.Contains(key,StringComparer.OrdinalIgnoreCase))all.Add(key);
            // Retain slots belonging to temporarily closed apps, as well as their relative order.
            var present=new HashSet<string>(before,StringComparer.OrdinalIgnoreCase);int index=0;
            var merged=all.Select(k=>present.Contains(k)?next[index++]:k).ToArray();
            var updated=new Dictionary<string,string[]>(orders,StringComparer.OrdinalIgnoreCase);updated[scope]=merged;return Commit(updated);
        }
        internal bool Reset(string prefix)
        {
            var updated=new Dictionary<string,string[]>(orders,StringComparer.OrdinalIgnoreCase);
            foreach(string key in orders.Keys.Where(k=>k.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)).ToArray())updated.Remove(key);
            return updated.Count!=orders.Count&&Commit(updated);
        }
        bool Commit(Dictionary<string,string[]> updated)
        {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(file));string temporary=file+".tmp";File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(updated));
                if(File.Exists(file))File.Replace(temporary,file,file+".previous");else File.Move(temporary,file);orders=updated;return true;
            }catch(Exception e){AppLog.Write("layout-save",e.Message);return false;}
        }
    }
    internal static class LayoutOrder
    {
        static readonly LayoutStore store=new LayoutStore(Path.Combine(AppLog.Data,"layout.json"));
        internal static event Action<string> Changed;
        internal static List<T> Arrange<T>(string scope,IEnumerable<T> items,Func<T,string> key) {return store.Arrange(scope,items,key);}
        internal static bool Move(string scope,IEnumerable<string> visible,string source,string target,bool after)
        {
            if(!store.Move(scope,visible,source,target,after))return false;var notify=Changed;if(notify!=null)notify(scope);return true;
        }
        internal static void Reset(string prefix) {if(!store.Reset(prefix))return;var notify=Changed;if(notify!=null)notify(prefix);}
    }
}
