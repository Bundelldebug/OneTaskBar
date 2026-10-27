using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace OneTaskBar
{
    internal sealed partial class StartWindow
    {
        string pinSignature,recentSignature;bool pinsPending;List<StartRecent> recentPending;
        void FlushRetainedUpdates()
        {
            if(closed||!IsVisible||IsDismissing)return;
            if(pinsPending){pinsPending=false;RenderPins();}
            var pending=recentPending;recentPending=null;if(pending!=null)RenderRecent(pending);
        }
        static string AppStamp(AppEntry app){return app==null?"":String.Join("|",app.Key,app.Name,app.AppID,app.Target,app.Executable,app.Icon==null?0:RuntimeHelpers.GetHashCode(app.Icon));}
        string PinSignature()
        {
            var current=CurrentTiles();
            return (openFolder??"")+":"+pinPage+":"+current.Count+":"+String.Join("\n",current.Skip(pinPage*18).Take(18).Select(tile=>tile.Key+"|"+tile.Name+"|"+(tile.Folder?tile.Children.Count+":"+String.Join(";",tile.Children.Take(4).Select(child=>AppStamp(child.App))):AppStamp(tile.App))));
        }
        string RecentSignature(List<StartRecent> entries){return moreRecent+":"+entries.Count+":"+String.Join("\n",entries.Take(moreRecent?24:8).Select(item=>AppStamp(item.App)+"|"+item.Time.Ticks));}
        internal Button FirstRecentButton {get{return recent.Children.OfType<Button>().FirstOrDefault();}}
    }
}
