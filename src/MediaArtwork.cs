using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    internal sealed class MediaArtwork
    {
        string track,digest;int retries;DateTime retryAt=DateTime.MaxValue;
        internal BitmapSource Image {get;private set;}
        internal int Reads {get;private set;}internal int Decodes {get;private set;}
        internal bool Due(DateTime now){return track!=null&&retries<2&&now>=retryAt;}
        internal void Clear(){track=digest=null;Image=null;retries=0;retryAt=DateTime.MaxValue;}
        internal void RetryFailed(DateTime now){if(!Due(now))return;retries++;retryAt=retries<2?now.AddSeconds(4):DateTime.MaxValue;}
        internal async Task<BitmapSource> Read(string key,Func<Task<byte[]>> read,DateTime now)
        {
            if(track!=key){track=key;digest=null;Image=null;retries=0;retryAt=now.AddSeconds(2);}else RetryFailed(now);
            try {
                Reads++;byte[] bytes=await read();if(bytes==null||bytes.Length==0||bytes.Length>8388608)return Image;
                string hash;using(var sha=SHA256.Create())hash=Convert.ToBase64String(sha.ComputeHash(bytes));
                if(Image!=null&&hash==digest)return Image;
                using(var stream=new MemoryStream(bytes)){var image=BitmapDecode.Frozen(stream,1280);if(image!=null){Image=image;digest=hash;Decodes++;}}
            }catch(Exception error){AppLog.Once("media-artwork",error.Message);}
            return Image;
        }
        static byte[] Cover(Color color){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(ArtworkPalette.Sample(color)));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}}
        static byte[] SizedCover(int width,int height){var pixels=new byte[width*height*4];for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}}
        internal static int Probe()
        {
            try{return Task.Run(async()=> {
                var manager=await SystemServices.Wait(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                var session=manager.GetCurrentSession();
                if(session!=null&&!YouTubeArtwork.Supported(session.SourceAppUserModelId)){AppLog.Json("media-source-probe.json",new {browserSessionAvailable=false,currentApp=session.SourceAppUserModelId,sessionApps=manager.GetSessions().Select(s=>s.SourceAppUserModelId).ToArray()});return 1;}
                if(session==null){AppLog.Json("media-source-probe.json",new {browserSessionAvailable=false});return 1;}
                var properties=await SystemServices.Wait(session.TryGetMediaPropertiesAsync());if(properties.Thumbnail==null)return 1;
                using(var stream=await SystemServices.Wait(properties.Thumbnail.OpenReadAsync()))using(var reader=new Windows.Storage.Streams.DataReader(stream)){
                    if(stream.Size>8388608)return 1;await SystemServices.Wait(reader.LoadAsync((uint)stream.Size));var bytes=new byte[(int)stream.Size];reader.ReadBytes(bytes);
                    using(var data=new MemoryStream(bytes)){
                        var image=BitmapDecode.Frozen(data,1280);var video=YouTubeArtwork.Allowed(session.SourceAppUserModelId,properties.Title)?YouTubeArtwork.Find(YouTubeArtwork.Profile(session.SourceAppUserModelId),properties.Title):null;byte[] high=video!=null?YouTubeArtwork.Download(video):null;BitmapSource sharp=null;if(high!=null)using(var highStream=new MemoryStream(high))sharp=BitmapDecode.Frozen(highStream,1280);
                        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(sharp??image));Directory.CreateDirectory(AppLog.Data);using(var file=File.Create(Path.Combine(AppLog.Data,"received-media-cover.png")))encoder.Save(file);
                        AppLog.Json("media-source-probe.json",new {source=session.SourceAppUserModelId,titleLength=(properties.Title??"").Length,authorized=YouTubeArtwork.Allowed(session.SourceAppUserModelId,properties.Title),profileAvailable=File.Exists(YouTubeArtwork.Profile(session.SourceAppUserModelId)),receivedBytes=bytes.Length,decodedWidth=image.PixelWidth,decodedHeight=image.PixelHeight,consentKey=video!=null?YouTubeArtwork.TrackKey(session.SourceAppUserModelId,properties.Title):null,lookup=YouTubeArtwork.LookupStatus,exactPlayingVideoMatched=video!=null,highWidth=sharp!=null?sharp.PixelWidth:0,highHeight=sharp!=null?sharp.PixelHeight:0});return sharp!=null?0:1;
                    }
                }
            }).GetAwaiter().GetResult();}
            catch(Exception error){AppLog.Json("media-source-probe.json",new {error=error.ToString()});return 1;}
        }
        internal static void Check(List<string> checks)
        {
            var cache=new MediaArtwork();var now=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);byte[] green=Cover(Colors.Green),red=Cover(Colors.Red);
            if(cache.Read("track",()=>Task.FromResult<byte[]>(null),now).GetAwaiter().GetResult()!=null||cache.Due(now.AddSeconds(1)))throw new Exception("Premature artwork retry");
            if(!cache.Due(now.AddSeconds(2)))throw new Exception("Delayed thumbnail retry missing");
            var first=cache.Read("track",()=>Task.FromResult(green),now.AddSeconds(2)).GetAwaiter().GetResult();
            var same=cache.Read("track",()=>Task.FromResult(green),now.AddSeconds(6)).GetAwaiter().GetResult();
            if(first==null||!Object.ReferenceEquals(first,same)||cache.Decodes!=1||cache.Due(now.AddMinutes(5)))throw new Exception("Artwork retry/decode is not bounded");
            var changed=cache.Read("track",()=>Task.FromResult(red),now.AddSeconds(7)).GetAwaiter().GetResult();
            if(changed==null||Object.ReferenceEquals(first,changed))throw new Exception("Same-title artwork event ignored");
            var retained=cache.Read("track",()=>Task.FromResult(new byte[]{1,2,3}),now.AddSeconds(8)).GetAwaiter().GetResult();
            if(!Object.ReferenceEquals(retained,changed))throw new Exception("Transient image failure cleared current cover");
            if(cache.Read("next",()=>Task.FromResult<byte[]>(null),now.AddSeconds(9)).GetAwaiter().GetResult()!=null)throw new Exception("Previous track artwork leaked");
            var sharp=cache.Read("sharp",()=>Task.FromResult(SizedCover(960,540)),now).GetAwaiter().GetResult();if(sharp==null||sharp.PixelWidth!=960||sharp.PixelHeight!=540)throw new Exception("High resolution cover was reduced or upscaled");var bounded=cache.Read("wide",()=>Task.FromResult(SizedCover(1600,900)),now).GetAwaiter().GetResult();if(bounded==null||bounded.PixelWidth!=1280||bounded.PixelHeight!=720)throw new Exception("Large cover dimensions not bounded");var portrait=cache.Read("portrait",()=>Task.FromResult(SizedCover(100,1600)),now).GetAwaiter().GetResult();if(portrait==null||portrait.PixelHeight!=1280||portrait.PixelWidth!=80)throw new Exception("Portrait cover exceeded decoded memory bound");checks.Add("Media artwork preserves native detail up to 1280 pixels, never upscales small sources and bounds wide/portrait dimensions");
            checks.Add("Late/same-title artwork updates retry twice, identical bytes reuse decoded images, and image failures retain the current track only");
        }
    }
}
