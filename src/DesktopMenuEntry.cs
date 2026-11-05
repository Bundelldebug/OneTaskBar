using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace OneTaskBar
{
    // Adds "위젯 추가" to the desktop's right-click menu, as an ordinary per-user menu entry.
    // Windows 11 lists such entries under "더 많은 옵션 표시" (or Shift+right-click); a place in its
    // short menu needs a packaged shell extension, which this app does not install.
    // Nothing outside the current user's own classes key is touched, and turning the option
    // off removes the entry again.
    internal static class DesktopMenuEntry
    {
        internal const string KeyPath=@"Software\Classes\DesktopBackground\Shell\OneTaskBar.Widgets";
        internal static int Writes {get;private set;}
        internal static string Status {get;private set;}
        // What this process, not a sandboxed shell, finds in the real registry.
        internal static bool Present
        {
            get
            {
                try
                {
                    using(var key=Registry.CurrentUser.OpenSubKey(KeyPath+@"\command"))return key!=null;
                }
                catch(Exception)
                {
                    return false;
                }
            }
        }
        internal static string Command(string executable)
        {
            return "\""+executable+"\" --open-panel widgets";
        }
        // Fixtures never write the real registry.
        internal static void Sync()
        {
            if(Program.TestProcess)return;
            try
            {
                string executable=Process.GetCurrentProcess().MainModule.FileName;
                if(Preferences.Current.DesktopMenuEntry)Write(executable);
                else Remove();
            }
            catch(Exception e)
            {
                Status=e.GetType().Name+": "+e.Message;
                AppLog.Once("desktop-menu",Status);
            }
        }
        static void Write(string executable)
        {
            string command=Command(executable);
            using(var existing=Registry.CurrentUser.OpenSubKey(KeyPath+@"\command"))
            {
                if(existing!=null&&String.Equals(existing.GetValue(null) as string,command,StringComparison.Ordinal))return;
            }
            using(var key=Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key.SetValue("MUIVerb","위젯 추가");
                key.SetValue("Icon","\""+executable+"\",0");
                key.SetValue("Position","Bottom");
            }
            using(var key=Registry.CurrentUser.CreateSubKey(KeyPath+@"\command"))key.SetValue(null,command);
            Writes++;
        }
        static void Remove()
        {
            using(var existing=Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                if(existing==null)return;
            }
            Registry.CurrentUser.DeleteSubKeyTree(KeyPath,false);
            Writes++;
        }
    }
}
