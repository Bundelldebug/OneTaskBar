using System;
using System.Windows;
using System.Windows.Media;
namespace OneTaskBar
{
    internal sealed partial class PillSlider
    {
        internal bool GlassStandalone=true;
        internal bool ContrastTrack;
        // Keep the existing capsule geometry, input mapping and icon positions.
        void DrawGlass(DrawingContext dc,double width,double height)
        {
            double cross=Vertical?width:height,axis=Vertical?height:width,shown=(double)GetValue(DisplayValueProperty);
            var track=new Rect(0,0,width,height);bool capsule=Tablet&&ShowIcon&&!LinearFill;
            var logical=capsule?CapsuleFillBounds(axis,cross,shown):new Rect(0,0,FillWidth(axis,shown),cross);
            var filled=Vertical?new Rect(logical.Y,height-logical.Right,logical.Height,logical.Width):logical;
            var fill=new RectangleGeometry(filled,Math.Min(filled.Width,filled.Height)/2,Math.Min(filled.Width,filled.Height)/2);
            dc.DrawRoundedRectangle(ContrastTrack?Paint.Brush("#20FFFFFF"):Glass95.Transparent?Glass95.Card:Glass95.Solid,null,track,cross/2,cross/2);
            bool clipped=LinearFill;if(clipped)dc.PushClip(new RectangleGeometry(track,cross/2,cross/2));
            if(shown>0)dc.DrawGeometry(Paint.ControlWhite,null,fill);
            if(GlassStandalone)GlassOptics.Draw(dc,width,height,cross/2,false);
            string kind=Kind.StartsWith("volume",StringComparison.Ordinal)?SoundIcon.For(value,false,IsEnabled):Kind;
            var center=ValueIconCenter(width,height,Vertical?new Point(width/2,height-cross*.5):new Point(cross*.5,height/2));
            if(ShowIcon){DrawIcon(dc,kind,ContrastTrack?Paint.ControlWhite:Glass95.Text,center,cross*.44);if(shown>0){dc.PushClip(fill);DrawIcon(dc,kind,Paint.Brush("#FF35413B"),center,cross*.44);dc.Pop();}}
            if(clipped)dc.Pop();
            DrawValueNumber(dc,fill,ValueNumberInk??(ContrastTrack?Paint.ControlWhite:Glass95.Text),Paint.OnLight);
            if(IsKeyboardFocused)dc.DrawRoundedRectangle(null,new Pen(Glass95.Accent,1.5),new Rect(1,1,Math.Max(0,width-2),Math.Max(0,height-2)),cross/2,cross/2);
        }
    }
}
