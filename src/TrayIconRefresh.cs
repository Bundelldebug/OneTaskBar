using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal sealed class TrayIconUpdate
    {
        internal TrayIconItem Item;
        internal string Name;
        internal BitmapSource Image;
    }

    internal sealed partial class TrayIconSession
    {
        readonly Dictionary<TrayIconItem, byte[]> lastPixels = new Dictionary<TrayIconItem, byte[]>();
        readonly Dictionary<TrayIconItem, TrayIconQuality> quality = new Dictionary<TrayIconItem, TrayIconQuality>();
        readonly HashSet<TrayIconItem> originalPixels = new HashSet<TrayIconItem>();
        bool reportedPixels;

        internal TrayIconUpdate[] ReadUpdates()
        {
            if (!SourceVisible) return new TrayIconUpdate[0];
            TrayIconItem[] items;
            lock (itemGate) items = Items.ToArray();
            var frame = TrayFrame.Read(source);
            if (frame == null || disposed) return new TrayIconUpdate[0];
            var updates = new List<TrayIconUpdate>();
            foreach (var item in items)
            {
                if (disposed) break;
                try
                {
                    if (item.Element == null) continue;
                    var current = item.Element.Current;
                    byte[] pixels;
                    byte[] previous;
                    lastPixels.TryGetValue(item,out previous);
                    System.Windows.Media.Color matte;
                    var image = TrayIconPixels.Extract(frame, current.BoundingRectangle, out pixels, out matte, previous);
                    bool changed = image != null;
                    if (changed)
                    {
                        lastPixels[item] = pixels;
                        TrayIconQuality choice;
                        if(!quality.TryGetValue(item,out choice))quality.Add(item,choice=new TrayIconQuality(item.OriginalIcon));
                        image=choice.Choose(image,matte);
                        if(Object.ReferenceEquals(image,item.OriginalIcon))originalPixels.Add(item);
                        else originalPixels.Remove(item);
                        changed=!Object.ReferenceEquals(image,item.Icon);
                    }
                    string name = current.Name ?? item.Name;
                    if (changed || name != item.Name)
                        updates.Add(new TrayIconUpdate {Item = item, Name = name, Image = changed ? image : null});
                }
                catch (System.Windows.Automation.ElementNotAvailableException) { }
            }
            if(!reportedPixels&&lastPixels.Count>0)
            {
                reportedPixels=true;
                AppLog.Write("tray-live-icons","Current source pixels: "+lastPixels.Count+" icons; original resolution: "+originalPixels.Count);
            }
            return updates.ToArray();
        }
    }
}
