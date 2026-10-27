using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Threading.Tasks;

namespace OneTaskBar
{
    internal static class QuickPanel
    {
        // Black tint over the live host backdrop keeps background color without a milky layer.
        static readonly Brush legacyGlass=LightPanel.Tint("#A0000000","#94000000","#9E000000"),legacyCard=Paint.Brush("#18FFFFFF");
        internal static Brush Glass {get{return PanelMaterial.PanelSurface(Glass95.Dark,Glass95.Enabled);}}
        internal static Brush Card {get{return Glass95.Enabled?Glass95.Card:PanelMaterial.Card(Glass95.Dark);}}
        internal const string Inactive="#2A000000";
    }
    internal static class GrayPanel
    {
        // Neutral frosted glass retains live background color with readable dark content.
        static readonly Brush legacyGlass=LightPanel.Tint("#8F858E87","#75747F77","#887F8981"),legacyCard=Paint.Brush("#55ADB5AE");
        internal static Brush Glass {get{return PanelMaterial.PanelSurface(Glass95.Dark,Glass95.Enabled);}}
        internal static Brush Card {get{return QuickPanel.Card;}}
        internal static Brush Muted {get{return LightPanel.Muted;}}
        internal static void Apply(DependencyObject root)
        {
            LightPanel.Apply(root);
        }
    }
    internal static class LightPanel
    {
        static readonly Brush legacyGlass=Tint("#9BE5EBE7","#7BC5D0C9","#8FD8E2DB"),legacyDock=Tint("#AADFE7E2","#82C3D0C7","#96D5E1D9"),legacyMuted=Paint.Brush("#FF4C5B52"),legacyCard=Paint.Brush("#24FFFFFF");
        internal static readonly Brush DockSolid=Paint.Brush("#FFBAC1C7");
        internal static Brush Glass {get{return PanelMaterial.PanelSurface(Glass95.Dark,Glass95.Enabled);}}
        internal static Brush DockGlass {get{return PanelMaterial.Surface(Glass95.DockDark,Glass95.Enabled);}}
        internal static Brush Muted {get{return Paint.Muted;}}
        internal static Brush Card {get{return QuickPanel.Card;}}
        internal static Brush Tint(string top,string middle,string bottom)
        {
            var brush=new LinearGradientBrush();brush.StartPoint=new Point(0,0);brush.EndPoint=new Point(0,1);
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(top),0));brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(middle),.42));brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(bottom),1));brush.Freeze();return brush;
        }
        internal static void Apply(DependencyObject root)
        {
            if(!Glass95.Enabled){var label=root as TextBlock;if(label!=null&&label.Foreground==Paint.Ink)label.Foreground=Paint.Text;var target=root as Control;if(target!=null&&target.Foreground==Paint.Ink)target.Foreground=Paint.Text;var icon=root as Glyph;if(icon!=null&&icon.Color==Paint.Ink)icon.Color=Paint.Text;foreach(object item in LogicalTreeHelper.GetChildren(root)){var element=item as DependencyObject;if(element!=null)Apply(element);}return;}
            var text=root as TextBlock;if(text!=null){if(text.Foreground==Paint.Text)text.Foreground=Paint.Ink;else if(text.Foreground==Paint.Muted)text.Foreground=Muted;}
            var control=root as Control;if(control!=null&&control.Foreground==Paint.Text)control.Foreground=Paint.Ink;
            var glyph=root as Glyph;if(glyph!=null){if(glyph.Color==Paint.Text)glyph.Color=Paint.Ink;else if(glyph.Color==Paint.Muted)glyph.Color=Muted;}
            foreach(object child in LogicalTreeHelper.GetChildren(root)){var element=child as DependencyObject;if(element!=null)Apply(element);}
        }
        internal static Border IconFrame(UIElement child,double size)
        {
            return new Border {Width=size,Height=size,CornerRadius=new CornerRadius(size/2),BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Background=Paint.Brush("#16FFFFFF"),Child=child};
        }
    }
    internal static class Paint
    {
        static ControlTemplate buttonTemplate;
        static readonly Geometry gear=Gear(),windowsGear=Gear(6,4.2);
        static Geometry Gear(int teeth=8,double hole=3.5)
        {
            var shape=new StreamGeometry {FillRule=FillRule.EvenOdd};using(var path=shape.Open()){
                for(int i=0;i<teeth*6;i++){double angle=i/(teeth*6.0)*2*Math.PI-Math.PI/2,radius=i%6==0||i%6==5?8.3:10.6;var point=new Point(12+radius*Math.Cos(angle),12+radius*Math.Sin(angle));if(i==0)path.BeginFigure(point,true,true);else path.LineTo(point,true,false);}
                path.BeginFigure(new Point(12+hole,12),true,true);path.ArcTo(new Point(12-hole,12),new Size(hole,hole),0,false,SweepDirection.Clockwise,true,false);path.ArcTo(new Point(12+hole,12),new Size(hole,hole),0,false,SweepDirection.Clockwise,true,false);
            }shape.Freeze();return shape;
        }
        static readonly System.Collections.Concurrent.ConcurrentDictionary<string,SolidColorBrush> brushes=new System.Collections.Concurrent.ConcurrentDictionary<string,SolidColorBrush>(StringComparer.OrdinalIgnoreCase);
        internal static SolidColorBrush Brush(string value) {SolidColorBrush cached;if(brushes.TryGetValue(value,out cached))return cached;var b=(SolidColorBrush)new BrushConverter().ConvertFromString(value);b.Freeze();if(brushes.Count<128)brushes.TryAdd(value,b);return b;}
        internal const string ControlWhiteColor="#FFF4F8F5";
        internal static readonly Brush ControlWhite=Brush(ControlWhiteColor);
        internal static readonly Brush OnLight=FixedOnLight();
        static Brush FixedOnLight(){var brush=new SolidColorBrush(Color.FromRgb(25,34,30));brush.Freeze();return brush;}
        static readonly Brush legacyText=Brush("#FFFFFFFF"),legacyMuted=Brush("#FFDBE3DF"),legacyInk=Brush("#FF19221E");
        internal static Brush Text {get{return Glass95.Enabled?Glass95.Text:PanelMaterial.Text(Glass95.Dark);}}
        internal static Brush Muted {get{return Glass95.Enabled?Glass95.Muted:PanelMaterial.Muted(Glass95.Dark);}}
        internal static Brush Ink {get{return Glass95.Enabled?Glass95.Text:legacyInk;}}
        internal static void Label(DrawingContext dc,string text,double size,Brush color,Point point,FontWeight weight)
        {
            var f=new FormattedText(text,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,weight,FontStretches.Normal),size,color,1.0);
            dc.DrawText(f,point);
        }
        internal static void Center(DrawingContext dc,string text,double size,Brush color,Rect bounds)
        {
            var f=new FormattedText(text,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),size,color,1.0);
            dc.DrawText(f,new Point(bounds.X+(bounds.Width-f.Width)/2,bounds.Y+(bounds.Height-f.Height)/2));
        }
        internal static void Icon(DrawingContext dc,string kind,Brush brush,Point center,double size,int generation=0,int signal=-1,int traffic=-1)
        {
            var pen=new Pen(brush,1.8) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round };
            dc.PushTransform(new TranslateTransform(center.X-size/2,center.Y-size/2)); dc.PushTransform(new ScaleTransform(size/24,size/24));
            Action<double,double,double,double> line=(x,y,a,b)=>dc.DrawLine(pen,new Point(x,y),new Point(a,b));
            if(kind.StartsWith("pref-",StringComparison.Ordinal))QuickSettingIcons.Draw(dc,kind.Substring(5),pen);
            else if(kind=="brightness") {
                dc.DrawEllipse(brush,null,new Point(12,12),4.2,4.2);
                for(int i=0;i<8;i++) { double a=i*Math.PI/4; line(12+8*Math.Cos(a),12+8*Math.Sin(a),12+10.6*Math.Cos(a),12+10.6*Math.Sin(a)); }
            } else if(kind=="scroll") {
                dc.DrawRoundedRectangle(null,pen,new Rect(6,2,12,20),6,6);line(12,6,12,10);
            } else if(kind=="wifi"||kind=="wifi-off"||kind=="wifi-disconnected") {
                bool badge=kind=="wifi"&&generation>=4&&generation<=7;int bars=kind=="wifi"?WifiLink.Bars(signal):0;
                var wifiPen=new Pen(brush,2.0){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
                dc.PushTransform(new TranslateTransform(badge||traffic>=0?0:1.5,0));
                string[] waves={"M7.2,15.1 C8.9,13.4 11.1,13.4 12.8,15.1","M4.3,11.9 C7.6,8.7 12.4,8.7 15.7,11.9","M1.4,8.7 C6.2,3.9 13.8,3.9 18.6,8.7"};
                for(int i=0;i<3;i++){dc.PushOpacity(bars>i?1:.2);dc.DrawGeometry(null,wifiPen,Geometry.Parse(waves[i]));dc.Pop();}dc.DrawEllipse(brush,null,new Point(10,18.9),1.35,1.35);dc.Pop();
                if(badge)Label(dc,generation.ToString(CultureInfo.InvariantCulture),7.4,brush,new Point(19,.2),FontWeights.Bold);
                if(kind=="wifi"&&traffic>=0){var arrow=new Pen(brush,1.25){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};dc.PushOpacity((traffic&1)!=0?1:.38);dc.DrawGeometry(null,arrow,Geometry.Parse("M18.5,22 L18.5,16 M16.8,17.7 L18.5,16 20.2,17.7"));dc.Pop();dc.PushOpacity((traffic&2)!=0?1:.38);dc.DrawGeometry(null,arrow,Geometry.Parse("M22.5,16 L22.5,22 M20.8,20.3 L22.5,22 24.2,20.3"));dc.Pop();}
                if(kind=="wifi-off")line(3,3,21,21);else if(kind=="wifi-disconnected"){line(18,15,22,19);line(22,15,18,19);}
            } else if(kind=="bluetooth") {
                dc.DrawGeometry(null,pen,Geometry.Parse("M6,6 L18,17 12,22 12,2 18,7 6,18"));
            } else if(kind=="computer") {dc.DrawRoundedRectangle(null,pen,new Rect(2,3,20,13),2,2);line(12,16,12,21);line(7,21,17,21);
            } else if(kind=="keyboard") {dc.DrawRoundedRectangle(null,pen,new Rect(1,5,22,14),2.5,2.5);for(int y=0;y<2;y++)for(int x=0;x<5;x++)dc.DrawRoundedRectangle(brush,null,new Rect(4+x*3.6,8+y*3.3,1.5,1.5),.2,.2);line(7,16,17,16);
            } else if(kind=="mouse") {dc.DrawRoundedRectangle(null,pen,new Rect(6,2,12,20),6,6);line(12,3,12,9);line(7,10,17,10);
            } else if(kind=="volume" || kind=="mute" || kind=="volume-low") {
                dc.DrawGeometry(brush,new Pen(brush,.6){LineJoin=PenLineJoin.Round},Geometry.Parse("M3,9 L7,9 12,5 12,19 7,15 3,15 Z"));
                if(kind=="mute") { line(16,9,22,15); line(22,9,16,15); }
                else {dc.DrawGeometry(null,pen,Geometry.Parse("M15.5,8 Q19.5,12 15.5,16"));if(kind!="volume-low")dc.DrawGeometry(null,pen,Geometry.Parse("M18.5,4.5 Q25,12 18.5,19.5"));}
            } else if(kind=="bolt") dc.DrawGeometry(brush,null,Geometry.Parse("M14,1 L4,14 11,14 9,23 21,9 14,9 Z"));
            else if(kind=="apps") { for(int i=0;i<9;i++) dc.DrawEllipse(brush,null,new Point(5+(i%3)*7,5+(i/3)*7),1.5,1.5); }
            else if(kind=="more") {for(int i=0;i<3;i++)dc.DrawEllipse(brush,null,new Point(5+i*7,12),1.5,1.5);}
            else if(kind=="windows-start") {foreach(int x in new[]{2,13})foreach(int y in new[]{2,13})dc.DrawRoundedRectangle(brush,null,new Rect(x,y,9,9),.5,.5);}
            else if(kind=="grip") {for(int i=0;i<6;i++)dc.DrawEllipse(brush,null,new Point(8+(i%2)*8,5+(i/2)*7),1.6,1.6);}
            else if(kind=="edit") {dc.DrawGeometry(null,pen,Geometry.Parse("M4,16 L16,4 20,8 8,20 3,21 Z M14,6 L18,10"));}
            else if(kind=="search") { dc.DrawEllipse(null,pen,new Point(10,10),6.5,6.5); line(15,15,22,22); }
            else if(kind=="dock-search") {var ring=new Pen(brush,1.9){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};dc.DrawEllipse(null,ring,new Point(10.3,10.3),6.8,6.8);dc.DrawLine(ring,new Point(15.3,15.3),new Point(21,21));}
            else if(kind=="dock-desktops") {dc.DrawRoundedRectangle(null,pen,new Rect(7,3,15,18),3,3);dc.DrawRoundedRectangle(null,pen,new Rect(2,6,2,12),1,1);}
            else if(kind=="screenshot") {dc.DrawGeometry(null,pen,Geometry.Parse("M8,3 L3,3 3,8 M16,3 L21,3 21,8 M21,16 L21,21 16,21 M8,21 L3,21 3,16"));dc.DrawRoundedRectangle(null,pen,new Rect(7,7,10,10),2,2);}
            else if(kind=="task-manager") {dc.DrawRoundedRectangle(null,pen,new Rect(2,2,20,20),3,3);dc.DrawRoundedRectangle(brush,null,new Rect(6,12,2.5,6),.6,.6);dc.DrawRoundedRectangle(brush,null,new Rect(10.8,6,2.5,12),.6,.6);dc.DrawRoundedRectangle(brush,null,new Rect(15.5,9,2.5,9),.6,.6);}
            else if(kind=="audio-output") {dc.DrawGeometry(brush,null,Geometry.Parse("M3,6 L12,12 3,18 Z"));dc.DrawGeometry(null,pen,Geometry.Parse("M14,6 A7,7 0 0 1 14,18 M17,2 A11,11 0 0 1 17,22"));}
            else if(kind=="headphones") {dc.DrawGeometry(null,pen,Geometry.Parse("M4,16 L4,10 A8,8 0 0 1 20,10 L20,16"));dc.DrawRoundedRectangle(brush,null,new Rect(2,12,5,9),2,2);dc.DrawRoundedRectangle(brush,null,new Rect(17,12,5,9),2,2);}
            else if(kind=="earbuds") {dc.DrawGeometry(null,pen,Geometry.Parse("M8,12 C2,13 2,3 7,3 C11,3 12,8 9,11 L9,20 Q9,22 7,22 L6,22 6,13 M16,12 C22,13 22,3 17,3 C13,3 12,8 15,11 L15,20 Q15,22 17,22 L18,22 18,13"));}
            else if(kind=="microphone") {dc.DrawRoundedRectangle(brush,null,new Rect(8,2,8,13),4,4);dc.DrawGeometry(null,pen,Geometry.Parse("M5,11 L5,12 A7,7 0 0 0 19,12 L19,11 M12,19 L12,22 M8,22 L16,22"));}
            else if(kind=="ethernet"||kind=="ethernet-off") {dc.DrawGeometry(null,pen,Geometry.Parse("M4,3 L20,3 Q21,3 21,4 L21,14 17,14 17,18 7,18 7,14 3,14 3,4 Q3,3 4,3 Z"));for(int i=0;i<4;i++)line(7.5+i*3,6,7.5+i*3,9);line(12,18,12,22);if(kind=="ethernet-off"){line(4,21,21,4);}}
            else if(kind=="network"||kind=="network-off") {dc.DrawRoundedRectangle(null,pen,new Rect(3,3,18,12),2,2);line(12,15,12,21);line(7,21,17,21);if(kind=="network-off")line(3,22,22,2);}
            else if(kind=="chevron") { line(8,5,15,12); line(15,12,8,19); }
            else if(kind=="back") {line(15,5,8,12);line(8,12,15,19);}
            else if(kind=="user") {dc.DrawEllipse(null,pen,new Point(12,7),4,4);dc.DrawGeometry(null,pen,Geometry.Parse("M3,22 L3,20 A9,8 0 0 1 21,20 L21,22"));}
            else if(kind=="phone") {dc.DrawRoundedRectangle(null,pen,new Rect(6,1,12,22),2.5,2.5);line(10,4,14,4);line(10,20,14,20);}
            else if(kind=="message") dc.DrawGeometry(null,pen,Geometry.Parse("M5,3 L19,3 Q22,3 22,6 L22,15 Q22,18 19,18 L10,18 5,22 5,18 Q2,18 2,15 L2,6 Q2,3 5,3 Z M6,8 L18,8 M6,12 L14,12"));
            else if(kind=="photo") {dc.DrawRoundedRectangle(null,pen,new Rect(2,2,20,20),3,3);dc.DrawEllipse(null,pen,new Point(8,8),2,2);dc.DrawGeometry(null,pen,Geometry.Parse("M3,19 L10,12 14,16 17,12 22,17"));}
            else if(kind=="send") dc.DrawGeometry(null,pen,Geometry.Parse("M2,3 L23,12 2,21 6,12 Z M6,12 L23,12"));
            else if(kind=="call") dc.DrawGeometry(null,pen,Geometry.Parse("M5,2 L9,6 7,10 Q9,15 14,17 L18,15 22,19 20,22 Q9,23 2,10 L2,5 Z"));
            else if(kind=="folder") dc.DrawGeometry(null,pen,Geometry.Parse("M2,7 L2,4 Q2,2 4,2 L10,2 13,5 20,5 Q22,5 22,7 L22,20 Q22,22 20,22 L4,22 Q2,22 2,20 Z M2,9 L22,9"));
            else if(kind=="up") { line(5,15,12,8); line(12,8,19,15); }
            else if(kind=="close") {line(6,6,18,18);line(18,6,6,18);}
            else if(kind=="pin") {dc.DrawGeometry(null,pen,Geometry.Parse("M8,3 L16,3 15,10 19,14 5,14 9,10 Z M12,14 L12,22"));}
            else if(kind=="airplane") dc.DrawGeometry(brush,null,Geometry.Parse("M11,2 Q12,0 13,2 L14,9 23,15 23,17 14,14 14,20 17,22 17,24 12,22 7,24 7,22 10,20 10,14 1,17 1,15 10,9 Z"));
            else if(kind=="battery") { dc.DrawRoundedRectangle(null,pen,new Rect(1,6,20,12),3,3); line(23,10,23,14); dc.DrawGeometry(brush,null,Geometry.Parse("M12,7 L7,13 11,13 10,17 17,10 13,10 Z")); }
            else if(kind=="battery-plain") {dc.DrawRoundedRectangle(null,pen,new Rect(1,6,20,12),3,3);line(23,10,23,14);dc.DrawRoundedRectangle(brush,null,new Rect(4,9,12,6),1,1);}
            else if(kind=="person") { dc.DrawEllipse(brush,null,new Point(12,4),2.5,2.5); line(3,9,21,9); line(12,7,12,15); line(12,15,7,23); line(12,15,17,23); }
            else if(kind=="settings") dc.DrawGeometry(brush,null,gear);
            else if(kind=="home") dc.DrawGeometry(null,pen,Geometry.Parse("M2,11 L12,2 22,11 M5,9 L5,21 10,21 10,14 14,14 14,21 19,21 19,9"));
            else if(kind=="notification") {dc.DrawGeometry(null,pen,Geometry.Parse("M4,17 L6,14 6,9 A6,6 0 0 1 18,9 L18,14 20,17 Z M10,21 Q12,23 14,21 M12,1 L12,3"));}
            else if(kind=="motion") {dc.DrawGeometry(null,pen,Geometry.Parse("M3,7 L14,7 M3,12 L10,12 M3,17 L14,17 M14,4 L22,12 14,20"));}
            else if(kind=="windows-settings") dc.DrawGeometry(brush,null,windowsGear);
            else if(kind=="window") { dc.DrawRoundedRectangle(null,pen,new Rect(2,3,20,18),2,2); line(2,8,22,8); }
            else if(kind=="desktops") {dc.DrawGeometry(null,pen,Geometry.Parse("M8,5 L8,3 Q8,2 10,2 L21,2 Q23,2 23,4 L23,15 Q23,17 21,17 L19,17"));dc.DrawRoundedRectangle(null,pen,new Rect(1,7,17,15),2,2);}
            else if(kind=="shield") dc.DrawGeometry(null,pen,Geometry.Parse("M12,2 Q17,5 21,5 L21,12 Q20,19 12,23 Q4,19 3,12 L3,5 Q7,5 12,2 Z M12,3 L12,21 M4,11 L20,11"));
            else if(kind=="shuffle") {dc.DrawGeometry(null,pen,Geometry.Parse("M3,6 L6,6 C10,6 14,18 18,18 L22,18 M18,14 L22,18 18,22 M3,18 L6,18 C10,18 14,6 18,6 L22,6 M18,2 L22,6 18,10"));}
            else if(kind=="play") dc.DrawGeometry(brush,null,Geometry.Parse("M6,3 L22,12 6,21 Z"));
            else if(kind=="pause") {dc.DrawRoundedRectangle(brush,null,new Rect(5,3,5,18),1.5,1.5);dc.DrawRoundedRectangle(brush,null,new Rect(14,3,5,18),1.5,1.5);}
            else if(kind=="next"||kind=="previous") {if(kind=="previous")dc.PushTransform(new ScaleTransform(-1,1,12,12));dc.DrawGeometry(brush,null,Geometry.Parse("M4,4 L17,12 4,20 Z"));dc.DrawRoundedRectangle(brush,null,new Rect(18,4,3,16),1,1);if(kind=="previous")dc.Pop();}
            else if(kind=="moon") dc.DrawGeometry(brush,null,Geometry.Parse("M16,2 A10,10 0 1 0 22,17 A10,10 0 0 1 16,2 Z"));
            else if(kind=="dnd") {dc.DrawEllipse(null,pen,new Point(12,12),9,9);line(7,12,17,12);}
            else if(kind=="lock") {dc.DrawRoundedRectangle(null,pen,new Rect(5,10,14,12),2,2);dc.DrawGeometry(null,pen,Geometry.Parse("M8,10 L8,6 A4,4 0 0 1 16,6 L16,10"));dc.DrawEllipse(brush,null,new Point(12,15),1.2,1.2);line(12,15,12,18);}
            else if(kind=="leaf") {dc.DrawGeometry(null,pen,Geometry.Parse("M4,17 Q1,5 21,3 Q23,22 9,21 Q5,20 4,17 M3,23 L17,9"));}
            else if(kind=="startup") {dc.DrawRoundedRectangle(null,pen,new Rect(3,3,18,18),3,3);line(12,18,12,7);line(7,12,12,7);line(12,7,17,12);}
            else if(kind=="power") dc.DrawGeometry(null,pen,Geometry.Parse("M12,1 L12,12 M7,4 A9,9 0 1 0 17,4"));
            else if(kind=="rotation") {dc.DrawGeometry(null,pen,Geometry.Parse("M3,11 A9,9 0 0 1 20,6 M17,3 L21,6 17,9 M21,13 A9,9 0 0 1 4,18 M7,15 L3,18 7,21"));}
            else if(kind=="devices") {dc.DrawRoundedRectangle(null,pen,new Rect(7,3,10,18),2,2);line(10,18,14,18);dc.DrawGeometry(null,pen,Geometry.Parse("M3,8 L1,12 3,16 M21,8 L23,12 21,16"));}
            else if(kind=="calendar") {dc.DrawRoundedRectangle(null,pen,new Rect(3,5,18,17),3,3);line(3,10,21,10);line(8,2,8,7);line(16,2,16,7);dc.DrawEllipse(brush,null,new Point(8,15),1.2,1.2);dc.DrawEllipse(brush,null,new Point(15,15),1.2,1.2);}
            else if(kind=="focus") {dc.DrawEllipse(null,pen,new Point(12,12),10,10);dc.DrawGeometry(brush,null,Geometry.Parse("M12,3 L12,12 20,7 A9,9 0 0 0 12,3 Z"));}
            else { dc.DrawEllipse(null,pen,new Point(12,12),8,8); dc.DrawEllipse(brush,null,new Point(12,12),2,2); }
            dc.Pop(); dc.Pop();
        }
        internal static Button Button(UIElement content,Action action,string hint,bool toggle=false)
        {
            Button b=toggle?(Button)new QuickToggleButton():new Button();b.Content=content;b.Foreground=Text;b.Background=Brush("#1CFFFFFF");b.BorderThickness=new Thickness(0);b.Padding=new Thickness(12,8,12,8);b.Cursor=Cursors.Hand;b.ToolTip=hint;b.HorizontalContentAlignment=HorizontalAlignment.Center;
            b.Resources["ButtonRadius"]=new CornerRadius(18);
            if(Glass95.Enabled&&content is TextBlock){b.Background=Glass95.Card;b.BorderBrush=Glass95.Rim;b.BorderThickness=new Thickness(.65);b.Resources["ButtonRadius"]=new CornerRadius(24);}
            if(buttonTemplate==null)buttonTemplate=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border x:Name='p' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='{DynamicResource ButtonRadius}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border></ControlTemplate>");
            b.Template=buttonTemplate;b.FocusVisualStyle=null;AutomationProperties.SetName(b,hint); b.Click+=(s,e)=>action();Motion.Button(b);return b;
        }
        internal static TextBlock TextBlock(string text,double size,Brush color)
        {return new TextBlock {Text=text,FontFamily=SystemFonts.MessageFontFamily,FontSize=size,Foreground=color,VerticalAlignment=VerticalAlignment.Center};}
        internal static Button Action(string text,Action action)
        {
            var button=Button(TextBlock(text,13,Text),action,text);button.VerticalContentAlignment=VerticalAlignment.Center;
            button.SizeChanged+=(s,e)=>button.Resources["ButtonRadius"]=new CornerRadius(Math.Max(0,Math.Min(button.ActualWidth,button.ActualHeight)/2));return button;
        }
    }
    internal sealed class Glyph : FrameworkElement
    {
        public string Kind; public Brush Color=Paint.Text;public int Generation;public int Signal=-1,Traffic=-1;
        DeviceSymbol device;
        internal bool HasDeviceIcon {get{return device!=null&&device.Source!=null;}}
        internal void SetDeviceIcon(ImageSource image){if(device==null){if(image==null)return;device=new DeviceSymbol();}if(device.Set(image))InvalidateVisual();}
        public Glyph(string kind,double size) { Kind=kind; Width=Height=size; IsHitTestVisible=false; }
        internal void Update(string kind,Brush color){if(Kind==kind&&Color==color)return;Kind=kind;Color=color;InvalidateVisual();}
        protected override void OnRender(DrawingContext dc) {var center=new Point(ActualWidth/2,ActualHeight/2);double size=Math.Min(ActualHeight,ActualWidth)-2;if(HasDeviceIcon)device.Draw(dc,Color,center,size,false);else Paint.Icon(dc,Kind,Color,center,size,Generation,Signal,Traffic);}
    }
    internal sealed partial class PillSlider : FrameworkElement
    {
        public string Kind; double value; bool dragging;
        DeviceSymbol device;
        internal bool HasDeviceIcon {get{return device!=null&&device.Source!=null;}}
        internal void SetDeviceIcon(ImageSource image){if(device==null){if(image==null)return;device=new DeviceSymbol();}if(device.Set(image))InvalidateVisual();}
        void DrawIcon(DrawingContext dc,string kind,Brush color,Point center,double size){if(HasDeviceIcon)device.Draw(dc,color,center,size,kind=="mute");else Paint.Icon(dc,kind,color,center,size);}
        static readonly DependencyProperty DisplayValueProperty=DependencyProperty.Register("DisplayValue",typeof(double),typeof(PillSlider),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));
        public bool Tablet=true,AnimateValue=true;
        public bool Light,DarkTrack;
        public bool ShowIcon=true,Slim,WheelAdjust=true;
        internal bool Vertical,NoTrackBorder,LinearFill;
        public event Action<double> Changed;
        internal Button OutputButton {get;private set;}
        internal FrameworkElement WithOutputAction(Action action)
        {
            var root=new Grid();root.Children.Add(this);OutputButton=Paint.Button(new Border(),action,"소리 및 출력");OutputButton.Padding=new Thickness(0);OutputButton.Background=Brushes.Transparent;OutputButton.Resources["ButtonRadius"]=new CornerRadius(40);root.Children.Add(OutputButton);
            Action place=()=>{double size=Math.Max(0,Math.Min(ActualWidth,ActualHeight));OutputButton.Width=OutputButton.Height=size;OutputButton.Margin=Margin;OutputButton.HorizontalAlignment=Vertical?HorizontalAlignment.Center:HorizontalAlignment.Left;OutputButton.VerticalAlignment=Vertical?VerticalAlignment.Bottom:VerticalAlignment.Center;};SizeChanged+=(s,e)=>place();OutputButton.PreviewMouseWheel+=(s,e)=>{if(IsEnabled)OnMouseWheel(e);};return root;
        }
        public double Value { get { return value; } set { double next=Clamp(value);if(this.value==next)return;this.value=next;double from=(double)GetValue(DisplayValueProperty);BeginAnimation(DisplayValueProperty,null);SetValue(DisplayValueProperty,next);if(AnimateValue&&!dragging&&Motion.Enabled&&IsVisible&&PresentationSource.FromVisual(this)!=null)BeginAnimation(DisplayValueProperty,Motion.Tween(from,next,160));InvalidateVisual(); } }
        public static double Clamp(double value) { return Double.IsNaN(value) || Double.IsInfinity(value) ? 0 : Math.Max(0,Math.Min(100,value)); }
        public static double FillWidth(double width,double value) { return Math.Max(0,width)*Clamp(value)/100; }
        public static Rect CapsuleFillBounds(double width,double height,double value) {width=Math.Max(0,width);height=Math.Max(0,height);double p=Clamp(value),diameter=Math.Min(width,height);if(p<10){double size=diameter*p/10;return new Rect((diameter-size)/2,(height-size)/2,size,size);}return new Rect(0,(height-diameter)/2,diameter+(width-diameter)*(p-10)/90,diameter);}
        public static double CapsuleFillWidth(double width,double height,double value) {return CapsuleFillBounds(width,height,value).Width;}
        public PillSlider(string kind) { Kind=kind; Height=44; MinWidth=90; Focusable=true; Cursor=Cursors.Hand; SnapsToDevicePixels=true;
            AutomationProperties.SetName(this,kind=="brightness" ? "밝기" : "음량");Loaded+=(s,e)=>ObserveValueOption(true);Unloaded+=(s,e)=>ObserveValueOption(false); }
        protected override AutomationPeer OnCreateAutomationPeer() { return new PillPeer(this); }
        protected override void OnRender(DrawingContext dc)
        {
            double w=ActualWidth,h=ActualHeight; if(w<=0 || h<=0) return;
            if(WaveMode){DrawWave(dc,w,h);return;}
            if(Glass95.Enabled&&Tablet&&!Slim){DrawGlass(dc,w,h);return;}
            double shown=(double)GetValue(DisplayValueProperty);string iconKind=Kind.StartsWith("volume",StringComparison.Ordinal)?SoundIcon.For(value,false,IsEnabled):Kind;
            double axis=Vertical?h:w,cross=Vertical?w:h;bool capsule=Tablet&&ShowIcon&&!Slim&&!LinearFill;double thickness=Slim?6:cross,top=(cross-thickness)/2;var track=Vertical?new Rect(top,0,thickness,h):new Rect(0,top,w,thickness);var logical=capsule?CapsuleFillBounds(axis,cross,shown):new Rect(0,top,FillWidth(axis,shown),thickness);var filled=Vertical?new Rect(logical.Y,h-logical.Right,logical.Height,logical.Width):logical;double fillRadius=Math.Min(filled.Width,filled.Height)/2;var fill=new RectangleGeometry(filled,fillRadius,fillRadius);
            dc.DrawRoundedRectangle(Paint.Brush(DarkTrack?"#26000000":IsEnabled ? "#3C77817F" : "#286E7775"),NoTrackBorder?null:new Pen(Paint.Brush("#288B9692"),.7),track,thickness/2,thickness/2);
            bool clipTrack=LinearFill&&!Slim;if(clipTrack)dc.PushClip(new RectangleGeometry(track,thickness/2,thickness/2));
            double fillOpacity=1;if(shown>0)dc.DrawGeometry(Tablet?Paint.ControlWhite:Brushes.Black,null,fill);
            var c=ValueIconCenter(w,h,Vertical?new Point(w/2,h-cross*(Tablet&&ShowIcon&&!Slim?.5:.70)):new Point(cross*(Tablet&&ShowIcon&&!Slim?.5:.70),h/2)); double icon=cross*.44;
            if(ShowIcon)DrawIcon(dc,iconKind,DarkTrack?Paint.ControlWhite:Tablet&&!Light?Paint.Brush("#D8E8EDEA"):Paint.Brush("#FF303A36"),c,icon);
            if(ShowIcon&&shown>0) {dc.PushOpacity(fillOpacity);dc.PushClip(fill);DrawIcon(dc,iconKind,Tablet?Paint.Brush(!Light&&Kind=="brightness"?"#FFEC7D4E":"#FF35413B"):Paint.Brush("#FFF5F5F1"),c,icon);dc.Pop();dc.Pop();}
            if(clipTrack)dc.Pop();
            DrawValueNumber(dc,fill,ValueNumberInk??(DarkTrack?Paint.ControlWhite:Paint.Text),Tablet?Paint.OnLight:Paint.ControlWhite);
            if(Slim&&IsEnabled)dc.DrawEllipse(Paint.ControlWhite,null,Vertical?new Point(w/2,h-FillWidth(Math.Max(0,h-10),shown)-5):new Point(FillWidth(Math.Max(0,w-10),shown)+5,h/2),4.5,4.5);
            if(IsKeyboardFocused) dc.DrawRoundedRectangle(null,new Pen(Paint.Brush("#9ED4F0E5"),1.5),new Rect(1,1,Math.Max(0,w-2),Math.Max(0,h-2)),Math.Min(w,h)/2,Math.Min(w,h)/2);
        }
        internal static double PositionValue(double width,double height,Point point,bool vertical,bool capsule){double axis=vertical?height:width,x=vertical?height-point.Y:point.X,diameter=capsule?Math.Min(width,height):0;return Math.Round(Clamp((x-diameter/2)/Math.Max(1,axis-diameter)*100));}
        void Update(Point point) {Value=WaveMode?PositionValue(Math.Max(1,ActualWidth-16),ActualHeight,new Point(point.X-8,point.Y),false,false):PositionValue(ActualWidth,ActualHeight,point,Vertical,Tablet&&ShowIcon&&!Slim&&!LinearFill);if(Changed!=null)Changed(Value);}
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { base.OnMouseLeftButtonDown(e); Focus(); dragging=true; CaptureMouse(); Update(e.GetPosition(this)); e.Handled=true; }
        protected override void OnMouseMove(MouseEventArgs e) { if(dragging && e.LeftButton==MouseButtonState.Pressed) Update(e.GetPosition(this)); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { dragging=false; ReleaseMouseCapture(); e.Handled=true; }
        protected override void OnLostMouseCapture(MouseEventArgs e) { dragging=false; }
        protected override void OnMouseWheel(MouseWheelEventArgs e) {if(!WheelAdjust){base.OnMouseWheel(e);return;}Value+=Math.Sign(e.Delta)*2; if(Changed!=null) Changed(Value); e.Handled=true; }
        protected override void OnKeyDown(KeyEventArgs e) {
            double n=Value;
            if(e.Key==Key.Home) n=0; else if(e.Key==Key.End) n=100; else if(e.Key==Key.Left || e.Key==Key.Down) n-=2;
            else if(e.Key==Key.Right || e.Key==Key.Up) n+=2; else { base.OnKeyDown(e); return; }
            Value=n; if(Changed!=null) Changed(Value); e.Handled=true;
        }
        sealed class PillPeer : FrameworkElementAutomationPeer, IRangeValueProvider
        {
            PillSlider Pill { get { return (PillSlider)Owner; } }
            public PillPeer(PillSlider owner):base(owner) { }
            protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Slider; }
            protected override string GetClassNameCore() { return "OneTaskBar.PillSlider"; }
            protected override AutomationOrientation GetOrientationCore(){return Pill.Vertical?AutomationOrientation.Vertical:AutomationOrientation.Horizontal;}
            public override object GetPattern(PatternInterface pattern) { return pattern==PatternInterface.RangeValue ? this : base.GetPattern(pattern); }
            public bool IsReadOnly { get { return !Pill.IsEnabled; } } public double LargeChange { get { return 10; } } public double SmallChange { get { return 2; } }
            public double Maximum { get { return 100; } } public double Minimum { get { return 0; } } public double Value { get { return Pill.Value; } }
            public void SetValue(double value) { if(IsReadOnly) throw new ElementNotEnabledException(); Pill.Value=value; if(Pill.Changed!=null) Pill.Changed(Pill.Value); }
        }
    }
    internal class GlassWindow : Window
    {
        int motion;bool closed,hiding;Brush glassTint,solidTint;LinearGradientBrush glassEdge;bool edgeDark;Point glassPointer;TaskCompletionSource<bool> dismissal;
        internal readonly int InterfaceVersion=Preferences.Current.UIVersion;
        internal bool GlassMaterial {get{return glassEdge!=null;}}
        internal Point GlassHighlight {get{return glassEdge==null?new Point():glassEdge.StartPoint;}}
        public readonly Border Surface;
        public double Radius=28; public bool Dark=true; public bool HasBlur;internal bool SoftCorners=true,OpaqueSurface,DockSurface;
        internal bool StableSurface=true,UnifiedFade,SmoothLauncher;internal FrameworkElement TransitionContent;
        GlassLens lens;
        static readonly DependencyProperty SurfaceFadeProperty=DependencyProperty.Register("SurfaceFade",typeof(double),typeof(GlassWindow),new PropertyMetadata(1.0,(d,e)=>{var window=(GlassWindow)d;if(window.Surface!=null)window.Surface.Opacity=(double)e.NewValue;Native.BlurOpacity(window,(double)e.NewValue);}));
        void ResetFade(){BeginAnimation(SurfaceFadeProperty,null);SetValue(SurfaceFadeProperty,1.0);Surface.Opacity=1;Native.BlurOpacity(this,1);}
        internal Rect? BackdropClip;
        internal bool IsDismissing {get{return hiding;}}
        internal DateTime HiddenSince=DateTime.UtcNow;
        internal virtual bool CanRelease {get{return true;}}
        internal bool IdleForRelease(DateTime now){return !closed&&!IsVisible&&!hiding&&CanRelease&&(now-HiddenSince).TotalSeconds>=20;}
        public GlassWindow()
        {
            MenuTheme.Install();
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true;Background=Brushes.Transparent;
            ShowInTaskbar=Program.CaptureReview;Topmost=true;FontFamily=SystemFonts.MessageFontFamily;
            Surface=new Border { Background=Paint.Brush("#B32C3932"),BorderBrush=Paint.Brush("#38FFFFFF"),BorderThickness=new Thickness(.7),CornerRadius=new CornerRadius(Radius) };
            Content=new AdornerDecorator {Child=Surface};
            var touchScroll=new Style(typeof(ScrollViewer));
            touchScroll.Setters.Add(new Setter(ScrollViewer.PanningModeProperty,PanningMode.VerticalFirst));
            touchScroll.Setters.Add(new Setter(ScrollViewer.PanningDecelerationProperty,.001));
            touchScroll.Setters.Add(new Setter(ScrollViewer.PanningRatioProperty,1.0));
            touchScroll.Setters.Add(new Setter(ScrollViewer.CanContentScrollProperty,false));
            Resources.Add(typeof(ScrollViewer),touchScroll);
            Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),System.Windows.Markup.XamlReader.Parse(
                "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'><Setter Property='Width' Value='4'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}'><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#55D5DFD9' CornerRadius='2'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></ControlTemplate></Setter.Value></Setter></Style>"));
            SourceInitialized+=(s,e)=> {
                if(DockSurface)Dark=Glass95.DockDark;else if(Glass95.Enabled||this is QuickWindow||this is NotificationWindow||this is DetailWindow||this is CalendarWindow||this is StartWindow||this is SearchWindow||this is DrawerWindow)Dark=Glass95.Dark;
                var hwnd=new WindowInteropHelper(this).Handle;int style=Native.GetWindowLong(hwnd,-20);Native.SetWindowLong(hwnd,-20,Program.CaptureReview?(style|0x40000)&~0x80:(style|0x80)&~0x40000);
                glassTint=Surface.Background;HasBlur=!OpaqueSurface&&Native.Blur(this,Radius,Dark);ApplyMaterial();
                var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);if(source!=null)source.AddHook((IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled)=> {
                    if(msg==0x5||msg==0x2E0)Dispatcher.BeginInvoke(new Action(()=> {if(!closed)Native.Round(this,Radius);}),System.Windows.Threading.DispatcherPriority.Render);return IntPtr.Zero;
                });
            };
            SizeChanged+=(s,e)=> { if(new WindowInteropHelper(this).Handle!=IntPtr.Zero) Native.Round(this,Radius); };
            Preferences.Changed+=ApplyMaterial;
            MouseMove+=(s,e)=>MoveGlassHighlight(e.GetPosition(Surface));MouseLeave+=(s,e)=>MoveGlassHighlight(new Point(Surface.ActualWidth/2,Surface.ActualHeight/2));
            Loaded+=(s,e)=>{if(lens==null){var layer=AdornerLayer.GetAdornerLayer(Surface);if(layer!=null){lens=new GlassLens(this);layer.Add(lens);}}ApplyMaterial();if(Program.CaptureReview){ShowInTaskbar=true;var h=new WindowInteropHelper(this).Handle;Native.SetWindowLong(h,-20,(Native.GetWindowLong(h,-20)|0x40000)&~0x80);}};
            IsVisibleChanged+=(s,e)=> {if(!IsVisible)HiddenSince=DateTime.UtcNow;};
            Closed+=(s,e)=> {closed=true;++motion;LauncherMotion.Cancel(TransitionContent);CompleteDismissal(false);Preferences.Changed-=ApplyMaterial;};
            PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Escape&&!DragReorder.Active) { Hide(); e.Handled=true; } };
        }
        void ApplyMaterial()
        {
            if(DockSurface)Dark=Glass95.DockDark;else if(!OpaqueSurface)Dark=Glass95.Dark;
            if(lens!=null){lens.Active=Glass95.Enabled&&!OpaqueSurface&&HasBlur&&Preferences.Current.Blur&&!(DockSurface&&DockAppearance.Custom)&&!(this is HudWindow);lens.InvalidateVisual();}
            if(glassTint==null)return;if(DockSurface&&DockAppearance.Custom){Native.EnableBlur(this,false);Surface.Background=DockAppearance.Background;Surface.BorderBrush=Glass95.Enabled?Glass95.Rim:Brushes.Transparent;Surface.BorderThickness=new Thickness(Glass95.Enabled?.7:0);return;}
            bool enabled=HasBlur&&Preferences.Current.Blur&&!(DockSurface&&Preferences.Current.DockSolidBackground)&&(!Glass95.Enabled||Glass95.Transparent);Native.EnableBlur(this,enabled);
            bool panel=PanelMaterial.ReadablePanel(this),popup=PanelMaterial.NotificationPopup(this);
            if(this is NotificationWindow){Dark=Glass95.Dark;Surface.Background=enabled?NotificationStyle.Surface:NotificationStyle.Solid;Surface.BorderBrush=Glass95.Enabled?Glass95.Rim:Paint.Brush("#28FFFFFF");Surface.BorderThickness=new Thickness(.7);return;}
            var detail=this as DetailWindow;if(detail!=null&&detail.PageKey=="sound"){Dark=true;if(lens!=null)lens.Active=false;Surface.Background=enabled?AudioOutputView.Glass:AudioOutputView.Solid;Surface.BorderBrush=Brushes.Transparent;Surface.BorderThickness=new Thickness(0);return;}
            if(this is HudWindow&&Glass95.Enabled){Surface.Background=enabled?HudWindow.ContrastGlass:HudWindow.ContrastSolid;Surface.BorderBrush=Paint.Brush("#60FFFFFF");Surface.BorderThickness=new Thickness(.7);return;}
            if(Glass95.Enabled&&!OpaqueSurface&&!(DockSurface&&Preferences.Current.DockSolidBackground)){
                bool rimDark=panel||popup||Dark;if(glassEdge==null||edgeDark!=rimDark){edgeDark=rimDark;glassEdge=Glass95.Edge(edgeDark);}Surface.BorderBrush=this is NowBar?(Brush)Brushes.Transparent:glassEdge;Surface.BorderThickness=new Thickness(0);
                Surface.Background=this is NowBar?(enabled?glassTint:solidTint??Glass95.Solid):popup?(enabled?PanelMaterial.PopupSurface(Dark):PanelMaterial.Solid(Dark)):panel?(enabled?PanelMaterial.PanelSurface(Dark,true):PanelMaterial.Solid(Dark)):enabled?PanelMaterial.Surface(Dark,true):Paint.Brush(Dark?"#FF242832":"#FFF1F4FA");
            }else{
                if(glassEdge!=null){glassEdge=null;Surface.BorderBrush=DockSurface?Brushes.Transparent:Paint.Brush("#38FFFFFF");Surface.BorderThickness=new Thickness(DockSurface?0:.7);}
                if(DockSurface)Surface.Background=enabled?LightPanel.DockGlass:Dark?Paint.Brush("#FF29302D"):LightPanel.DockSolid;
                else if(popup)Surface.Background=enabled?PanelMaterial.PopupSurface(Dark):PanelMaterial.Solid(Dark);
                else if(panel){Dark=Glass95.Dark;Surface.Background=enabled?PanelMaterial.PanelSurface(Dark,false):PanelMaterial.Solid(Dark);}
                else Surface.Background=enabled?glassTint:solidTint??Paint.Brush(Dark?"#FF344139":"#FFE0E5E2");
            }
            if(!OpaqueSurface&&!(this is NowBar)&&!DockSurface)PanelMaterial.RefreshCards(Surface);
        }
        internal void RefreshGlassMaterial(){ApplyMaterial();if(DockSurface)Glass95.DockInk(Surface);else Glass95.RefreshColors(Surface);}
        internal void MoveGlassHighlight(Point point)
        {
            if(glassEdge==null||!Motion.Enabled||!Preferences.Current.Hover||!IsVisible||IsDismissing||Surface.ActualWidth<1||Surface.ActualHeight<1)return;
            var next=new Point(Math.Max(0,Math.Min(1,point.X/Surface.ActualWidth)),Math.Max(0,Math.Min(1,point.Y/Surface.ActualHeight)));if(Math.Abs(next.X-glassPointer.X)+Math.Abs(next.Y-glassPointer.Y)<.03)return;glassPointer=next;
            glassEdge.StartPoint=new Point(next.X*.55,next.Y*.25);glassEdge.EndPoint=new Point(.55+next.X*.45,.75+next.Y*.25);
            if(lens!=null)lens.Move(next);
        }
        internal void SetMaterial(Brush tint,Brush solid){glassTint=tint;solidTint=solid;if(new WindowInteropHelper(this).Handle==IntPtr.Zero)Surface.Background=tint;else ApplyMaterial();}
        public void ShowAt(System.Drawing.Rectangle screen,double x,double y,double width,double height)
        {
            ++motion;Surface.BeginAnimation(UIElement.OpacityProperty,null);Surface.Opacity=1;
            Width=width; Height=height;
            new WindowInteropHelper(this).EnsureHandle(); Native.Place(this,screen,x,y,width,height); Show();
            Native.Place(this,screen,x,y,width,height);UpdateLayout(); Native.Round(this,Radius);
        }
        public new void Show()
        {
            bool resume=StableSurface&&!UnifiedFade&&IsVisible&&hiding;++motion;hiding=false;CompleteDismissal(false);if(UnifiedFade)ResetFade();Surface.IsHitTestVisible=true;Surface.BeginAnimation(UIElement.OpacityProperty,null);Surface.Opacity=1;Surface.RenderTransform=Transform.Identity;
            if(StableSurface&&TransitionContent!=null){if(resume&&Motion.Enabled){if(SmoothLauncher)LauncherMotion.Run(TransitionContent,true);else Motion.Launcher(TransitionContent,true);}else{if(SmoothLauncher)LauncherMotion.Cancel(TransitionContent);TransitionContent.BeginAnimation(OpacityProperty,null);TransitionContent.Opacity=1;TransitionContent.RenderTransform=Transform.Identity;}}
            base.Show();
        }
        public void Pop()
        {
            if(TransitionContent==null)TransitionContent=Surface.Child as FrameworkElement;
            if(!Motion.Enabled){if(SmoothLauncher)LauncherMotion.Cancel(TransitionContent);return;}
            if(StableSurface&&TransitionContent!=null){++motion;Surface.RenderTransform=Transform.Identity;Surface.Opacity=1;if(SmoothLauncher)LauncherMotion.Run(TransitionContent,true);else Motion.Launcher(TransitionContent,true);return;}
            ++motion;Surface.RenderTransformOrigin=new Point(.5,.5);var scale=new ScaleTransform(.94,.94);double offset=Math.Min(14,Math.Max(0,Surface.ActualHeight)*.025);var translate=new TranslateTransform(0,offset);var group=new TransformGroup();group.Children.Add(scale);group.Children.Add(translate);Surface.RenderTransform=group;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,Motion.PanelTween(.94,1,310));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Motion.PanelTween(.94,1,310));translate.BeginAnimation(TranslateTransform.YProperty,Motion.PanelTween(offset,0,310));
            Surface.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(150)));
        }
        public new void Hide()
        {
            if(TransitionContent==null)TransitionContent=Surface.Child as FrameworkElement;
            if(closed||!IsVisible){CompleteDismissal(!closed);return;}if(hiding)return;
            if(!Motion.Enabled){base.Hide();CompleteDismissal(true);return;}
            if(UnifiedFade){hiding=true;Surface.IsHitTestVisible=false;int fadeToken=++motion;double from=(double)GetValue(SurfaceFadeProperty);SetValue(SurfaceFadeProperty,0.0);var fade=Motion.Tween(from,0,145);fade.Completed+=(s,e)=>{if(!closed&&fadeToken==motion){base.Hide();hiding=false;Surface.IsHitTestVisible=true;ResetFade();CompleteDismissal(true);}};BeginAnimation(SurfaceFadeProperty,fade);return;}
            if(StableSurface&&TransitionContent!=null){hiding=true;Surface.IsHitTestVisible=false;int contentToken=++motion;var fade=SmoothLauncher?LauncherMotion.Run(TransitionContent,false):Motion.Launcher(TransitionContent,false);fade.Completed+=(s,e)=> {if(!closed&&contentToken==motion){base.Hide();hiding=false;Surface.IsHitTestVisible=true;TransitionContent.BeginAnimation(OpacityProperty,null);TransitionContent.Opacity=1;TransitionContent.RenderTransform=Transform.Identity;CompleteDismissal(true);}};TransitionContent.BeginAnimation(OpacityProperty,fade);return;}
            hiding=true;Surface.IsHitTestVisible=false;int token=++motion;Surface.RenderTransformOrigin=new Point(.85,1);var scale=new ScaleTransform(1,1);var shift=new TranslateTransform();var group=new TransformGroup();group.Children.Add(scale);group.Children.Add(shift);Surface.RenderTransform=group;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.97,TimeSpan.FromMilliseconds(180)));scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.97,TimeSpan.FromMilliseconds(180)));shift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(9,TimeSpan.FromMilliseconds(180)));
            var animation=new DoubleAnimation(0,TimeSpan.FromMilliseconds(180)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseIn}};
            animation.Completed+=(s,e)=> {if(!closed&&token==motion){base.Hide();hiding=false;Surface.IsHitTestVisible=true;Surface.BeginAnimation(UIElement.OpacityProperty,null);Surface.Opacity=1;Surface.RenderTransform=Transform.Identity;CompleteDismissal(true);}};Surface.BeginAnimation(UIElement.OpacityProperty,animation);
        }
        public Task<bool> DismissAsync()
        {
            if(closed)return Task.FromResult(false);if(!IsVisible)return Task.FromResult(true);
            if(dismissal==null)dismissal=new TaskCompletionSource<bool>();var pending=dismissal.Task;Hide();return pending;
        }
        internal void HideImmediately(){++motion;hiding=false;LauncherMotion.Cancel(TransitionContent);if(UnifiedFade)ResetFade();Surface.BeginAnimation(OpacityProperty,null);Surface.Opacity=1;Surface.RenderTransform=Transform.Identity;Surface.IsHitTestVisible=true;if(TransitionContent!=null){TransitionContent.BeginAnimation(OpacityProperty,null);TransitionContent.Opacity=1;TransitionContent.RenderTransform=Transform.Identity;}base.Hide();CompleteDismissal(true);}
        void CompleteDismissal(bool completed) {var pending=dismissal;dismissal=null;if(pending!=null)pending.TrySetResult(completed);}
    }
}
