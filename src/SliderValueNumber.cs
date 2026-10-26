using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed partial class PillSlider
    {
        bool observingValueOption;FormattedText numberBase,numberFill;string numberLabel;double numberSize,numberDpi;Brush numberBaseInk,numberFillInk;
        internal Brush ValueNumberInk;
        internal bool ValueNumberVisible {get{return Preferences.Current.SliderValueNumbers&&!WaveMode&&!Slim&&(Kind=="brightness"||Kind=="mute"||Kind.StartsWith("volume",StringComparison.Ordinal));}}
        internal string ValueNumberText {get{return IsEnabled?Math.Round(Value).ToString("0",CultureInfo.InvariantCulture):"–";}}
        void ObserveValueOption(bool observe)
        {
            if(observingValueOption==observe)return;observingValueOption=observe;if(observe)Preferences.Changed+=ValueOptionChanged;else Preferences.Changed-=ValueOptionChanged;InvalidateVisual();
        }
        void ValueOptionChanged(){InvalidateVisual();}
        bool SquareNumber(double width,double height){return ValueNumberVisible&&width<height*1.55&&height<width*1.55;}
        Point ValueIconCenter(double width,double height,Point original){return SquareNumber(width,height)?new Point(width/2,height*.32):original;}
        internal Rect ValueNumberBounds {get;private set;}
        void DrawValueNumber(DrawingContext dc,Geometry fill,Brush baseInk,Brush fillInk)
        {
            ValueNumberBounds=Rect.Empty;if(!ValueNumberVisible)return;double w=ActualWidth,h=ActualHeight,cross=Math.Min(w,h),size=Math.Max(10,Math.Min(16,cross*.25)),dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;string label=ValueNumberText;
            if(numberBase==null||numberLabel!=label||numberSize!=size||numberDpi!=dpi||numberBaseInk!=baseInk||numberFillInk!=fillInk){numberLabel=label;numberSize=size;numberDpi=dpi;numberBaseInk=baseInk;numberFillInk=fillInk;var typeface=new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);numberBase=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,typeface,size,baseInk,dpi);numberFill=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,typeface,size,fillInk,dpi);}
            Point center=SquareNumber(w,h)?new Point(w/2,h*.78):Vertical?new Point(w/2,Math.Max(numberBase.Height/2+6,cross*.45)):new Point(w-Math.Max(cross*.34,numberBase.Width/2+9),h/2);var origin=new Point(center.X-numberBase.Width/2,center.Y-numberBase.Height/2);ValueNumberBounds=new Rect(origin,new Size(numberBase.Width,numberBase.Height));
            dc.DrawText(numberBase,origin);if(fill!=null){dc.PushClip(fill);dc.DrawText(numberFill,origin);dc.Pop();}
        }
    }
}
