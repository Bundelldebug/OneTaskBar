using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class AppIconTheme
    {
        sealed class Entry
        {
            internal string Key;
            internal ImageSource Source, Image;
            internal LinkedListNode<Entry> Node;
        }

        static readonly object gate = new object ();
        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        static readonly LinkedList<Entry> recent = new LinkedList<Entry>();
        internal const int CacheLimit = 64;
        internal static int CachedCount
        {
            get
            {
                lock (gate)
                    return cache.Count;
            }
        }

        internal static event Action Changed;
        internal static string Identity(AppEntry app)
        {
            if (app == null)
                return "";
            return !String.IsNullOrWhiteSpace(app.AppID) ? app.AppID : (app.Target ?? app.Executable ?? app.Name ?? "");
        }

        internal static ImageSource Resolve(AppEntry app, ImageSource original)
        {
            if (!Preferences.Current.SquircleIcons)
                return original;
            if (original == null)
            {
                var rule = AppIconOverrides.Find(Identity(app));
                if (rule == null || rule.Mode != 3)
                    return null;
            }

            string key = Identity(app) + "|" + (original == null ? 0 : RuntimeHelpers.GetHashCode(original));
            lock (gate)
            {
                Entry hit;
                if (cache.TryGetValue(key, out hit) && Object.ReferenceEquals(hit.Source, original))
                {
                    recent.Remove(hit.Node);
                    recent.AddFirst(hit.Node);
                    return hit.Image;
                }
            }

            ImageSource result;
            try
            {
                result = Preview(app, original, AppIconOverrides.Find(Identity(app)));
            }
            catch (Exception error)
            {
                AppLog.Once("icon-theme-render", error.Message);
                result = original;
            }

            lock (gate)
            {
                Entry old;
                if (cache.TryGetValue(key, out old))
                    recent.Remove(old.Node);
                var entry = new Entry
                {
                    Key = key,
                    Source = original,
                    Image = result
                };
                entry.Node = recent.AddFirst(entry);
                cache[key] = entry;
                while (cache.Count > CacheLimit)
                {
                    var last = recent.Last.Value;
                    recent.RemoveLast();
                    cache.Remove(last.Key);
                }
            }

            return result;
        }

        internal static ImageSource Preview(AppEntry app, ImageSource original, AppIconOverride rule)
        {
            if (rule != null && rule.Mode == 1)
                return original;
            if (rule != null && rule.Mode == 3)
            {
                var custom = AppIconOverrides.Image(rule);
                if (custom != null)
                    return AppIconShape.Tile(custom, rule.Color, rule.ArtworkPercent);
            }

            if (rule != null && rule.Mode == 2)
                return AppIconShape.Tile(original, rule.Color, rule.ArtworkPercent);
            if (AppIconShape.AlreadySquircle(original))
                return original;
            if (IsSpotify(app))
                return AppIconShape.Tile(original, "#000000", 72);
            return AppIconShape.Automatic(original, CompactArtwork(app));
        }

        static bool IsSpotify(AppEntry app)
        {
            if (app == null)
                return false;
            string identity = app.AppID ?? "";
            if (identity.StartsWith("SpotifyAB.SpotifyMusic_", StringComparison.OrdinalIgnoreCase) || String.Equals(identity, "Spotify", StringComparison.OrdinalIgnoreCase))
                return true;
            string file = Path.GetFileName(app.Executable ?? app.Target ?? "");
            return String.Equals(file, "Spotify.exe", StringComparison.OrdinalIgnoreCase) || String.Equals(app.Name, "Spotify", StringComparison.OrdinalIgnoreCase);
        }

        static bool CompactArtwork(AppEntry app)
        {
            if (app == null)
                return true;
            string identity = app.AppID ?? "";
            if (identity.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) || identity.StartsWith("MicrosoftCorporationII.", StringComparison.OrdinalIgnoreCase))
                return false;
            string file = Path.GetFileName(app.Executable ?? app.Target ?? app.AppID ?? "").ToLowerInvariant();
            switch (file)
            {
                case "explorer.exe":
                case "notepad.exe":
                case "mspaint.exe":
                case "calc.exe":
                case "control.exe":
                case "taskmgr.exe":
                case "powershell.exe":
                case "cmd.exe":
                    return false;
                default:
                    return true;
            }
        }

        internal static string Description(AppEntry app, ImageSource original, AppIconOverride rule)
        {
            if (rule != null && rule.Mode == 1)
                return "원본 유지";
            if (rule != null && rule.Mode == 3)
                return "사용자 이미지";
            if (rule != null && rule.Mode == 2)
                return "원본 그림으로 만든 스퀘어클";
            if (AppIconShape.AlreadySquircle(original))
                return "이미 스퀘어클 · 원본 유지";
            return "원본 그림으로 만든 스퀘어클";
        }

        internal static void NotifyChanged()
        {
            lock (gate)
            {
                cache.Clear();
                recent.Clear();
            }

            var handler = Changed;
            if (handler != null)
                handler();
        }
    }
}
