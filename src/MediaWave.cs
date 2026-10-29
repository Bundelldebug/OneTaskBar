using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // Cubic segments preserve the tangent at the elapsed track and between peaks.
    internal static class MediaWave
    {
        static double Height(double t,double phase,int layer,out double slope)
        {
            double angle=Math.PI*t,frequency=Math.PI*(4.2+layer*.6);
            double oscillation=frequency*t-phase*(1+layer*.12)+layer*1.8;
            double envelope=Math.Sin(angle),level=.71+.29*Math.Sin(oscillation);
            slope=Math.PI*Math.Sin(2*angle)*level+envelope*envelope*.29*frequency*Math.Cos(oscillation);
            return envelope*envelope*level;
        }
        internal static StreamGeometry Shape(double length,double baseline,double amplitude,double phase,int layer)
        {
            length=Math.Max(0,length);amplitude=Math.Max(0,amplitude);
            int samples=Math.Max(16,Math.Min(64,(int)Math.Ceiling(length/20)));
            double step=1.0/samples,previousSlope;
            var previous=new Point(8,baseline);Height(0,phase,layer,out previousSlope);
            var shape=new StreamGeometry();
            using(var dc=shape.Open())
            {
                dc.BeginFigure(previous,true,true);
                for(int i=1;i<=samples;i++)
                {
                    double t=i*step,slope,level=Height(t,phase,layer,out slope);
                    var next=new Point(8+length*t,baseline-amplitude*level);
                    var first=new Point(previous.X+length*step/3,previous.Y-amplitude*previousSlope*step/3);
                    var second=new Point(next.X-length*step/3,next.Y+amplitude*slope*step/3);
                    first.Y=Math.Max(baseline-amplitude,Math.Min(baseline,first.Y));
                    second.Y=Math.Max(baseline-amplitude,Math.Min(baseline,second.Y));
                    dc.BezierTo(first,second,next,true,true);previous=next;previousSlope=slope;
                }
            }
            shape.Freeze();return shape;
        }
        internal static void Check()
        {
            foreach(double length in new[]{0.0,1,90,480,4000})foreach(double phase in new[]{0.0,1,12})for(int layer=0;layer<3;layer++)
            {
                var shape=Shape(length,24,16,phase,layer);var bounds=shape.Bounds;
                if(!shape.IsFrozen||bounds.Left<7.99||bounds.Right>8+length+.01||bounds.Top<7.99||bounds.Bottom>24.01)throw new InvalidOperationException("Playback wave escapes the elapsed track");
            }
        }
    }
    internal sealed partial class PillSlider
    {
        bool waveMode,wavePlaying,waveHooked,waveObserved;
        readonly Stopwatch waveClock=new Stopwatch();
        double waveStrength,waveFrom,waveTarget,waveAt,lastWaveFrame=-1;
        TimeSpan waveRendering=TimeSpan.MinValue;
        static readonly Brush waveTrack=Paint.Brush("#608E9695");
        Brush waveAccent=Paint.Brush("#FFE124E9");
        Brush[] waves={Paint.Brush("#70F9AEFF"),Paint.Brush("#90FAEEFF"),Paint.Brush("#90E124E9")},glow={Paint.Brush("#0FE124E9"),Paint.Brush("#24E124E9"),Paint.Brush("#40E124E9")};
        Color waveColor=Color.FromRgb(225,36,233);
        static Brush WaveBrush(Color color,byte alpha){var brush=new SolidColorBrush(Color.FromArgb(alpha,color.R,color.G,color.B));brush.Freeze();return brush;}
        internal Color WaveColor {get{return waveColor;}set{if(waveColor==value)return;waveColor=value;waveAccent=WaveBrush(value,255);var light=Color.FromRgb((byte)((value.R+255)/2),(byte)((value.G+255)/2),(byte)((value.B+255)/2));waves=new[]{WaveBrush(light,112),WaveBrush(Colors.White,144),WaveBrush(value,144)};glow=new[]{WaveBrush(value,15),WaveBrush(value,36),WaveBrush(value,64)};InvalidateVisual();}}
        internal Func<double> WaveProgress;
        internal bool WaveMode
        {
            get{return waveMode;}
            set
            {
                if(waveMode==value)return;waveMode=value;
                if(!value)SetValue(DisplayValueProperty,this.value);
                if(!waveObserved){waveObserved=true;IsVisibleChanged+=(s,e)=>UpdateWaveClock();Loaded+=(s,e)=>UpdateWaveClock();Unloaded+=(s,e)=>StopWave();}
                UpdateWaveClock();InvalidateVisual();
            }
        }
        internal bool WavePlaying {get{return wavePlaying;}set{bool changed=wavePlaying!=value;wavePlaying=value;UpdateWaveClock();if(changed)InvalidateVisual();}}
        internal bool WaveAnimating {get{return waveHooked;}}
        internal double WaveStrength {get{return waveStrength;}}
        internal int WaveFrames {get;private set;}
        void StopWave()
        {
            if(waveHooked)CompositionTarget.Rendering-=WaveFrame;
            waveHooked=false;waveClock.Stop();lastWaveFrame=-1;waveRendering=TimeSpan.MinValue;waveStrength=waveFrom=waveTarget=0;
        }
        void UpdateWaveClock()
        {
            if(!waveMode||!IsVisible||!Motion.Enabled){StopWave();return;}
            double target=wavePlaying?1:0;
            if(target!=waveTarget){waveFrom=waveStrength;waveTarget=target;waveAt=waveClock.Elapsed.TotalSeconds;}
            if(!wavePlaying&&waveStrength<=.001){StopWave();return;}
            if(!waveHooked){waveClock.Start();waveHooked=true;CompositionTarget.Rendering+=WaveFrame;}
        }
        void WaveFrame(object sender,EventArgs args)
        {
            if(!waveMode||!IsVisible||!Motion.Enabled){StopWave();return;}
            var frame=args as RenderingEventArgs;if(frame!=null){if(frame.RenderingTime==waveRendering)return;waveRendering=frame.RenderingTime;}
            double now=waveClock.Elapsed.TotalSeconds;
            if(now==lastWaveFrame)return;lastWaveFrame=now;
            double t=Math.Min(1,(now-waveAt)/.26),ease=t*t*(3-2*t);
            waveStrength=waveFrom+(waveTarget-waveFrom)*ease;
            if(!dragging&&WaveProgress!=null)Value=WaveProgress();
            WaveFrames++;InvalidateVisual();
            if(!wavePlaying&&t>=1)StopWave();
        }
        void DrawWave(DrawingContext dc,double width,double height)
        {
            double y=Math.Max(6,height-7),span=Math.Max(0,width-16),length=span*Value/100,x=8+length;
            dc.PushClip(new RectangleGeometry(new Rect(0,0,width,height)));
            dc.DrawRoundedRectangle(waveTrack,null,new Rect(8,y-1.6,span,3.2),1.6,1.6);
            if(waveStrength>.001&&Motion.Enabled&&length>0){double phase=waveClock.Elapsed.TotalSeconds*2.7;for(int layer=0;layer<3;layer++)dc.DrawGeometry(waves[layer],null,MediaWave.Shape(length,y,Math.Max(0,y-3)*(1-layer*.14)*waveStrength,phase,layer));}
            if(length>0)dc.DrawRoundedRectangle(waveAccent,null,new Rect(8,y-1.8,length,3.6),1.8,1.8);
            if(IsEnabled){for(int i=0;i<3;i++)dc.DrawEllipse(glow[i],null,new Point(x,y),10-i*2,10-i*2);dc.DrawEllipse(Paint.ControlWhite,null,new Point(x,y),6,6);dc.DrawEllipse(waveAccent,null,new Point(x,y),4.4,4.4);}
            if(IsKeyboardFocused)dc.DrawRoundedRectangle(null,new Pen(Paint.ControlWhite,1),new Rect(.5,.5,Math.Max(0,width-1),Math.Max(0,height-1)),6,6);
            dc.Pop();
        }
    }
}
