using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;

namespace OneTaskBar
{
    internal sealed class DesktopWidgetSlot
    {
        public string Id,Key,Monitor;
        public double X,Y,Width,Height;
        internal DesktopWidgetSlot Copy()
        {
            return (DesktopWidgetSlot)MemberwiseClone();
        }
    }

    internal static class DesktopWidgetCatalog
    {
        internal static readonly string[] Keys=InfoTileCatalog.Tokens.Concat(new[]{"clock","battery","media"}).Concat(QuickActionCatalog.Specs.Select(s=>s[0])).ToArray();
        internal static bool Known(string key)
        {
            return key!=null&&Keys.Contains(key);
        }
        internal static string Title(string key)
        {
            if(InfoTileCatalog.Known(key))return InfoTileCatalog.Title(key);
            if(key=="clock")return "시계 · 날짜";
            if(key=="battery")return "배터리";
            if(key=="media")return "미디어 플레이어";
            var spec=QuickActionCatalog.Specs.FirstOrDefault(s=>s[0]==key);
            return spec==null?"위젯":spec[2];
        }
        internal static string Icon(string key)
        {
            if(InfoTileCatalog.Known(key))return InfoTileCatalog.Family(key).Icon;
            if(key=="clock")return "focus";
            if(key=="battery")return "battery";
            if(key=="media")return "play";
            return QuickActionCatalog.Specs.First(s=>s[0]==key)[1];
        }
        internal static Size Minimum(string key)
        {
            if(InfoTileCatalog.Known(key))
            {
                var design=InfoTileCatalog.Design(key);
                return new Size(design.Width*.6,design.Height*.6);
            }
            return key=="media"?new Size(280,180):key=="clock"?new Size(180,110):key=="battery"?new Size(150,100):new Size(80,80);
        }
        internal static Size Initial(string key)
        {
            if(InfoTileCatalog.Known(key))return InfoTileCatalog.Design(key);
            return key=="media"?new Size(360,210):key=="clock"?new Size(260,150):key=="battery"?new Size(220,130):new Size(208,88);
        }
    }

    internal sealed class DesktopWidgetStore
    {
        internal const int Limit=24;
        readonly string file;
        internal readonly List<DesktopWidgetSlot> Items=new List<DesktopWidgetSlot>();
        internal DesktopWidgetStore(string path)
        {
            file=path;
            try
            {
                if(!File.Exists(file)||new FileInfo(file).Length>131072)return;
                var values=new JavaScriptSerializer().Deserialize<List<DesktopWidgetSlot>>(File.ReadAllText(file));
                if(values==null)return;
                var ids=new HashSet<string>(StringComparer.Ordinal);
                foreach(var item in values.Take(Limit))
                {
                    Guid id;
                    if(item==null||!DesktopWidgetCatalog.Known(item.Key)||!Guid.TryParse(item.Id,out id)||!ids.Add(item.Id))continue;
                    Migrate(item);
                    Normalize(item);
                    Items.Add(item);
                }
            }
            catch(Exception e)
            {
                AppLog.Once("desktop-widgets-load",e.GetType().Name);
            }
        }
        // Widgets saved before the sized tiles existed keep their place and take the matching tile.
        static void Migrate(DesktopWidgetSlot item)
        {
            string token=item.Key=="clock"?"clock-digital:2x2":item.Key=="battery"?"gauges:2x1":item.Key=="media"?"player:4x1":null;
            if(token==null)return;
            var size=DesktopWidgetCatalog.Initial(token);
            item.Key=token;
            item.Width=size.Width;
            item.Height=size.Height;
        }
        internal static double Bounded(double value,double minimum,double maximum,double fallback)
        {
            return Double.IsNaN(value)||Double.IsInfinity(value)?fallback:Math.Max(minimum,Math.Min(maximum,value));
        }
        internal static void Normalize(DesktopWidgetSlot item)
        {
            var minimum=DesktopWidgetCatalog.Minimum(item.Key);
            var initial=DesktopWidgetCatalog.Initial(item.Key);
            item.Width=Bounded(item.Width,minimum.Width,720,initial.Width);
            item.Height=Bounded(item.Height,minimum.Height,600,initial.Height);
            item.X=Bounded(item.X,0,32768,24);
            item.Y=Bounded(item.Y,0,32768,24);
            if(item.Monitor!=null&&item.Monitor.Length>256)item.Monitor=null;
        }
        // Saved coordinates are DIPs relative to a monitor's current work area.
        // A disconnected monitor falls back without overwriting its saved placement.
        internal static Rect Fit(DesktopWidgetSlot item,Size available)
        {
            double width=Math.Min(item.Width,Math.Max(1,available.Width));
            double height=Math.Min(item.Height,Math.Max(1,available.Height));
            return new Rect(Math.Max(0,Math.Min(item.X,available.Width-width)),Math.Max(0,Math.Min(item.Y,available.Height-height)),width,height);
        }
        internal const double GridStep=24,Clearance=8;
        // The free grid position nearest to the wanted one, or the wanted one if the screen is full.
        internal static Rect FindFree(Rect wanted,IList<Rect> blocked,Size available)
        {
            Rect best=wanted;
            double bestDistance=Double.MaxValue;
            for(double y=GridStep;y+wanted.Height<=available.Height;y+=GridStep)
            {
                for(double x=GridStep;x+wanted.Width<=available.Width;x+=GridStep)
                {
                    double distance=(x-wanted.X)*(x-wanted.X)+(y-wanted.Y)*(y-wanted.Y);
                    if(distance>=bestDistance)continue;
                    var clearance=new Rect(x-Clearance,y-Clearance,wanted.Width+Clearance*2,wanted.Height+Clearance*2);
                    bool free=true;
                    for(int i=0;i<blocked.Count&&free;i++)free=!clearance.IntersectsWith(blocked[i]);
                    if(!free)continue;
                    best=new Rect(x,y,wanted.Width,wanted.Height);
                    bestDistance=distance;
                }
            }
            return best;
        }
        internal DesktopWidgetSlot Add(string key,string monitor,Size available,IEnumerable<Rect> avoid=null)
        {
            if(Items.Count>=Limit||!DesktopWidgetCatalog.Known(key))return null;
            var size=DesktopWidgetCatalog.Initial(key);
            var item=new DesktopWidgetSlot {Id=Guid.NewGuid().ToString("N"),Key=key,Monitor=monitor,Width=size.Width,Height=size.Height,X=GridStep,Y=GridStep};
            var occupied=Items.Where(s=>s.Monitor==monitor).Select(s=>Fit(s,available)).ToList();
            if(avoid!=null)occupied.AddRange(avoid);
            Rect chosen=FindFree(Fit(item,available),occupied,available);
            item.X=chosen.X;
            item.Y=chosen.Y;
            Items.Add(item);
            Save();
            return item;
        }
        internal void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                string temporary=file+".tmp";
                File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(Items));
                if(File.Exists(file))File.Replace(temporary,file,file+".previous");
                else File.Move(temporary,file);
            }
            catch(Exception e)
            {
                AppLog.Write("desktop-widgets-save",e.Message);
            }
        }
    }
}
