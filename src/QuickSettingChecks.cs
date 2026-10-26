using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    internal static class QuickSettingChecks
    {
        internal static void Validate(List<string> checks)
        {
            var images=new HashSet<string>();foreach(var setting in QuickSettingCatalog.All){var prefs=new PreferenceData();bool initial=setting.Read(prefs);setting.Write(prefs,!initial);if(setting.Read(prefs)==initial)throw new Exception("Setting toggle binding: "+setting.Key);setting.Write(prefs,initial);if(setting.Read(prefs)!=initial)throw new Exception("Setting toggle restoration: "+setting.Key);var visual=new DrawingVisual();using(var context=visual.RenderOpen())Paint.Icon(context,setting.Icon,Brushes.White,new Point(20,20),34);var bitmap=new RenderTargetBitmap(40,40,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var pixels=new byte[6400];bitmap.CopyPixels(pixels,160,0);if(!pixels.Any(b=>b!=0)||!images.Add(Convert.ToBase64String(pixels)))throw new Exception("Settings require distinct nonempty icons: "+setting.Key);}
            if(QuickSettingCatalog.All.Select(s=>s.Key).Distinct().Count()!=QuickSettingCatalog.All.Length)throw new Exception("Duplicate quick setting key");checks.Add("22 app-setting bindings reverse correctly on isolated preference objects and have distinct nonempty vector icons");
        }
    }
}
