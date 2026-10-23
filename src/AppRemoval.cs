using System;
namespace OneTaskBar
{
    internal static class AppRemoval
    {
        internal static string SettingsUri(AppEntry entry)
        {
            string id=entry==null?null:entry.AppID;if(!String.IsNullOrWhiteSpace(id)&&id.IndexOf('!')>0){string family=id.Substring(0,id.IndexOf('!'));if(family.IndexOfAny(new[]{'\\','/',':','?'})<0)return "ms-settings:appsfeatures-app?"+Uri.EscapeDataString(family);}return "ms-settings:appsfeatures";
        }
        internal static void Open(AppEntry entry,ShellController shell)
        {
            foreach(var panel in new GlassWindow[]{shell.StartMenu,shell.Drawer,shell.Finder})if(panel!=null)panel.HideImmediately();Native.Open(SettingsUri(entry));
        }
    }
}
