using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal sealed class SearchHistory
    {
        readonly string file;internal readonly List<string> Items=new List<string>();
        internal SearchHistory(string path) {file=path;try{if(File.Exists(file)){var saved=new JavaScriptSerializer().Deserialize<List<string>>(File.ReadAllText(file));if(saved!=null)foreach(var value in saved.Where(v=>!String.IsNullOrWhiteSpace(v)).Distinct(StringComparer.CurrentCultureIgnoreCase).Take(10))Items.Add(value.Length>120?value.Substring(0,120):value);}}catch{}}
        internal void Add(string query) {query=(query??"").Trim();if(query.Length==0||query.Length>120)return;Items.RemoveAll(v=>String.Equals(v,query,StringComparison.CurrentCultureIgnoreCase));Items.Insert(0,query);if(Items.Count>10)Items.RemoveRange(10,Items.Count-10);Save();}
        internal void Remove(string query) {Items.RemoveAll(v=>String.Equals(v,query,StringComparison.CurrentCultureIgnoreCase));Save();}
        internal void Clear() {Items.Clear();Save();}
        void Save() {try{Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(Items));if(File.Exists(file)){try{File.Replace(temp,file,null);}catch(IOException){File.Copy(temp,file,true);File.Delete(temp);}}else File.Move(temp,file);}catch(Exception e){AppLog.Once("search-history",e.Message);}}
    }
    internal sealed class SearchSetting
    {
        internal string Name,Keywords,Icon,Uri;
        internal SearchSetting(string name,string keywords,string icon,string uri) {Name=name;Keywords=keywords;Icon=icon;Uri=uri;}
    }
    internal sealed class SearchFile
    {
        internal string Name,Path;internal bool Folder,Picture;internal ImageSource Icon;
    }
    internal static class LauncherSearch
    {
        internal static readonly SearchSetting[] Settings={
            new SearchSetting("알림","notification 방해금지 집중","focus","ms-settings:notifications"),
            new SearchSetting("디스플레이","밝기 화면 해상도 배율 display brightness","window","ms-settings:display"),
            new SearchSetting("소리","음량 음소거 volume sound 오디오","volume","ms-settings:sound"),
            new SearchSetting("화면 꾸미기","배경 테마 색상 wallpaper theme","window","ms-settings:personalization"),
            new SearchSetting("성능 및 전원","배터리 power battery 절전","battery","ms-settings:powersleep"),
            new SearchSetting("Wi-Fi","와이파이 인터넷 무선 네트워크 network","wifi","ms-settings:network-wifi"),
            new SearchSetting("블루투스","bluetooth 기기 연결 장치","bluetooth","ms-settings:bluetooth"),
            new SearchSetting("앱","프로그램 application 설치 제거","apps","ms-settings:appsfeatures"),
            new SearchSetting("키보드","keyboard 입력 언어 터치","window","ms-settings:typing"),
            new SearchSetting("One TaskBar 설정","런처 독 시작 검색","settings",null)};
        static string Fold(string text) {return new string((text??"").Normalize(NormalizationForm.FormKC).ToLowerInvariant().Where(c=>!Char.IsWhiteSpace(c)).ToArray());}
        internal static string Initials(string text) {var result=new StringBuilder();foreach(char c in Fold(text)){if(c>=0xac00&&c<=0xd7a3)result.Append((char)(0x1100+(c-0xac00)/588));else result.Append(c);}return result.ToString();}
        internal static bool Matches(string text,string query) {string normal=Fold(text),heads=Initials(text);return (query??"").Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries).All(word=>normal.Contains(Fold(word))||heads.Contains(Fold(word)));}
        static string Aliases(AppEntry app) {string name=app.Name??"",id=(app.AppID??"")+" "+(app.Target??"");if((name+id).IndexOf("ChatGPT",StringComparison.OrdinalIgnoreCase)>=0)name+=" 챗지피티 챗GPT";if((name+id).IndexOf("Codex",StringComparison.OrdinalIgnoreCase)>=0)name+=" 코덱스";return name;}
        internal static List<AppEntry> Apps(IEnumerable<AppEntry> apps,string query) {return apps.Where(a=>Matches(Aliases(a),query)).OrderBy(a=>Fold(a.Name)==Fold(query)?0:Fold(a.Name).StartsWith(Fold(query))?1:2).ThenBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).Take(40).ToList();}
        internal static bool IsPicture(string path) {return new[]{".png",".jpg",".jpeg",".bmp"}.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());}
        internal static List<SearchFile> Files(string query,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();var result=new List<SearchFile>();if(String.IsNullOrWhiteSpace(query)||query.Length>120)return result;
            string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            try {using(var connection=new OleDbConnection("Provider=Search.CollatorDSO;Extended Properties='Application=Windows';")) {
                connection.Open();using(var command=connection.CreateCommand()) {
                    var words=query.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries).Take(6).Select(w=>w.Replace("'","''").Replace("%","").Replace("_","").Replace("[","").Replace("]","")).Where(w=>w.Length>0).ToArray();
                    if(words.Length==0)return result;
                    command.CommandText="SELECT TOP 30 System.ItemNameDisplay, System.ItemPathDisplay FROM SYSTEMINDEX WHERE SCOPE='file:"+home.Replace('\\','/').Replace("'","''")+"' AND "+String.Join(" AND ",words.Select(w=>"System.ItemNameDisplay LIKE '%"+w+"%'"))+" ORDER BY System.DateModified DESC";command.CommandTimeout=2;
                    using(token.Register(()=> {try{command.Cancel();}catch{}}))using(var reader=command.ExecuteReader())while(reader!=null&&reader.Read()){token.ThrowIfCancellationRequested();string path=reader.IsDBNull(1)?null:reader.GetString(1);if(!String.IsNullOrEmpty(path)&&Inside(path,home))AddFile(result,path);}
                }
            }}catch(OperationCanceledException){throw;}catch{}
            if(result.Count==0) {
                var queue=new Queue<Tuple<string,int>>();foreach(string path in new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),Path.Combine(home,"Downloads")}.Distinct(StringComparer.OrdinalIgnoreCase))if(Directory.Exists(path))queue.Enqueue(Tuple.Create(path,0));
                var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var watch=Stopwatch.StartNew();int examined=0;
                while(queue.Count>0&&result.Count<24&&examined<6500&&watch.ElapsedMilliseconds<900) {
                    token.ThrowIfCancellationRequested();var item=queue.Dequeue();if(!visited.Add(item.Item1))continue;
                    try {foreach(string path in Directory.EnumerateFileSystemEntries(item.Item1)) {
                        token.ThrowIfCancellationRequested();if(++examined>6500||watch.ElapsedMilliseconds>=900||result.Count>=24)break;
                        var attributes=File.GetAttributes(path);if((attributes&(FileAttributes.ReparsePoint|FileAttributes.Hidden|FileAttributes.System))!=0)continue;
                        if(Matches(Path.GetFileName(path),query))AddFile(result,path);
                        if((attributes&FileAttributes.Directory)!=0&&item.Item2<3)queue.Enqueue(Tuple.Create(path,item.Item2+1));
                    }}catch(OperationCanceledException){throw;}catch{}
                }
            }
            foreach(var file in result){token.ThrowIfCancellationRequested();try{
                if(file.Picture){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=220;image.UriSource=new Uri(file.Path);image.EndInit();image.Freeze();file.Icon=image;}
                else file.Icon=Native.AppIcon(file.Path);
            }catch{}}
            return result;
        }
        static bool Inside(string path,string home) {try{return Path.GetFullPath(path).StartsWith(Path.GetFullPath(home).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}catch{return false;}}
        static void AddFile(List<SearchFile> result,string path) {if(result.Any(r=>String.Equals(r.Path,path,StringComparison.OrdinalIgnoreCase)))return;bool folder=Directory.Exists(path);if(!folder&&!File.Exists(path))return;result.Add(new SearchFile {Name=Path.GetFileName(path),Path=path,Folder=folder,Picture=!folder&&IsPicture(path)});}
    }
}
