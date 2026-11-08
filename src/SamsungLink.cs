using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OneTaskBar
{
    // Samsung's PC apps expose no connection API. Galaxy Connect adds a software camera that is named after the
    // phone while a phone is linked, so that present device node is the signal for every Samsung link feature.
    internal static class SamsungLink
    {
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]
        static extern int CM_Locate_DevNodeW(out uint device,string id,uint flags);
        const string Root=@"SYSTEM\CurrentControlSet\Enum\ROOT\CAMERASHAREDRIVERSOURCE";
        static string name;
        static DateTime checkedAt;

        internal static readonly string[][] Features=
        {
            new[]{"ai-select","SmartSelect_","AI 셀렉트"},
            new[]{"quick-share","SamsungQuickShare","퀵 쉐어"},
            new[]{"second-screen","SecondScreen","세컨드 스크린"},
            new[]{"storage-share","4438638898209","저장공간 공유"},
            new[]{"camera-share","16297BCCB59BC","카메라 공유"}
        };
        internal static bool Feature(string key){return Features.Any(f=>f[0]==key);}
        internal static string Marker(string key){return Features.First(f=>f[0]==key)[1];}
        // Features that report which phone they are linked to, like Galaxy Buds do.
        internal static bool Reports(string key){return key=="second-screen"||key=="storage-share"||key=="camera-share";}

        // Phone name, or null when no phone is linked. Re-read at most every few seconds.
        internal static string Phone()
        {
            var now=DateTime.UtcNow;
            if((now-checkedAt).TotalSeconds<4)return name;
            checkedAt=now;
            name=Read();
            return name;
        }

        static string Read()
        {
            try
            {
                using(var root=Registry.LocalMachine.OpenSubKey(Root))
                {
                    if(root==null)return null;
                    foreach(string instance in root.GetSubKeyNames())
                    {
                        uint node;
                        if(CM_Locate_DevNodeW(out node,@"ROOT\CAMERASHAREDRIVERSOURCE\"+instance,0)!=0)continue;
                        using(var key=root.OpenSubKey(instance))
                        {
                            string text=key==null?null:key.GetValue("FriendlyName") as string;
                            if(String.IsNullOrEmpty(text))continue;
                            return Clean(text);
                        }
                    }
                }
            }
            catch(Exception error)
            {
                AppLog.Once("samsung-link",error.GetType().Name);
            }
            return null;
        }

        internal static string Clean(string text)
        {
            int paren=text.LastIndexOf(" (",StringComparison.Ordinal);
            return (paren>0?text.Substring(0,paren):text).Trim();
        }
    }
}
