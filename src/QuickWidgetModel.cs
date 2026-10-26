using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace OneTaskBar
{
    internal sealed class QuickWidgetSlot
    {
        public string Key;
        public int X,Y,Columns,Rows;
        public bool Hidden;
        internal QuickWidgetSlot Copy(){return (QuickWidgetSlot)MemberwiseClone();}
    }
    internal sealed class QuickWidgetLayout
    {
        internal const int Columns=4,MaximumHeight=5,MaximumRow=96;
        internal static readonly string[] BaseKeys={"wifi","bluetooth","toggles","brightness","theme","volume","mute","media","devices","buds","windows","capture","manager","dnd","efficiency","awake","animations","startup","slider-scroll"};
        internal static readonly string[] Keys=BaseKeys.Concat(QuickSettingCatalog.All.Select(s=>s.Key)).ToArray();
        readonly string file;readonly bool presentation;
        internal List<QuickWidgetSlot> Items {get;private set;}
        internal QuickWidgetLayout(string path,bool presentation=false){file=path;this.presentation=presentation;Reset(false);try{if(File.Exists(file)&&new FileInfo(file).Length<=65536){var saved=new JavaScriptSerializer {MaxJsonLength=65536}.Deserialize<List<QuickWidgetSlot>>(File.ReadAllText(file));Restore(saved);}}catch(Exception e){AppLog.Once("quick9-layout-read",e.GetType().Name);}}
        internal QuickWidgetSlot Find(string key){return Items.FirstOrDefault(i=>i.Key==key);}
        internal static bool Slider(string key){return key=="brightness"||key=="volume";}
        internal static int MinimumColumns(string key){return key=="media"?2:1;}
        int MinimumRows(string key){return key=="media"&&!presentation?2:1;}
        internal List<QuickWidgetSlot> Copy(){return Items.Select(i=>i.Copy()).ToList();}
        internal void Restore(IEnumerable<QuickWidgetSlot> saved)
        {
            var known=new HashSet<string>(Keys,StringComparer.Ordinal);var clean=new List<QuickWidgetSlot>();
            foreach(var source in (saved??Enumerable.Empty<QuickWidgetSlot>()).Take(48)){
                if(source==null||!known.Remove(source.Key??""))continue;var item=source.Copy();item.Columns=Clamp(item.Columns,MinimumColumns(item.Key),4);item.Rows=Clamp(item.Rows,MinimumRows(item.Key),5);if(Slider(item.Key)&&item.Columns>1&&item.Rows>1)item.Rows=1;item.X=Clamp(item.X,0,4-item.Columns);item.Y=Clamp(item.Y,0,MaximumRow+Keys.Length*MaximumHeight);clean.Add(item);
            }
            foreach(var item in Defaults())if(known.Contains(item.Key))clean.Add(item);
            Items=clean;Pack(null);
        }
        internal void Reset(bool save=true){Items=Defaults();if(save)Save();}
        static List<QuickWidgetSlot> Defaults()
        {
            return new List<QuickWidgetSlot>{Slot("wifi",0,0,2,1),Slot("bluetooth",2,0,2,1),Slot("toggles",0,1,4,1),Slot("brightness",0,2,3,1),Slot("theme",3,2,1,1),Slot("volume",0,3,3,1),Slot("mute",3,3,1,1),Slot("media",0,4,4,2),Slot("devices",0,6,2,1),Slot("buds",2,6,2,1),Slot("windows",0,7,2,1),Slot("capture",2,7,2,1),Slot("manager",0,8,2,1),Slot("dnd",0,9,1,1,true),Slot("efficiency",1,9,1,1,true),Slot("awake",2,9,1,1,true),Slot("animations",3,9,1,1,true),Slot("startup",0,10,1,1,true),Slot("slider-scroll",1,10,1,1,true)}.Concat(QuickSettingCatalog.All.Select((s,i)=>Slot(s.Key,i%4,11+i/4,1,1,true))).ToList();
        }
        static QuickWidgetSlot Slot(string key,int x,int y,int w,int h,bool hidden=false){return new QuickWidgetSlot {Key=key,X=x,Y=y,Columns=w,Rows=h,Hidden=hidden};}
        internal bool Move(string key,int x,int y)
        {
            var item=Find(key);if(item==null||item.Hidden)return false;x=Clamp(x,0,4-item.Columns);y=Clamp(y,0,MaximumRow);if(item.X==x&&item.Y==y)return false;item.X=x;item.Y=y;Pack(item);return true;
        }
        internal bool Resize(string key,int columns,int rows,bool vertical)
        {
            var item=Find(key);if(item==null||item.Hidden)return false;columns=Clamp(columns,MinimumColumns(key),4);rows=Clamp(rows,MinimumRows(key),5);
            if(Slider(key)){if(vertical)columns=1;else rows=1;}
            if(item.Columns==columns&&item.Rows==rows)return false;item.Columns=columns;item.Rows=rows;item.X=Math.Min(item.X,4-columns);Pack(item);return true;
        }
        internal void SetHidden(string key,bool hidden){var item=Find(key);if(item==null||item.Hidden==hidden)return;item.Hidden=hidden;if(!hidden)Pack(item);}
        void Pack(QuickWidgetSlot priority)
        {
            var placed=new List<QuickWidgetSlot>();if(priority!=null&&!priority.Hidden)placed.Add(priority);
            foreach(var item in Items.Where(i=>!i.Hidden&&i!=priority).OrderBy(i=>i.Y).ThenBy(i=>i.X)){
                int row=item.Y;while(placed.Any(p=>Overlaps(item,p))){item.Y=++row;}placed.Add(item);
            }
        }
        internal static bool Overlaps(QuickWidgetSlot a,QuickWidgetSlot b){return !a.Hidden&&!b.Hidden&&a.X<b.X+b.Columns&&b.X<a.X+a.Columns&&a.Y<b.Y+b.Rows&&b.Y<a.Y+a.Rows;}
        internal bool Fits(){var active=Items.Where(i=>!i.Hidden).ToArray();return active.All(i=>i.Columns>=MinimumColumns(i.Key)&&i.Columns<=4&&i.Rows>=MinimumRows(i.Key)&&i.Rows<=5&&i.X>=0&&i.X+i.Columns<=4&&(!Slider(i.Key)||i.Columns==1||i.Rows==1))&&!active.Any(a=>active.Any(b=>a!=b&&Overlaps(a,b)));}
        internal bool Save()
        {
            if(presentation)return false;
            try{Directory.CreateDirectory(Path.GetDirectoryName(file));string temporary=file+".tmp";File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(Items));if(File.Exists(file))File.Replace(temporary,file,file+".previous");else File.Move(temporary,file);return true;}catch(Exception e){AppLog.Write("quick9-layout-save",e.GetType().Name);return false;}
        }
        static int Clamp(int n,int min,int max){return Math.Max(min,Math.Min(max,n));}
    }
}
