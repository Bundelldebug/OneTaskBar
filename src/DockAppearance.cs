using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class DockAppearance
    {
        static string requested,imageFile,colorKey;static Brush image,solid;static Brush imageInk=Paint.Brush("#FF19221E");static int revision;
        internal static int Mode {get{return Preferences.Current.DockBackgroundKind==2?2:Preferences.Current.DockBackgroundKind==1||Preferences.Current.DockSolidBackground?1:0;}}
        internal static bool Custom {get{return Preferences.Current.DockSolidBackground||Preferences.Current.DockBackgroundKind==1||Preferences.Current.DockBackgroundKind==2;}}
        internal static bool TryColor(string value,out Color color)
        {
            color=Color.FromRgb(186,193,199);try{if(String.IsNullOrEmpty(value)||value.Length!=7&&value.Length!=9||value[0]!='#')return false;color=(Color)ColorConverter.ConvertFromString(value);color.A=255;return true;}catch{return false;}
        }
        internal static Brush Solid
        {
            get{string key=Preferences.Current.DockBackgroundColor;Color color;if(!TryColor(key,out color))color=Color.FromRgb(186,193,199);if(colorKey!=key||solid==null){colorKey=key;var brush=new SolidColorBrush(color);brush.Freeze();solid=color==Color.FromRgb(186,193,199)?LightPanel.DockSolid:brush;}return solid;}
        }
        internal static Brush Background
        {
            get{if(Preferences.Current.DockBackgroundKind!=2)return Solid;string file=Preferences.Current.DockBackgroundImage;if(!String.Equals(requested,file,StringComparison.Ordinal)){requested=file;Load(file,++revision);}return image!=null&&String.Equals(imageFile,file,StringComparison.Ordinal)?image:Solid;}
        }
        internal static Brush Foreground
        {
            get{if(Preferences.Current.DockBackgroundKind==2&&image!=null&&imageFile==Preferences.Current.DockBackgroundImage)return imageInk;var color=((SolidColorBrush)Solid).Color;return Ink(color.R*.2126+color.G*.7152+color.B*.0722);}
        }
        static Brush Ink(double brightness){return Paint.Brush(brightness<145?"#FFF8F9FC":"#FF19221E");}
        static async void Load(string file,int token)
        {
            try{
                var result=await BackgroundWork.Run(()=>{
                    if(String.IsNullOrEmpty(file)||!Path.IsPathRooted(file)||file.StartsWith("\\\\",StringComparison.Ordinal)||!File.Exists(file)||new FileInfo(file).Length>16777216)return null;
                    int width,height;using(var stream=File.OpenRead(file)){var header=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);width=header.Frames[0].PixelWidth;height=header.Frames[0].PixelHeight;}
                    if(width<1||height<1||width>100000||height>100000)return null;var bitmap=new BitmapImage();using(var stream=File.OpenRead(file)){bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;if(width>=height)bitmap.DecodePixelWidth=Math.Min(1200,width);else bitmap.DecodePixelHeight=Math.Min(1200,height);bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();}
                    double ratio=Math.Min(1,32.0/Math.Max(bitmap.PixelWidth,bitmap.PixelHeight));var small=new FormatConvertedBitmap(new TransformedBitmap(bitmap,new ScaleTransform(ratio,ratio)),PixelFormats.Bgra32,null,0);int stride=small.PixelWidth*4;var pixels=new byte[stride*small.PixelHeight];small.CopyPixels(pixels,stride,0);double light=0;int count=0;for(int i=0;i<pixels.Length;i+=4){if(pixels[i+3]<80)continue;light+=pixels[i+2]*.2126+pixels[i+1]*.7152+pixels[i]*.0722;count++;}
                    var brush=new ImageBrush(bitmap){Stretch=Stretch.UniformToFill};brush.Freeze();return new {Brush=(Brush)brush,Ink=Ink(count==0?190:light/count)};
                });
                if(token!=revision)return;image=result==null?null:result.Brush;imageInk=result==null?Ink(190):result.Ink;imageFile=file;var app=Application.Current;if(app!=null&&!app.Dispatcher.HasShutdownStarted)await app.Dispatcher.InvokeAsync(new Action(()=>{foreach(Window window in app.Windows){var glass=window as GlassWindow;if(glass!=null&&glass.DockSurface){glass.RefreshGlassMaterial();Glass95.DockInk(glass.Surface);}}}));
            }catch(Exception error){AppLog.Once("dock-background",error.GetType().Name);}
        }
    }
}
