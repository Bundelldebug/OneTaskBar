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
                    var image = TrayIconPixels.Extract(frame, current.BoundingRectangle, out pixels, previous);
                    bool changed = image != null;
                    if (changed) lastPixels[item] = pixels;
                    string name = current.Name ?? item.Name;
                    if (changed || name != item.Name)
                        updates.Add(new TrayIconUpdate {Item = item, Name = name, Image = changed ? image : null});
                }
                catch (System.Windows.Automation.ElementNotAvailableException) { }
            }
            if(!reportedPixels&&lastPixels.Count>0)
            {
                reportedPixels=true;
                AppLog.Write("tray-live-icons","Current source pixels: "+lastPixels.Count+" icons");
            }
            return updates.ToArray();
        }
    }
}
