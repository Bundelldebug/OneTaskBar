using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace OneTaskBar
{
    internal sealed class QuickToggleOrder
    {
        internal static readonly string[] Defaults={"mute","theme","dnd","efficiency","awake","wifi","bluetooth","animations","startup","slider-scroll"};
        readonly string file;
        internal string[] Keys {get;private set;}
        internal QuickToggleOrder(string path){file=path;Keys=Defaults.ToArray();try{if(File.Exists(path)&&new FileInfo(path).Length<=8192){var saved=new JavaScriptSerializer {MaxJsonLength=8192}.Deserialize<string[]>(File.ReadAllText(path));Keys=Clean(saved);}}catch(Exception e){AppLog.Once("quick9-action-order-read",e.GetType().Name);}}
        static string[] Clean(IEnumerable<string> saved){return (saved??Enumerable.Empty<string>()).Where(k=>Defaults.Contains(k)).Concat(Defaults).Distinct(StringComparer.Ordinal).ToArray();}
        internal bool Move(string source,string target,bool after){var next=Keys.ToList();if(source==target||!next.Remove(source)||!next.Contains(target))return false;next.Insert(next.IndexOf(target)+(after?1:0),source);if(next.SequenceEqual(Keys))return false;try{Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(next));if(File.Exists(file))File.Replace(temp,file,file+".previous");else File.Move(temp,file);Keys=next.ToArray();return true;}catch(Exception e){AppLog.Write("quick9-action-order-save",e.GetType().Name);return false;}}
    }
}
