using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Path=System.Windows.Shapes.Path;

namespace OneTaskBar
{
    // Information tiles shared by the collapsed fullscreen media screen and desktop widgets.
    // Each tile has a fixed design size for its grid span; its host scales it.
    // Refresh only touches changed values, so an unchanged tile causes no layout.
    internal abstract class InfoTile : Grid
    {
        internal const double Cell=88,Gap=14;
        internal static double Span(int cells){return cells*Cell+(cells-1)*Gap;}
        // One-row wide tiles are pills; everything else keeps the rounded card corner.
        internal static bool Pill(int columns,int rows){return rows==1&&columns>1;}
        internal static double Radius(int columns,int rows){return Pill(columns,rows)?Span(rows)/2:24;}
        protected static Brush Faint {get{return TileScheme.Creating.Muted;}}
        protected readonly TileScheme Scheme=TileScheme.Creating;
        protected readonly int Wide,Tall;
        protected InfoTile(int columns,int rows)
        {
            Wide=columns;
            Tall=rows;
            Width=Span(columns);
            Height=Span(rows);
            double radius=Radius(columns,rows);
            var corners=new RectangleGeometry(new Rect(0,0,Width,Height),radius,radius);
            corners.Freeze();
            Clip=corners;
        }
        protected static TextBlock Label(string text,double size,Brush color=null)
        {
            var label=Paint.TextBlock(text,size,color??TileScheme.Creating.Ink);
            label.TextTrimming=TextTrimming.CharacterEllipsis;
            return label;
        }
        internal abstract void Refresh(ShellController shell,DateTime now);
        internal virtual void Retire(){}
    }

    internal sealed class InfoTileFamily
    {
        internal string Key,Title,Icon;
        internal string[] Spans;
    }

    // A token such as "clock-digital:2x1" names a tile family and its grid span.
    internal static class InfoTileCatalog
    {
        internal static readonly InfoTileFamily[] Families=
        {
            new InfoTileFamily {Key="weather",Title="날씨",Icon="brightness",Spans=new[]{"2x2"}},
            new InfoTileFamily {Key="moon",Title="달의 위상",Icon="moon",Spans=new[]{"1x1"}},
            new InfoTileFamily {Key="rain",Title="강수 확률",Icon="brightness",Spans=new[]{"1x1"}},
            new InfoTileFamily {Key="gauges",Title="배터리 상태(원형)",Icon="battery",Spans=new[]{"2x1","2x2","4x1"}},
            new InfoTileFamily {Key="power-list",Title="배터리 상태(목록형)",Icon="battery",Spans=new[]{"2x2","4x2"}},
            new InfoTileFamily {Key="calendar",Title="달력",Icon="calendar",Spans=new[]{"2x2"}},
            new InfoTileFamily {Key="todo",Title="리마인더",Icon="focus",Spans=new[]{"2x1","2x2","4x2"}},
            new InfoTileFamily {Key="clock-digital",Title="디지털 시계",Icon="focus",Spans=new[]{"2x1","2x2","4x1","4x2"}},
            new InfoTileFamily {Key="clock-analog",Title="아날로그 시계",Icon="focus",Spans=new[]{"2x1","2x2"}},
            new InfoTileFamily {Key="clock-dual",Title="듀얼 시계",Icon="focus",Spans=new[]{"2x1","2x2","4x1","4x2"}},
            new InfoTileFamily {Key="player",Title="플레이어",Icon="play",Spans=new[]{"2x2","4x1"}},
            new InfoTileFamily {Key="care",Title="디바이스 케어",Icon="computer",Spans=new[]{"2x1","2x2","4x1","4x2"}},
            new InfoTileFamily {Key="search",Title="검색",Icon="search",Spans=new[]{"2x1","4x1"}}
        };
        internal static readonly string[] Tokens=Families.SelectMany(f=>f.Spans.Select(s=>f.Key+":"+s)).ToArray();
        internal static bool Known(string token){return token!=null&&Tokens.Contains(token);}
        internal static string FamilyKey(string token){return token.Substring(0,token.IndexOf(':'));}
        internal static string SpanOf(string token){return token.Substring(token.IndexOf(':')+1);}
        internal static InfoTileFamily Family(string token){var key=FamilyKey(token);return Families.First(f=>f.Key==key);}
        internal static int Columns(string token){return SpanOf(token)[0]-'0';}
        internal static int Rows(string token){return SpanOf(token)[2]-'0';}
        internal static string SpanLabel(string token){return SpanOf(token).Replace('x','×');}
        internal static string Title(string token){return Family(token).Title+" "+SpanLabel(token);}
        internal static Size Design(string token){return new Size(InfoTile.Span(Columns(token)),InfoTile.Span(Rows(token)));}
        internal static InfoTile Create(string token,bool acceptsText,ShellController shell,TileScheme scheme)
        {
            TileScheme.Creating=scheme;
            int columns=Columns(token),rows=Rows(token);
            switch(FamilyKey(token))
            {
                case "weather":return new WeatherTile(columns,rows);
                case "moon":return new MoonTile(columns,rows);
                case "rain":return new RainTile(columns,rows);
                case "gauges":return new GaugeTile(columns,rows);
                case "power-list":return new PowerListTile(columns,rows);
                case "calendar":return new CalendarTile(columns,rows);
                case "todo":return new TodoTile(columns,rows,acceptsText);
                case "clock-digital":return new DigitalClockTile(columns,rows);
                case "clock-analog":return new AnalogClockTile(columns,rows);
                case "clock-dual":return new DualClockTile(columns,rows);
                case "player":return new PlayerTile(columns,rows,shell);
                case "care":return new CareTile(columns,rows);
                case "search":return new SearchTile(columns,rows,shell);
                default:return null;
            }
        }
    }

    internal sealed class CalendarTile : InfoTile
    {
        readonly TextBlock month=Label("",14);
        readonly TextBlock[] days=new TextBlock[42];
        readonly Border[] cells=new Border[42];
        readonly Brush Today=TileScheme.Creating.Track;
        DateTime shown=DateTime.MinValue;
        readonly UniformGrid grid;
        internal static int Weeks(DateTime first){return (Offset(first)+DateTime.DaysInMonth(first.Year,first.Month)+6)/7;}
        internal CalendarTile(int columns,int rows):base(columns,rows)
        {
            var stack=new StackPanel {Margin=new Thickness(8)};
            month.FontWeight=FontWeights.SemiBold;
            month.HorizontalAlignment=HorizontalAlignment.Center;
            month.Margin=new Thickness(0,0,0,4);
            stack.Children.Add(month);
            var names=new UniformGrid {Columns=7};
            foreach(string name in new[]{"일","월","화","수","목","금","토"})
            {
                var label=Label(name,9.5,Faint);
                label.HorizontalAlignment=HorizontalAlignment.Center;
                names.Children.Add(label);
            }
            stack.Children.Add(names);
            grid=new UniformGrid {Columns=7,Rows=6,Height=Span(2)-62,Margin=new Thickness(0,2,0,0)};
            for(int i=0;i<days.Length;i++)
            {
                days[i]=Label("",10);
                days[i].HorizontalAlignment=HorizontalAlignment.Center;
                cells[i]=new Border {CornerRadius=new CornerRadius(5),Child=days[i]};
                grid.Children.Add(cells[i]);
            }
            stack.Children.Add(grid);
            Children.Add(stack);
        }
        internal static int Offset(DateTime first){return (int)first.DayOfWeek;}
        internal override void Refresh(ShellController shell,DateTime now)
        {
            if(shown==now.Date)return;
            shown=now.Date;
            month.Text=now.Month+"월";
            var first=new DateTime(now.Year,now.Month,1);
            int offset=Offset(first),count=DateTime.DaysInMonth(now.Year,now.Month),weeks=Weeks(first);
            // Only the month's weeks take space, so rows spread evenly with no gap below.
            grid.Rows=weeks;
            for(int i=0;i<days.Length;i++)
            {
                cells[i].Visibility=i<weeks*7?Visibility.Visible:Visibility.Collapsed;
                int day=i-offset+1;
                bool real=day>=1&&day<=count;
                days[i].Text=real?day.ToString(CultureInfo.InvariantCulture):"";
                cells[i].Background=real&&day==now.Day?Today:null;
            }
        }
    }

    internal sealed class MoonTile : InfoTile
    {
        static readonly DateTime NewMoon=new DateTime(2000,1,6,18,14,0,DateTimeKind.Utc);
        const double Synodic=29.530588853;
        readonly Path lit=new Path {Fill=TileScheme.Creating.Ink};
        int shownDay=-1;
        internal MoonTile(int columns,int rows):base(columns,rows)
        {
            Children.Add(new Ellipse {Width=64,Height=64,Fill=TileScheme.Creating.Track});
            lit.Width=lit.Height=64;
            lit.Stretch=Stretch.None;
            Children.Add(lit);
        }
        // 0 is new moon, .5 is full moon.
        internal static double Phase(DateTime utc)
        {
            double cycles=(utc-NewMoon).TotalDays/Synodic;
            return cycles-Math.Floor(cycles);
        }
        internal static Geometry Shape(double phase,double radius)
        {
            bool waning=phase>.5;
            double fold=waning?1-phase:phase;
            double inner=radius*Math.Abs(Math.Cos(2*Math.PI*fold));
            var shape=new StreamGeometry();
            using(var context=shape.Open())
            {
                var top=new Point(radius,0);
                var bottom=new Point(radius,radius*2);
                context.BeginFigure(top,true,true);
                context.ArcTo(bottom,new Size(radius,radius),0,false,SweepDirection.Clockwise,true,false);
                context.ArcTo(top,new Size(Math.Max(.01,inner),radius),0,false,fold<.25?SweepDirection.Counterclockwise:SweepDirection.Clockwise,true,false);
            }
            if(waning)shape.Transform=new ScaleTransform(-1,1,radius,radius);
            shape.Freeze();
            return shape;
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            if(shownDay==now.DayOfYear)return;
            shownDay=now.DayOfYear;
            lit.Data=Shape(Phase(now.ToUniversalTime()),32);
        }
    }

    internal sealed class RainTile : InfoTile
    {
        readonly TextBlock percent=Label("--",15);
        WeatherReport shown;
        internal RainTile(int columns,int rows):base(columns,rows)
        {
            var stack=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            stack.Children.Add(new Path {Width=28,Height=28,Stretch=Stretch.Uniform,Fill=TileScheme.Creating.Ink,
                Data=Geometry.Parse("M12,2 A10,10 0 0 0 2,12 L22,12 A10,10 0 0 0 12,2 Z M11,12 L13,12 L13,19 A3,3 0 0 1 7,19 L9,19 A1,1 0 0 0 11,19 Z")});
            percent.HorizontalAlignment=HorizontalAlignment.Center;
            percent.FontWeight=FontWeights.SemiBold;
            percent.Margin=new Thickness(0,4,0,0);
            stack.Children.Add(percent);
            Children.Add(stack);
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            WeatherService.Request();
            var report=WeatherService.Current;
            if(report==shown)return;
            shown=report;
            percent.Text=report==null?"--":report.RainPercent+"%";
        }
    }

    internal sealed class WeatherTile : InfoTile
    {
        readonly TextBlock temperature=Label("--°",24),summary=Label("",11),place=Label("",10,Faint);
        readonly Path symbol=new Path {Width=40,Height=40,Stretch=Stretch.Uniform,Fill=TileScheme.Creating.Ink,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,14,14,0)};
        WeatherReport shown;
        string shownPlace;
        bool loaded;
        internal WeatherTile(int columns,int rows):base(columns,rows)
        {
            temperature.FontWeight=FontWeights.SemiBold;
            summary.TextWrapping=TextWrapping.Wrap;
            summary.TextTrimming=TextTrimming.None;
            summary.Margin=new Thickness(0,2,0,4);
            var stack=new StackPanel {VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(14)};
            stack.Children.Add(temperature);
            stack.Children.Add(summary);
            stack.Children.Add(place);
            Children.Add(stack);
            Children.Add(symbol);
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            WeatherService.Request();
            var report=WeatherService.Current;
            string where=Preferences.Current.WeatherPlace;
            if(report==shown&&where==shownPlace&&loaded)return;
            shown=report;
            shownPlace=where;
            loaded=true;
            symbol.Data=WeatherService.Symbol(report);
            temperature.Text=report==null?"--°":Math.Round(report.Temperature).ToString(CultureInfo.InvariantCulture)+"°";
            summary.Text=report!=null?report.Summary:WeatherService.Configured?"날씨를 불러오는 중입니다":"설정에서 위치를 입력하세요";
            place.Text=where??"";
        }
    }
}
