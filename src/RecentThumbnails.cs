using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal sealed class RecentThumbnail : Grid
    {
        readonly string path;readonly Border preview=new Border {CornerRadius=new CornerRadius(6)};readonly FrameworkElement fallback;
        CancellationTokenSource pending;int revision;internal bool HasPreview {get;private set;}
        internal RecentThumbnail(AppEntry app)
        {
            path=app.Target;Width=48;Height=36;HorizontalAlignment=HorizontalAlignment.Left;VerticalAlignment=VerticalAlignment.Center;fallback=AppIcons.Visual(app,30);Children.Add(fallback);Children.Add(preview);
            IsVisibleChanged+=(s,e)=>{if(IsVisible)Load();else Cancel();};Unloaded+=(s,e)=>Cancel();
        }
        async void Load()
        {
            if(HasPreview||pending!=null)return;var request=new CancellationTokenSource();pending=request;int version=++revision;
            try{var image=await RecentThumbnails.Get(path,request.Token);if(image==null||request.IsCancellationRequested||version!=revision||!IsVisible)return;preview.Background=new ImageBrush(image) {Stretch=Stretch.UniformToFill};fallback.Visibility=Visibility.Collapsed;HasPreview=true;}
            catch(OperationCanceledException){}catch(Exception error){AppLog.Once("recent-thumbnail-ui",error.GetType().Name);}finally{if(pending==request)pending=null;request.Dispose();}
        }
        void Cancel(){revision++;var request=pending;pending=null;if(request!=null)request.Cancel();}
    }

    internal static class RecentThumbnails
    {
        const int Edge=128,Limit=48;
        sealed class Request {internal string Path;internal CancellationToken Token;internal TaskCompletionSource<BitmapSource> Result;}
        sealed class Cached {internal string Key;internal BitmapSource Image;internal DateTime Created=DateTime.UtcNow;}
        static readonly object gate=new object();static readonly Queue<Request> queue=new Queue<Request>();static readonly LinkedList<Cached> cache=new LinkedList<Cached>();static Thread worker;
        static readonly HashSet<string> images=new HashSet<string>(new[]{".png",".jpg",".jpeg",".bmp",".gif",".tif",".tiff",".webp",".heic",".avif"},StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> documents=new HashSet<string>(new[]{".mp4",".mkv",".mov",".avi",".wmv",".webm",".mp3",".m4a",".flac",".pdf",".doc",".docx",".ppt",".pptx",".xls",".xlsx",".hwp",".hwpx"},StringComparer.OrdinalIgnoreCase);
        internal static int CacheCount {get{lock(gate)return cache.Count;}}
        internal static bool Supported(string path){return StartRecentFeed.Local(path)&&(images.Contains(System.IO.Path.GetExtension(path))||documents.Contains(System.IO.Path.GetExtension(path)));}
        internal static Task<BitmapSource> Get(string path,CancellationToken token)
        {
            if(!Supported(path)||token.IsCancellationRequested)return Task.FromResult<BitmapSource>(null);var result=new TaskCompletionSource<BitmapSource>();
            lock(gate){if(queue.Count>=Limit){result.SetResult(null);return result.Task;}queue.Enqueue(new Request {Path=path,Token=token,Result=result});if(worker==null){worker=new Thread(Work) {IsBackground=true,Name="One TaskBar recent previews"};worker.SetApartmentState(ApartmentState.STA);worker.Start();}Monitor.Pulse(gate);}return result.Task;
        }
        static void Work()
        {
            while(true){Request request;lock(gate){while(queue.Count==0){if(!Monitor.Wait(gate,15000)&&queue.Count==0){worker=null;return;}}request=queue.Dequeue();}
                if(request.Token.IsCancellationRequested){request.Result.TrySetCanceled();continue;}BitmapSource image=null;try{image=Read(request.Path);}catch(Exception error){AppLog.Once("recent-thumbnail",error.GetType().Name);}if(request.Token.IsCancellationRequested)request.Result.TrySetCanceled();else request.Result.TrySetResult(image);
            }
        }
        static BitmapSource Read(string path)
        {
            var drive=new DriveInfo(System.IO.Path.GetPathRoot(path));if(drive.DriveType!=DriveType.Fixed&&drive.DriveType!=DriveType.Removable)return null;
            var info=new FileInfo(path);if(!info.Exists||info.Length<=0||info.Length>128L*1024*1024||((int)info.Attributes&(0x1000|0x40000|0x400000))!=0)return null;
            string key=info.FullName+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks;lock(gate){for(var node=cache.First;node!=null;node=node.Next)if(node.Value.Key==key){var found=node.Value;cache.Remove(node);if(found.Image==null&&(DateTime.UtcNow-found.Created).TotalSeconds>3)break;cache.AddFirst(found);return found.Image;}}
            BitmapSource image=null;if(images.Contains(info.Extension)&&info.Length<=32L*1024*1024)try{image=ReadImage(info.FullName);}catch{}
            if(image==null)image=ReadShell(info.FullName);lock(gate){cache.AddFirst(new Cached {Key=key,Image=image});while(cache.Count>Limit)cache.RemoveLast();}return image;
        }
        internal static BitmapSource ReadImage(string path)
        {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
                var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);var frame=decoder.Frames[0];int w=frame.PixelWidth,h=frame.PixelHeight;if(w<1||h<1||w>65536||h>65536||(long)w*h>200000000)return null;
                stream.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.CreateOptions=BitmapCreateOptions.IgnoreColorProfile;if(w>=h)image.DecodePixelWidth=Math.Min(Edge,w);else image.DecodePixelHeight=Math.Min(Edge,h);image.StreamSource=stream;image.EndInit();image.Freeze();return image;
            }
        }
        [StructLayout(LayoutKind.Sequential)]struct Size {public int Width,Height;}
        [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IImageFactory {[PreserveSig]int GetImage(Size size,uint flags,out IntPtr bitmap);}
        [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IImageFactory factory);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr bitmap);
        static BitmapSource ReadShell(string path)
        {
            IImageFactory factory=null;IntPtr bitmap=IntPtr.Zero;try{var iid=typeof(IImageFactory).GUID;if(SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out factory)<0||factory==null)return null;
                // Documents and videos use only an existing Windows thumbnail: never launch the file or hydrate cloud content.
                if(factory.GetImage(new Size {Width=Edge,Height=Edge},0x18,out bitmap)<0||bitmap==IntPtr.Zero)return null;var image=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(Edge,Edge));image.Freeze();return image;
            }catch{return null;}finally{if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(factory!=null)Marshal.FinalReleaseComObject(factory);}
        }
    }
}
