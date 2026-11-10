using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OneTaskBar
{
    internal static class TileTime
    {
        static CultureInfo Korean{get{return Loc.Culture;}}
        // Keeps the time itself on the center line, with AM/PM hanging to its left.
        internal static Grid Centered(TextBlock period,TextBlock clock)
        {
            var line=new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition());
            line.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            line.ColumnDefinitions.Add(new ColumnDefinition());
            period.HorizontalAlignment=HorizontalAlignment.Right;
            Grid.SetColumn(clock,1);
            line.Children.Add(period);
            line.Children.Add(clock);
            return line;
        }
        internal static string Period(DateTime value){return value.ToString("tt",Korean);}
        internal static string Clock(DateTime value){return value.ToString("h:mm",CultureInfo.InvariantCulture);}
        internal static string LongDate(DateTime value){return Loc.Date(value,"M월 d일 dddd");}
        internal static string ShortDate(DateTime value){return Loc.Date(value,"dddd,\nM월 d일");}
    }

    internal sealed class DigitalClockTile : InfoTile
    {
        readonly TextBlock period,clock,date;
        string shown;
        internal DigitalClockTile(int columns,int rows):base(columns,rows)
        {
            bool tall=rows==2,wide=columns==4;
            period=Label("",tall?12:11,Faint);
            clock=Label("",wide?(tall?60:44):tall?38:24);
            clock.FontWeight=FontWeights.SemiBold;
            date=Label("",wide?20:14);
            date.FontWeight=FontWeights.SemiBold;
            var root=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            if(wide&&tall)
            {
                date.HorizontalAlignment=period.HorizontalAlignment=clock.HorizontalAlignment=HorizontalAlignment.Center;
                root.Children.Add(date);
                period.Margin=new Thickness(0,8,0,0);
                root.Children.Add(period);
                root.Children.Add(clock);
                date.Visibility=Visibility.Visible;
            }
            else if(tall)
            {
                date.TextWrapping=TextWrapping.Wrap;
                date.TextTrimming=TextTrimming.None;
                date.Margin=new Thickness(16,16,16,0);
                date.VerticalAlignment=VerticalAlignment.Top;
                Children.Add(date);
                var line=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(16,0,12,14)};
                period.VerticalAlignment=VerticalAlignment.Bottom;
                period.Margin=new Thickness(0,0,4,8);
                line.Children.Add(period);
                line.Children.Add(clock);
                Children.Add(line);
                return;
            }
            else if(wide)
            {
                period.VerticalAlignment=VerticalAlignment.Bottom;
                period.Margin=new Thickness(0,0,6,10);
                root.Children.Add(TileTime.Centered(period,clock));
                date.Visibility=Visibility.Collapsed;
            }
            else
            {
                period.HorizontalAlignment=clock.HorizontalAlignment=HorizontalAlignment.Center;
                root.Children.Add(period);
                root.Children.Add(clock);
                date.Visibility=Visibility.Collapsed;
            }
            Children.Add(root);
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            string text=TileTime.Clock(now);
            if(text==shown)return;
            shown=text;
            clock.Text=text;
            period.Text=TileTime.Period(now);
            date.Text=Wide==4?TileTime.LongDate(now):Loc.Date(now,"M월 d일\ndddd");
        }
    }

    internal sealed class AnalogClockFace : Grid
    {
        readonly RotateTransform hour=new RotateTransform(),minute=new RotateTransform(),second=new RotateTransform();
        readonly Line secondHand;
        int shown=-1;
        internal AnalogClockFace(double size)
        {
            Width=Height=size;
            double center=size/2;
            Children.Add(new Image {Width=size,Height=size,Source=Dial()});
            Children.Add(Hand(center,size*.25,size*.028,TileScheme.Creating.Ink,hour));
            Children.Add(Hand(center,size*.38,size*.022,TileScheme.Creating.Ink,minute));
            secondHand=Hand(center,size*.42,Math.Max(.6,size*.006),Paint.Brush("#FFE5483B"),second);
            Children.Add(secondHand);
        }
        static Line Hand(double center,double length,double thickness,Brush brush,RotateTransform turn)
        {
            turn.CenterX=center;
            turn.CenterY=center;
            return new Line {X1=center,Y1=center,X2=center,Y2=center-length,Stroke=brush,StrokeThickness=thickness,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,RenderTransform=turn,IsHitTestVisible=false};
        }
        static ImageSource Dial()
        {
            var ticks=new GeometryGroup();
            var bold=new GeometryGroup();
            for(int i=0;i<60;i++)
            {
                double angle=i*Math.PI/30,inner=i%5==0?.84:.92;
                var from=new Point(.5+.48*inner*Math.Sin(angle)*1.04,.5-.48*inner*Math.Cos(angle)*1.04);
                var to=new Point(.5+.49*Math.Sin(angle),.5-.49*Math.Cos(angle));
                (i%5==0?bold:ticks).Children.Add(new LineGeometry(from,to));
            }
            var drawing=new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,1,1))));
            drawing.Children.Add(new GeometryDrawing(null,new Pen(TileScheme.Creating.Muted,.004),ticks));
            drawing.Children.Add(new GeometryDrawing(null,new Pen(TileScheme.Creating.Ink,.012),bold));
            var image=new DrawingImage(drawing);
            image.Freeze();
            return image;
        }
        // The second hand is left out on battery so a hidden per-second redraw cannot drain it.
        internal void Update(DateTime time,bool seconds)
        {
            int stamp=seconds?time.Second+time.Minute*60:time.Minute*60-1;
            if(stamp==shown)return;
            shown=stamp;
            hour.Angle=(time.Hour%12+time.Minute/60.0)*30;
            minute.Angle=time.Minute*6;
            secondHand.Visibility=seconds?Visibility.Visible:Visibility.Collapsed;
            if(seconds)second.Angle=time.Second*6;
        }
    }

    internal sealed class AnalogClockTile : InfoTile
    {
        readonly AnalogClockFace face;
        readonly TextBlock date;
        internal AnalogClockTile(int columns,int rows):base(columns,rows)
        {
            if(rows==2)
            {
                face=new AnalogClockFace(Span(2)-12);
                Children.Add(face);
                return;
            }
            face=new AnalogClockFace(72) {HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(8,0,0,0)};
            Children.Add(face);
            date=Label("",13);
            date.FontWeight=FontWeights.SemiBold;
            date.TextWrapping=TextWrapping.Wrap;
            date.TextTrimming=TextTrimming.None;
            date.Margin=new Thickness(92,0,10,0);
            Children.Add(date);
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            face.Update(now,shell.Power.Connected||!shell.Power.Known);
            if(date==null)return;
            string text=TileTime.ShortDate(now);
            if(date.Text!=text)date.Text=text;
        }
    }

    internal sealed class DualClockTile : InfoTile
    {
        readonly Brush SecondPanel=TileScheme.Creating.Track;
        readonly TextBlock[] period=new TextBlock[2],clock=new TextBlock[2],place=new TextBlock[2],detail=new TextBlock[2];
        string shown;
        internal DualClockTile(int columns,int rows):base(columns,rows)
        {
            bool vertical=columns==2&&rows==2;
            if(vertical)
            {
                RowDefinitions.Add(new RowDefinition());
                RowDefinitions.Add(new RowDefinition());
            }
            else
            {
                ColumnDefinitions.Add(new ColumnDefinition());
                ColumnDefinitions.Add(new ColumnDefinition());
            }
            for(int i=0;i<2;i++)
            {
                double main=columns==4&&rows==2?38:columns==2&&rows==1?18:28;
                place[i]=Center(Label("",columns==4&&rows==2?15:11));
                place[i].FontWeight=FontWeights.SemiBold;
                detail[i]=Center(Label("",10));
                period[i]=Label("",10,Faint);
                period[i].VerticalAlignment=VerticalAlignment.Bottom;
                period[i].Margin=new Thickness(0,0,3,main*.16);
                clock[i]=Label("",main);
                clock[i].FontWeight=FontWeights.SemiBold;
                var stack=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
                stack.Children.Add(place[i]);
                stack.Children.Add(TileTime.Centered(period[i],clock[i]));
                if(rows==2||columns==4)stack.Children.Add(detail[i]);
                var panel=new Border {Child=stack,Background=i==1?SecondPanel:null};
                if(vertical)Grid.SetRow(panel,i);
                else Grid.SetColumn(panel,i);
                Children.Add(panel);
            }
        }
        static TextBlock Center(TextBlock text)
        {
            text.HorizontalAlignment=HorizontalAlignment.Center;
            return text;
        }
        static DateTime Other(DateTime now,out string label,out string gap)
        {
            string id=Preferences.Current.DualClockZone;
            label=DualClockZones.Name(id);
            try
            {
                var zone=TimeZoneInfo.FindSystemTimeZoneById(id);
                var there=TimeZoneInfo.ConvertTime(now,TimeZoneInfo.Local,zone);
                double hours=(there-now).TotalHours;
                gap=(hours>=0?"+":"")+hours.ToString("0.#",CultureInfo.InvariantCulture)+"시간";
                return there;
            }
            catch(Exception e)
            {
                AppLog.Once("dual-clock",e.GetType().Name);
                gap="";
                return now;
            }
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            string text=TileTime.Clock(now)+Preferences.Current.DualClockZone;
            if(text==shown)return;
            shown=text;
            string label,gap;
            var there=Other(now,out label,out gap);
            Show(0,now,DualClockZones.Local(),Rows2()?"현재 시간대":TileTime.LongDate(now));
            Show(1,there,label,Rows2()?gap:TileTime.LongDate(there));
        }
        bool Rows2(){return Tall==2;}
        void Show(int index,DateTime time,string name,string note)
        {
            place[index].Text=name;
            period[index].Text=TileTime.Period(time);
            clock[index].Text=TileTime.Clock(time);
            detail[index].Text=note;
        }
    }

    internal static class DualClockZones
    {
        internal static readonly string[][] All=
        {
            new[]{"GMT Standard Time","런던"},
            new[]{"Eastern Standard Time","뉴욕"},
            new[]{"Pacific Standard Time","로스앤젤레스"},
            new[]{"Romance Standard Time","파리"},
            new[]{"Tokyo Standard Time","도쿄"},
            new[]{"China Standard Time","베이징"},
            new[]{"AUS Eastern Standard Time","시드니"},
            new[]{"UTC","UTC"},
            new[]{"Korea Standard Time","서울"}
        };
        internal static string Local()
        {
            string id=TimeZoneInfo.Local.Id,name=Name(id);
            return name==id?"현재 위치":name;
        }
        internal static string Name(string id)
        {
            foreach(var zone in All)
            {
                if(zone[0]==id)return zone[1];
            }
            return id;
        }
    }
}
