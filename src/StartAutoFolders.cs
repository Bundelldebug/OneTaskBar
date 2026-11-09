using System;
using System.Collections.Generic;
using System.Linq;

namespace OneTaskBar
{
    // The app screen's automatic folders, like the app library on a phone: installed apps of one kind are gathered into
    // a folder named for it. Off unless the owner turns it on; turning it off puts every app back.
    internal static class AppCategories
    {
        internal const string KeyPrefix="folder:auto:";
        // The order is the order a name is tried in, so a more specific kind goes first.
        static readonly string[][] kinds=
        {
            new[]{"삼성","samsung","galaxy","갤럭시","bixby","knox","smartthings","smart switch","quick share","samsung"},
            new[]{"개발","visual studio","vscode","code","git","github","python","node","java","android studio","docker","terminal","powershell","cmd","wsl","sdk","jetbrains","intellij","pycharm","postman","sublime","unity","cursor","claude"},
            new[]{"브라우저","chrome","edge","firefox","whale","웨일","brave","opera","vivaldi","internet","인터넷","browser","브라우저"},
            new[]{"게임","steam","epic games","game","게임","riot","league of legends","valorant","minecraft","blizzard","battle.net","ubisoft","origin","xbox","nvidia geforce","roblox"},
            new[]{"소셜","kakao","카카오","discord","telegram","line","slack","teams","zoom","whatsapp","메신저","messenger","skype","wechat","signal"},
            new[]{"Office","word","excel","powerpoint","outlook","onenote","access","publisher","한글","hwp","hancom","한컴","office","acrobat","pdf","notion","obsidian","메모","memo","notes","onedrive"},
            new[]{"미디어","music","spotify","vlc","media player","potplayer","video","영화","tv","youtube","netflix","photos","사진","gallery","갤러리","camera","카메라","podcast","음악","player","플레이어","recorder","녹음","sharex","obs","audacity","premiere","capcut","clip studio"},
            new[]{"디자인","photoshop","illustrator","figma","paint","krita","blender","gimp","lightroom","canva","sketch","penup","after effects","davinci","디자인","studio"},
            new[]{"유틸리티","settings","설정","calculator","계산기","notepad","메모장","cleaner","clean","manager","tool","도구","utility","update","업데이트","diagnos","drive","backup","zip","7-zip","winrar","everything","registry","restore","recovery","defender","보안","security","antivirus","vpn","remote","원격","files","파일","explorer","탐색기","task","작업","snipping","캡처","clock","시계","weather","날씨","map","지도","store","스토어","help","도움말","feedback","피드백","tips","팁","support","지원"}
        };
        internal static string Kind(AppEntry app)
        {
            if(app==null)return null;string text=((app.Name??"")+" "+(app.Key??"")).ToLowerInvariant();
            foreach(var kind in kinds)
                for(int i=1;i<kind.Length;i++)
                    if(Contains(text,kind[i].ToLowerInvariant()))return kind[0];
            return null;
        }
        // Short Latin words must stand alone ("git" is not in "digital"); Korean words and longer names match anywhere.
        static bool Contains(string text,string word)
        {
            int at=text.IndexOf(word,StringComparison.Ordinal);
            if(word.Length>4||word.Any(c=>c>127))return at>=0;
            while(at>=0)
            {
                bool before=at==0||!Char.IsLetter(text[at-1]);
                int end=at+word.Length;bool after=end>=text.Length||!Char.IsLetter(text[end]);
                if(before&&after)return true;
                at=text.IndexOf(word,at+1,StringComparison.Ordinal);
            }
            return false;
        }
    }
    internal sealed partial class StartPinStore
    {
        const int MinimumInFolder=3;
        // Gathers loose apps by kind into automatic folders, or dissolves them again. A kind with fewer than three apps stays loose.
        internal bool ApplyAutoFolders(List<AppEntry> apps,bool enabled)
        {
            var kindOf=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var app in apps)
            {
                if(String.IsNullOrEmpty(app.Key)||kindOf.ContainsKey(app.Key))continue;
                string kind=AppCategories.Kind(app);if(kind!=null)kindOf[app.Key]=kind;
            }
            return Change(list=>
            {
                string before=FolderSignature(list);
                for(int i=list.Count-1;i>=0;i--)
                {
                    var folder=list[i];
                    if(!folder.Folder||!folder.Key.StartsWith(AppCategories.KeyPrefix,StringComparison.Ordinal))continue;
                    list.RemoveAt(i);list.InsertRange(i,folder.Children);
                }
                if(enabled)
                {
                    var groups=list.Where(n=>!n.Folder&&kindOf.ContainsKey(n.Key)).GroupBy(n=>kindOf[n.Key],StringComparer.CurrentCultureIgnoreCase).Where(g=>g.Count()>=MinimumInFolder).ToArray();
                    foreach(var group in groups)
                    {
                        var children=group.ToList();int index=list.IndexOf(children[0]);
                        foreach(var child in children)list.Remove(child);
                        list.Insert(Math.Min(index,list.Count),new StartPinNode {Key=AppCategories.KeyPrefix+Uri.EscapeDataString(group.Key.ToUpperInvariant()),Name=group.Key,Children=children});
                    }
                }
                return before!=FolderSignature(list);
            });
        }
    }
}
