using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // The percentage interrupts the upper ring; the network and curved volume dots stay separate.
    internal sealed class DuoStatusGlyph : FrameworkElement
    {
        static readonly DependencyProperty ChargePhaseProperty=DependencyProperty.Register("ChargePhase",typeof(double),typeof(DuoStatusGlyph),new FrameworkPropertyMetadata(1.0,FrameworkPropertyMetadataOptions.AffectsRender));
        internal static readonly Point RingCenter=new Point(16,18);
        static readonly Geometry track=Gauge(1);
        static readonly Brush warningInk=Paint.Brush("#FFDA3345"),lightCharge=Paint.Brush("#FF15875A"),darkCharge=Paint.Brush("#FF74E5B0");
        int level=-1,steps,signal=-1;bool known,low,powered,chargeRunning;string network="wifi-off";Brush ink=Paint.ControlWhite;Geometry batteryFill=Geometry.Empty;
        FormattedText label;string labelText;Brush labelInk;double labelDpi;
        internal int VolumeDots {get{return steps;}}
        internal bool BatteryVisible {get{return known;}}
        internal bool ChargeAnimating {get{return chargeRunning;}}
        internal Brush BatteryColor {get{return powered?Glass95.DockDark?darkCharge:lightCharge:low?warningInk:ink;}}
        internal string BatteryNumberText {get{return !known?"":level<0?"–":Math.Min(100,level).ToString(CultureInfo.InvariantCulture);}}
        internal Rect BatteryNumberBounds {get;private set;}
        internal static Point VolumeDot(int index){return At(RingCenter,12.7,120-index*20);}
        internal static int Dots(double volume,bool muted,bool available){return !available||muted?0:(int)Math.Ceiling(PillSlider.Clamp(volume)/25);}
        internal DuoStatusGlyph(){Width=32;Height=34;IsHitTestVisible=false;Unloaded+=(s,e)=>StopPulse();IsVisibleChanged+=(s,e)=>{if(!IsVisible)StopPulse();};}
        internal void StopPulse(){chargeRunning=false;BeginAnimation(ChargePhaseProperty,null);SetValue(ChargePhaseProperty,1.0);}
        internal void ChargePulse()
        {
            if(!known||!Motion.Enabled||!Preferences.Current.ChargeAnimation||!Preferences.Current.DuoStatusIcon||!IsVisible)return;
            StopPulse();chargeRunning=true;var animation=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(2200)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};animation.Completed+=(s,e)=>StopPulse();BeginAnimation(ChargePhaseProperty,animation);
        }
        internal void Update(PowerState power,string kind,int wifiSignal,double volume,bool muted,bool available,Brush color)
        {
            int next=Dots(volume,muted,available);bool battery=power.Known,warning=BatteryPill.IsLow(power),connected=power.Connected||power.Charging;
            if(level==power.Level&&known==battery&&low==warning&&powered==connected&&network==kind&&steps==next&&signal==wifiSignal&&ink==color)return;
            if(!battery||powered&&!connected)StopPulse();level=power.Level;known=battery;low=warning;powered=connected;network=kind;steps=next;signal=wifiSignal;ink=color;batteryFill=Gauge(Math.Max(0,level)/100.0);InvalidateVisual();
        }
        static Point At(Point center,double radius,double degrees){double angle=degrees*Math.PI/180;return new Point(center.X+radius*Math.Cos(angle),center.Y+radius*Math.Sin(angle));}
        static void Segment(StreamGeometryContext drawing,double start,double sweep)
        {
            if(sweep<=0)return;drawing.BeginFigure(At(RingCenter,12.7,start),false,false);drawing.ArcTo(At(RingCenter,12.7,start+sweep),new Size(12.7,12.7),0,false,SweepDirection.Clockwise,true,false);
        }
        static Geometry Gauge(double value)
        {
            double sweep=Math.Max(0,Math.Min(1,value))*150;var path=new StreamGeometry();using(var drawing=path.Open()){Segment(drawing,150,Math.Min(75,sweep));Segment(drawing,315,Math.Max(0,sweep-75));}path.Freeze();return path;
        }
        static Pen Stroke(Brush color){var pen=new Pen(color,1.9){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();return pen;}
        void DrawNumber(DrawingContext dc,Brush color)
        {
            string text=BatteryNumberText;double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;if(label==null||labelText!=text||labelInk!=color||labelDpi!=dpi){labelText=text;labelInk=color;labelDpi=dpi;label=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,FontWeights.Bold,FontStretches.Normal),8.5,color,dpi);}
            var origin=new Point(16-label.Width/2,5.3-label.Height/2);BatteryNumberBounds=new Rect(origin,new Size(label.Width,label.Height));dc.DrawText(label,origin);
        }
        protected override void OnRender(DrawingContext dc)
        {
            if(ActualWidth<=0||ActualHeight<=0)return;double scale=Math.Min(ActualWidth/32,ActualHeight/34);dc.PushTransform(new ScaleTransform(scale,scale));var pen=Stroke(ink);BatteryNumberBounds=Rect.Empty;
            if(known){dc.PushOpacity(.22);dc.DrawGeometry(null,pen,track);dc.Pop();double phase=(double)GetValue(ChargePhaseProperty),growth=chargeRunning?Math.Min(1,phase/.75):1;var fill=chargeRunning?Gauge(Math.Max(0,level)/100.0*growth):batteryFill;dc.DrawGeometry(null,Stroke(BatteryColor),fill);
                if(chargeRunning){var accent=Glass95.DockDark?darkCharge:lightCharge;dc.PushOpacity(Math.Min(1,(1-phase)/.25));dc.DrawGeometry(null,Stroke(accent),fill);dc.Pop();dc.PushOpacity(.7+.3*Math.Sin(Math.PI*phase));Paint.Icon(dc,"bolt",accent,new Point(16,5.3),9.5);dc.Pop();}else DrawNumber(dc,low?warningInk:ink);}
            Paint.Icon(dc,network,ink,RingCenter,14,0,signal,-1);
            for(int i=0;i<4;i++){if(i>=steps)dc.PushOpacity(.24);dc.DrawEllipse(ink,null,VolumeDot(i),1.5,1.5);if(i>=steps)dc.Pop();}dc.Pop();
        }
    }
}
