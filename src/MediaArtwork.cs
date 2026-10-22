using System;
using System.Collections.Generic;
using System.IO;
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
                using(var stream=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=320;image.StreamSource=stream;image.EndInit();image.Freeze();Image=image;digest=hash;Decodes++;}
            }catch(Exception error){AppLog.Once("media-artwork",error.Message);}
            return Image;
        }
        static byte[] Cover(Color color){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(ArtworkPalette.Sample(color)));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}}
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
            checks.Add("Late/same-title artwork updates retry twice, identical bytes reuse decoded images, and image failures retain the current track only");
        }
    }
}
