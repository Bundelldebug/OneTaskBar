using System;
using System.Collections.Generic;
using System.Linq;

namespace OneTaskBar
{
    internal sealed partial class StartPinStore
    {
        internal string CreateFolder(string appKey)
        {
            string key="folder:"+Guid.NewGuid().ToString("N");
            bool saved=Change(list=>
            {
                int index=list.FindIndex(n=>!n.Folder&&Same(n.Key,appKey));
                if(index<0)return false;
                var app=list[index];
                list[index]=new StartPinNode {Key=key,Name="폴더",Children=new List<StartPinNode>{app}};
                return true;
            });
            return saved?key:null;
        }
        internal bool ApplyWindowsFolders(List<AppEntry> apps,bool enabled)
        {
            var names=apps.Where(a=>!String.IsNullOrWhiteSpace(a.SourceFolder)&&!String.IsNullOrEmpty(a.Key))
                .GroupBy(a=>a.Key,StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g=>g.Key,g=>g.First().SourceFolder,StringComparer.OrdinalIgnoreCase);
            return Change(list=>
            {
                string before=FolderSignature(list);
                for(int i=list.Count-1;i>=0;i--)
                {
                    var folder=list[i];
                    if(!folder.Folder||!folder.Key.StartsWith("folder:windows:",StringComparison.Ordinal))continue;
                    list.RemoveAt(i);
                    list.InsertRange(i,folder.Children);
                }
                if(enabled)
                {
                    var groups=list.Where(n=>!n.Folder&&names.ContainsKey(n.Key))
                        .GroupBy(n=>names[n.Key],StringComparer.CurrentCultureIgnoreCase).ToArray();
                    foreach(var group in groups)
                    {
                        var children=group.ToList();
                        int index=list.IndexOf(children[0]);
                        foreach(var child in children)list.Remove(child);
                        list.Insert(index,new StartPinNode
                        {
                            Key="folder:windows:"+Uri.EscapeDataString(group.Key.ToUpperInvariant()),
                            Name=Name(group.Key),Children=children
                        });
                    }
                }
                return before!=FolderSignature(list);
            });
        }
        static string FolderSignature(IEnumerable<StartPinNode> nodes)
        {
            return String.Join("|",nodes.Select(n=>n.Key+(n.Folder?"["+String.Join(",",n.Children.Select(c=>c.Key))+"]":"")));
        }
    }
}
