using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class WeatherReport
    {
        internal double Temperature;
        internal int Code,RainPercent;
        internal bool Day;
        internal string Summary;
    }

    // Optional. Nothing is sent until the user enters a place or turns on the PC's location.
    // Requests carry only that place name or rounded coordinates, run only while a weather
    // tile is shown, and are spaced at least 30 minutes apart. The Weather Channel needs the
    // user's own API key; without one, or if it fails, Open-Meteo answers.
    internal static class WeatherService
    {
        static DateTime nextAt=DateTime.MinValue;
        static bool busy;
        internal static WeatherReport Current {get;private set;}
        internal static int Requests {get;private set;}
        internal static bool Configured {get{return Preferences.Current.WeatherAutoLocation||!String.IsNullOrWhiteSpace(Preferences.Current.WeatherPlace);}}
        internal static string Problem {get;private set;}
        internal static string Source {get;private set;}
        internal static string PlaceName {get{return Preferences.Current.WeatherAutoLocation?"현재 위치":Preferences.Current.WeatherPlace;}}
        internal static void Reset()
        {
            Current=null;
            Problem=null;
            nextAt=DateTime.MinValue;
        }
        internal static void Request()
        {
            var now=DateTime.UtcNow;
            if(busy||now<nextAt||!Configured)return;
            busy=true;
            nextAt=now.AddMinutes(30);
            Requests++;
            Fetch();
        }
        static async void Fetch()
        {
            try
            {
                double latitude=Preferences.Current.WeatherLatitude,longitude=Preferences.Current.WeatherLongitude;
                if(Preferences.Current.WeatherAutoLocation)
                {
                    var position=await Locate();
                    if(position==null)
                    {
                        nextAt=DateTime.UtcNow.AddMinutes(5);
                        return;
                    }
                    latitude=Math.Round(position.Coordinate.Point.Position.Latitude,2);
                    longitude=Math.Round(position.Coordinate.Point.Position.Longitude,2);
                }
                Problem=null;
                string key=Preferences.Current.WeatherChannelKey;
                if(!String.IsNullOrWhiteSpace(key)&&await FetchWeatherChannel(latitude,longitude,key.Trim()))return;
                string url=String.Format(CultureInfo.InvariantCulture,"https://api.open-meteo.com/v1/forecast?latitude={0:0.###}&longitude={1:0.###}&current=temperature_2m,weather_code,is_day&hourly=precipitation_probability&forecast_days=2&timezone=auto",latitude,longitude);
                string json=await BackgroundWork.Run(()=>Download(url));
                Current=Parse(json,DateTime.Now);
                Source="Open-Meteo";
            }
            catch(Exception e)
            {
                nextAt=DateTime.UtcNow.AddMinutes(10);
                AppLog.Once("weather",e.GetType().Name);
            }
            finally{busy=false;}
        }
        // Windows' own location service, subject to the user's privacy setting. It is held only for this request.
        static async Task<Windows.Devices.Geolocation.Geoposition> Locate()
        {
            try
            {
                var access=await SystemServices.Wait(Windows.Devices.Geolocation.Geolocator.RequestAccessAsync());
                if(access!=Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed)
                {
                    Problem="Windows 설정에서 위치 권한을 켜 주세요";
                    AppLog.Once("weather-location",access.ToString());
                    return null;
                }
                return await SystemServices.Wait(new Windows.Devices.Geolocation.Geolocator().GetGeopositionAsync());
            }
            catch(Exception e)
            {
                Problem="현재 위치를 찾지 못했습니다";
                AppLog.Once("weather-location",e.GetType().Name);
                return null;
            }
        }
        static async Task<bool> FetchWeatherChannel(double latitude,double longitude,string key)
        {
            try
            {
                string query=String.Format(CultureInfo.InvariantCulture,"?geocode={0:0.##},{1:0.##}&format=json&units=m&language=ko-KR&apiKey=",latitude,longitude)+Uri.EscapeDataString(key);
                string current=await BackgroundWork.Run(()=>Download("https://api.weather.com/v3/wx/observations/current"+query));
                string hourly=await BackgroundWork.Run(()=>Download("https://api.weather.com/v3/wx/forecast/hourly/2day"+query));
                Current=ParseWeatherChannel(current,hourly,DateTime.Now);
                Source="The Weather Channel";
                return true;
            }
            catch(Exception e)
            {
                // The exception text can hold the request URL, and so the key. Only its type is logged.
                AppLog.Once("weather-channel",e.GetType().Name);
                return false;
            }
        }
        internal static WeatherReport ParseWeatherChannel(string currentJson,string hourlyJson,DateTime now)
        {
            var serializer=new JavaScriptSerializer();
            var current=(Dictionary<string,object>)serializer.DeserializeObject(currentJson);
            var report=new WeatherReport
            {
                Temperature=Convert.ToDouble(current["temperature"],CultureInfo.InvariantCulture),
                Code=CodeFromIcon(Convert.ToInt32(current["iconCode"],CultureInfo.InvariantCulture)),
                Day=Convert.ToString(current["dayOrNight"],CultureInfo.InvariantCulture)!="N",
                Summary=Convert.ToString(current["wxPhraseLong"],CultureInfo.InvariantCulture)
            };
            var hourly=(Dictionary<string,object>)serializer.DeserializeObject(hourlyJson);
            var times=(IList)hourly["validTimeLocal"];
            var chances=(IList)hourly["precipChance"];
            int seen=0;
            for(int i=0;i<times.Count&&i<chances.Count&&seen<6;i++)
            {
                DateTimeOffset at;
                if(chances[i]==null||!DateTimeOffset.TryParseExact(Convert.ToString(times[i],CultureInfo.InvariantCulture),"yyyy-MM-dd'T'HH:mm:sszzz",CultureInfo.InvariantCulture,DateTimeStyles.None,out at)||at.DateTime.AddHours(1)<=now)continue;
                report.RainPercent=Math.Max(report.RainPercent,Convert.ToInt32(chances[i],CultureInfo.InvariantCulture));
                seen++;
            }
            if(String.IsNullOrEmpty(report.Summary))report.Summary=Describe(report.Code);
            return report;
        }
        // The Weather Channel's icon numbers, as the weather kinds the tiles draw.
        internal static int CodeFromIcon(int icon)
        {
            if(icon<=4||icon==37||icon==38||icon==47)return 95;
            if(icon>=13&&icon<=16||icon>=41&&icon<=43||icon==46)return 71;
            if(icon>=5&&icon<=12||icon>=17&&icon<=18||icon==35||icon==39||icon==40||icon==45)return 61;
            if(icon>=19&&icon<=22)return 45;
            if(icon>=23&&icon<=28)return 3;
            if(icon==29||icon==30)return 2;
            return 0;
        }
        static string Download(string url)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(url);
            request.Timeout=request.ReadWriteTimeout=10000;
            request.UserAgent=Program.AppName;
            using(var response=request.GetResponse())
            using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))
            {
                var text=new char[262144];
                int count=reader.ReadBlock(text,0,text.Length);
                return new string(text,0,count);
            }
        }
        internal static async Task<bool> SetPlace(string name)
        {
            name=(name??"").Trim();
            if(name.Length==0)
            {
                Preferences.SetMediaAppearance(p=>p.WeatherPlace=null);
                Reset();
                return true;
            }
            string url="https://geocoding-api.open-meteo.com/v1/search?count=1&language=ko&name="+Uri.EscapeDataString(name);
            string json;
            try{json=await BackgroundWork.Run(()=>Download(url));}
            catch(Exception e)
            {
                AppLog.Once("weather-place",e.GetType().Name);
                return false;
            }
            double latitude,longitude;
            string found;
            if(!ParsePlace(json,out found,out latitude,out longitude))return false;
            Reset();
            Preferences.SetMediaAppearance(p=>
            {
                p.WeatherPlace=found;
                p.WeatherLatitude=latitude;
                p.WeatherLongitude=longitude;
            });
            return true;
        }
        internal static bool ParsePlace(string json,out string name,out double latitude,out double longitude)
        {
            name=null;
            latitude=longitude=0;
            var root=new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string,object>;
            object results;
            if(root==null||!root.TryGetValue("results",out results))return false;
            var list=results as IList;
            var first=list!=null&&list.Count>0?list[0] as Dictionary<string,object>:null;
            if(first==null||!first.ContainsKey("latitude")||!first.ContainsKey("longitude")||!first.ContainsKey("name"))return false;
            name=Convert.ToString(first["name"],CultureInfo.InvariantCulture);
            latitude=Convert.ToDouble(first["latitude"],CultureInfo.InvariantCulture);
            longitude=Convert.ToDouble(first["longitude"],CultureInfo.InvariantCulture);
            return !String.IsNullOrEmpty(name);
        }
        internal static WeatherReport Parse(string json,DateTime now)
        {
            var root=(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(json);
            var current=(Dictionary<string,object>)root["current"];
            var report=new WeatherReport
            {
                Temperature=Convert.ToDouble(current["temperature_2m"],CultureInfo.InvariantCulture),
                Code=Convert.ToInt32(current["weather_code"],CultureInfo.InvariantCulture),
                Day=Convert.ToInt32(current["is_day"],CultureInfo.InvariantCulture)!=0
            };
            var hourly=(Dictionary<string,object>)root["hourly"];
            var times=(IList)hourly["time"];
            var chances=(IList)hourly["precipitation_probability"];
            DateTime? rainAt=null;
            int next=0,seen=0;
            for(int i=0;i<times.Count&&i<chances.Count&&seen<12;i++)
            {
                DateTime at;
                if(chances[i]==null||!DateTime.TryParse(Convert.ToString(times[i],CultureInfo.InvariantCulture),CultureInfo.InvariantCulture,DateTimeStyles.None,out at)||at.AddHours(1)<=now)continue;
                int chance=Convert.ToInt32(chances[i],CultureInfo.InvariantCulture);
                if(seen<6)next=Math.Max(next,chance);
                if(chance>=50&&!rainAt.HasValue)rainAt=at;
                seen++;
            }
            report.RainPercent=next;
            report.Summary=rainAt.HasValue&&!Wet(report.Code)?rainAt.Value.ToString("tt h:mm",Loc.Culture)+" 이후에 비가 내릴 가능성이 있습니다":Describe(report.Code);
            return report;
        }
        static bool Wet(int code){return code>=51&&code<=67||code>=80&&code<=82||code>=95;}
        internal static string Describe(int code)
        {
            if(code==0)return "맑음";
            if(code<=2)return "대체로 맑음";
            if(code==3)return "흐림";
            if(code==45||code==48)return "안개";
            if(code>=51&&code<=57)return "이슬비";
            if(code>=61&&code<=67)return "비";
            if(code>=71&&code<=77||code==85||code==86)return "눈";
            if(code>=80&&code<=82)return "소나기";
            if(code>=95)return "뇌우";
            return "날씨 정보";
        }
        const string Cloud="M7,19 A5,5 0 0 1 6,9.1 A7,7 0 0 1 19.5,10 A4.5,4.5 0 0 1 19,19 Z";
        internal static Geometry Symbol(WeatherReport report)
        {
            string data;
            if(report==null)data=Cloud;
            else if(report.Code>=51)data=Cloud+" M8,21 L7,24 M13,21 L12,24 M18,21 L17,24";
            else if(report.Code>=3)data=Cloud;
            else if(report.Day)data="M12,6 A6,6 0 1 1 11.99,6 Z";
            else data="M16,2 A10,10 0 1 0 22,17 A10,10 0 0 1 16,2 Z";
            var shape=Geometry.Parse(data);
            shape.Freeze();
            return shape;
        }
    }
}
