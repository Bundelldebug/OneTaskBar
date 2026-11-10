using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneTaskBar
{
    // Launcher search helpers for Korean: text typed with the English layout still on (qmfkdnwj) is turned back
    // into Hangul (브라우저), Hangul typed where English was meant is turned back into letters, and well-known
    // apps with English names answer to their Korean names.
    internal static class SearchKorean
    {
        const string Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        const string Jung = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
        const string Jong = " ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";
        const string Letters = "qwertyuiopasdfghjklzxcvbnm";
        const string Plain = "ㅂㅈㄷㄱㅅㅛㅕㅑㅐㅔㅁㄴㅇㄹㅎㅗㅓㅏㅣㅋㅌㅊㅍㅠㅜㅡ";
        const string ShiftLetters = "qwertop";
        const string ShiftJamo = "ㅃㅉㄸㄲㅆㅒㅖ";
        static readonly string[] vowelPairs = { "ㅗㅏㅘ", "ㅗㅐㅙ", "ㅗㅣㅚ", "ㅜㅓㅝ", "ㅜㅔㅞ", "ㅜㅣㅟ", "ㅡㅣㅢ" };
        static readonly string[] endPairs = { "ㄱㅅㄳ", "ㄴㅈㄵ", "ㄴㅎㄶ", "ㄹㄱㄺ", "ㄹㅁㄻ", "ㄹㅂㄼ", "ㄹㅅㄽ", "ㄹㅌㄾ", "ㄹㅍㄿ", "ㄹㅎㅀ", "ㅂㅅㅄ" };

        static char Key(char letter)
        {
            char lower = Char.ToLowerInvariant(letter);
            int index = Letters.IndexOf(lower);
            if (index < 0)
                return '\0';
            if (letter != lower)
            {
                int shifted = ShiftLetters.IndexOf(lower);
                if (shifted >= 0)
                    return ShiftJamo[shifted];
            }

            return Plain[index];
        }

        static string Pair(string[] table, char first, char second)
        {
            foreach (string entry in table)
                if (entry[0] == first && entry[1] == second)
                    return entry.Substring(2);
            return null;
        }

        sealed class Composer
        {
            readonly StringBuilder output = new StringBuilder();
            int cho = -1, jung = -1, jong;

            internal string Result
            {
                get
                {
                    Flush();
                    return output.ToString();
                }
            }

            void Flush()
            {
                if (cho >= 0 && jung >= 0)
                    output.Append((char)(0xAC00 + (cho * 21 + jung) * 28 + jong));
                else if (cho >= 0)
                    output.Append(Cho[cho]);
                else if (jung >= 0)
                    output.Append(Jung[jung]);
                cho = jung = -1;
                jong = 0;
            }

            internal void Other(char value)
            {
                Flush();
                output.Append(value);
            }

            internal void Add(char jamo)
            {
                int vowel = Jung.IndexOf(jamo);
                if (vowel >= 0)
                {
                    AddVowel(jamo, vowel);
                    return;
                }

                int start = Cho.IndexOf(jamo);
                if (cho < 0 || jung < 0)
                {
                    Flush();
                    cho = start;
                    return;
                }

                if (jong == 0)
                {
                    int end = Jong.IndexOf(jamo);
                    if (end > 0)
                    {
                        jong = end;
                        return;
                    }

                    Flush();
                    cho = start;
                    return;
                }

                string joined = Pair(endPairs, Jong[jong], jamo);
                if (joined != null)
                {
                    jong = Jong.IndexOf(joined[0]);
                    return;
                }

                Flush();
                cho = start;
            }

            void AddVowel(char jamo, int vowel)
            {
                if (cho >= 0 && jung < 0)
                {
                    jung = vowel;
                    return;
                }

                if (cho >= 0 && jung >= 0 && jong == 0)
                {
                    string joined = Pair(vowelPairs, Jung[jung], jamo);
                    if (joined != null)
                    {
                        jung = Jung.IndexOf(joined[0]);
                        return;
                    }
                }

                if (cho >= 0 && jung >= 0 && jong > 0)
                {
                    char last = Jong[jong];
                    int carried;
                    string split = endPairs.FirstOrDefault(entry => entry[2] == last);
                    if (split != null)
                    {
                        jong = Jong.IndexOf(split[0]);
                        carried = Cho.IndexOf(split[1]);
                    }
                    else
                    {
                        jong = 0;
                        carried = Cho.IndexOf(last);
                    }

                    Flush();
                    cho = carried;
                    jung = vowel;
                    return;
                }

                Flush();
                jung = vowel;
            }
        }

        // "qmfkdnwj" -> "브라우저". Anything that is not a letter key is kept as it is.
        internal static string FromEnglishLayout(string text)
        {
            var composer = new Composer();
            foreach (char letter in text ?? "")
            {
                char jamo = Key(letter);
                if (jamo == '\0')
                    composer.Other(letter);
                else
                    composer.Add(jamo);
            }

            return composer.Result;
        }

        // "챗" typed where "chat" was meant comes back as the keys that make it.
        internal static string ToEnglishLayout(string text)
        {
            var result = new StringBuilder();
            foreach (char value in text ?? "")
            {
                if (value >= 0xAC00 && value <= 0xD7A3)
                {
                    int index = value - 0xAC00;
                    Spell(result, Cho[index / 588]);
                    Spell(result, Jung[index % 588 / 28]);
                    int end = index % 28;
                    if (end > 0)
                        Spell(result, Jong[end]);
                }
                else if (value >= 0x3131 && value <= 0x3163)
                    Spell(result, value);
                else
                    result.Append(value);
            }

            return result.ToString();
        }

        static void Spell(StringBuilder result, char jamo)
        {
            string[] table = vowelPairs.Any(e => e[2] == jamo) ? vowelPairs : endPairs.Any(e => e[2] == jamo) ? endPairs : null;
            if (table != null)
            {
                string entry = table.First(e => e[2] == jamo);
                Spell(result, entry[0]);
                Spell(result, entry[1]);
                return;
            }

            int shifted = ShiftJamo.IndexOf(jamo);
            if (shifted >= 0)
            {
                result.Append(Char.ToUpperInvariant(ShiftLetters[shifted]));
                return;
            }

            int index = Plain.IndexOf(jamo);
            if (index >= 0)
                result.Append(Letters[index]);
        }

        // The wording to try besides what was typed: the English-layout reading and the Hangul-layout reading.
        internal static IEnumerable<string> Variants(string query)
        {
            query = (query ?? "").Trim();
            yield return query;
            if (query.Length < 2)
                yield break;
            bool latin = query.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));
            bool hangul = query.Any(c => (c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x3131 && c <= 0x3163));
            if (latin && !hangul)
            {
                string korean = FromEnglishLayout(query);
                if (korean != query)
                    yield return korean;
            }
            else if (hangul && !latin)
            {
                string english = ToEnglishLayout(query);
                if (english != query)
                    yield return english;
            }
        }

        static readonly string[][] names =
        {
            new[] { "chatgpt", "챗지피티 챗GPT 챗쥐피티 지피티" },
            new[] { "codex", "코덱스" },
            new[] { "claude", "클로드" },
            new[] { "chrome", "크롬 구글크롬" },
            new[] { "edge", "엣지 마이크로소프트엣지" },
            new[] { "firefox", "파이어폭스 파폭" },
            new[] { "whale", "웨일 네이버웨일" },
            new[] { "opera", "오페라" },
            new[] { "brave", "브레이브" },
            new[] { "samsung internet", "삼성인터넷 삼성브라우저" },
            new[] { "steam", "스팀" },
            new[] { "epicgames", "에픽게임즈 에픽" },
            new[] { "discord", "디스코드 디코" },
            new[] { "spotify", "스포티파이 스포티" },
            new[] { "telegram", "텔레그램 텔레" },
            new[] { "slack", "슬랙" },
            new[] { "zoom", "줌" },
            new[] { "teams", "팀즈 팀스" },
            new[] { "line", "라인" },
            new[] { "kakaotalk", "카카오톡 카톡" },
            new[] { "notion", "노션" },
            new[] { "obsidian", "옵시디언" },
            new[] { "figma", "피그마" },
            new[] { "obs", "오비에스 방송" },
            new[] { "vlc", "브이엘씨" },
            new[] { "potplayer", "팟플레이어" },
            new[] { "youtube", "유튜브" },
            new[] { "netflix", "넷플릭스" },
            new[] { "github", "깃허브 깃헙" },
            new[] { "visual studio code", "비주얼스튜디오코드 브이에스코드 비쥬얼스튜디오" },
            new[] { "visual studio", "비주얼스튜디오 비쥬얼스튜디오" },
            new[] { "vscode", "브이에스코드 비주얼스튜디오코드" },
            new[] { "powershell", "파워쉘 파워셸 파워쉘" },
            new[] { "terminal", "터미널 명령 프롬프트" },
            new[] { "word", "워드" },
            new[] { "excel", "엑셀" },
            new[] { "powerpoint", "파워포인트 피피티" },
            new[] { "onenote", "원노트" },
            new[] { "outlook", "아웃룩" },
            new[] { "photoshop", "포토샵 포샵" },
            new[] { "illustrator", "일러스트레이터 일러스트" },
            new[] { "premiere", "프리미어" },
            new[] { "lightroom", "라이트룸" },
            new[] { "minecraft", "마인크래프트 마크" },
            new[] { "league of legends", "리그오브레전드 롤" },
            new[] { "riot", "라이엇" },
            new[] { "battle.net", "배틀넷" },
            new[] { "valorant", "발로란트 발로" },
            new[] { "overwatch", "오버워치" },
            new[] { "paint", "그림판" },
            new[] { "notepad", "메모장" },
            new[] { "calculator", "계산기" },
            new[] { "explorer", "탐색기 파일" },
            new[] { "settings", "설정" },
            new[] { "store", "스토어 마이크로소프트스토어" },
            new[] { "whatsapp", "왓츠앱 왓츠앱" },
            new[] { "instagram", "인스타그램 인스타" },
            new[] { "twitter", "트위터" },
            new[] { "facebook", "페이스북" },
            new[] { "tiktok", "틱톡" },
            new[] { "twitch", "트위치" },
            new[] { "dropbox", "드롭박스" },
            new[] { "onedrive", "원드라이브" },
            new[] { "google drive", "구글드라이브" },
            new[] { "adobe", "어도비" },
            new[] { "autocad", "오토캐드" },
            new[] { "blender", "블렌더" },
            new[] { "unity", "유니티" },
            new[] { "docker", "도커" },
            new[] { "python", "파이썬" },
            new[] { "java", "자바" },
            new[] { "zip", "압축 알집" },
            new[] { "winrar", "윈라르 압축" },
            new[] { "7-zip", "세븐집 압축" },
            new[] { "bandizip", "반디집 압축" },
            new[] { "everything", "에브리씽" }
        };

        // Lower-case letters and digits only, split into words, so "word" does not match "keyword".
        static string Words(string text)
        {
            var result = new StringBuilder();
            foreach (char value in (text ?? "").ToLowerInvariant())
                result.Append(Char.IsLetterOrDigit(value) ? value : ' ');
            return String.Join(" ", result.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        // Korean spellings of the app's English name, added to the words its name is searched against.
        internal static string Aliases(string name, string identity)
        {
            string haystack = " " + Words((name ?? "") + " " + (identity ?? "")) + " ";
            var extra = new StringBuilder();
            foreach (var entry in names)
                if (haystack.Contains(" " + Words(entry[0]) + " "))
                    extra.Append(' ').Append(entry[1]);
            return extra.ToString();
        }
    }
}
