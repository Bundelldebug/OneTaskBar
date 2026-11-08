using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OneTaskBar
{
    // Samsung's PC apps have no connection API, so each feature is read from what it leaves on the system:
    //  second screen  - the Miracast device of the tablet plus the "SecondScreen" monitor it creates
    //  camera share   - the phone-named camera device, and the last connection state in Camera Share's own log
    //  storage share  - the last connection line in Storage Share's own log
    // The read runs off the interface thread and is repeated at most every few seconds.
    internal static class SamsungLink
    {
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
        // Features that report which device they are linked to, like Galaxy Buds do.
        internal static bool Reports(string key){return key=="second-screen"||key=="storage-share"||key=="camera-share";}

        static string second,camera,storage;
        static DateTime checkedAt=DateTime.MinValue;
        static int running;

        // Name of the device a feature is connected to, or null. Starts a refresh in the background when the answer is stale.
        internal static string Device(string key)
        {
            if((DateTime.UtcNow-checkedAt).TotalSeconds>=5&&System.Threading.Interlocked.CompareExchange(ref running,1,0)==0)Task.Run(()=>Refresh());
            return key=="second-screen"?second:key=="camera-share"?camera:key=="storage-share"?storage:null;
        }

        static void Refresh()
        {
            try
            {
                string tablet=null,cameraName=null;bool monitor=false;
                using(var search=new ManagementObjectSearcher("SELECT Name,PNPClass,PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass='Miracast' OR PNPClass='Camera' OR Name LIKE '%(SecondScreen)%'"))
                using(var found=search.Get())
                {
                    foreach(ManagementBaseObject item in found)
                    {
                        string name=item["Name"] as string,kind=item["PNPClass"] as string,id=(item["PNPDeviceID"] as string)??"";
                        if(String.IsNullOrEmpty(name))continue;
                        if(name.IndexOf("(SecondScreen)",StringComparison.OrdinalIgnoreCase)>=0)monitor=true;
                        else if(kind=="Miracast"&&id.EndsWith("#MIRACAST",StringComparison.OrdinalIgnoreCase)&&tablet==null)tablet=name;
                        else if(kind=="Camera"&&id.StartsWith("ROOT\\CAMERASHAREDRIVERSOURCE",StringComparison.OrdinalIgnoreCase))cameraName=Clean(name);
                    }
                }
                second=monitor?(tablet??"연결됨"):null;
                camera=cameraName!=null&&LastState(Package("16297BCCB59BC","CameraShare.log"),@"connection state = (\d)")>=1?cameraName:null;
                storage=Storage();
            }
            catch(Exception error)
            {
                AppLog.Once("samsung-link",error.GetType().Name);
            }
            finally
            {
                checkedAt=DateTime.UtcNow;
                System.Threading.Interlocked.Exchange(ref running,0);
            }
        }

        static string Package(string family,string log)
        {
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Packages");
            try
            {
                foreach(string folder in Directory.GetDirectories(root,"SAMSUNGELECTRONICSCoLtd."+family+"_*"))return Path.Combine(folder,"LocalState","Logs",log);
            }
            catch(IOException){}
            return null;
        }

        // The log is shared with the running app, so it is read from its end without locking it.
        static string Tail(string path,int bytes)
        {
            if(path==null||!File.Exists(path))return "";
            try
            {
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                {
                    stream.Seek(Math.Max(0,stream.Length-bytes),SeekOrigin.Begin);
                    using(var reader=new StreamReader(stream))return reader.ReadToEnd();
                }
            }
            catch(IOException){return "";}
        }

        static int LastState(string path,string pattern)
        {
            var matches=Regex.Matches(Tail(path,600000),pattern);
            return matches.Count==0?-1:Int32.Parse(matches[matches.Count-1].Groups[1].Value);
        }

        // Storage Share writes its tray tooltip to its log whenever the link changes ("저장공간 공유 - <phone>에 연결됨").
        // The newest such line says what it is connected to now; a tooltip without a phone means nothing is connected.
        static string Storage()
        {
            string path=Package("4438638898209","StorageShare.log");
            foreach(int bytes in new[]{600000,6000000})
            {
                var lines=Regex.Matches(Tail(path,bytes),@"Updating system tray tooltip to: ([^\r\n]+)");
                if(lines.Count==0)continue;
                var phone=Regex.Match(lines[lines.Count-1].Groups[1].Value,@"-\s*(.+?)에 연결됨");
                return phone.Success?phone.Groups[1].Value.Trim():null;
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
