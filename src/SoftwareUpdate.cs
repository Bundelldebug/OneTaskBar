using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace OneTaskBar
{
    // Updates come from the project's own GitHub releases and are only contacted when the user asks.
    internal sealed class ReleaseInfo
    {
        internal string Tag,Notes,SetupUrl,Digest,Suffix="";
        internal string VersionText {get{return Version.Major+"."+Version.Minor+"."+Math.Max(0,Version.Build)+(Suffix.Length==1?Suffix:"");}}
        // The number with the build code ("10.0.0 A1A1") for places that show the exact build.
        internal string DetailText {get{return Version.Major+"."+Version.Minor+"."+Math.Max(0,Version.Build)+(Suffix.Length==0?"":(Suffix.Length==1?Suffix:" "+Suffix));}}
        internal long Size;
        internal Version Version;
    }

    internal static class SoftwareUpdate
    {
        internal const string Repository="Bundelldebug/OneTaskBar";
        const string DownloadPrefix="https://github.com/"+Repository+"/releases/download/";
        const long MaxSetupBytes=64L*1024*1024;

        internal static Version Current
        {
            get
            {
                Version value;
                return Version.TryParse(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(),out value)?value:new Version(0,0);
            }
        }

        // "9.5.13", or "9.5.13b" for a re-release of the same version; the letter comes from the product version.
        internal static string CurrentText
        {
            get
            {
                var version=Current;
                string number=version.Major+"."+version.Minor+"."+version.Build;
                string suffix=CurrentSuffix;
                return number+(suffix.Length==1?suffix:"");
            }
        }

        // The exact build: "10.0.0 A1A1", or the same as CurrentText for a build without a code.
        internal static string DetailText
        {
            get
            {
                string suffix=CurrentSuffix;
                return suffix.Length<=1?CurrentText:Current.Major+"."+Current.Minor+"."+Current.Build+" "+suffix;
            }
        }

        internal static string CurrentSuffix
        {
            get
            {
                try
                {
                    var attribute=System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false);
                    if(attribute.Length==0)return "";
                    return Suffix(((System.Reflection.AssemblyInformationalVersionAttribute)attribute[0]).InformationalVersion);
                }
                catch(Exception)
                {
                    return "";
                }
            }
        }

        // What follows the numbers of a tag or product version: one lowercase letter ("v9.5.13b" -> "b") or a build code of
        // two letter+digit pairs ("10.0.0 A1A1" -> "A1A1"), or "". The code counts up in its last digit to 9; the tenth
        // build moves the letter before it on and restarts that digit at 1 (A1A9 -> A1B1).
        internal static string Suffix(string text)
        {
            var code=Regex.Match(text??"",@"^[vV]?\d+\.\d+\.\d+[ _-]?([A-Za-z]\d[A-Za-z]\d)(?![A-Za-z0-9])");
            if(code.Success)return code.Groups[1].Value.ToUpperInvariant();
            var match=Regex.Match(text??"",@"^[vV]?\d+\.\d+\.\d+([a-z])(?![A-Za-z0-9])");
            return match.Success?match.Groups[1].Value:"";
        }

        // Orders suffixes: none, then single letters, then build codes; codes compare letter by letter and digit by digit.
        internal static string SuffixKey(string suffix)
        {
            suffix=suffix??"";
            return suffix.Length==0?"0":suffix.Length==1?"1"+suffix:"2"+suffix.ToUpperInvariant();
        }

        // The next build code after this one: the last digit up to 9, then the letter before it advances and the digit restarts at 1.
        internal static string NextCode(string code)
        {
            if(!Regex.IsMatch(code??"",@"^[A-Z]\d[A-Z]\d$"))return code;
            char tail=code[3],letter=code[2];
            if(tail<'9')return code.Substring(0,3)+(char)(tail+1);
            if(letter<'Z')return code.Substring(0,2)+(char)(letter+1)+"1";
            // The second letter is used up: the first digit and letter move on.
            char digit=code[1],first=code[0];
            if(digit<'9')return code[0].ToString()+(char)(digit+1)+"A1";
            return ((char)(first+1)).ToString()+"1A1";
        }

        // Newest stable release, or null when the repository has none.
        internal static ReleaseInfo Latest()
        {
            string json=Get("https://api.github.com/repos/"+Repository+"/releases/latest");
            return Parse(json);
        }

        internal static ReleaseInfo Parse(string json)
        {
            var root=new JavaScriptSerializer {MaxJsonLength=4*1024*1024}.DeserializeObject(json) as Dictionary<string,object>;
            if(root==null||!root.ContainsKey("tag_name"))return null;
            string tag=Convert.ToString(root["tag_name"]);
            Version version;
            // A re-release of the same version carries a letter after it (v9.5.12b); the number is what is compared.
            if(!Version.TryParse(Regex.Replace(Regex.Replace(Regex.Replace(tag,"^[vV]",""),"-beta.*$",""),"(?<=[0-9])([ _-]?[A-Za-z][0-9][A-Za-z][0-9]|[A-Za-z])$",""),out version))return null;
            var info=new ReleaseInfo {Tag=tag,Version=version,Suffix=Suffix(tag),Notes=root.ContainsKey("body")?Convert.ToString(root["body"]):""};
            var assets=root.ContainsKey("assets")?root["assets"] as object[]:null;
            if(assets!=null)
            {
                foreach(var item in assets.OfType<Dictionary<string,object>>())
                {
                    if(Convert.ToString(item["name"])!="OneTaskBar-Setup.exe")continue;
                    info.SetupUrl=Convert.ToString(item["browser_download_url"]);
                    info.Size=item.ContainsKey("size")?Convert.ToInt64(item["size"]):0;
                    string digest=item.ContainsKey("digest")?Convert.ToString(item["digest"]):null;
                    if(digest!=null&&digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase))info.Digest=digest.Substring(7).ToLowerInvariant();
                }
            }
            return info;
        }

        internal static bool IsNewer(ReleaseInfo release)
        {
            if(release==null)return false;
            var current=Current;
            var latest=release.Version;
            int order=new Version(latest.Major,latest.Minor,Math.Max(0,latest.Build)).CompareTo(new Version(current.Major,current.Minor,Math.Max(0,current.Build)));
            // Same number: a later letter or build code (none < a < b < A1A1 < A1A2 < A1B1) is the newer build.
            if(order==0)order=String.CompareOrdinal(SuffixKey(release.Suffix),SuffixKey(CurrentSuffix));
            return order>0;
        }

        // Downloads the installer to a private temp folder and checks size and SHA-256 against what GitHub published.
        internal static string Download(ReleaseInfo release,Action<int> progress)
        {
            if(release.SetupUrl==null||!release.SetupUrl.StartsWith(DownloadPrefix,StringComparison.Ordinal))throw new InvalidOperationException("릴리스에 설치 파일이 없습니다");
            if(String.IsNullOrEmpty(release.Digest))throw new InvalidOperationException("릴리스의 SHA-256 정보를 확인할 수 없어 설치하지 않습니다");
            if(release.Size<=0||release.Size>MaxSetupBytes)throw new InvalidOperationException("설치 파일 크기가 올바르지 않습니다");
            string folder=Path.Combine(Path.GetTempPath(),"OneTaskBar-update-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"OneTaskBar-Setup.exe");
            var request=Create(release.SetupUrl);
            using(var response=(HttpWebResponse)request.GetResponse())
            using(var input=response.GetResponseStream())
            using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                var buffer=new byte[81920];
                long total=0;
                int read;
                int last=-1;
                while((read=input.Read(buffer,0,buffer.Length))>0)
                {
                    total+=read;
                    if(total>MaxSetupBytes)throw new InvalidOperationException("설치 파일이 예상보다 큽니다");
                    output.Write(buffer,0,read);
                    int percent=(int)(total*100/release.Size);
                    if(percent!=last&&progress!=null){last=percent;progress(Math.Min(100,percent));}
                }
            }
            var file=new FileInfo(path);
            if(file.Length!=release.Size||!String.Equals(Hash(path),release.Digest,StringComparison.OrdinalIgnoreCase))
            {
                try{Directory.Delete(folder,true);}catch(IOException){}
                throw new InvalidOperationException("내려받은 파일이 릴리스와 일치하지 않아 설치하지 않았습니다");
            }
            return path;
        }

        internal static string Hash(string path)
        {
            using(var sha=SHA256.Create())
            using(var stream=File.OpenRead(path))return String.Concat(sha.ComputeHash(stream).Select(b=>b.ToString("x2")));
        }

        // The installer stops this app, replaces the files and starts the new version itself.
        internal static void Install(string setup)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(setup,"--quiet") {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(setup)});
        }

        static HttpWebRequest Create(string url)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(url);
            request.UserAgent="OneTaskBar-Updater/"+CurrentText;
            request.Accept="application/vnd.github+json, application/octet-stream";
            request.Timeout=15000;
            request.ReadWriteTimeout=30000;
            request.AllowAutoRedirect=true;
            return request;
        }

        static string Get(string url)
        {
            using(var response=(HttpWebResponse)Create(url).GetResponse())
            using(var stream=response.GetResponseStream())
            using(var reader=new StreamReader(stream,System.Text.Encoding.UTF8))return reader.ReadToEnd();
        }
    }
}
