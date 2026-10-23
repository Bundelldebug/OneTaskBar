using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    // The Windows browser session supplies only a tiny cover. Resolve the exact
    // playing title in that browser's current local profile; never search the web by title.
    internal static class YouTubeArtwork
    {
        [ThreadStatic]internal static string LookupStatus;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Progress(IntPtr state);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open_v2(byte[] file,out IntPtr database,int flags,IntPtr vfs);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr database);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_busy_timeout(IntPtr database,int milliseconds);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern void sqlite3_progress_handler(IntPtr database,int instructions,Progress callback,IntPtr state);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_prepare_v2(IntPtr database,byte[] sql,int bytes,out IntPtr statement,IntPtr tail);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_text(IntPtr statement,int index,byte[] text,int bytes,IntPtr destructor);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_step(IntPtr statement);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_bytes(IntPtr statement,int column);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_text(IntPtr statement,int column);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_finalize(IntPtr statement);
        internal static string VideoId(string address)
        {
            Uri uri;if(!Uri.TryCreate(address,UriKind.Absolute,out uri)||uri.Scheme!="https")return null;string value=null;if(uri.Host.Equals("youtu.be",StringComparison.OrdinalIgnoreCase))value=uri.AbsolutePath.Trim('/');
            else if(uri.Host.Equals("www.youtube.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("youtube.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("m.youtube.com",StringComparison.OrdinalIgnoreCase)){if(uri.AbsolutePath!="/watch")return null;foreach(var part in uri.Query.TrimStart('?').Split('&'))if(part.StartsWith("v=",StringComparison.Ordinal)){value=Uri.UnescapeDataString(part.Substring(2));break;}}
            return value!=null&&Regex.IsMatch(value,@"\A[A-Za-z0-9_-]{11}\z")?value:null;
        }
        internal static string Profile(string appId)
        {
            string folder=String.Equals(appId,"Internet",StringComparison.OrdinalIgnoreCase)||String.Equals(appId,"Internet.exe",StringComparison.OrdinalIgnoreCase)?@"Samsung\Internet\User Data":null;if(folder==null)return null;
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@"AppData\Local",folder),profile="Default",state=Path.Combine(root,"Local State");
            try{if(File.Exists(state)&&new FileInfo(state).Length<4194304){var json=new JavaScriptSerializer {MaxJsonLength=4194304}.DeserializeObject(File.ReadAllText(state)) as Dictionary<string,object>;object value;if(json!=null&&json.TryGetValue("profile",out value)){var info=value as Dictionary<string,object>;if(info!=null&&info.TryGetValue("last_used",out value)){string candidate=Convert.ToString(value);if(candidate=="Default"||Regex.IsMatch(candidate??"",@"\AProfile \d+\z"))profile=candidate;}}}}catch{}
            return Path.Combine(root,profile,"History");
        }
        internal static bool Enabled {get{return Preferences.Current.YouTubeHighResArtwork||!String.IsNullOrEmpty(Preferences.Current.YouTubeArtworkTrack);}}
        internal static string TrackKey(string appId,string title){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes((appId??"").ToLowerInvariant()+"|"+(title??""))));}
        internal static bool Allowed(string appId,string title){return (String.Equals(appId,"Internet",StringComparison.OrdinalIgnoreCase)||String.Equals(appId,"Internet.exe",StringComparison.OrdinalIgnoreCase))&&(Preferences.Current.YouTubeHighResArtwork||String.Equals(Preferences.Current.YouTubeArtworkTrack,TrackKey(appId,title),StringComparison.Ordinal));}
        internal static string Find(string file,string title)
        {
            LookupStatus="missing-profile-or-title";if(String.IsNullOrWhiteSpace(file)||String.IsNullOrWhiteSpace(title)||title.Length>1024||!File.Exists(file))return null;IntPtr database=IntPtr.Zero,statement=IntPtr.Zero;var clock=Stopwatch.StartNew();Progress progress=state=>clock.ElapsedMilliseconds>60?1:0;
            try{if(sqlite3_open_v2(Encoding.UTF8.GetBytes(file+"\0"),out database,1,IntPtr.Zero)!=0)return null;sqlite3_busy_timeout(database,20);sqlite3_progress_handler(database,2000,progress,IntPtr.Zero);string sql="SELECT url FROM urls WHERE title=?1 OR title=?2 ORDER BY last_visit_time DESC LIMIT 9";if(sqlite3_prepare_v2(database,Encoding.UTF8.GetBytes(sql+"\0"),-1,out statement,IntPtr.Zero)!=0)return null;
                for(int i=1;i<=2;i++){var bytes=Encoding.UTF8.GetBytes(i==1?title:title+" - YouTube");sqlite3_bind_text(statement,i,bytes,bytes.Length,new IntPtr(-1));}var ids=new HashSet<string>(StringComparer.Ordinal);int status,rows=0;clock.Restart();while((status=sqlite3_step(statement))==100){rows++;int size=sqlite3_column_bytes(statement,0);if(size<=0||size>8192)continue;var bytes=new byte[size];Marshal.Copy(sqlite3_column_text(statement,0),bytes,0,size);string id=VideoId(Encoding.UTF8.GetString(bytes));if(id!=null)ids.Add(id);}LookupStatus="sqlite="+status+";exactRows="+rows+";videoIds="+ids.Count;if(status!=101||rows>8||ids.Count!=1)return null;foreach(var id in ids)return id;
            }catch(Exception error){AppLog.Once("media-browser-cover",error.GetType().Name);}finally{if(statement!=IntPtr.Zero)sqlite3_finalize(statement);if(database!=IntPtr.Zero)sqlite3_close(database);GC.KeepAlive(progress);}return null;
        }
        internal static byte[] Read(string appId,string title)
        {
            string video=Find(Profile(appId),title);return video==null?null:Download(video);
        }
        internal static byte[] Download(string id)
        {
            if(!Regex.IsMatch(id??"",@"\A[A-Za-z0-9_-]{11}\z"))return null;
            foreach(string size in new[]{"maxresdefault","sddefault"})try{var request=(HttpWebRequest)WebRequest.Create("https://i.ytimg.com/vi/"+id+"/"+size+".jpg");request.Timeout=2200;request.ReadWriteTimeout=1500;request.AllowAutoRedirect=false;request.CookieContainer=null;request.UseDefaultCredentials=false;using(var response=(HttpWebResponse)request.GetResponse()){if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>2097152||!response.ContentType.StartsWith("image/",StringComparison.OrdinalIgnoreCase))continue;using(var stream=response.GetResponseStream())using(var data=new MemoryStream()){var buffer=new byte[16384];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){data.Write(buffer,0,count);if(data.Length>2097152)return null;}if(data.Length<=1024)continue;data.Position=0;var frame=BitmapDecoder.Create(data,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand).Frames[0];if(frame.PixelWidth>=480&&frame.PixelHeight>=270)return data.ToArray();}}}catch(WebException){}catch(Exception error){AppLog.Once("media-high-cover",error.GetType().Name);break;}return null;
        }
    }
}
