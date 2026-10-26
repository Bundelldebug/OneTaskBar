using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class BatteryPill : FrameworkElement
    {
        internal static readonly DependencyProperty MorphProperty=DependencyProperty.Register("Morph",typeof(double),typeof(BatteryPill),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender,(s,e)=> {var pill=(BatteryPill)s;if(pill.MorphChanged!=null)pill.MorphChanged((double)e.NewValue);}));
        internal Action<double> MorphChanged;
        internal double Morph {get{return (double)GetValue(MorphProperty);}set{SetValue(MorphProperty,value);}}
        internal double ExpandedWidth=164,Reveal=1;
        internal double DrawWidth {get{return ActualWidth+(Math.Max(ActualWidth,ExpandedWidth)-ActualWidth)*Morph;}}
        public PowerState State=new PowerState();
        public bool Celebration;public double Phase;internal bool Compact;
        internal static bool IsLow(PowerState state,bool celebration=false){return state!=null&&state.Known&&state.Level>=0&&state.Level<=20&&!state.Connected&&!state.Charging&&!celebration;}
        int desktopWatts;
        public BatteryPill(){Width=52;Height=23;IsHitTestVisible=false;}
        public void Update(PowerState state){int watts=state.Known?0:PowerDisplay.Clamp(Preferences.Current.DesktopPowerWatts);if(State.Level==state.Level&&State.Connected==state.Connected&&State.Charging==state.Charging&&State.Known==state.Known&&desktopWatts==watts)return;State=state;desktopWatts=watts;InvalidateVisual();}
        static Color Mix(Color a,Color b,double value){return Color.FromRgb((byte)(a.R+(b.R-a.R)*value),(byte)(a.G+(b.G-a.G)*value),(byte)(a.B+(b.B-a.B)*value));}
        Brush ChargeBrush()
        {
            var purple=Color.FromRgb(157,112,255);var cyan=Color.FromRgb(27,219,241);var blue=Color.FromRgb(29,38,246);
            double blend=(Math.Sin(Phase*Math.PI*2)+1)/2;
            var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,0)};
            brush.GradientStops.Add(new GradientStop(Mix(purple,cyan,blend),0));brush.GradientStops.Add(new GradientStop(Mix(cyan,blue,blend),.48));brush.GradientStops.Add(new GradientStop(Mix(blue,purple,blend),1));return brush;
        }
        void Digits(DrawingContext dc,string label,Rect bounds,Brush color,bool bolt)
        {
            double size=bounds.Height*.61,glyph=bounds.Height*.53,gap=bounds.Height*.055;
            double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;var text=new FormattedText(label,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),size,color,dpi);
            double group=text.Width+(bolt?glyph+gap:0),x=bounds.X+(bounds.Width-group)/2;
            if(group>bounds.Width-8){size*=Math.Max(.5,(bounds.Width-8)/group);text=new FormattedText(label,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),size,color,dpi);group=text.Width+(bolt?glyph+gap:0);x=bounds.X+(bounds.Width-group)/2;}
            if(bolt){Paint.Icon(dc,"bolt",color,new Point(x+glyph/2,bounds.Y+bounds.Height/2),glyph);x+=glyph+gap;}
            dc.DrawText(text,new Point(x,bounds.Y+(bounds.Height-text.Height)/2));
        }
        protected override void OnRender(DrawingContext dc)
        {
            if(ActualWidth<=0||ActualHeight<=0)return;
            double width=DrawWidth,height=ActualHeight+((Compact?24:32)-ActualHeight)*Morph;
            // The right edge stays attached to the real icon. Only the capsule geometry changes.
            var rect=new Rect(ActualWidth-width,(ActualHeight-height)/2,width,height);
            var outline=new RectangleGeometry(rect,height/2,height/2);
            if(!State.Known){int watts=PowerDisplay.Clamp(Preferences.Current.DesktopPowerWatts);if(watts>0){dc.DrawRoundedRectangle(Paint.Brush("#FF19221E"),null,rect,height/2,height/2);Digits(dc,watts+" W",rect,Paint.ControlWhite,false);}return;}
            double fillWidth=width*Math.Max(0,State.Level)/100*(Celebration?Reveal:1);
            string label=State.Level>=0?State.Level.ToString():"–";bool bolt=State.Connected||Celebration;
            bool low=IsLow(State,Celebration);dc.PushClip(outline);dc.DrawRectangle(Paint.Brush(Celebration?"#FF5F7470":"#667B8581"),null,rect);
            if(fillWidth>0)dc.DrawRectangle(Celebration?ChargeBrush():low?Paint.Brush("#FFB92835"):Paint.Brush("#FF19221E"),null,new Rect(rect.X,rect.Y,fillWidth,height));
            Digits(dc,label,rect,Paint.ControlWhite,bolt);
            dc.Pop();
        }
        internal static void Check(){foreach(int level in new[]{-1,0,1,19,20,21,100}){var state=new PowerState {Known=true,Level=level};if(IsLow(state)!=(level>=0&&level<=20))throw new InvalidOperationException("Low battery threshold");state.Connected=true;if(IsLow(state))throw new InvalidOperationException("AC must keep the charging presentation");state.Connected=false;state.Charging=true;if(IsLow(state))throw new InvalidOperationException("Charging must keep its existing icon");state.Charging=false;state.Known=false;if(IsLow(state))throw new InvalidOperationException("Unknown battery cannot be low");}if(IsLow(new PowerState {Known=true,Level=19},true))throw new InvalidOperationException("Charging animation cannot turn red");}
    }
    internal static class PowerDisplay
    {
        internal static int Clamp(int watts){return Math.Max(0,Math.Min(9999,watts));}
        internal static double Slot(PowerState state,int watts){return state.Known?58:Clamp(watts)>0?78:0;}
        internal static double DockSlot(PowerState state,int watts){return Preferences.Current.DuoStatusIcon?0:Slot(state,watts);}
    }
}
