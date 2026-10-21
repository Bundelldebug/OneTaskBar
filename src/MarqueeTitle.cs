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
        string text="";FormattedText formatted;bool running;readonly Stopwatch clock=new Stopwatch();
        readonly DispatcherTimer timer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(33)};
        internal string Text {get{return text;}set{value=value??"";if(text==value)return;text=value;formatted=null;clock.Restart();System.Windows.Automation.AutomationProperties.SetName(this,text);InvalidateMeasure();InvalidateVisual();UpdateTimer();}}
        internal bool Running {get{return running;}set{if(running==value)return;running=value;UpdateTimer();}}
        FormattedText Formatted {get{if(formatted==null)formatted=new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),12,Paint.ControlWhite,1.0);return formatted;}}
        internal double NaturalWidth {get{return Formatted.WidthIncludingTrailingWhitespace;}}
        internal double Offset {get{double distance=Math.Max(0,NaturalWidth-ActualWidth);if(distance<=0||!timer.IsEnabled)return 0;double travel=distance/25.0,phase=clock.Elapsed.TotalSeconds%(2.4+travel*2);return phase<1.2?0:phase<1.2+travel?(phase-1.2)*25:phase<2.4+travel?distance:Math.Max(0,distance-(phase-2.4-travel)*25);}}
        internal MarqueeTitle(){Height=22;ClipToBounds=true;IsHitTestVisible=false;timer.Tick+=(s,e)=>InvalidateVisual();SizeChanged+=(s,e)=> {clock.Restart();UpdateTimer();};IsVisibleChanged+=(s,e)=>UpdateTimer();Unloaded+=(s,e)=>timer.Stop();}
        void UpdateTimer(){bool enabled=running&&IsVisible&&ActualWidth>1&&NaturalWidth>ActualWidth+.5&&Motion.Enabled;if(enabled){if(!timer.IsEnabled){clock.Restart();timer.Start();}}else{timer.Stop();clock.Reset();}InvalidateVisual();}
        protected override Size MeasureOverride(Size constraint){return new Size(Math.Min(NaturalWidth,Double.IsInfinity(constraint.Width)?NaturalWidth:constraint.Width),22);}
        protected override void OnRender(DrawingContext dc){dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));dc.DrawText(Formatted,new Point(-Offset,Math.Max(0,(ActualHeight-Formatted.Height)/2)));dc.Pop();}
    }
}
