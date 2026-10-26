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
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void SqlFunction(IntPtr context,int count,IntPtr values);
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
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_create_function_v2(IntPtr database,byte[] name,int count,int encoding,IntPtr state,SqlFunction function,IntPtr step,IntPtr final,IntPtr destroy);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_value_text(IntPtr value);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_value_bytes(IntPtr value);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern void sqlite3_result_null(IntPtr context);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern void sqlite3_result_text(IntPtr context,byte[] text,int bytes,IntPtr destructor);
        static void VideoKey(IntPtr context,int count,IntPtr values)
        {
            try{if(count==1&&values!=IntPtr.Zero){var value=Marshal.ReadIntPtr(values);var text=sqlite3_value_text(value);int size=sqlite3_value_bytes(value);if(text!=IntPtr.Zero&&size>0&&size<=8192){var bytes=new byte[size];Marshal.Copy(text,bytes,0,size);string id=VideoId(Encoding.UTF8.GetString(bytes));if(id!=null){bytes=Encoding.UTF8.GetBytes(id);sqlite3_result_text(context,bytes,bytes.Length,new IntPtr(-1));return;}}}}catch{}sqlite3_result_null(context);
        }
        internal static string VideoId(string address)
        {
            Uri uri;if(!Uri.TryCreate(address,UriKind.Absolute,out uri)||uri.Scheme!="https")return null;string value=null;if(uri.Host.Equals("youtu.be",StringComparison.OrdinalIgnoreCase))value=uri.AbsolutePath.Trim('/');
            else if(uri.Host.Equals("www.youtube.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("youtube.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("m.youtube.com",StringComparison.OrdinalIgnoreCase)){if(uri.AbsolutePath!="/watch")return null;foreach(var part in uri.Query.TrimStart('?').Split('&'))if(part.StartsWith("v=",StringComparison.Ordinal)){value=Uri.UnescapeDataString(part.Substring(2));break;}}
            return value!=null&&Regex.IsMatch(value,@"\A[A-Za-z0-9_-]{11}\z")?value:null;
        }
        internal static string Profile(string appId)
        {
            string folder=Supported(appId)?@"Samsung\Internet\User Data":null;if(folder==null)return null;
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@"AppData\Local",folder),profile="Default",state=Path.Combine(root,"Local State");
            try{if(File.Exists(state)&&new FileInfo(state).Length<4194304){var json=new JavaScriptSerializer {MaxJsonLength=4194304}.DeserializeObject(File.ReadAllText(state)) as Dictionary<string,object>;object value;if(json!=null&&json.TryGetValue("profile",out value)){var info=value as Dictionary<string,object>;if(info!=null&&info.TryGetValue("last_used",out value)){string candidate=Convert.ToString(value);if(candidate=="Default"||Regex.IsMatch(candidate??"",@"\AProfile \d+\z"))profile=candidate;}}}}catch{}
            return Path.Combine(root,profile,"History");
        }
        internal static bool Enabled {get{return Preferences.Current.YouTubeHighResArtwork||!String.IsNullOrEmpty(Preferences.Current.YouTubeArtworkTrack);}}
        internal static string TrackKey(string appId,string title){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes((appId??"").ToLowerInvariant()+"|"+(title??""))));}
        internal static bool Supported(string appId){return String.Equals(appId,"Internet",StringComparison.OrdinalIgnoreCase)||String.Equals(appId,"Internet.exe",StringComparison.OrdinalIgnoreCase)||String.Equals(appId,"samsunginternet",StringComparison.OrdinalIgnoreCase)||String.Equals(appId,"samsunginternet.exe",StringComparison.OrdinalIgnoreCase);}
        internal static bool Allowed(string appId,string title){return Supported(appId)&&(Preferences.Current.YouTubeHighResArtwork||String.Equals(Preferences.Current.YouTubeArtworkTrack,TrackKey(appId,title),StringComparison.Ordinal));}
        internal static string Find(string file,string title)
        {
            string id=Query(file,title,false);if(id!=null||!(LookupStatus=="sqlite-prepare=5"||LookupStatus=="sqlite-open=5"||LookupStatus!=null&&LookupStatus.StartsWith("sqlite=5;",StringComparison.Ordinal)))return id;
            // Chromium can retain an exclusive SQLite advisory lock between writes.
            // Read only a stable rollback-mode file, never write it or copy its history.
            string before=StableStamp(file);if(before==null){LookupStatus="browser-writing";return null;}id=Query(file,title,true);string after=StableStamp(file);if(after!=before){LookupStatus="browser-changed";return null;}LookupStatus="readonly-stable;"+LookupStatus;return id;
        }
        static string StableStamp(string file)
        {
            try{
                var wal=new FileInfo(file+"-wal");if(wal.Exists&&wal.Length>0)return null;
                var journal=new FileInfo(file+"-journal");if(journal.Exists&&journal.Length>0){var prefix=new byte[8];using(var stream=new FileStream(journal.FullName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){if(stream.Read(prefix,0,8)!=8)return null;}foreach(byte value in prefix)if(value!=0)return null;}
                var header=new byte[100];long length;using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){if(stream.Read(header,0,100)!=100||header[18]!=1||header[19]!=1)return null;length=stream.Length;}
                var info=new FileInfo(file);return length+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks+"|"+Convert.ToBase64String(header);
            }catch{return null;}
        }
        static string Query(string file,string title,bool unlocked)
        {
            LookupStatus="missing-profile-or-title";if(String.IsNullOrWhiteSpace(file)||String.IsNullOrWhiteSpace(title)||title.Length>1024||!File.Exists(file))return null;IntPtr database=IntPtr.Zero,statement=IntPtr.Zero;var clock=Stopwatch.StartNew();Progress progress=state=>clock.ElapsedMilliseconds>60?1:0;SqlFunction videoKey=VideoKey;
            try{string path=unlocked?new Uri(Path.GetFullPath(file)).AbsoluteUri+"?mode=ro&nolock=1":file;int opened=sqlite3_open_v2(Encoding.UTF8.GetBytes(path+"\0"),out database,unlocked?65:1,IntPtr.Zero);if(opened!=0){LookupStatus="sqlite-open="+opened;return null;}sqlite3_busy_timeout(database,20);sqlite3_progress_handler(database,2000,progress,IntPtr.Zero);int registered=sqlite3_create_function_v2(database,Encoding.UTF8.GetBytes("one_taskbar_video\0"),1,2049,IntPtr.Zero,videoKey,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);if(registered!=0){LookupStatus="sqlite-function="+registered;return null;}
                // Deduplicate playlist/index/seek URLs by validated video ID before limiting results.
                // The title predicate remains exact; two distinct videos still fail closed.
                string sql="SELECT DISTINCT one_taskbar_video(url) AS video FROM urls WHERE (title=?1 OR title=?2) AND one_taskbar_video(url) IS NOT NULL LIMIT 2";int prepared=sqlite3_prepare_v2(database,Encoding.UTF8.GetBytes(sql+"\0"),-1,out statement,IntPtr.Zero);if(prepared!=0){LookupStatus="sqlite-prepare="+prepared;return null;}
                for(int i=1;i<=2;i++){var bytes=Encoding.UTF8.GetBytes(i==1?title:title+" - YouTube");sqlite3_bind_text(statement,i,bytes,bytes.Length,new IntPtr(-1));}int status,rows=0;string video=null;clock.Restart();while((status=sqlite3_step(statement))==100){rows++;int size=sqlite3_column_bytes(statement,0);if(size!=11)continue;var bytes=new byte[size];Marshal.Copy(sqlite3_column_text(statement,0),bytes,0,size);video=Encoding.UTF8.GetString(bytes);}LookupStatus="sqlite="+status+";exactVideoIds="+rows;return status==101&&rows==1?video:null;
            }catch(Exception error){AppLog.Once("media-browser-cover",error.GetType().Name);}finally{if(statement!=IntPtr.Zero)sqlite3_finalize(statement);if(database!=IntPtr.Zero)sqlite3_close(database);GC.KeepAlive(progress);GC.KeepAlive(videoKey);}return null;
        }
        internal static byte[] Read(string appId,string title)
        {
            if(!Allowed(appId,title)){LookupStatus="not-authorized";return null;}string video=Find(Profile(appId),title);return video==null?null:Download(video);
        }
        internal static byte[] Download(string id)
        {
            if(!Regex.IsMatch(id??"",@"\A[A-Za-z0-9_-]{11}\z"))return null;
            foreach(string size in new[]{"maxresdefault","sddefault"})try{var request=(HttpWebRequest)WebRequest.Create("https://i.ytimg.com/vi/"+id+"/"+size+".jpg");request.Timeout=2200;request.ReadWriteTimeout=1500;request.AllowAutoRedirect=false;request.CookieContainer=null;request.UseDefaultCredentials=false;using(var response=(HttpWebResponse)request.GetResponse()){if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>2097152||!response.ContentType.StartsWith("image/",StringComparison.OrdinalIgnoreCase))continue;using(var stream=response.GetResponseStream())using(var data=new MemoryStream()){var buffer=new byte[16384];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){data.Write(buffer,0,count);if(data.Length>2097152)return null;}if(data.Length<=1024)continue;data.Position=0;var frame=BitmapDecoder.Create(data,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand).Frames[0];if(frame.PixelWidth>=480&&frame.PixelHeight>=270)return data.ToArray();}}}catch(WebException){}catch(Exception error){AppLog.Once("media-high-cover",error.GetType().Name);break;}return null;
        }
    }
}
