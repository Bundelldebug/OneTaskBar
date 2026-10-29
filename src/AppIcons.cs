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
        static readonly SemaphoreSlim loaders=new SemaphoreSlim(3);
        internal static int CachedCount {get{lock(cacheLock)return cache.Count;}}
        static string Key(AppEntry app){string key=!String.IsNullOrEmpty(app.AppID)&&app.AppID.Contains("!")?app.AppID:(app.AppID??"")+"|"+(app.Target??app.Executable??"");return WebAppIcons.HasSeparateIdentity(app.AppID)?app.AppID+(app.Windows.Count>0?"|window":"|shortcut"):key;}
        static ImageSource Ready(AppEntry app){lock(cacheLock){CachedIcon item;if(!cache.TryGetValue(Key(app),out item)||!item.Image.IsValueCreated)return null;recent.Remove(item.Position);recent.AddFirst(item.Position);return item.Image.Value;}}
        internal static FrameworkElement Visual(AppEntry app,double size){var image=app.Icon??Ready(app);return image!=null?Visual(image,size):new DeferredIcon(app,size);}
        // Decode only icons that have entered a visible viewport, with bounded parallel work.
        sealed class DeferredIcon : Decorator
        {
            readonly AppEntry entry;readonly double size;ScrollViewer scroll;bool requested;int attempts;
            internal DeferredIcon(AppEntry app,double pixels){entry=app;size=pixels;Width=Height=size;Child=new Glyph("window",size){Color=Paint.Text};Loaded+=(s,e)=>{if(!requested)attempts=0;Observe();};Unloaded+=(s,e)=>Stop();SizeChanged+=(s,e)=>Check();IsVisibleChanged+=(s,e)=>{if(IsVisible){if(!requested)attempts=0;Observe();}else Stop();};}
            void Stop(){if(scroll!=null)scroll.ScrollChanged-=Scrolled;scroll=null;}
            void Scrolled(object sender,ScrollChangedEventArgs args){Check();}
            void Observe(){if(requested||!IsLoaded)return;Stop();DependencyObject parent=this;while((parent=VisualTreeHelper.GetParent(parent))!=null){scroll=parent as ScrollViewer;if(scroll!=null)break;}if(scroll!=null)scroll.ScrollChanged+=Scrolled;Check();}
            async void Check()
            {
                if(requested||!IsLoaded||!IsVisible||ActualWidth<=0||ActualHeight<=0||attempts>=2)return;
                if(scroll!=null){try{var bounds=TransformToAncestor(scroll).TransformBounds(new Rect(0,0,ActualWidth,ActualHeight));if(!bounds.IntersectsWith(new Rect(0,0,scroll.ActualWidth,scroll.ActualHeight)))return;}catch(InvalidOperationException){return;}}
                requested=true;attempts++;Stop();try{await loaders.WaitAsync();ImageSource icon;try{icon=await BackgroundWork.Run(()=>Get(entry));}finally{loaders.Release();}if(icon!=null){LauncherMotion.InvalidateCache(this);Child=Visual(icon,size);InvalidateVisual();return;}}catch(Exception error){AppLog.Once("icon-load",error.Message);}requested=false;if(attempts<2&&IsLoaded&&IsVisible){await Task.Delay(200);Observe();}
            }
        }
        internal static FrameworkElement Visual(ImageSource source,double size)
        {
            var image=new Image {Source=source,Width=size,Height=size,Stretch=Stretch.Uniform};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
            return image;
        }
        internal static ImageSource Get(AppEntry app)
        {
            string key=Key(app);
            Lazy<ImageSource> image;lock(cacheLock){CachedIcon item;if(cache.TryGetValue(key,out item)){recent.Remove(item.Position);recent.AddFirst(item.Position);image=item.Image;}else {image=new Lazy<ImageSource>(()=>Load(app));cache[key]=new CachedIcon {Image=image,Position=recent.AddFirst(key)};while(cache.Count>CacheLimit){string oldest=recent.Last.Value;recent.RemoveLast();cache.Remove(oldest);}}}ImageSource result;try{result=image.Value;}catch{Discard(key,image);throw;}if(result==null)Discard(key,image);return result;
        }
        static void Discard(string key,Lazy<ImageSource> image){lock(cacheLock){CachedIcon item;if(cache.TryGetValue(key,out item)&&Object.ReferenceEquals(item.Image,image)){cache.Remove(key);recent.Remove(item.Position);}}}
        static ImageSource Load(AppEntry app)
        {
            ImageSource result=WebAppIcons.Read(app);
            if(result==null&&(String.IsNullOrEmpty(app.AppID)||!app.AppID.Contains("!")))result=InternetShortcuts.Icon(app);
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
        internal static BitmapSource Normalize(BitmapSource original,int maximumPixels=160)
        {
            if(original==null)return null;
            try {
                maximumPixels=Math.Max(32,Math.Min(256,maximumPixels));if(original.PixelWidth>maximumPixels||original.PixelHeight>maximumPixels){double scale=(double)maximumPixels/Math.Max(original.PixelWidth,original.PixelHeight);var reduced=new TransformedBitmap(original,new ScaleTransform(scale,scale));reduced.Freeze();original=reduced;}
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
