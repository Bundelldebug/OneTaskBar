using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace OneTaskBar
{
    internal static class MediaApplication
    {
        internal static AppEntry Resolve(string id,IEnumerable<AppEntry> apps)
        {
            if(String.IsNullOrWhiteSpace(id))return null;var entries=apps.ToList();var exact=entries.FirstOrDefault(a=>String.Equals(a.AppID,id,StringComparison.OrdinalIgnoreCase));if(exact!=null)return exact;
            // Legacy GSMTC clients sometimes publish an executable name instead of an AUMID.
            // Only use a complete executable identity; never infer an app from track text.
            if(id.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))return entries.FirstOrDefault(a=>MatchesExecutable(a.Executable,id)||MatchesExecutable(a.Target,id));
            return null;
        }
        static bool MatchesExecutable(string path,string id){if(String.IsNullOrEmpty(path))return false;try{return String.Equals(Path.IsPathRooted(id)?Path.GetFullPath(path):Path.GetFileName(path),id,StringComparison.OrdinalIgnoreCase);}catch{return false;}}
    }
}
