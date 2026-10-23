using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace OneTaskBar
{
    internal sealed class ScanIndicator : FrameworkElement
    {
        static readonly Geometry shape=CreateShape();static readonly Pen stroke=CreatePen();
        static Geometry CreateShape(){var shape=Geometry.Parse("M6,6 A8,8 0 1 1 4,15 M6,2 L6,7 11,7");shape.Freeze();return shape;}
        static Pen CreatePen(){var pen=new Pen(Paint.ControlWhite,1.8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();return pen;}
        readonly RotateTransform rotation=new RotateTransform();bool requested,spinning;
        internal bool Spinning {get{return spinning;}}
        internal double Angle {get{return rotation.Angle;}}
        internal ScanIndicator(){Width=Height=20;RenderTransformOrigin=new Point(.5,.5);RenderTransform=rotation;IsVisibleChanged+=(s,e)=>Apply();Unloaded+=(s,e)=>Stop();}
        internal void SetScanning(bool value){requested=value;Apply();}
        void Stop(){rotation.BeginAnimation(RotateTransform.AngleProperty,null);rotation.Angle=0;spinning=false;}
        void Apply()
        {
            bool next=requested&&IsVisible&&Motion.Enabled;if(next==spinning)return;Stop();if(!next)return;spinning=true;
            rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,360,TimeSpan.FromMilliseconds(850)){RepeatBehavior=RepeatBehavior.Forever});
        }
        protected override void OnRender(DrawingContext context)
        {
            context.PushTransform(new ScaleTransform(ActualWidth/24,ActualHeight/24));context.DrawGeometry(null,stroke,shape);context.Pop();
        }
    }
}
