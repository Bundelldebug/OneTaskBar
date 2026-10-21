using System;
using System.Collections.Generic;
using System.Linq;
namespace OneTaskBar
{
    // Enumeration and foreground changes must not reorder already-open applications.
    internal sealed class RuntimeDockOrder
    {
        readonly List<string> order=new List<string>();HashSet<string> running=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool initialized;
        internal List<AppEntry> Arrange(IEnumerable<AppEntry> source)
        {
            var items=source.Where(a=>!String.IsNullOrEmpty(a.Key)).GroupBy(a=>a.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();var live=new HashSet<string>(items.Where(a=>a.Windows.Count>0).Select(a=>a.Key),StringComparer.OrdinalIgnoreCase);var present=new HashSet<string>(items.Select(a=>a.Key),StringComparer.OrdinalIgnoreCase);
            order.RemoveAll(k=>!present.Contains(k));
            foreach(var item in items.Where(a=>!live.Contains(a.Key)))if(!order.Contains(item.Key,StringComparer.OrdinalIgnoreCase)){int first=order.FindIndex(live.Contains);order.Insert(first<0?order.Count:first,item.Key);}
            foreach(var item in items.Where(a=>live.Contains(a.Key)&&(!initialized||!running.Contains(a.Key))).OrderBy(a=>a.StartedUtcTicks>0?a.StartedUtcTicks:Int64.MaxValue)){order.RemoveAll(k=>String.Equals(k,item.Key,StringComparison.OrdinalIgnoreCase));order.Add(item.Key);}
            foreach(var item in items)if(!order.Contains(item.Key,StringComparer.OrdinalIgnoreCase))order.Add(item.Key);running=live;initialized=true;var ranks=order.Select((key,index)=>new {key,index}).ToDictionary(a=>a.key,a=>a.index,StringComparer.OrdinalIgnoreCase);return items.OrderBy(a=>ranks[a.Key]).ToList();
        }
        internal bool Move(string source,string target,bool after)
        {
            int from=order.FindIndex(k=>String.Equals(k,source,StringComparison.OrdinalIgnoreCase)),to=order.FindIndex(k=>String.Equals(k,target,StringComparison.OrdinalIgnoreCase));if(from<0||to<0||from==to)return false;string moving=order[from];order.RemoveAt(from);to=order.FindIndex(k=>String.Equals(k,target,StringComparison.OrdinalIgnoreCase));order.Insert(to+(after?1:0),moving);return true;
        }
        internal void Reset(){order.Clear();running.Clear();initialized=false;}
        internal static void Check()
        {
            Func<string,long,AppEntry> live=(key,time)=>{var item=new AppEntry {Target=key,StartedUtcTicks=time};item.Windows.Add(new IntPtr(1));return item;};var pin=new AppEntry {Target="pin"};var a=live("a",1);var b=live("b",2);var c=live("c",3);var d=live("d",4);var state=new RuntimeDockOrder();Func<IEnumerable<AppEntry>,string> keys=items=>String.Join(",",state.Arrange(items).Select(x=>x.Key));
            if(keys(new[]{pin,b,a})!="pin,a,b"||keys(new[]{b,pin,a})!="pin,a,b"||keys(new[]{c,b,pin,a})!="pin,a,b,c"||keys(new[]{pin,a,c})!="pin,a,c"||keys(new[]{b,c,pin,a})!="pin,a,c,b")throw new Exception("Dock launch ordering failed");state.Move("c","a",false);if(keys(new[]{d,b,c,pin,a})!="pin,c,a,b,d")throw new Exception("Manual order or newest-app append failed");
        }
    }
}
