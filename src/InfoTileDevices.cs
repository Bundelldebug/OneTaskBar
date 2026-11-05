using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path=System.Windows.Shapes.Path;

namespace OneTaskBar
{
    // One connected device with a battery reading, shown by the battery tiles.
    internal sealed class DeviceLevel
    {
        internal string Key,Name,Icon;
        internal bool Buds;
        internal int Level;
    }

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

        const int ReadSeconds=10;
        sealed class SystemReading
        {
            internal int? Level;
            internal DateTime ChangedAt;
        }
        static readonly Dictionary<string,SystemReading> system=new Dictionary<string,SystemReading>(StringComparer.OrdinalIgnoreCase);
        static List<DeviceLevel> levels=new List<DeviceLevel>();
        static DateTime readAt=DateTime.MinValue;
        static bool reading;
        internal static void ReadSoon(){readAt=DateTime.MinValue;}
        internal static int ConnectedCount {get{return levels.Count;}}
        // Tiles share one list. Windows' value is reread every few seconds while a tile is showing,
        // and a watcher update from a connecting or changing device asks for it at once.
        internal static List<DeviceLevel> Connected(ShellController shell)
        {
            var now=DateTime.UtcNow;
            if(now<readAt)return levels;
            readAt=now.AddSeconds(ReadSeconds);
            var peers=shell.Buds.ConnectedPeers();
            var next=new List<DeviceLevel>();
            foreach(var peer in peers)
            {
                bool buds=BudsInfo.IsBuds(peer.Name);
                string key=PeerKey(peer);
                SystemReading stored;
                system.TryGetValue(key,out stored);
                int? fromWindows=peer.Device!=null?BudsInfo.Battery(peer.Device.Properties):null;
                if(!fromWindows.HasValue&&stored!=null)fromWindows=stored.Level;
                int? level=fromWindows;
                if(buds)
                {
                    DateTime appAt;
                    int? fromApp=shell.Buds.AppBatteryFor(peer.Name,out appAt);
                    level=Freshest(fromApp,appAt,fromWindows,stored!=null?stored.ChangedAt:DateTime.MinValue,now);
                }
                if(level.HasValue)next.Add(new DeviceLevel {Key=key,Name=peer.Name,Icon=buds?"headphones":peer.Icon,Buds=buds,Level=level.Value});
            }
            string output=shell.Services.OutputName;
            if(peers.Count==0&&BudsInfo.IsBuds(output))
            {
                SystemReading stored;
                if(system.TryGetValue(output,out stored)&&stored.Level.HasValue)next.Add(new DeviceLevel {Key=output,Name=output,Icon="headphones",Buds=true,Level=stored.Level.Value});
            }
            levels=next;
            if(!reading)ReadWindowsLevels(peers.Select(p=>Tuple.Create(PeerKey(p),p.Container,p.Name)).Concat(peers.Count==0&&BudsInfo.IsBuds(output)?new[]{Tuple.Create(output,(Guid?)null,output)}:new Tuple<string,Guid?,string>[0]).ToList());
            return levels;
        }
        static string PeerKey(BluetoothPeer peer){return peer.Container.HasValue?peer.Container.Value.ToString("N"):BudsConnectionTracker.Normalize(peer.Name);}
        // The app's notification is used until Windows reports a newer change. The app sends
        // nothing between notifications, so an old one must not hide a live Windows value.
        internal static int? Freshest(int? app,DateTime appAt,int? windows,DateTime windowsChangedAt,DateTime now)
        {
            if(!app.HasValue)return windows;
            if((now-appAt).TotalMinutes>=60)return windows;
            return windows.HasValue&&windowsChangedAt>appAt?windows:app;
        }
        static async void ReadWindowsLevels(List<Tuple<string,Guid?,string>> targets)
        {
            if(targets.Count==0)return;
            reading=true;
            try
            {
                var read=await BackgroundWork.Run(()=>targets.Select(t=>Tuple.Create(t.Item1,BudsDeviceData.Battery(t.Item2,t.Item3))).ToList());
                bool changed=false;
                foreach(var item in read)
                {
                    SystemReading stored;
                    if(!system.TryGetValue(item.Item1,out stored))
                    {
                        stored=new SystemReading();
                        system[item.Item1]=stored;
                    }
                    if(stored.Level==item.Item2)continue;
                    stored.Level=item.Item2;
                    stored.ChangedAt=DateTime.UtcNow;
                    changed=true;
                }
                if(changed)ReadSoon();
            }
            catch(Exception error){AppLog.Once("tile-buds-battery",error.GetType().Name);}
            finally{reading=false;}
        }
        internal static string Signature(IEnumerable<DeviceLevel> devices)
        {
            return String.Join("|",devices.Select(d=>d.Key));
        }
    }

    internal sealed class GaugeTile : InfoTile
    {
        readonly System.Windows.Controls.Primitives.UniformGrid grid=new System.Windows.Controls.Primitives.UniformGrid {HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Center};
        readonly List<Gauge> gauges=new List<Gauge>();
        readonly TextBlock none=Label("배터리 정보 없음",11,Faint);
        readonly int columns,slots;
        string shownDevices;
        internal int Shown {get{return gauges.Count;}}
        internal GaugeTile(int columns,int rows):base(columns,rows)
        {
            this.columns=columns;
            slots=columns*rows;
            Children.Add(grid);
            none.HorizontalAlignment=HorizontalAlignment.Center;
            none.VerticalAlignment=VerticalAlignment.Center;
            none.Visibility=Visibility.Collapsed;
            Children.Add(none);
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            int? pc=TileDevices.Pc(shell);
            var devices=TileDevices.Connected(shell).Take(Math.Max(0,slots-(pc.HasValue?1:0))).ToList();
            string layout=(pc.HasValue?"pc|":"")+TileDevices.Signature(devices);
            if(layout!=shownDevices)
            {
                shownDevices=layout;
                grid.Children.Clear();
                gauges.Clear();
                if(pc.HasValue)Add("laptop",null);
                foreach(var device in devices)Add(device.Icon,device.Buds?device.Name:null);
                int count=Math.Max(1,gauges.Count);
                // Symmetric rows, so one or two devices sit centered instead of leaving empty rings.
                grid.Columns=Math.Min(count,columns);
                grid.Rows=(int)Math.Ceiling(count/(double)grid.Columns);
                none.Visibility=gauges.Count==0?Visibility.Visible:Visibility.Collapsed;
            }
            int index=0;
            if(pc.HasValue)gauges[index++].Update(pc);
            foreach(var device in devices)gauges[index++].Update(device.Level);
        }
        void Add(string icon,string budsModel)
        {
            var gauge=new Gauge(icon);
            gauges.Add(gauge);
            grid.Children.Add(gauge);
            if(budsModel!=null)TileDevices.LoadBudsIcon(budsModel,gauge.SetSymbol);
        }
        sealed class Gauge : Grid
        {
            readonly TileScheme scheme=TileScheme.Creating;
            Glyph symbol;
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
        readonly Grid inner;
        double shown=-1;
        internal void SetSymbol(ImageSource image){glyph.SetDeviceIcon(image);}
        internal LevelBar(string icon,string name,double height)
        {
            Height=height;
            
            fill.CornerRadius=new CornerRadius(height/2);
            var track=new Border {Background=Track,CornerRadius=new CornerRadius(height/2)};
            inner=new Grid();
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
            if(ActualWidth<=0)return;
            // A square fill end would stick out past the round track at full charge.
            inner.Clip=new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight),ActualHeight/2,ActualHeight/2);
            // The fill keeps a round end at every level, so it never meets the track in a hard edge.
            if(shown>=0)fill.Width=shown<=0?0:Math.Max(ActualHeight,ActualWidth*shown);
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
        readonly StackPanel stack=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12)};
        readonly List<LevelBar> bars=new List<LevelBar>();
        readonly TextBlock none=Label("배터리 정보 없음",11,Faint);
        const int Slots=3;
        string shownDevices;
        internal int Shown {get{return bars.Count;}}
        internal PowerListTile(int columns,int rows):base(columns,rows)
        {
            Children.Add(stack);
            none.HorizontalAlignment=HorizontalAlignment.Center;
            none.VerticalAlignment=VerticalAlignment.Center;
            none.Visibility=Visibility.Collapsed;
            Children.Add(none);
        }
        internal override void Refresh(ShellController shell,DateTime now)
        {
            int? pc=TileDevices.Pc(shell);
            var devices=TileDevices.Connected(shell).Take(Slots-(pc.HasValue?1:0)).ToList();
            string layout=(pc.HasValue?"pc|":"")+TileDevices.Signature(devices);
            if(layout!=shownDevices)
            {
                shownDevices=layout;
                stack.Children.Clear();
                bars.Clear();
                int count=devices.Count+(pc.HasValue?1:0);
                double height=count>=3?28:34;
                if(pc.HasValue)Add("laptop",Wide==4?"노트북":null,height,null);
                foreach(var device in devices)Add(device.Icon,Wide==4?device.Name:null,height,device.Buds?device.Name:null);
                none.Visibility=count==0?Visibility.Visible:Visibility.Collapsed;
            }
            int index=0;
            if(pc.HasValue)
            {
                bars[index++].Update(pc/100.0,pc+"%",Bar(pc.Value));
            }
            foreach(var device in devices)bars[index++].Update(device.Level/100.0,device.Level+"%",Bar(device.Level));
        }
        void Add(string icon,string name,double height,string budsModel)
        {
            var bar=new LevelBar(icon,name,height);
            if(bars.Count>0)bar.Margin=new Thickness(0,8,0,0);
            bars.Add(bar);
            stack.Children.Add(bar);
            if(budsModel!=null)TileDevices.LoadBudsIcon(budsModel,bar.SetSymbol);
        }
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
