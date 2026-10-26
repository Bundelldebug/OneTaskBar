using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // One vector icon: battery arc, network in the center, four volume dots below.
    internal sealed class DuoStatusGlyph : FrameworkElement
    {
        int level=-1,steps,signal=-1;bool known,low;string network="wifi-off";Brush ink=Paint.ControlWhite;
        internal int VolumeDots {get{return steps;}}
        internal bool BatteryVisible {get{return known;}}
        internal static Point VolumeDot(int index){return At(new Point(16,14.5),12.7,120-index*20);}
        internal static int Dots(double volume,bool muted,bool available){return !available||muted?0:(int)Math.Ceiling(PillSlider.Clamp(volume)/25);}
        internal DuoStatusGlyph(){Width=32;Height=34;IsHitTestVisible=false;}
        internal void StopPulse(){BeginAnimation(OpacityProperty,null);Opacity=1;}
        internal void ChargePulse(){if(!Motion.Enabled||!IsVisible)return;BeginAnimation(OpacityProperty,new DoubleAnimation(.45,1,TimeSpan.FromMilliseconds(220)){AutoReverse=true,RepeatBehavior=new RepeatBehavior(3),FillBehavior=FillBehavior.Stop});}
        internal void Update(PowerState power,string kind,int wifiSignal,double volume,bool muted,bool available,Brush color)
        {
            int next=Dots(volume,muted,available);bool battery=power.Known,warning=BatteryPill.IsLow(power);
            if(level==power.Level&&known==battery&&low==warning&&network==kind&&steps==next&&signal==wifiSignal&&ink==color)return;
            level=power.Level;known=battery;low=warning;network=kind;steps=next;signal=wifiSignal;ink=color;InvalidateVisual();
        }
        static Point At(Point center,double radius,double degrees){double angle=degrees*Math.PI/180;return new Point(center.X+radius*Math.Cos(angle),center.Y+radius*Math.Sin(angle));}
        static Geometry Arc(Point center,double radius,double sweep)
        {
            var path=new StreamGeometry();if(sweep>0)using(var drawing=path.Open()){drawing.BeginFigure(At(center,radius,150),false,false);drawing.ArcTo(At(center,radius,150+sweep),new Size(radius,radius),0,sweep>180,SweepDirection.Clockwise,true,false);}path.Freeze();return path;
        }
        protected override void OnRender(DrawingContext dc)
        {
            if(ActualWidth<=0||ActualHeight<=0)return;double scale=Math.Min(ActualWidth/32,ActualHeight/34);dc.PushTransform(new ScaleTransform(scale,scale));var center=new Point(16,14.5);var pen=new Pen(ink,1.9){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();
            if(known){dc.PushOpacity(.22);dc.DrawGeometry(null,pen,Arc(center,12.7,240));dc.Pop();var fill=low?new Pen(Paint.Brush("#FFDA3345"),1.9){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round}:pen;dc.DrawGeometry(null,fill,Arc(center,12.7,240*Math.Max(0,Math.Min(100,level))/100.0));}
            Paint.Icon(dc,network,ink,new Point(16,14.5),15,0,signal,-1);
            for(int i=0;i<4;i++){if(i>=steps)dc.PushOpacity(.24);dc.DrawEllipse(ink,null,VolumeDot(i),1.5,1.5);if(i>=steps)dc.Pop();}dc.Pop();
        }
    }
}
