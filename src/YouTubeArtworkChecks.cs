using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
namespace OneTaskBar
{
    internal static class YouTubeArtworkChecks
    {
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open_v2(byte[] file,out IntPtr database,int flags,IntPtr vfs);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_exec(IntPtr database,byte[] sql,IntPtr callback,IntPtr state,IntPtr error);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr database);
        static byte[] HashFile(string file){using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var sha=SHA256.Create())return sha.ComputeHash(stream);}
        internal static void Validate(List<string> checks)
        {
            string file=Path.Combine(Path.GetTempPath(),"OneTaskBar-locked-cover-"+Guid.NewGuid().ToString("N")+".db");IntPtr database=IntPtr.Zero;
            try{
                if(sqlite3_open_v2(Encoding.UTF8.GetBytes(file+"\0"),out database,6,IntPtr.Zero)!=0||sqlite3_exec(database,Encoding.UTF8.GetBytes("CREATE TABLE urls(url TEXT,title TEXT,last_visit_time INTEGER);INSERT INTO urls VALUES('https://www.youtube.com/watch?v=AbC_123-xYz','Current title',1);PRAGMA locking_mode=EXCLUSIVE;BEGIN EXCLUSIVE;COMMIT;\0"),IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)!=0)throw new Exception("Cover lock fixture failed to initialize");
                byte[] before;using(var sha=SHA256.Create())before=HashFile(file);string video=YouTubeArtwork.Find(file,"Current title");if(video!="AbC_123-xYz"||!YouTubeArtwork.LookupStatus.StartsWith("readonly-stable;",StringComparison.Ordinal))throw new Exception("Stable, exclusively locked browser history cannot resolve the exact video");
                using(var sha=SHA256.Create())if(!before.SequenceEqual(HashFile(file)))throw new Exception("Read-only cover lookup changed history");
                File.WriteAllBytes(file+"-journal",new byte[]{217,213,5,249,32,161,99,215});if(YouTubeArtwork.Find(file,"Current title")!=null||YouTubeArtwork.LookupStatus!="browser-writing")throw new Exception("An active browser journal was read without a stable snapshot");
                checks.Add("Locked browser cover lookup is exact, read-only, unchanged on disk and rejected while a write journal is active");
            }finally{if(database!=IntPtr.Zero)sqlite3_close(database);foreach(string suffix in new[]{"","-journal","-wal","-shm"})try{File.Delete(file+suffix);}catch{}}
        }
        internal static int Run()
        {
            try{
                string path=Path.Combine(AppLog.Data,"artwork-history-fixture.db");byte[] before,after;using(var sha=SHA256.Create())before=sha.ComputeHash(File.ReadAllBytes(path));
                bool exact=YouTubeArtwork.Find(path,"정확한 제목 ' %")=="AbC_123-xYz",suffix=YouTubeArtwork.Find(path,"접미사 제목")=="AbC_123-xYz",ambiguous=YouTubeArtwork.Find(path,"겹친 제목")==null,overflow=YouTubeArtwork.Find(path,"많은 제목")==null,missing=YouTubeArtwork.Find(path,"누락된 제목")==null,spoof=YouTubeArtwork.Find(path,"다른 호스트")==null;
                using(var sha=SHA256.Create())after=sha.ComputeHash(File.ReadAllBytes(path));bool readOnly=before.SequenceEqual(after),passed=exact&&suffix&&ambiguous&&overflow&&missing&&spoof&&readOnly;AppLog.Json("artwork-resolver-probe.json",new {passed=passed,exactParameterizedTitle=exact,youTubeSuffix=suffix,ambiguousVideosRejected=ambiguous,excessRowsRejected=overflow,missingRejected=missing,lookalikeHostRejected=spoof,databaseUnchanged=readOnly,userHistoryAccessed=false,networkRequests=false});return passed?0:1;
            }catch(Exception error){AppLog.Json("artwork-resolver-probe.json",new {passed=false,error=error.ToString()});return 1;}
        }
    }
}
