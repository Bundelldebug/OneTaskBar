using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    // A title that scrolls when it is wider than its slot. The text is drawn once into a child visual; scrolling only
    // moves that visual's transform on every display frame, so no text is laid out or redrawn while it moves.
    internal sealed class MarqueeTitle : FrameworkElement
    {
        string text="";FormattedText formatted;bool running,conservePower,engaged,completed,active;readonly Stopwatch clock=new Stopwatch();
        double lastOffset=Double.NaN;
        double textSize=12;bool centered;FontWeight weight=FontWeights.SemiBold;
        readonly DrawingVisual body=new DrawingVisual();readonly TranslateTransform shift=new TranslateTransform();
        bool bodyDrawn;
        internal double TextSize {get{return textSize;}set{if(textSize==value)return;textSize=value;ResetFormat();}}
        internal bool Centered {get{return centered;}set{if(centered==value)return;centered=value;Place();}}
        internal FontWeight TextWeight {get{return weight;}set{if(weight==value)return;weight=value;ResetFormat();}}
        void ResetFormat(){formatted=null;completed=false;bodyDrawn=false;InvalidateMeasure();UpdateTimer();}
        internal double TextOrigin {get{return centered&&NaturalWidth<=ActualWidth?Math.Max(0,(ActualWidth-NaturalWidth)/2):-Offset;}}
        readonly DispatcherTimer timer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(33)};
        internal string Text {get{return text;}set{value=value??"";if(text==value)return;text=value;formatted=null;bodyDrawn=false;completed=false;clock.Restart();System.Windows.Automation.AutomationProperties.SetName(this,text);InvalidateMeasure();UpdateTimer();}}
        internal bool Running {get{return running;}set{if(running==value){if(active&&!Motion.Enabled)UpdateTimer();return;}running=value;UpdateTimer();}}
        internal bool ConservePower {get{return conservePower;}set{if(conservePower==value)return;conservePower=value;completed=false;UpdateTimer();}}
        internal bool Engaged {get{return engaged;}set{if(engaged==value)return;engaged=value;if(value)completed=false;UpdateTimer();}}
        internal bool IsAnimating {get{return active;}}
        // Counts each time the title is drawn or moved.
        internal int RenderCount {get;private set;}
        double Distance {get{return Math.Max(0,NaturalWidth-ActualWidth);}}
        double Cycle {get{return 2.4+Distance/25.0*2;}}
        FormattedText Formatted {get{if(formatted==null)formatted=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,weight,FontStretches.Normal),textSize,Paint.ControlWhite,VisualTreeHelper.GetDpi(this).PixelsPerDip);return formatted;}}
        internal double NaturalWidth {get{return Formatted.WidthIncludingTrailingWhitespace;}}
        internal double LineHeight {get{return Math.Ceiling(Formatted.Height)+2;}}
        internal double Offset {get{double distance=Distance;if(distance<=0||!active)return 0;double travel=distance/25.0,phase=clock.Elapsed.TotalSeconds%Cycle;return phase<1.2?0:phase<1.2+travel?(phase-1.2)*25:phase<2.4+travel?distance:Math.Max(0,distance-(phase-2.4-travel)*25);}}
        internal MarqueeTitle()
        {
            Height=22;ClipToBounds=true;IsHitTestVisible=false;
            body.Transform=shift;AddVisualChild(body);
            timer.Tick+=(s,e)=>Frame();
            SizeChanged+=(s,e)=> {completed=false;clock.Restart();UpdateTimer();};
            IsVisibleChanged+=(s,e)=>UpdateTimer();
            Unloaded+=(s,e)=>Stop();
        }
        protected override int VisualChildrenCount {get{return 1;}}
        protected override Visual GetVisualChild(int index){if(index!=0)throw new ArgumentOutOfRangeException("index");return body;}
        void Stop(){timer.Stop();Unhook();active=false;clock.Reset();}
        void UpdateTimer()
        {
            bool enabled=running&&IsVisible&&ActualWidth>1&&Distance>.5&&Motion.Enabled&&(!conservePower||engaged||!completed);
            if(enabled){if(!active){clock.Restart();active=true;}Frame();}else Stop();
            lastOffset=Double.NaN;Place();
        }
        // Draws the text into its own visual when the text or font changed, then positions it.
        void Place()
        {
            if(!bodyDrawn)
            {
                using(var dc=body.RenderOpen())dc.DrawText(Formatted,new Point(0,0));
                bodyDrawn=true;
            }
            shift.X=TextOrigin;
            shift.Y=Math.Max(0,(ActualHeight-Formatted.Height)/2);
            RenderCount++;
        }
        void Frame()
        {
            if(!running||!IsVisible||!Motion.Enabled){Stop();Place();return;}
            if(conservePower&&!engaged&&clock.Elapsed.TotalSeconds>=Cycle){completed=true;UpdateTimer();return;}
            double offset=Offset;if(Double.IsNaN(lastOffset)||Math.Abs(offset-lastOffset)>.01){lastOffset=offset;Place();}
            double travel=Distance/25.0,phase=clock.Elapsed.TotalSeconds%Cycle,delay=0;
            // The title is stationary at either end: sleep through those pauses.
            if(phase<1.2)delay=(1.2-phase)*1000;
            else if(phase>=1.2+travel&&phase<2.4+travel)delay=(2.4+travel-phase)*1000;
            if(delay>0){Unhook();timer.Interval=TimeSpan.FromMilliseconds(Math.Max(1,delay));if(!timer.IsEnabled)timer.Start();return;}
            // While the title moves it follows the display's own frames; a timer drifts against them and the scroll stutters.
            timer.Stop();
            if(!hooked){hooked=true;lastRendering=TimeSpan.MinValue;CompositionTarget.Rendering+=Rendering;}
        }
        bool hooked;TimeSpan lastRendering;
        void Unhook(){if(hooked){CompositionTarget.Rendering-=Rendering;hooked=false;}}
        void Rendering(object sender,EventArgs args)
        {
            var frame=args as RenderingEventArgs;
            // Sixty updates a second everywhere: a 120 Hz display skips every other frame, a 60 Hz one uses all of them.
            if(frame!=null){if(frame.RenderingTime==lastRendering||lastRendering!=TimeSpan.MinValue&&(frame.RenderingTime-lastRendering).TotalMilliseconds<14)return;lastRendering=frame.RenderingTime;}
            Frame();
        }
        protected override Size MeasureOverride(Size constraint){return new Size(Math.Min(NaturalWidth,Double.IsInfinity(constraint.Width)?NaturalWidth:constraint.Width),Math.Min(Math.Max(22,Formatted.Height),constraint.Height));}
        protected override Size ArrangeOverride(Size size){Place();return size;}
    }
}
