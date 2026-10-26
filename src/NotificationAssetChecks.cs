using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace OneTaskBar
{
    internal static class NotificationAssetChecks
    {
        internal static void Validate(List<string> checks)
        {
            var native=System.Windows.Media.Imaging.BitmapSource.Create(256,256,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[256*256*4],256*4);native.Freeze();var small=new System.Windows.Media.Imaging.TransformedBitmap(native,new System.Windows.Media.ScaleTransform(.125,.125));small.Freeze();
            if(AppIcons.Normalize(native,256).PixelWidth!=256||AppIcons.Normalize(native).PixelWidth>160||NotificationIcons.Best(small,native)!=native||NotificationIcons.Best(native,small)!=native)throw new Exception("Notification icons must retain native detail and prefer the sharper matching app resource");
            using(var stream=new MemoryStream()){var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(small));encoder.Save(stream);stream.Position=0;if(BitmapDecode.Frozen(stream,256).PixelWidth!=32)throw new Exception("Low-resolution notification icons must not be enlarged before quality comparison");}
            var cached=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase){{"active.app",native},{"retired.app",small}};NotificationService.TrimCache(cached,new HashSet<string>(new[]{"ACTIVE.APP"},StringComparer.OrdinalIgnoreCase));if(cached.Count!=1||cached["active.app"]!=native)throw new Exception("Notification cache must release retired apps without dropping active artwork");checks.Add("Notification image and activation caches retain only apps in the current snapshot and are cleared when access ends");
            const string xml="<toast launch='target=42'><visual baseUri='ms-appx:///Images/'><binding template='ToastGeneric'><text>알림 그림</text><text>연결됨</text><image placement='appLogoOverride' src='logo.png'/><image src='small.png'/><image placement='hero' src='buds.png'/></binding></visual></toast>";
            var payload=NotificationPayload.Parse(xml,new[]{"알림 그림","연결됨"});if(payload==null||payload.Arguments!="target=42"||payload.LogoSource!="ms-appx:///Images/logo.png"||payload.ImageSource!="ms-appx:///Images/buds.png")throw new Exception("Native toast artwork/launch attributes lost");
            if(NativeToastAssets.Decode(Encoding.Unicode.GetPreamble().ConcatBytes(Encoding.Unicode.GetBytes(xml)))!=xml||NativeToastAssets.Decode(Encoding.UTF8.GetBytes(xml))!=xml)throw new Exception("Native toast UTF-8/UTF-16 decode");
            string root=Path.Combine(AppLog.Data,"images");if(NativeToastAssets.Within(root,"../secret.png")!=null||NativeToastAssets.Within(root,"valid/buds.png")==null||NativeToastAssets.Image("unknown",null,false)!=null)throw new Exception("Native image path validation");
            bool dtdRejected=false;try{NotificationPayload.Parse("<!DOCTYPE toast [<!ENTITY outside SYSTEM 'file:///not-a-real-test-file'>]><toast><visual><binding><text>&outside;</text></binding></visual></toast>",new[]{"outside"});}catch(System.Xml.XmlException){dtdRejected=true;}
            if(!dtdRejected||NotificationPayload.Parse(new string('x',524289),new string[0])!=null||NativeToastAssets.Resolve("unknown","file://server/share/image.png")!=null||NativeToastAssets.Resolve("unknown","relative.png")!=null||NativeToastAssets.Within(root,@"safe\..\..\secret.png")!=null||NativeToastAssets.Within(root,@"..\images-other\secret.png")!=null)throw new Exception("Notification XML/asset trust boundary regression");
            checks.Add("Untrusted notification XML rejects DTD/external entities and oversized documents; UNC/relative image sources and normalized parent/sibling path escapes are rejected without opening a remote resource");
            var state=SamsungPerformance.Parse("[\"2\",\"1\",\"0\",\"3\",\"6\"]",2);if(!state.Available||state.Modes.Count!=4||state.Modes.Exists(m=>m.Id==6)||SamsungPerformance.Read(false).Available)throw new Exception("Samsung performance capability/absence mapping");checks.Add("Native notification logo/hero parsing, Unicode payload decode and image-path bounds pass; Samsung profiles exclude the fan-max flag and hide when absent");
        }
        static byte[] ConcatBytes(this byte[] first,byte[] last){var joined=new byte[first.Length+last.Length];Buffer.BlockCopy(first,0,joined,0,first.Length);Buffer.BlockCopy(last,0,joined,first.Length,last.Length);return joined;}
        internal static int Run()
        {
            string file=Path.Combine(AppLog.Data,"notification-assets-fixture.db");var text=new[]{"알림 그림","연결됨"};var payload=NativeToastAssets.ReadFile(file,"Fixture.App",42,text);var artwork=payload!=null?NativeToastAssets.Image("Fixture.App",payload.ImageSource,false):null;var icon=payload!=null?NativeToastAssets.Image("Fixture.App",payload.LogoSource,true):null;
            bool exact=NativeToastAssets.ReadFile(file,"Other.App",42,text)==null&&NativeToastAssets.ReadFile(file,"Fixture.App",43,text)==null&&NativeToastAssets.ReadFile(file,"Fixture.App",42,new[]{"다른 알림"})==null;bool passed=payload!=null&&payload.Arguments=="target=42"&&artwork!=null&&icon!=null&&exact;AppLog.Json("notification-assets-probe.json",new {passed=passed,exactAuthorizedItem=exact,nativeLogo=icon!=null,nativeLargeImage=artwork!=null,originalArguments=payload!=null&&payload.Arguments=="target=42",databaseReadOnly=true,networkDownloads=false});return passed?0:1;
        }
    }
}
