using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path=System.Windows.Shapes.Path;

namespace OneTaskBar
{
    internal static class TileDevices
    {
        internal static string BudsModel(ShellController shell)
        {
            string model=shell.Buds.ModelName;
            if(!String.IsNullOrEmpty(model))return model;
            string output=shell.Services.OutputName;
            return BudsInfo.IsBuds(output)?output:null;
        }
        // The same model symbol the media player shows for this output.
        internal static async void LoadBudsIcon(string model,Action<ImageSource> show)
        {
            try
            {
                var image=await BackgroundWork.Run(()=>BudsDeviceData.ShortcutIcon(model));
                show(image);
            }
            catch(Exception error)
            {
                AppLog.Once("tile-buds-symbol",error.GetType().Name);
            }
        }
        internal static int? Pc(ShellController shell){return shell.Power.Known?(int?)shell.Power.Level:null;}
        static int? budsLevel;
        static DateTime budsAt=DateTime.MinValue;
        // Headset battery changes slowly, so tiles share one reading for 15 seconds.
        internal static int? Buds(ShellController shell)
        {
            var now=DateTime.UtcNow;
            if(now<budsAt)return budsLevel;
            budsAt=now.AddSeconds(15);
            // App notification value first, then the battery Windows keeps for the device.
            budsLevel=shell.Buds.ConnectedBudsBattery??budsSystem;
            string model=BudsModel(shell);
            if(model==null)
            {
                budsSystem=null;
                return budsLevel;
            }
            if(!budsReading)ReadSystemBattery(shell.Buds.ConnectedBudsContainer,model);
            return budsLevel;
        }
        static int? budsSystem;
        static bool budsReading;
        static async void ReadSystemBattery(Guid? container,string model)
        {
            budsReading=true;
            try{budsSystem=await BackgroundWork.Run(()=>BudsDeviceData.Battery(container,model));}
            catch(Exception error){AppLog.Once("tile-buds-battery",error.GetType().Name);}
            finally{budsReading=false;}
        }
    }

    internal sealed class GaugeTile : InfoTile
    {
        readonly List<Gauge> gauges=new List<Gauge>();
        internal GaugeTile(int columns,int rows):base(columns,rows)
        {
            int slots=columns*rows;
            for(int i=0;i<columns;i++)ColumnDefinitions.Add(new ColumnDefinition());
            for(int i=0;i<rows;i++)RowDefinitions.Add(new RowDefinition());
            for(int i=0;i<slots;i++)
            {
                var gauge=new Gauge(i==0?"laptop":i==1?"headphones":null);
                Grid.SetColumn(gauge,i%columns);
                Grid.SetRow(gauge,i/columns);
                gauges.Add(gauge);
                Children.Add(gauge);
            }
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            gauges[0].Update(TileDevices.Pc(shell));
            gauges[1].Update(TileDevices.Buds(shell));
            string model=TileDevices.BudsModel(shell);
            gauges[1].ShowSymbol(model!=null);
            if(model==shownModel)return;
            shownModel=model;
            if(model==null)return;
            TileDevices.LoadBudsIcon(model,image=>
            {
                if(shownModel==model)gauges[1].SetSymbol(image);
            });
        }
        string shownModel;
        sealed class Gauge : Grid
        {
            readonly TileScheme scheme=TileScheme.Creating;
            Glyph symbol;
            internal void ShowSymbol(bool visible)
            {
                if(symbol!=null)symbol.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
            }
            internal void SetSymbol(ImageSource image)
            {
                if(symbol!=null)symbol.SetDeviceIcon(image);
            }
            readonly Path arc=new Path {StrokeThickness=5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Stretch=Stretch.None,Width=56,Height=56,VerticalAlignment=VerticalAlignment.Top};
            readonly TextBlock value=Label("",11);
            int? shown=-1;
            internal Gauge(string icon)
            {
                Height=60;
                VerticalAlignment=VerticalAlignment.Center;
                Children.Add(new Path {Stroke=TileScheme.Creating.Track,StrokeThickness=5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Stretch=Stretch.None,Width=56,Height=56,VerticalAlignment=VerticalAlignment.Top,Data=Sweep(100)});
                Children.Add(arc);
                if(icon!=null)
                {
                    symbol=new Glyph(icon,20){Color=TileScheme.Creating.Ink,VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,15,0,0)};
                    Children.Add(symbol);
                }
                value.HorizontalAlignment=HorizontalAlignment.Center;
                value.VerticalAlignment=VerticalAlignment.Top;
                value.Margin=new Thickness(0,40,0,0);
                Children.Add(value);
            }
            internal static Geometry Sweep(int percent)
            {
                const double radius=25,start=135,range=270;
                double angle=start+range*Math.Max(0,Math.Min(100,percent))/100.0;
                Func<double,Point> at=d=>new Point(28+radius*Math.Cos(d*Math.PI/180),28+radius*Math.Sin(d*Math.PI/180));
                var shape=new StreamGeometry();
                using(var context=shape.Open())
                {
                    context.BeginFigure(at(start),false,false);
                    if(percent>0)context.ArcTo(at(angle),new Size(radius,radius),0,angle-start>180,SweepDirection.Clockwise,true,false);
                }
                shape.Freeze();
                return shape;
            }
            internal void Update(int? percent)
            {
                if(shown==percent)return;
                shown=percent;
                arc.Data=percent.HasValue?Sweep(percent.Value):null;
                arc.Stroke=percent.HasValue?scheme.Level(percent.Value):null;
                value.Text=percent.HasValue?percent.Value.ToString(CultureInfo.InvariantCulture):"";
            }
        }
    }

    // A rounded level bar with an icon, used by battery lists and device care.
    internal sealed class LevelBar : Grid
    {
        readonly Brush Track=TileScheme.Creating.Track;
        readonly Border fill=new Border {HorizontalAlignment=HorizontalAlignment.Left};
        readonly TextBlock caption,value;
        readonly Glyph glyph;
        double shown=-1;
        internal void SetSymbol(ImageSource image){glyph.SetDeviceIcon(image);}
        internal LevelBar(string icon,string name,double height)
        {
            Height=height;
            fill.CornerRadius=new CornerRadius(height/2,0,0,height/2);
            var track=new Border {Background=Track,CornerRadius=new CornerRadius(height/2)};
            var inner=new Grid();
            inner.Children.Add(fill);
            track.Child=inner;
            Children.Add(track);
            var line=new DockPanel {Margin=new Thickness(height*.35,0,height*.35,0)};
            glyph=new Glyph(icon,height*.62){Color=TileScheme.Creating.Ink,VerticalAlignment=VerticalAlignment.Center};
            DockPanel.SetDock(glyph,Dock.Left);
            line.Children.Add(glyph);
            value=Paint.TextBlock("",12,TileScheme.Creating.Ink);
            DockPanel.SetDock(value,Dock.Right);
            line.Children.Add(value);
            caption=Paint.TextBlock(name??"",12,TileScheme.Creating.Ink);
            caption.Margin=new Thickness(6,0,4,0);
            caption.TextTrimming=TextTrimming.CharacterEllipsis;
            line.Children.Add(caption);
            Children.Add(line);
            SizeChanged+=(s,e)=>Apply();
        }
        void Apply()
        {
            if(shown>=0)fill.Width=Math.Max(0,ActualWidth*shown);
        }
        internal void Update(double? share,string text,Brush color,string name=null)
        {
            if(name!=null&&caption.Text!=name)caption.Text=name;
            if(value.Text!=text)value.Text=text;
            if(fill.Background!=color)fill.Background=color;
            double next=share.HasValue?Math.Max(0,Math.Min(1,share.Value)):0;
            if(next==shown)return;
            shown=next;
            Apply();
        }
    }

    internal sealed class PowerListTile : InfoTile
    {
        readonly LevelBar pc,buds;
        internal PowerListTile(int columns,int rows):base(columns,rows)
        {
            var stack=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12)};
            pc=new LevelBar("laptop",columns==4?"노트북":null,34) {Margin=new Thickness(0,0,0,8)};
            buds=new LevelBar("headphones",columns==4?"Galaxy Buds":null,34);
            stack.Children.Add(pc);
            stack.Children.Add(buds);
            Children.Add(stack);
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            int? level=TileDevices.Pc(shell),other=TileDevices.Buds(shell);
            pc.Update(level/100.0,level.HasValue?level+"%":"--",level.HasValue?Bar(level.Value):null);
            string model=TileDevices.BudsModel(shell);
            buds.Update(other/100.0,other.HasValue?other+"%":model!=null?"--":"연결 안 됨",other.HasValue?Bar(other.Value):null,model!=null&&Wide==4?model:null);
            if(model==shownModel)return;
            shownModel=model;
            if(model==null)return;
            TileDevices.LoadBudsIcon(model,image=>
            {
                if(shownModel==model)buds.SetSymbol(image);
            });
        }
        string shownModel;
        Brush Bar(int percent){return Scheme.Monochrome?Scheme.Fill:Scheme.Level(percent);}
    }

    internal sealed class CareTile : InfoTile
    {
        static readonly Brush StorageFill=Paint.Brush("#FF58B6CF"),MemoryFill=Paint.Brush("#FF4F8BF0");
        readonly LevelBar storage,memory;
        DateTime nextRead=DateTime.MinValue;
        [StructLayout(LayoutKind.Sequential)]
        sealed class MemoryStatus
        {
            public uint Length=64,Load;
            public ulong Total,Available,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended;
        }
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx([In,Out]MemoryStatus status);
        internal CareTile(int columns,int rows):base(columns,rows)
        {
            double bar=rows==2?38:30;
            var bars=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            storage=new LevelBar("computer",null,bar) {Margin=new Thickness(0,0,0,8)};
            memory=new LevelBar("apps",null,bar);
            bars.Children.Add(storage);
            bars.Children.Add(memory);
            var open=Paint.Button(new Glyph("settings",rows==2?30:24){Color=TileScheme.Creating.Ink},()=>Native.Open("ms-settings:storagesense"),"저장공간 설정 열기");
            open.Background=Brushes.Transparent;
            open.Padding=new Thickness(0);
            double size=columns==2&&rows==1?36:44;
            open.Width=open.Height=size;
            open.Resources["ButtonRadius"]=new CornerRadius(size/2);
            if(columns==2&&rows==2)
            {
                open.VerticalAlignment=VerticalAlignment.Top;
                open.Margin=new Thickness(0,14,0,0);
                bars.VerticalAlignment=VerticalAlignment.Bottom;
                bars.Margin=new Thickness(14,0,14,14);
                Children.Add(open);
                Children.Add(bars);
                return;
            }
            var line=new DockPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,14,0)};
            open.Margin=new Thickness(0,0,8,0);
            DockPanel.SetDock(open,Dock.Left);
            line.Children.Add(open);
            line.Children.Add(bars);
            Children.Add(line);
        }
        static string Size(double bytes){return (bytes/1073741824).ToString("0.#",CultureInfo.InvariantCulture)+"GB";}
        internal override void Refresh(ShellController shell,DateTime now)
        {
            if(now<nextRead)return;
            nextRead=now.AddSeconds(30);
            try
            {
                var drive=new DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory));
                double used=drive.TotalSize-drive.TotalFreeSpace;
                storage.Update(used/drive.TotalSize,Size(used)+(Wide==4?" / "+Size(drive.TotalSize):""),Scheme.Monochrome?Scheme.Fill:StorageFill);
                var status=new MemoryStatus();
                if(GlobalMemoryStatusEx(status))
                {
                    double taken=status.Total-status.Available;
                    memory.Update(taken/status.Total,Size(taken)+(Wide==4?" / "+Size(status.Total):""),Scheme.Monochrome?Scheme.Fill:MemoryFill);
                }
            }
            catch(Exception e)
            {
                AppLog.Once("care-tile",e.GetType().Name);
            }
        }
    }
}
