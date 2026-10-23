using System;
using System.Linq;
using System.Collections.Generic;
using Windows.Networking.Connectivity;
namespace OneTaskBar
{
    internal enum NetworkRoute {None,Wifi,Ethernet,Other}
    internal static class NetworkRouteInfo
    {
        internal static NetworkRoute Classify(uint type,bool connected){return !connected?NetworkRoute.None:type==6?NetworkRoute.Ethernet:type==71?NetworkRoute.Wifi:NetworkRoute.Other;}
        internal static NetworkRoute Read()
        {
            var profile=NetworkInformation.GetInternetConnectionProfile();if(profile==null)profile=NetworkInformation.GetConnectionProfiles().Where(p=>p.GetNetworkConnectivityLevel()!=NetworkConnectivityLevel.None).OrderByDescending(p=>p.NetworkAdapter!=null&&p.NetworkAdapter.IanaInterfaceType==6).FirstOrDefault();
            return profile==null?NetworkRoute.None:Classify(profile.NetworkAdapter!=null?profile.NetworkAdapter.IanaInterfaceType:0,profile.GetNetworkConnectivityLevel()!=NetworkConnectivityLevel.None);
        }
        internal static string Glyph(NetworkRoute route,bool? wifiPresent,bool radioOff,bool wifiConnected,bool networkConnected)
        {
            if(route==NetworkRoute.Ethernet)return "ethernet";
            if(route==NetworkRoute.Wifi)return "wifi";
            if(route==NetworkRoute.Other)return "network";
            return wifiPresent==false?(networkConnected?"network":"ethernet-off"):radioOff?"wifi-off":wifiConnected?"wifi":"wifi-disconnected";
        }
        internal static void Validate(List<string> checks)
        {
            if(Classify(6,true)!=NetworkRoute.Ethernet||Classify(71,true)!=NetworkRoute.Wifi||Classify(6,false)!=NetworkRoute.None||Glyph(NetworkRoute.Ethernet,true,true,false,true)!="ethernet"||Glyph(NetworkRoute.Ethernet,true,false,true,true)!="ethernet"||Glyph(NetworkRoute.Wifi,true,false,true,true)!="wifi"||Glyph(NetworkRoute.None,false,false,false,false)!="ethernet-off")throw new InvalidOperationException("Default-route Ethernet/Wi-Fi classification");
            checks.Add("An active Ethernet route selects the dedicated wired glyph even with a Wi-Fi adapter/radio present; Wi-Fi and disconnected routes remain distinct");
        }
        internal static int Probe(){try{Validate(new List<string>());var route=Read();AppLog.Json("network-route-probe.json",new {passed=true,route=route.ToString(),eventDriven=true,ethernetAndWifiSelectionValidated=true,networkSettingsChanged=false});return 0;}catch(Exception error){AppLog.Json("network-route-probe.json",new {passed=false,error=error.ToString()});return 1;}}
    }
}
