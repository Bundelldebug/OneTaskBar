using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace OneTaskBar
{
    // The interface is written in Korean. Another language is applied where text reaches the screen: every text
    // block, tooltip and text content is looked up by its Korean wording in the language's table, so no screen
    // has to be rewritten, and changing the language re-reads the Korean source of every open window.
    // Wording that is not in a table (names, numbers, rare messages) stays as written.
    internal static class Loc
    {
        sealed class Track
        {
            internal string Source, Last;
            internal bool Watching, UnloadHooked;
            internal EventHandler Handler;
        }

        static readonly string[] codes = { "ko", "en", "ja", "zh" };
        static readonly ConditionalWeakTable<DependencyObject, Track> texts = new ConditionalWeakTable<DependencyObject, Track>();
        static readonly ConditionalWeakTable<DependencyObject, Track> tips = new ConditionalWeakTable<DependencyObject, Track>();
        static readonly ConditionalWeakTable<DependencyObject, Track> contents = new ConditionalWeakTable<DependencyObject, Track>();
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly DependencyPropertyDescriptor textDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        static string code;
        static Dictionary<string, string> table;
        static string[] pieces;
        static bool installed;

        // The setting: "auto" follows the Windows display language; otherwise ko, en, ja or zh.
        internal static string Setting
        {
            get
            {
                string value = Preferences.Current.Language;
                return Array.IndexOf(codes, value) >= 0 ? value : "auto";
            }
        }

        internal static string Code
        {
            get
            {
                if (code == null)
                    Resolve();
                return code;
            }
        }

        internal static bool Korean
        {
            get { return Code == "ko"; }
        }

        internal static CultureInfo Culture
        {
            get
            {
                switch (Code)
                {
                    case "ja":
                        return CultureInfo.GetCultureInfo("ja-JP");
                    case "zh":
                        return CultureInfo.GetCultureInfo("zh-CN");
                    case "en":
                        return CultureInfo.GetCultureInfo("en-US");
                    default:
                        return CultureInfo.GetCultureInfo("ko-KR");
                }
            }
        }

        static string FromWindows()
        {
            string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return two == "ko" || two == "ja" || two == "zh" ? two : "en";
        }

        static void Resolve()
        {
            string choice = Setting, result = choice;
            // Test fixtures run with their own data folder and keep the Korean wording the checks read.
            if (choice == "auto")
                result = Environment.GetEnvironmentVariable("ONE_TASKBAR_DATA") != null ? "ko" : FromWindows();
            Load(result);
        }

        static void Load(string language)
        {
            cache.Clear();
            table = null;
            pieces = null;
            code = language;
            if (language == "ko")
                return;
            string[] pairs = language == "ja" ? LocTables.JapanesePairs() : language == "zh" ? LocTables.ChinesePairs() : LocTables.EnglishPairs();
            table = new Dictionary<string, string>(pairs.Length / 2, StringComparer.Ordinal);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                table[pairs[i]] = pairs[i + 1];
            // Longer wording first, so a phrase wins over the words inside it. One- and two-letter words only match whole.
            pieces = table.Keys.Where(k => k.Trim().Length >= 3 && HasHangul(k)).OrderByDescending(k => k.Length).ToArray();
        }

        static bool HasHangul(string text)
        {
            foreach (char value in text)
                if (value >= 0xAC00 && value <= 0xD7A3)
                    return true;
            return false;
        }

        // The wording for the current language; the Korean text itself when the language is Korean or nothing matches.
        internal static string T(string korean)
        {
            if (String.IsNullOrEmpty(korean) || Code == "ko")
                return korean;
            string value;
            if (table.TryGetValue(korean, out value) || cache.TryGetValue(korean, out value))
                return value;
            value = Join(korean);
            if (cache.Count > 4096)
                cache.Clear();
            cache[korean] = value;
            return value;
        }

        // Text built from several known pieces ("독 배경" + " 미세 조정"); pieces that are not known stay as they are.
        static string Join(string korean)
        {
            if (!HasHangul(korean))
                return korean;
            var result = new StringBuilder();
            bool any = false;
            int at = 0;
            while (at < korean.Length)
            {
                string found = null;
                foreach (string key in pieces)
                {
                    if (key.Length <= korean.Length - at && String.CompareOrdinal(korean, at, key, 0, key.Length) == 0)
                    {
                        found = key;
                        break;
                    }
                }

                if (found == null)
                {
                    result.Append(korean[at++]);
                    continue;
                }

                result.Append(table[found]);
                at += found.Length;
                any = true;
            }

            return any ? result.ToString() : korean;
        }

        // A date or time written the way the language writes it. The formats are the Korean ones used in the code.
        internal static string Date(DateTime value, string koreanFormat)
        {
            string format = koreanFormat;
            switch (Code)
            {
                case "en":
                    format = koreanFormat == "M월 d일 dddd" ? "dddd, MMMM d" : koreanFormat == "yyyy년 M월" ? "MMMM yyyy" : koreanFormat == "yyyy년 M월 d일 dddd" ? "dddd, MMMM d, yyyy" : koreanFormat.Replace("yyyy년 M월 d일", "MMMM d, yyyy").Replace("yyyy년 M월", "MMMM yyyy").Replace("M월 d일", "MMM d");
                    break;
                case "ja":
                case "zh":
                    format = koreanFormat.Replace("년", "年").Replace("월", "月").Replace("일", "日");
                    break;
            }

            return value.ToString(format, Culture);
        }

        // A table must hold whole pairs with a non-empty wording, and keep the numbers and symbols of the Korean text.
        internal static void Check()
        {
            foreach (var pairs in new[] { LocTables.EnglishPairs(), LocTables.JapanesePairs(), LocTables.ChinesePairs() })
            {
                if (pairs.Length % 2 != 0)
                    throw new InvalidOperationException("Language table has an unpaired entry");
                for (int i = 0; i < pairs.Length; i += 2)
                    if (String.IsNullOrWhiteSpace(pairs[i + 1]) || pairs[i].Contains("\n") != pairs[i + 1].Contains("\n"))
                        throw new InvalidOperationException("Language table entry is empty or lost a line break: " + pairs[i]);
            }
        }

        internal static void Install()
        {
            if (installed)
                return;
            installed = true;
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnSized));
            EventManager.RegisterClassHandler(typeof(FrameworkElement), ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler(OnToolTip));
        }

        // The setting changed: read the language again and redo every open window from its Korean text.
        internal static void Changed()
        {
            Resolve();
            var application = Application.Current;
            if (application == null)
                return;
            foreach (Window window in application.Windows)
                Walk(window);
        }

        static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Code == "ko")
                return;
            Visit(sender as DependencyObject, true);
            Settle(sender as DependencyObject);
        }

        static void OnSized(object sender, SizeChangedEventArgs e)
        {
            if (Code != "ko")
                Settle(sender as DependencyObject);
        }

        static readonly HashSet<Window> pending = new HashSet<Window>();

        // Elements that arrive with a page are visited once more after the page has settled, so none is missed.
        static void Settle(DependencyObject element)
        {
            var window = element == null ? null : Window.GetWindow(element);
            if (window == null || !pending.Add(window))
                return;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                pending.Remove(window);
                if (Code != "ko")
                    Walk(window);
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        static void Walk(DependencyObject element)
        {
            Visit(element, false);
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
                Walk(VisualTreeHelper.GetChild(element, i));
        }

        // Inside the Loaded event the element counts as loaded even though its IsLoaded flag may not be set yet.
        static void Visit(DependencyObject element, bool loading)
        {
            var block = element as TextBlock;
            if (block != null && (loading || block.IsLoaded))
                Attach(block);
            var control = element as ContentControl;
            if (control != null && (control.Content is string || contents.TryGetValue(control, out dummy)))
                Content(control);
            var frame = element as FrameworkElement;
            if (frame != null && frame.ToolTip is string)
                Tip(frame);
        }

        static Track dummy;

        static void Content(ContentControl control)
        {
            var track = contents.GetOrCreateValue(control);
            string now = control.Content as string;
            if (now == null)
                return;
            if (now != track.Last)
                track.Source = now;
            string want = T(track.Source);
            track.Last = want;
            if (want != now)
                control.Content = want;
        }

        static void Tip(FrameworkElement element)
        {
            var track = tips.GetOrCreateValue(element);
            string now = element.ToolTip as string;
            if (now == null)
                return;
            if (now != track.Last)
                track.Source = now;
            string want = T(track.Source);
            track.Last = want;
            if (want != now)
                element.ToolTip = want;
        }

        static void OnToolTip(object sender, ToolTipEventArgs e)
        {
            if (Code == "ko")
                return;
            var element = sender as FrameworkElement;
            if (element != null && element.ToolTip is string)
                Tip(element);
        }

        static void Attach(TextBlock block)
        {
            if (BindingOperations.IsDataBound(block, TextBlock.TextProperty))
                return;
            if (block.Inlines.Count > 1 || block.Inlines.FirstInline != null && !(block.Inlines.FirstInline is Run))
            {
                foreach (Run run in block.Inlines.OfType<Run>())
                    Piece(run);
                return;
            }

            var track = texts.GetOrCreateValue(block);
            Refresh(block, track);
            if (!track.UnloadHooked)
            {
                track.UnloadHooked = true;
                block.Unloaded += (s, e) => Unwatch(block, track);
            }

            if (!track.Watching)
            {
                track.Watching = true;
                track.Handler = (s, e) => Refresh(block, track);
                textDescriptor.AddValueChanged(block, track.Handler);
            }
        }

        static void Unwatch(TextBlock block, Track track)
        {
            if (!track.Watching)
                return;
            track.Watching = false;
            textDescriptor.RemoveValueChanged(block, track.Handler);
        }

        static void Refresh(TextBlock block, Track track)
        {
            string now = block.Text ?? "";
            // Text that is not what this code last wrote was set by the program: it is the new Korean source.
            if (now != track.Last)
                track.Source = now;
            string want = T(track.Source);
            track.Last = want;
            if (want != now)
                block.Text = want;
        }

        // A run inside formatted text keeps its formatting; only its words change.
        static void Piece(Run run)
        {
            var track = texts.GetOrCreateValue(run);
            string now = run.Text ?? "";
            if (now != track.Last)
                track.Source = now;
            string want = T(track.Source);
            track.Last = want;
            if (want != now)
                run.Text = want;
        }
    }
}
