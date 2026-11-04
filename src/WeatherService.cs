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

    // Optional. Nothing is sent until the user enters a place. Requests carry only
    // that place name or its coordinates, run only while a weather tile is shown,
    // and are spaced at least 30 minutes apart.
    internal static class WeatherService
    {
        static DateTime nextAt=DateTime.MinValue;
        static bool busy;
        internal static WeatherReport Current {get;private set;}
        internal static int Requests {get;private set;}
        internal static bool Configured {get{return !String.IsNullOrWhiteSpace(Preferences.Current.WeatherPlace);}}
        internal static void Reset()
        {
            Current=null;
            nextAt=DateTime.MinValue;
        }
        internal static void Request()
        {
            var now=DateTime.UtcNow;
            if(busy||now<nextAt||!Configured)return;
            busy=true;
            nextAt=now.AddMinutes(30);
            Requests++;
            double latitude=Preferences.Current.WeatherLatitude,longitude=Preferences.Current.WeatherLongitude;
            Fetch(latitude,longitude);
        }
        static async void Fetch(double latitude,double longitude)
        {
            try
            {
                string url=String.Format(CultureInfo.InvariantCulture,"https://api.open-meteo.com/v1/forecast?latitude={0:0.###}&longitude={1:0.###}&current=temperature_2m,weather_code,is_day&hourly=precipitation_probability&forecast_days=2&timezone=auto",latitude,longitude);
                string json=await BackgroundWork.Run(()=>Download(url));
                Current=Parse(json,DateTime.Now);
            }
            catch(Exception e)
            {
                nextAt=DateTime.UtcNow.AddMinutes(10);
                AppLog.Once("weather",e.GetType().Name);
            }
            finally{busy=false;}
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
            report.Summary=rainAt.HasValue&&!Wet(report.Code)?rainAt.Value.ToString("tt h:mm",CultureInfo.GetCultureInfo("ko-KR"))+" 이후에 비가 내릴 가능성이 있습니다":Describe(report.Code);
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
