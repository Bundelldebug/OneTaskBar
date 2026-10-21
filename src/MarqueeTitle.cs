using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal sealed class MarqueeTitle : FrameworkElement
    {
        string text="";FormattedText formatted;bool running,conservePower,engaged,completed;readonly Stopwatch clock=new Stopwatch();
        double lastOffset=Double.NaN;
        readonly DispatcherTimer timer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(33)};
        internal string Text {get{return text;}set{value=value??"";if(text==value)return;text=value;formatted=null;completed=false;clock.Restart();System.Windows.Automation.AutomationProperties.SetName(this,text);InvalidateMeasure();InvalidateVisual();UpdateTimer();}}
        internal bool Running {get{return running;}set{if(running==value)return;running=value;UpdateTimer();}}
        internal bool ConservePower {get{return conservePower;}set{if(conservePower==value)return;conservePower=value;completed=false;UpdateTimer();}}
        internal bool Engaged {get{return engaged;}set{if(engaged==value)return;engaged=value;if(value)completed=false;UpdateTimer();}}
        internal bool IsAnimating {get{return timer.IsEnabled;}}
        internal int RenderCount {get;private set;}
        double Distance {get{return Math.Max(0,NaturalWidth-ActualWidth);}}
        double Cycle {get{return 2.4+Distance/25.0*2;}}
        FormattedText Formatted {get{if(formatted==null)formatted=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),12,Paint.ControlWhite,1.0);return formatted;}}
        internal double NaturalWidth {get{return Formatted.WidthIncludingTrailingWhitespace;}}
        internal double Offset {get{double distance=Distance;if(distance<=0||!timer.IsEnabled)return 0;double travel=distance/25.0,phase=clock.Elapsed.TotalSeconds%Cycle;return phase<1.2?0:phase<1.2+travel?(phase-1.2)*25:phase<2.4+travel?distance:Math.Max(0,distance-(phase-2.4-travel)*25);}}
        internal MarqueeTitle(){Height=22;ClipToBounds=true;IsHitTestVisible=false;timer.Tick+=(s,e)=>Frame();SizeChanged+=(s,e)=> {completed=false;clock.Restart();UpdateTimer();};IsVisibleChanged+=(s,e)=>UpdateTimer();Unloaded+=(s,e)=>timer.Stop();}
        void UpdateTimer(){bool enabled=running&&IsVisible&&ActualWidth>1&&Distance>.5&&Motion.Enabled&&(!conservePower||engaged||!completed);if(enabled){if(!timer.IsEnabled){clock.Restart();timer.Interval=TimeSpan.FromSeconds(1.2);timer.Start();}}else{timer.Stop();clock.Reset();}lastOffset=Double.NaN;InvalidateVisual();}
        void Frame()
        {
            if(conservePower&&!engaged&&clock.Elapsed.TotalSeconds>=Cycle){completed=true;UpdateTimer();return;}
            double offset=Offset;if(Double.IsNaN(lastOffset)||Math.Abs(offset-lastOffset)>.01){lastOffset=offset;InvalidateVisual();}
            double travel=Distance/25.0,phase=clock.Elapsed.TotalSeconds%Cycle,delay=conservePower?50:33;
            // The title is stationary at either end: sleep through those pauses.
            if(phase<1.2)delay=Math.Max(delay,(1.2-phase)*1000);
            else if(phase>=1.2+travel&&phase<2.4+travel)delay=Math.Max(delay,(2.4+travel-phase)*1000);
            timer.Interval=TimeSpan.FromMilliseconds(delay);
        }
        protected override Size MeasureOverride(Size constraint){return new Size(Math.Min(NaturalWidth,Double.IsInfinity(constraint.Width)?NaturalWidth:constraint.Width),22);}
        protected override void OnRender(DrawingContext dc){RenderCount++;dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));dc.DrawText(Formatted,new Point(-Offset,Math.Max(0,(ActualHeight-Formatted.Height)/2)));dc.Pop();}
    }
}
