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
            const string xml="<toast launch='target=42'><visual baseUri='ms-appx:///Images/'><binding template='ToastGeneric'><text>알림 그림</text><text>연결됨</text><image placement='appLogoOverride' src='logo.png'/><image src='small.png'/><image placement='hero' src='buds.png'/></binding></visual></toast>";
            var payload=NotificationPayload.Parse(xml,new[]{"알림 그림","연결됨"});if(payload==null||payload.Arguments!="target=42"||payload.LogoSource!="ms-appx:///Images/logo.png"||payload.ImageSource!="ms-appx:///Images/buds.png")throw new Exception("Native toast artwork/launch attributes lost");
            if(NativeToastAssets.Decode(Encoding.Unicode.GetPreamble().ConcatBytes(Encoding.Unicode.GetBytes(xml)))!=xml||NativeToastAssets.Decode(Encoding.UTF8.GetBytes(xml))!=xml)throw new Exception("Native toast UTF-8/UTF-16 decode");
            string root=Path.Combine(AppLog.Data,"images");if(NativeToastAssets.Within(root,"../secret.png")!=null||NativeToastAssets.Within(root,"valid/buds.png")==null||NativeToastAssets.Image("unknown",null,false)!=null)throw new Exception("Native image path validation");
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
