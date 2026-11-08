using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace OneTaskBar
{
    internal sealed class QuickToggleOrder
    {
        // The arrangement the app ships with; any key it does not name keeps its catalog position at the end.
        static readonly string[] Preferred={"mute","theme","dnd","efficiency","wifi","bluetooth","manager","slider-scroll","numbers","awake","startup","animations","quick-share","ai-select","second-screen","devices","windows","capture","buds","blur","charge-motion","hover","running-dots","seconds","notification-popups","buds-popup","dock-theme","solid-dock","duo","window-drop","cover","charge-sound","emergency-exit","native-start","native-search","native-switcher","native-center","native-quick","native-button","notification-sync","storage-share","camera-share"};
        internal static readonly string[] Defaults=Preferred.Where(k=>QuickActionCatalog.Specs.Any(s=>s[0]==k)).Concat(QuickActionCatalog.Specs.Select(s=>s[0])).Distinct(StringComparer.Ordinal).ToArray();
        readonly string file;
        internal string[] Keys {get;private set;}
        readonly HashSet<string> outside=new HashSet<string>(StringComparer.Ordinal);
        sealed class Saved {public string[] Order,Outside;}
        internal string[] Members {get{return Keys.Where(k=>!outside.Contains(k)).ToArray();}}
        internal QuickToggleOrder(string path){file=path;Keys=Defaults.ToArray();foreach(string key in QuickActionCatalog.DefaultOutside)outside.Add(key);try{if(File.Exists(path)&&new FileInfo(path).Length<=8192){string json=File.ReadAllText(path);var serializer=new JavaScriptSerializer {MaxJsonLength=8192};if(json.TrimStart().StartsWith("[",StringComparison.Ordinal))Keys=Clean(serializer.Deserialize<string[]>(json));else{var saved=serializer.Deserialize<Saved>(json);if(saved!=null){Keys=Clean(saved.Order);outside.Clear();foreach(string key in QuickActionCatalog.DefaultOutside.Where(k=>saved.Order==null||!saved.Order.Contains(k)).Concat(saved.Outside??new string[0]))if(Defaults.Contains(key))outside.Add(key);}}}}catch(Exception e){AppLog.Once("quick9-action-order-read",e.GetType().Name);}}
        static string[] Clean(IEnumerable<string> saved){return (saved??Enumerable.Empty<string>()).Where(k=>Defaults.Contains(k)).Concat(Defaults).Distinct(StringComparer.Ordinal).ToArray();}
        internal bool Move(string source,string target,bool after){var next=Keys.ToList();if(source==target||!next.Remove(source)||!next.Contains(target))return false;next.Insert(next.IndexOf(target)+(after?1:0),source);return !next.SequenceEqual(Keys)&&Commit(next.ToArray(),outside.ToArray());}
        internal bool Extract(string key){return Defaults.Contains(key)&&!outside.Contains(key)&&Commit(Keys,outside.Concat(new[]{key}).ToArray());}
        internal bool Regroup(string key){return Defaults.Contains(key)&&outside.Contains(key)&&Commit(Keys.Where(k=>k!=key).Concat(new[]{key}).ToArray(),outside.Where(k=>k!=key).ToArray());}
        internal bool Contains(string key){return Defaults.Contains(key)&&!outside.Contains(key);}
        bool Commit(string[] order,string[] external)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                string temp=file+".tmp";
                File.WriteAllText(temp,new JavaScriptSerializer().Serialize(new Saved {Order=order,Outside=external}));
                for(int attempt=0;;attempt++)
                {
                    try
                    {
                        if(File.Exists(file))File.Replace(temp,file,file+".previous");
                        else File.Move(temp,file);
                        break;
                    }
                    catch(IOException error)
                    {
                        int code=error.HResult&0xffff;
                        if(attempt>=2||(code!=32&&code!=33))throw;
                        System.Threading.Thread.Sleep(10);
                    }
                }
                Keys=order;
                outside.Clear();
                foreach(string key in external)outside.Add(key);
                return true;
            }
            catch(Exception error)
            {
                AppLog.Write("quick9-action-order-save",error.GetType().Name+" 0x"+error.HResult.ToString("X8")+": "+error.Message);
                return false;
            }
        }
    }
}
