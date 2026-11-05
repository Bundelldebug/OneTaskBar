using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace OneTaskBar
{
    // Captures what the keyboard volume and brightness bar really shows on screen, for several outputs.
    internal static class HudChecks
    {
        static void Require(bool ok,string message)
        {
            if(!ok)throw new InvalidOperationException("Volume bar: "+message);
        }
        // Which symbol the keyboard bar picks for each kind of output, and that guards hold the Windows bar's cover.
        internal static void Verify(HudWindow hud)
        {
            hud.Set("volume",60,false,"스피커(Realtek Audio)");
            Require(hud.Slider.Kind=="volume","ordinary speakers keep the level icon");
            hud.Set("volume",20,false,"스피커(Realtek Audio)");
            Require(hud.Slider.Kind=="volume-low","the level icon still changes with the volume");
            hud.Set("volume",60,false,"Headphones (Sony WH-1000XM4)");
            Require(hud.Slider.Kind=="headphones","headphones show their own symbol");
            hud.Set("volume",60,false,"Galaxy Buds2 Pro");
            Require(hud.Slider.Kind=="earbuds","earbuds show a symbol until their picture loads");
            hud.Set("volume",60,false,"NVIDIA High Definition Audio (HDMI)");
            Require(hud.Slider.Kind=="computer","a display output shows a display symbol");
            hud.Set("volume",60,true,"Headphones (Sony WH-1000XM4)");
            Require(hud.Slider.Kind=="mute","a muted output shows the muted icon");
            hud.Set("brightness",60,false,"Headphones (Sony WH-1000XM4)");
            Require(hud.Slider.Kind=="brightness","brightness never takes the sound output's symbol");
            Require(OutputIcons.Kind(null)==null&&OutputIcons.Kind("")==null&&OutputIcons.Kind("Speakers (USB Audio)")==null,"unknown outputs fall back to the level icon");
            Require(OutputIcons.Kind("AirPods Pro")=="earbuds"&&OutputIcons.Kind("헤드셋 (Logitech G733)")=="headphones"&&OutputIcons.Kind("LG TV (HDMI)")=="computer","common names are recognized in either language");
            var ready=DateTime.MinValue;
            var start=new DateTime(2026,11,5,12,0,0,DateTimeKind.Utc);
            Require(!NativeOsdGate.Hold(false,start,ref ready,NativeOsdGate.Grace),"nothing is held before the bar was ever replaceable");
            Require(NativeOsdGate.Hold(true,start,ref ready,NativeOsdGate.Grace),"a replaceable bar is held");
            Require(NativeOsdGate.Hold(false,start.AddSeconds(30),ref ready,NativeOsdGate.Grace),"a short loss of audio keeps the cover on");
            Require(!NativeOsdGate.Hold(false,start.AddSeconds(61),ref ready,NativeOsdGate.Grace),"a long loss lets Windows' bar back");
            Require(LiveBackdrop.SweepOrphans()>=0,"orphan sweep runs without error");
        }
        internal static async Task<int> Shots(HudWindow hud,string[] outputs,double volume,string prefix)
        {
            int taken=0;
            foreach(string output in outputs)
            {
                hud.Set("volume",volume,false,output);
                await Task.Delay(1000);
                Native.RectI rect;
                if(!Native.GetWindowRect(new WindowInteropHelper(hud).Handle,out rect))continue;
                using(var bitmap=new System.Drawing.Bitmap(rect.Right-rect.Left,rect.Bottom-rect.Top))
                using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(rect.Left,rect.Top,0,0,bitmap.Size);
                    bitmap.Save(Path.Combine(AppLog.Data,prefix+(++taken)+".png"));
                }
            }
            return taken;
        }
    }
}
