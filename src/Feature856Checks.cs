using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Input;
namespace OneTaskBar
{
    internal static class Feature856Checks
    {
        internal static void Validate(List<string> checks)
        {
            if(!AppClick.Elevate(ModifierKeys.Control|ModifierKeys.Shift)||AppClick.Elevate(ModifierKeys.Control)||AppClick.Elevate(ModifierKeys.Shift)||AppClick.Elevate(ModifierKeys.Control|ModifierKeys.Shift|ModifierKeys.Alt))throw new Exception("Ctrl+Shift app launch gesture");checks.Add("Ctrl+Shift app clicks request elevation; partial or Alt-modified chords retain normal launch behavior without invoking UAC in tests");
            Directory.CreateDirectory(AppLog.Data);string path=Path.Combine(AppLog.Data,"recent-fixture-"+Guid.NewGuid().ToString("N")+".json");var a=new AppEntry {AppID="recent.a",Name="A"};var b=new AppEntry {AppID="recent.b",Name="B"};var c=new AppEntry {AppID="recent.c",Name="C",StartedUtcTicks=5};var time=new DateTime(2026,10,23,0,0,0,DateTimeKind.Utc);
            try{
                using(var history=new RecentAppUsage(path)){history.Record(a,time);history.Record(b,time.AddSeconds(1));history.Record(a,time.AddSeconds(2));history.Record(new AppEntry {Target="private-document.txt"},time.AddSeconds(3));if(String.Join(",",history.Recommend(new[]{a,b,c},new[]{c}).Select(e=>e.Key))!="recent.a,recent.b,recent.c")throw new Exception("Recent app order or running fallback");history.Flush();}
                using(var restored=new RecentAppUsage(path)){if(String.Join(",",restored.Recommend(new[]{a,b,c},new AppEntry[0]).Select(e=>e.Key))!="recent.a,recent.b"||restored.Recommend(new[]{b},new AppEntry[0]).Single().Key!="recent.b"||restored.Recommend(new AppEntry[0],new AppEntry[0]).Count!=0)throw new Exception("Recent app reload or removed apps");for(int i=0;i<90;i++)restored.Record(new AppEntry {AppID="bounded."+i},time.AddMinutes(i));restored.Flush();}
                string json=File.ReadAllText(path);var rows=new JavaScriptSerializer().Deserialize<List<RecentAppUse>>(json);if(rows.Count!=64||json.Contains("private-document")||json.Contains("Name"))throw new Exception("Recent app retention/privacy bounds");
            }finally{if(File.Exists(path))File.Delete(path);}
            checks.Add("Search recommendations follow persisted app-use recency, deduplicate identities, skip removed apps, fall back only to running apps and retain at most 64 IDs without document titles/paths");
            foreach(double scale in new[]{1.0,1.25,1.5,2.0,2.5})foreach(var work in new[]{new System.Drawing.Rectangle(0,0,1920,1000),new System.Drawing.Rectangle(-1600,-500,1600,900),new System.Drawing.Rectangle(2560,120,1080,1800),new System.Drawing.Rectangle(0,0,640,480)}){
                var left=WindowDropPlan.At(work,new System.Drawing.Point(work.Left+1,work.Top+work.Height/2),scale);var right=WindowDropPlan.At(work,new System.Drawing.Point(work.Right-1,work.Top+work.Height/2),scale);var middle=WindowDropPlan.At(work,new System.Drawing.Point(work.Left+work.Width/2,work.Top+work.Height/2),scale);var cancel=WindowDropPlan.At(work,new System.Drawing.Point(work.Left+work.Width/2,work.Bottom-1),scale);
                if(left.Kind!=WindowDropKind.Left||right.Kind!=WindowDropKind.Right||middle.Kind!=WindowDropKind.Popup||cancel.Kind!=WindowDropKind.Cancel||left.Bounds.Right!=right.Bounds.Left||left.Bounds.Width+right.Bounds.Width!=work.Width||!work.Contains(middle.Bounds)||left.OtherHalf!=right.Bounds)throw new Exception("Window drop bounds/DPI/negative origin");
                int boundary=(int)Math.Min(work.Width*.23,180*scale);var held=WindowDropPlan.At(work,new System.Drawing.Point(work.Left+boundary+(int)(6*scale),work.Top+work.Height/2),scale,WindowDropKind.Left);if(held.Kind!=WindowDropKind.Left)throw new Exception("Window drop boundary hysteresis");
            }
            checks.Add("Dock drag left/right/pop-up/cancel zones stay in the target work area across 20 DPI/monitor layouts, including negative origins, portrait and narrow monitors");
            var properties=new Dictionary<string,object>{{"System.Devices.Aep.IsConnected",true},{"System.Devices.Aep.Bluetooth.Cod.Major",4},{"System.Devices.Aep.Bluetooth.Cod.Minor",6}};var audio=BluetoothPeer.From("audio","Buds",true,properties);var unknown=BluetoothPeer.From("unknown","Registered",true,new Dictionary<string,object>());if(!audio.Paired||!audio.Connected||audio.Icon!="headphones"||audio.State!="연결됨"||unknown.State!="등록됨"||unknown.Icon!="bluetooth")throw new Exception("Bluetooth row classification");foreach(var subtype in new[]{new{Minor=1,Icon="headphones"},new{Minor=2,Icon="headphones"},new{Minor=4,Icon="microphone"},new{Minor=5,Icon="volume"},new{Minor=6,Icon="headphones"}}){properties["System.Devices.Aep.Bluetooth.Cod.Minor"]=subtype.Minor;if(BluetoothPeer.From("audio","Audio",true,properties).Icon!=subtype.Icon)throw new Exception("Bluetooth audio subtype");}checks.Add("Bluetooth rows distinguish registered, connected and new devices, use native device-class icons, and do not invent a disconnected state when Windows omits connectivity");
        }
    }
}
