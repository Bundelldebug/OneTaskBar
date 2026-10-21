using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Threading;

namespace OneTaskBar
{
    internal sealed class WindowsThemeState
    {
        internal int AppsLight=1,SystemLight=1;
        internal bool Available;
        internal bool Dark {get{return AppsLight==0;}}
    }
    internal static class WindowsTheme
    {
        const string key=@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        static readonly object gate=new object();static WindowsThemeState cached;static DateTime until;
        static bool notifying;static int revision;
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessageTimeout(IntPtr window,uint message,UIntPtr parameter,string value,uint flags,uint timeout,out UIntPtr result);
        internal static void Invalidate() {lock(gate)until=DateTime.MinValue;}
        internal static WindowsThemeState Read(bool force=false)
        {
            lock(gate){if(!force&&cached!=null&&DateTime.UtcNow<until)return cached;var state=new WindowsThemeState();
                try {using(var entry=Registry.CurrentUser.OpenSubKey(key)){if(entry!=null){state.AppsLight=Convert.ToInt32(entry.GetValue("AppsUseLightTheme",1));state.SystemLight=Convert.ToInt32(entry.GetValue("SystemUsesLightTheme",1));}state.Available=true;}}
                catch(Exception e){AppLog.Once("theme-read",e.Message);}cached=state;until=DateTime.UtcNow.AddSeconds(1);return state;}
        }
        internal static void SetDark(bool dark) {Apply(dark?0:1,dark?0:1);}
        internal static void Apply(int appsLight,int systemLight)
        {
            if((appsLight!=0&&appsLight!=1)||(systemLight!=0&&systemLight!=1))throw new ArgumentOutOfRangeException("theme");
            using(var entry=Registry.CurrentUser.CreateSubKey(key)) {
                object oldApps=entry.GetValue("AppsUseLightTheme",null),oldSystem=entry.GetValue("SystemUsesLightTheme",null);
                try {entry.SetValue("AppsUseLightTheme",appsLight,RegistryValueKind.DWord);entry.SetValue("SystemUsesLightTheme",systemLight,RegistryValueKind.DWord);}
                catch {if(oldApps==null)entry.DeleteValue("AppsUseLightTheme",false);else entry.SetValue("AppsUseLightTheme",oldApps,RegistryValueKind.DWord);if(oldSystem==null)entry.DeleteValue("SystemUsesLightTheme",false);else entry.SetValue("SystemUsesLightTheme",oldSystem,RegistryValueKind.DWord);throw;}
            }
            var actual=Read(true);if(!actual.Available||actual.AppsLight!=appsLight||actual.SystemLight!=systemLight)throw new InvalidOperationException("Windows 색 모드를 적용하지 못했습니다.");
            // Coalesce notifications off the UI thread; a slow app must not delay the toggle.
            lock(gate){revision++;if(notifying)return;notifying=true;}
            ThreadPool.QueueUserWorkItem(_=> {while(true){int current;lock(gate)current=revision;UIntPtr result;SendMessageTimeout(new IntPtr(0xffff),0x1a,UIntPtr.Zero,"ImmersiveColorSet",2,100,out result);lock(gate){if(current==revision){notifying=false;return;}}}});
        }
    }
}
