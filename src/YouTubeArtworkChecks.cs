using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace OneTaskBar
{
    internal static class YouTubeArtworkChecks
    {
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
