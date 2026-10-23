using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class AppIcons
    {
        sealed class CachedIcon {internal Lazy<ImageSource> Image;internal LinkedListNode<string> Position;}
        static readonly object cacheLock=new object();
        static readonly Dictionary<string,CachedIcon> cache=new Dictionary<string,CachedIcon>(StringComparer.OrdinalIgnoreCase);
        static readonly LinkedList<string> recent=new LinkedList<string>();
        internal const int CacheLimit=48;
        sealed class Plate {internal bool Needed;}
        static readonly SemaphoreSlim loaders=new SemaphoreSlim(3);
        internal static int CachedCount {get{lock(cacheLock)return cache.Count;}}
        internal static FrameworkElement Visual(AppEntry app,double size){return app.Icon!=null?Visual(app.Icon,size):new DeferredIcon(app,size);}
        // Decode only icons that have entered a visible viewport, with bounded parallel work.
        sealed class DeferredIcon : Decorator
        {
            readonly AppEntry entry;readonly double size;ScrollViewer scroll;bool requested;
            internal DeferredIcon(AppEntry app,double pixels){entry=app;size=pixels;Width=Height=size;Child=new Glyph("window",size){Color=Paint.Text};Loaded+=(s,e)=>Observe();Unloaded+=(s,e)=>Stop();}
            void Stop(){if(scroll!=null)scroll.ScrollChanged-=Scrolled;scroll=null;}
            void Scrolled(object sender,ScrollChangedEventArgs args){Check();}
            void Observe(){if(requested)return;DependencyObject parent=this;while((parent=VisualTreeHelper.GetParent(parent))!=null){scroll=parent as ScrollViewer;if(scroll!=null)break;}if(scroll!=null)scroll.ScrollChanged+=Scrolled;Check();}
            async void Check()
            {
                if(requested||!IsVisible)return;
                if(scroll!=null){try{var bounds=TransformToAncestor(scroll).TransformBounds(new Rect(0,0,ActualWidth,ActualHeight));if(!bounds.IntersectsWith(new Rect(0,0,scroll.ActualWidth,scroll.ActualHeight)))return;}catch(InvalidOperationException){return;}}
                requested=true;Stop();try{await loaders.WaitAsync();ImageSource icon;try{icon=await BackgroundWork.Run(()=>Get(entry));}finally{loaders.Release();}if(icon!=null)Child=Visual(icon,size);}catch(Exception error){AppLog.Once("icon-load",error.Message);}
            }
        }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource,Plate> plates=new System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource,Plate>();
        internal static FrameworkElement Visual(ImageSource source,double size)
        {
            var image=new Image {Source=source,Width=size,Height=size,Stretch=Stretch.Uniform};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
            if(!plates.GetValue(source,key=>new Plate {Needed=NeedsPlate(key)}).Needed)return image;
            return new Border {Width=size,Height=size,CornerRadius=new CornerRadius(size*.26),Background=Paint.Brush("#E62C3832"),Padding=new Thickness(size*.08),Child=new Image {Source=source,Stretch=Stretch.Uniform}};
        }
        static bool NeedsPlate(ImageSource source)
        {
            var bitmap=source as BitmapSource;if(bitmap==null)return false;
            try {var image=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);int stride=image.PixelWidth*4;var pixels=new byte[stride*image.PixelHeight];image.CopyPixels(pixels,stride,0);int solid=0,light=0;
                for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]>80){solid++;if(pixels[i]>205&&pixels[i+1]>205&&pixels[i+2]>205)light++;}
                return solid>0&&light/(double)solid>.88;
            }catch{return false;}
        }
        internal static ImageSource Get(AppEntry app)
        {
            string key=!String.IsNullOrEmpty(app.AppID)&&app.AppID.Contains("!")?app.AppID:(app.AppID??"")+"|"+(app.Target??app.Executable??"");
            if(WebAppIcons.IsWebApp(app.AppID))key=app.AppID+(app.Windows.Count>0?"|window":"|shortcut");
            Lazy<ImageSource> image;lock(cacheLock){CachedIcon item;if(cache.TryGetValue(key,out item)){recent.Remove(item.Position);recent.AddFirst(item.Position);image=item.Image;}else {image=new Lazy<ImageSource>(()=>Load(app));cache[key]=new CachedIcon {Image=image,Position=recent.AddFirst(key)};while(cache.Count>CacheLimit){string oldest=recent.Last.Value;recent.RemoveLast();cache.Remove(oldest);}}}return image.Value;
        }
        static ImageSource Load(AppEntry app)
        {
            ImageSource result=WebAppIcons.Read(app);
            if(!String.IsNullOrEmpty(app.AppID)&&app.AppID.Contains("!")) {
                result=TaskbarLogo.Read(app.AppID)??Native.AppIcon("shell:AppsFolder\\"+app.AppID);
                if(result==null){var read=Task.Run(async()=>await SystemServices.AppLogo(app.AppID));if(read.Wait(1600)&&read.Status==TaskStatus.RanToCompletion)result=read.Result;}
            }
            if(result==null&&!String.IsNullOrEmpty(app.Target)&&app.Target.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase))result=Native.ShortcutIcon(app.Target);
            string executable=app.Executable??app.Target;
            if(result==null&&!String.IsNullOrEmpty(executable)&&File.Exists(executable)&&!executable.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase))result=Native.ResourceIcon(executable,0);
            if(result==null)result=Native.AppIcon(!String.IsNullOrEmpty(app.Target)?app.Target:"shell:AppsFolder\\"+app.AppID);
            return Normalize(result as BitmapSource)??result;
        }
        // Preserve the original artwork, colors and aspect ratio; remove transparent asset padding only.
        internal static BitmapSource Normalize(BitmapSource original)
        {
            if(original==null)return null;
            try {
                if(original.PixelWidth>160||original.PixelHeight>160){double scale=160.0/Math.Max(original.PixelWidth,original.PixelHeight);var reduced=new TransformedBitmap(original,new ScaleTransform(scale,scale));reduced.Freeze();original=reduced;}
                var image=new FormatConvertedBitmap(original,PixelFormats.Bgra32,null,0);int w=image.PixelWidth,h=image.PixelHeight,stride=w*4;
                if(w<2||h<2||w>2048||h>2048)return original;
                var pixels=new byte[stride*h];image.CopyPixels(pixels,stride,0);int left=w,top=h,right=-1,bottom=-1;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[y*stride+x*4+3]>24){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
                if(right<left)return original;
                int pad=Math.Max(1,(int)Math.Ceiling(Math.Max(right-left+1,bottom-top+1)*.025));left=Math.Max(0,left-pad);top=Math.Max(0,top-pad);right=Math.Min(w-1,right+pad);bottom=Math.Min(h-1,bottom+pad);
                var cropped=new CroppedBitmap(original,new Int32Rect(left,top,right-left+1,bottom-top+1));var compact=new WriteableBitmap(cropped);compact.Freeze();return compact;
            }catch{return original;}
        }
    }
}
