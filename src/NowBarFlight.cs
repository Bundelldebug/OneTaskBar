using System;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed partial class NowBar
    {
        FlightHandoff flight;
        internal bool FlightMotion {get{return flight!=null;}}
        internal Rect FlightShape {get{return flight==null?current:flight.Shape;}}

        internal FlightHandoff ReceiveFlight(Rect source)
        {
            if(closed||!wanted||!IsVisible||expanded||animating||!Motion.Enabled)return null;
            if(flight!=null)flight.Dispose();
            flight=new FlightHandoff(this,source);
            return flight;
        }

        internal sealed class FlightHandoff : IDisposable
        {
            readonly NowBar bar;
            readonly Rect resting,source;
            readonly Transform previous;
            readonly ScaleTransform stretch=new ScaleTransform();
            readonly TranslateTransform offset=new TranslateTransform();
            bool disposed;
            internal Rect Shape {get;private set;}

            internal FlightHandoff(NowBar owner,Rect incoming)
            {
                bar=owner;
                resting=owner.current;
                Shape=resting;
                source=new Rect((incoming.X-owner.screen.Bounds.X)/owner.monitorScale,
                    (incoming.Y-owner.screen.Bounds.Y)/owner.monitorScale,
                    incoming.Width/owner.monitorScale,incoming.Height/owner.monitorScale);
                previous=owner.Surface.RenderTransform;
                var transform=new TransformGroup();
                if(previous!=null)transform.Children.Add(previous);
                stretch.CenterX=resting.Width/2;
                stretch.CenterY=resting.Height/2;
                transform.Children.Add(stretch);
                transform.Children.Add(offset);
                owner.Surface.RenderTransform=transform;
                owner.title.Running=false;
                Native.BeginBackdropMotion(owner);
                Apply(0);
            }

            internal void Apply(double progress)
            {
                if(disposed)return;
                if(bar.closed||!bar.IsVisible||bar.expanded||bar.animating)
                {
                    Dispose();
                    return;
                }
                Shape=MediaCapsulePose.At(resting,source,progress);
                stretch.ScaleX=Shape.Width/resting.Width;
                stretch.ScaleY=Shape.Height/resting.Height;
                offset.X=Shape.X-resting.X-(resting.Width-Shape.Width)/2;
                offset.Y=Shape.Y-resting.Y-(resting.Height-Shape.Height)/2;
                bar.BackdropClip=new Rect(Shape.X-bar.hostBox.X,Shape.Y-bar.hostBox.Y,Shape.Width,Shape.Height);
                Native.TransformBackdropMotion(bar,new Rect(resting.X-bar.hostBox.X,resting.Y-bar.hostBox.Y,
                    resting.Width,resting.Height),bar.BackdropClip.Value,bar.monitorScale);
            }

            public void Dispose()
            {
                if(disposed)return;
                disposed=true;
                bar.Surface.RenderTransform=previous;
                if(bar.flight==this)bar.flight=null;
                if(!bar.closed)
                {
                    bar.BackdropClip=new Rect(bar.current.X-bar.hostBox.X,bar.current.Y-bar.hostBox.Y,
                        bar.current.Width,bar.current.Height);
                    Native.EndBackdropMotion(bar,bar.Radius);
                    bar.title.Running=bar.wanted&&!bar.expanded&&!bar.animating&&Motion.Enabled;
                }
            }
        }
    }
}
