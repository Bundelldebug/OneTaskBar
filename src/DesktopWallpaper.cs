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
        // The wallpaper file of one monitor and the desktop's background color, from the same source Explorer uses. This also
        // follows a slideshow, Windows Spotlight and a per-monitor picture, where the classic setting is empty or stale.
        internal static string CurrentFile(System.Drawing.Rectangle screen,out Color background)
        {
            string path=null;uint color=0;object instance=null;
            try{
                instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")));
                var desktop=(IWallpaper)instance;uint count;desktop.GetMonitorDevicePathCount(out count);long best=-1;
                for(uint n=0;n<count;n++){
                    // One monitor that cannot be asked (disconnected, no picture of its own) must not hide the others.
                    try{
                        string id;Native.RectI bounds;desktop.GetMonitorDevicePathAt(n,out id);desktop.GetMonitorRECT(id,out bounds);
                        long area=(long)Math.Max(0,Math.Min(screen.Right,bounds.Right)-Math.Max(screen.Left,bounds.Left))*Math.Max(0,Math.Min(screen.Bottom,bounds.Bottom)-Math.Max(screen.Top,bounds.Top));
                        if(area<=best)continue;
                        string file;desktop.GetWallpaper(id,out file);
                        if(!String.IsNullOrWhiteSpace(file)){best=area;path=file;}
                    }catch(Exception error){AppLog.Once("wallpaper-monitor-query",error.GetType().Name);}
                }
                // A single picture for every monitor is answered without a monitor name.
                if(String.IsNullOrWhiteSpace(path)){string any;desktop.GetWallpaper(null,out any);if(!String.IsNullOrWhiteSpace(any))path=any;}
            }catch(Exception error){AppLog.Once("wallpaper-file-query",error.GetType().Name);}
            try{if(instance!=null)((IWallpaper)instance).GetBackgroundColor(out color);}catch(Exception){}
            finally{if(instance!=null&&Marshal.IsComObject(instance))Marshal.ReleaseComObject(instance);}
            if(String.IsNullOrWhiteSpace(path)||!File.Exists(path)){
                var buffer=new System.Text.StringBuilder(32768);
                if(SystemParametersInfo(0x73,buffer.Capacity,buffer,0)&&File.Exists(buffer.ToString()))path=buffer.ToString();
            }
            // Windows keeps the picture it is showing here, whatever the source (theme, slideshow, Spotlight).
            if(String.IsNullOrWhiteSpace(path)||!File.Exists(path)){
                string cached=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Microsoft\Windows\Themes\TranscodedWallpaper");
                if(File.Exists(cached))path=cached;
            }
            background=Color.FromRgb((byte)color,(byte)(color>>8),(byte)(color>>16));
            return path;
        }
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
            string engine=WallpaperEngineSource.Current(screen);
            if(engine!=null){path=engine;position=4;}
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
