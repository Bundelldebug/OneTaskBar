using System;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // A small code-drawn illustration: no desktop capture, image decode or live blur host.
    internal sealed class UIVersionPreview : FrameworkElement
    {
        int version;
        internal int Version
        {
            get{return version;}
            set{if(version==value)return;version=value;InvalidateVisual();}
        }
        internal UIVersionPreview(){Width=130;Height=132;IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext dc)
        {
            bool glass=version>=95,modern=version>=90;
            var background=new Rect(0,0,130,132);
            dc.DrawRoundedRectangle(Paint.Brush(glass?"#FF456A7A":"#FF454B50"),null,background,16,16);
            if(glass)
            {
                var glow=new LinearGradientBrush(Colors.Teal,Colors.CornflowerBlue,35);glow.Freeze();
                dc.DrawRoundedRectangle(glow,null,new Rect(4,4,122,124),13,13);
            }
            Brush card=Paint.Brush(glass?"#589BA6B1":"#747E878C");
            Pen rim=glass?new Pen(Paint.Brush("#78FFFFFF"),.7):null;
            foreach(double x in new[]{9.0,69})
            {
                dc.DrawRoundedRectangle(card,rim,new Rect(x,11,52,24),12,12);
                dc.DrawEllipse(Paint.ControlWhite,null,new Point(x+12,23),8,8);
                dc.DrawRoundedRectangle(Paint.Brush("#DAFFFFFF"),null,new Rect(x+25,20,18,3),1.5,1.5);
            }
            dc.DrawRoundedRectangle(card,rim,new Rect(9,41,112,25),12,12);
            for(int i=0;i<4;i++)dc.DrawEllipse(Paint.Brush("#70FFFFFF"),null,new Point(25+i*27,53.5),7.5,7.5);
            for(int i=0;i<2;i++)
            {
                double top=72+i*21;
                dc.DrawRoundedRectangle(card,rim,new Rect(9,top,modern?88:112,16),8,8);
                dc.DrawRoundedRectangle(Paint.ControlWhite,null,new Rect(9,top,i==0?58:31,16),8,8);
                if(modern)dc.DrawEllipse(Paint.Brush("#90FFFFFF"),rim,new Point(113,top+8),8,8);
                else dc.DrawEllipse(Paint.Brush("#90FFFFFF"),null,new Point(110,top+8),5,5);
            }
            dc.DrawRoundedRectangle(card,rim,new Rect(9,114,112,9),4.5,4.5);
        }
    }
}
