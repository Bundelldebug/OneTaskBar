using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    // Optional read-only fallback for artwork omitted by the notification-listener projection.
    // Called only for an already authorized listener item. No history enumeration or DB writes.
    internal static class NativeToastAssets
    {
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open_v2(byte[] path,out IntPtr database,int flags,IntPtr vfs);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr database);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_busy_timeout(IntPtr database,int milliseconds);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_prepare_v2(IntPtr database,byte[] sql,int bytes,out IntPtr statement,IntPtr tail);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_int64(IntPtr statement,int index,long value);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_text(IntPtr statement,int index,byte[] text,int bytes,IntPtr destructor);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_step(IntPtr statement);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_bytes(IntPtr statement,int column);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_blob(IntPtr statement,int column);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_finalize(IntPtr statement);
        [DllImport("wininet.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetUrlCacheEntryInfo(string url,IntPtr info,ref int bytes);
        internal static NotificationPayload Read(string appId,uint id,string[] text)
        {
            string file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@"AppData\Local\Microsoft\Windows\Notifications\wpndatabase.db");return ReadFile(file,appId,id,text);
        }
        internal static NotificationPayload ReadFile(string file,string appId,uint id,string[] text)
        {
            IntPtr database=IntPtr.Zero,statement=IntPtr.Zero;
            try{
                if(String.IsNullOrEmpty(appId)||!File.Exists(file)||sqlite3_open_v2(Encoding.UTF8.GetBytes(file+"\0"),out database,1,IntPtr.Zero)!=0)return null;
                sqlite3_busy_timeout(database,30);string query="SELECT n.Payload FROM Notification n JOIN NotificationHandler h ON n.HandlerId=h.RecordId WHERE n.Id=?1 AND h.PrimaryId=?2 LIMIT 1";
                if(sqlite3_prepare_v2(database,Encoding.UTF8.GetBytes(query+"\0"),-1,out statement,IntPtr.Zero)!=0)return null;var sender=Encoding.UTF8.GetBytes(appId);sqlite3_bind_int64(statement,1,id);sqlite3_bind_text(statement,2,sender,sender.Length,new IntPtr(-1));if(sqlite3_step(statement)!=100)return null;
                int size=sqlite3_column_bytes(statement,0);if(size<=0||size>524288)return null;var bytes=new byte[size];Marshal.Copy(sqlite3_column_blob(statement,0),bytes,0,size);string xml=Decode(bytes);return NotificationPayload.Parse(xml,text);
            }catch(Exception error){AppLog.Once("notification-native-artwork",error.GetType().Name);return null;}
            finally{if(statement!=IntPtr.Zero)sqlite3_finalize(statement);if(database!=IntPtr.Zero)sqlite3_close(database);}
        }
        internal static string Decode(byte[] bytes){bool unicode=bytes.Length>1&&(bytes[0]==255&&bytes[1]==254||bytes[1]==0);return (unicode?Encoding.Unicode:Encoding.UTF8).GetString(bytes).TrimStart('\ufeff').TrimEnd('\0');}
        internal static ImageSource Image(string appId,string source,bool logo,int pixels=640)
        {
            try{
                string path=Resolve(appId,source);if(path==null||!File.Exists(path)||new FileInfo(path).Length>8388608)return null;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){var image=BitmapDecode.Frozen(stream,logo?96:Math.Max(96,Math.Min(1280,pixels)));return logo?AppIcons.Normalize(image):image;}
            }catch{return null;}
        }
        internal static string Resolve(string appId,string source)
        {
            if(String.IsNullOrWhiteSpace(source)||source.Length>4096)return null;Uri uri;if(!Uri.TryCreate(source,UriKind.Absolute,out uri))return null;if(uri.IsFile)return uri.IsUnc?null:uri.LocalPath;
            if(uri.Scheme=="http"||uri.Scheme=="https")return Cached(uri.AbsoluteUri);
            if(uri.Scheme!="ms-appx"&&uri.Scheme!="ms-appdata")return null;
            var app=Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(appId);if(app==null||app.Package==null)return null;string root,relative=Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
            if(uri.Scheme=="ms-appx")root=app.Package.InstalledPath;
            else{root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@"AppData\Local\Packages",app.Package.Id.FamilyName);if(relative.StartsWith("local/",StringComparison.OrdinalIgnoreCase))relative="LocalState/"+relative.Substring(6);else if(relative.StartsWith("roaming/",StringComparison.OrdinalIgnoreCase))relative="RoamingState/"+relative.Substring(8);else if(relative.StartsWith("temp/",StringComparison.OrdinalIgnoreCase))relative="TempState/"+relative.Substring(5);else return null;}
            return Within(root,relative);
        }
        internal static string Within(string root,string relative){string directory=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;string path=Path.GetFullPath(Path.Combine(directory,relative.Replace('/',Path.DirectorySeparatorChar)));return path.StartsWith(directory,StringComparison.OrdinalIgnoreCase)?path:null;}
        static string Cached(string url)
        {
            int size=0;GetUrlCacheEntryInfo(url,IntPtr.Zero,ref size);if(size<=0||size>1048576)return null;IntPtr buffer=Marshal.AllocHGlobal(size);try{if(!GetUrlCacheEntryInfo(url,buffer,ref size))return null;var pointer=Marshal.ReadIntPtr(buffer,IntPtr.Size==8?16:8);return pointer==IntPtr.Zero?null:Marshal.PtrToStringUni(pointer);}finally{Marshal.FreeHGlobal(buffer);}
        }
    }
}
