using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OneTaskBar
{
    // Over a black wallpaper the dock's dark text disappears, and over a white one light text does. Like the top bar,
    // each of the three dock pieces reads what is behind it and picks light or dark text for the whole piece.
    // The read is 64 samples per piece off the interface thread, so a one-second cadence stays cheap.
    internal sealed partial class DockHost
    {
        readonly bool?[] lightInk=new bool?[3];
        DateTime inkAt=DateTime.MinValue;
        bool inkBusy;
        static bool AdaptiveInk {get{return !DockAppearance.Custom||DockAppearance.IconsOnly;}}
        Brush InkFor(int piece)
        {
            if(!AdaptiveInk||!lightInk[piece].HasValue)return Glass95.DockForeground;
            return lightInk[piece].Value?Paint.ControlWhite:Paint.OnLight;
        }
        internal Brush StartInk {get{return InkFor(0);}}
        internal Brush AppsInk {get{return InkFor(1);}}
        internal Brush StatusInk {get{return InkFor(2);}}
        Brush InkOf(GlassWindow surface){return surface==Start?StartInk:surface==Apps?AppsInk:StatusInk;}
        GlassWindow Piece(int piece){return piece==0?Start:piece==1?Apps:Status;}
        // Called with every refresh, but reads the screen only every few seconds, and only while the dock is showing.
        void RefreshInk()
        {
            if(closed||inkBusy||!ready)return;
            if(!AdaptiveInk)
            {
                bool had=lightInk[0].HasValue||lightInk[1].HasValue||lightInk[2].HasValue;
                if(had){lightInk[0]=lightInk[1]=lightInk[2]=null;ApplyInk(-1);}
                return;
            }
            var now=DateTime.UtcNow;
            if(now<inkAt||!visible||fullScreen||!controller.DisplayAwake)return;
            var areas=new System.Drawing.Rectangle[3];
            bool any=false;
            for(int piece=0;piece<3;piece++)
            {
                var window=Piece(piece);
                if(!window.IsVisible)continue;
                var handle=new WindowInteropHelper(window).Handle;
                Native.RectI box;
                if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out box)||box.Right-box.Left<8||box.Bottom-box.Top<8)continue;
                areas[piece]=System.Drawing.Rectangle.FromLTRB(box.Left,box.Top,box.Right,box.Bottom);
                any=true;
            }
            // A change of wallpaper or window behind the dock should show within a second or two; battery reads a little less often.
            inkAt=now.AddSeconds(!any?2:controller.Power.Connected?1:2);
            if(!any)return;
            inkBusy=true;
            // A wallpaper change should repaint the text promptly, so a fresh reading is not held back by hysteresis for long.
            BackgroundWork.Run(()=>
            {
                var values=new double[3];
                for(int piece=0;piece<3;piece++)values[piece]=areas[piece].Width>0?LiquidReadability.Sample(areas[piece]):-1;
                return values;
            }).ContinueWith(task=>
            {
                var dispatcher=Application.Current==null?null:Application.Current.Dispatcher;
                if(dispatcher==null||dispatcher.HasShutdownStarted)return;
                dispatcher.BeginInvoke(new Action(()=>
                {
                    inkBusy=false;
                    if(closed||task.Status!=System.Threading.Tasks.TaskStatus.RanToCompletion||!AdaptiveInk)return;
                    for(int piece=0;piece<3;piece++)UseReading(piece,task.Result[piece]);
                }));
            });
        }
        void UseReading(int piece,double sampled)
        {
            if(sampled<0)return;
            // What the screen shows there is the picture behind plus the glass tint laid over it; take the tint back out.
            double coverage=DockAppearance.IconsOnly?0:.3,own=Glass95.DockDark?.1:.9;
            double behind=Math.Max(0,Math.Min(1,(sampled-coverage*own)/(1-coverage)));
            var previous=lightInk[piece];
            // Two thresholds, so a value near the middle cannot make the text flip back and forth.
            bool light=previous.HasValue?(previous.Value?behind<.60:behind<.50):behind<.55;
            if(previous==light)return;
            lightInk[piece]=light;
            ApplyInk(piece);
        }
        void ApplyInk(int piece)
        {
            for(int index=0;index<3;index++)
            {
                if(piece>=0&&index!=piece)continue;
                var window=Piece(index);
                Glass95.DockInk(window.Surface,InkFor(index));
                if(index==0)startGlyph.Color=Preferences.Current.NativeStartButton?Paint.Brush("#FF1975DD"):StartInk;
                if(index==1)
                {
                    pinDivider.Background=AppsInk;
                    foreach(var dot in indicators.Values)dot.Background=AppsInk;
                    foreach(var cell in cells.Values)if(cell.Dot!=null)cell.Dot.Background=AppsInk;
                }
                if(index==2)UpdateStatusSymbols();
            }
            if(piece<0||piece==2)Refresh(controller.Power);
        }
    }
}
