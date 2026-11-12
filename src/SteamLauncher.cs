using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OneTaskBar
{
    // Steam's client window is owned by steamwebhelper, but that process is
    // neither its launcher nor its artwork. Game AppIDs must stay separate.
    internal static class SteamLauncher
    {
        internal static bool IsClient(AppEntry app)
        {
            if (app == null) return false;
            string id = app.AppID ?? "";
            if (id.Equals("Steam", StringComparison.OrdinalIgnoreCase) ||
                id.Equals("Valve.Steam", StringComparison.OrdinalIgnoreCase) ||
                id.Equals("com.valvesoftware.steam", StringComparison.OrdinalIgnoreCase)) return true;
            try
            {
                if (id.Length == 0 && !String.IsNullOrEmpty(app.Executable) &&
                    Path.GetFileName(app.Executable).Equals("steamwebhelper.exe", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (ArgumentException) { return false; }
            return (id.Length == 0 || id.Equals(app.Executable, StringComparison.OrdinalIgnoreCase) ||
                id.Equals(app.Target, StringComparison.OrdinalIgnoreCase)) && IsExecutable(app.Executable ?? app.Target ?? id);
        }

        static bool IsExecutable(string path)
        {
            try { return !String.IsNullOrEmpty(path) && Path.GetFileName(path).Equals("steam.exe", StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { return false; }
        }

        static string Existing(string path)
        {
            return IsExecutable(path) && File.Exists(path) ? path : null;
        }

        internal static AppEntry Resolve(AppEntry saved, IEnumerable<AppEntry> installed)
        {
            if (!IsClient(saved)) return saved;
            var native = installed.FirstOrDefault(app => IsClient(app) &&
                Existing(app.Executable ?? app.Target ?? app.AppID) != null);
            string executable = native == null ? null : Existing(native.Executable ?? native.Target ?? native.AppID);
            executable = executable ?? Existing(saved.Executable) ?? Existing(saved.Target) ?? Existing(saved.AppID);
            if (executable == null)
            {
                // Covers a client pinned from its Chromium window, including a
                // portable install with no Start-menu shortcut.
                try
                {
                    string path = saved.Executable;
                    if (!String.IsNullOrEmpty(path) && Path.GetFileName(path).Equals("steamwebhelper.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var folder = Directory.GetParent(path);
                        for (int i = 0; folder != null && i < 5; i++, folder = folder.Parent)
                        {
                            executable = Existing(Path.Combine(folder.FullName, "steam.exe"));
                            if (executable != null) break;
                        }
                    }
                }
                catch (ArgumentException) { }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            if (executable == null)
            {
                var shortcut = WebAppIcons.LaunchShortcut("Steam", "Steam");
                if (shortcut != null) executable = Existing(shortcut.Target);
            }
            if (executable == null) return saved;
            var result = saved.Copy();
            result.StoredKey = saved.Key;
            result.Name = "Steam";
            result.Target = result.Executable = executable;
            if (native != null && native.Icon != null) result.Icon = native.Icon;
            else if (!String.Equals(saved.Executable, executable, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(saved.Target, executable, StringComparison.OrdinalIgnoreCase)) result.Icon = null;
            return result;
        }

        internal static ProcessStartInfo Prepare(AppEntry app)
        {
            var resolved = Resolve(app, Enumerable.Empty<AppEntry>());
            string path = Existing(resolved.Executable);
            return path == null ? null : new ProcessStartInfo(path) {
                UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)
            };
        }
    }
}
