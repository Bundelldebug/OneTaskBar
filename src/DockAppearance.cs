using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;

namespace OneTaskBar
{
    internal static class DockAppearance
    {
        static string requested,imageFile,colorKey;static Brush image,solid;static int revision;
        static readonly Brush lightGlass=LightPanel.Tint("#4DDEE7ED","#4DC4D0D9","#4DD5E1E8");
        static readonly DropShadowEffect contrast=CreateContrast();
        static DropShadowEffect CreateContrast(){var effect=new DropShadowEffect {Color=Colors.Black,Opacity=.92,BlurRadius=2.5,ShadowDepth=0,RenderingBias=RenderingBias.Performance};effect.Freeze();return effect;}
        internal static Effect Readability {get{return IconsOnly?contrast:null;}}
        internal static Brush Glass {get{return Glass95.DockDark?PanelMaterial.Surface(true,Glass95.Enabled):lightGlass;}}
        internal static bool IconsOnly {get{return Preferences.Current.DockBackgroundKind==3;}}
        internal static int Mode {get{return IconsOnly?3:Preferences.Current.DockBackgroundKind==2?2:Preferences.Current.DockBackgroundKind==1||Preferences.Current.DockSolidBackground?1:0;}}
        internal static bool Custom {get{return IconsOnly||Preferences.Current.DockSolidBackground||Preferences.Current.DockBackgroundKind==1||Preferences.Current.DockBackgroundKind==2;}}
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
            get{if(IconsOnly)return Brushes.Transparent;if(Preferences.Current.DockBackgroundKind!=2)return Solid;string file=Preferences.Current.DockBackgroundImage;if(!String.Equals(requested,file,StringComparison.Ordinal)){requested=file;Load(file,++revision);}return image!=null&&String.Equals(imageFile,file,StringComparison.Ordinal)?image:Solid;}
        }
        internal static Brush Foreground
        {
            get{return IconsOnly||Glass95.DockDark?Paint.ControlWhite:Paint.OnLight;}
        }
        static async void Load(string file,int token)
        {
            try{
                var result=await BackgroundWork.Run(()=>{
                    if(String.IsNullOrEmpty(file)||!Path.IsPathRooted(file)||file.StartsWith("\\\\",StringComparison.Ordinal)||!File.Exists(file)||new FileInfo(file).Length>16777216)return null;
                    int width,height;using(var stream=File.OpenRead(file)){var header=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);width=header.Frames[0].PixelWidth;height=header.Frames[0].PixelHeight;}
                    if(width<1||height<1||width>100000||height>100000)return null;var bitmap=new BitmapImage();using(var stream=File.OpenRead(file)){bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;if(width>=height)bitmap.DecodePixelWidth=Math.Min(1200,width);else bitmap.DecodePixelHeight=Math.Min(1200,height);bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();}
                    var brush=new ImageBrush(bitmap){Stretch=Stretch.UniformToFill};brush.Freeze();return (Brush)brush;
                });
                if(token!=revision)return;image=result;imageFile=file;var app=Application.Current;if(app!=null&&!app.Dispatcher.HasShutdownStarted)await app.Dispatcher.InvokeAsync(new Action(()=>{foreach(Window window in app.Windows){var glass=window as GlassWindow;if(glass!=null&&glass.DockSurface){glass.RefreshGlassMaterial();Glass95.DockInk(glass.Surface);}}}));
            }catch(Exception error){AppLog.Once("dock-background",error.GetType().Name);}
        }
    }
}
