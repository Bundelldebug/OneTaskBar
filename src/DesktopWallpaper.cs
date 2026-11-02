using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class DesktopWallpaper
    {
        [ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IWallpaper
        {
            void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]string file);
            void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]out string file);
            void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)]out string monitor);
            void GetMonitorDevicePathCount(out uint count);
            void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)]string monitor,out Native.RectI bounds);
            void SetBackgroundColor(uint color);
            void GetBackgroundColor(out uint color);
            void SetPosition(int position);
            void GetPosition(out int position);
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        static extern bool SystemParametersInfo(int action,int length,System.Text.StringBuilder result,int flags);
        internal sealed class Snapshot
        {
            internal Brush Brush;
            internal bool HasImage;
        }
        internal static Snapshot Read(System.Drawing.Rectangle screen)
        {
            string path=null;int position=4;uint background=0;object instance=null;
            var virtualBounds=screen;
            try{
                instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")));
                var desktop=(IWallpaper)instance;uint count;desktop.GetMonitorDevicePathCount(out count);
                long best=-1;
                for(uint n=0;n<count;n++){
                    string id;Native.RectI bounds;desktop.GetMonitorDevicePathAt(n,out id);desktop.GetMonitorRECT(id,out bounds);
                    virtualBounds=System.Drawing.Rectangle.Union(virtualBounds,System.Drawing.Rectangle.FromLTRB(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom));
                    long area=(long)Math.Max(0,Math.Min(screen.Right,bounds.Right)-Math.Max(screen.Left,bounds.Left))*
                        Math.Max(0,Math.Min(screen.Bottom,bounds.Bottom)-Math.Max(screen.Top,bounds.Top));
                    if(area<=best)continue;
                    best=area;desktop.GetWallpaper(id,out path);
                }
                desktop.GetPosition(out position);desktop.GetBackgroundColor(out background);
            }catch(Exception error){AppLog.Once("media-wallpaper-query",error.GetType().Name);}
            finally{if(instance!=null&&Marshal.IsComObject(instance))Marshal.ReleaseComObject(instance);}
            if(String.IsNullOrWhiteSpace(path)){
                var buffer=new System.Text.StringBuilder(32768);
                if(SystemParametersInfo(0x73,buffer.Capacity,buffer,0))path=buffer.ToString();
            }
            var color=Color.FromRgb((byte)background,(byte)(background>>8),(byte)(background>>16));
            var snapshot=new Snapshot {Brush=FrozenColor(color)};
            try{
                if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||path.StartsWith("\\\\",StringComparison.Ordinal)||!File.Exists(path)||new FileInfo(path).Length>67108864)return snapshot;
                var image=new BitmapImage();int originalWidth,originalHeight;
                using(var stream=File.OpenRead(path)){
                    var metadata=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None).Frames[0];
                    originalWidth=metadata.PixelWidth;originalHeight=metadata.PixelHeight;
                    stream.Position=0;
                    image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
                    if(Math.Max(originalWidth,originalHeight)>2560){if(originalWidth>=originalHeight)image.DecodePixelWidth=2560;else image.DecodePixelHeight=2560;}
                    image.StreamSource=stream;image.EndInit();image.Freeze();
                }
                snapshot.Brush=Compose(image,color,position,screen,originalWidth,originalHeight,virtualBounds);snapshot.HasImage=true;
            }catch(Exception error){AppLog.Once("media-wallpaper-image",error.GetType().Name);}
            return snapshot;
        }
        static Brush FrozenColor(Color color){var brush=new SolidColorBrush(color);brush.Freeze();return brush;}
        internal static Brush Compose(BitmapSource image,Color color,int position,System.Drawing.Rectangle screen,int originalWidth=0,int originalHeight=0,System.Drawing.Rectangle virtualBounds=new System.Drawing.Rectangle())
        {
            var size=new Size(Math.Max(1,screen.Width),Math.Max(1,screen.Height));
            var group=new DrawingGroup();
            group.Children.Add(new GeometryDrawing(FrozenColor(color),null,new RectangleGeometry(new Rect(size))));
            var picture=new ImageBrush(image) {Stretch=position==3?Stretch.Uniform:position==2?Stretch.Fill:Stretch.UniformToFill};
            double iw=originalWidth>0?originalWidth:image.PixelWidth,ih=originalHeight>0?originalHeight:image.PixelHeight;
            if(position==0||position==1){
                picture.Stretch=Stretch.Fill;picture.ViewportUnits=BrushMappingMode.Absolute;
                picture.Viewport=new Rect((size.Width-iw)/2,(size.Height-ih)/2,iw,ih);
                if(position==1){picture.TileMode=TileMode.Tile;picture.Viewport=new Rect(0,0,iw,ih);}
            }
            if(position==5&&virtualBounds.Width>0&&virtualBounds.Height>0){
                double scale=Math.Max(virtualBounds.Width/iw,virtualBounds.Height/ih),sw=iw*scale,sh=ih*scale;
                double left=(virtualBounds.Width-sw)/2,top=(virtualBounds.Height-sh)/2;
                picture.Stretch=Stretch.Fill;picture.ViewboxUnits=BrushMappingMode.RelativeToBoundingBox;
                picture.Viewbox=new Rect((screen.X-virtualBounds.X-left)/sw,(screen.Y-virtualBounds.Y-top)/sh,size.Width/sw,size.Height/sh);
            }
            picture.Freeze();group.Children.Add(new GeometryDrawing(picture,null,new RectangleGeometry(new Rect(size))));
            group.Freeze();var brush=new DrawingBrush(group) {Stretch=Stretch.Fill};brush.Freeze();return brush;
        }
    }
}
