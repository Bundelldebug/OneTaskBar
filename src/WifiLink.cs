using System;
using System.Text;

namespace OneTaskBar
{
    internal sealed class WifiLink
    {
        internal string Name;internal int Generation,Signal=-1;internal long Sent=-1,Received=-1;
        internal static int Bars(int signal){return signal<0?3:signal>=75?3:signal>=40?2:signal>0?1:0;}
        internal static int GenerationFor(int phy){return phy==7?4:phy==8?5:phy==10?6:phy==11?7:0;}
        // WLAN_CONNECTION_ATTRIBUTES: profile WCHAR[256], then association SSID, BSS,
        // MAC[6]+alignment, DOT11_PHY_TYPE. See Microsoft's wlanapi.h structure docs.
        internal static WifiLink Parse(byte[] bytes)
        {
            var link=new WifiLink();if(bytes==null||bytes.Length<556||BitConverter.ToInt32(bytes,0)!=1)return link;
            int length=BitConverter.ToInt32(bytes,520);if(length<1||length>32)return link;
            link.Name=Encoding.UTF8.GetString(bytes,524,length);
            if(bytes.Length>=580){link.Generation=GenerationFor(BitConverter.ToInt32(bytes,568));link.Signal=Math.Max(0,Math.Min(100,BitConverter.ToInt32(bytes,576)));}return link;
        }
    }
}
