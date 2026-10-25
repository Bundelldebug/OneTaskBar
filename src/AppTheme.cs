using System;
namespace OneTaskBar
{
    internal sealed class AppThemeState
    {
        internal readonly bool Dark,Available=true;internal AppThemeState(bool value){Dark=value;}
    }
    internal static class AppTheme
    {
        static readonly AppThemeState light=new AppThemeState(false),dark=new AppThemeState(true);
        internal static int Mode {get{return Preferences.Current.UITheme>=0&&Preferences.Current.UITheme<=2?Preferences.Current.UITheme:0;}}
        internal static bool Resolve(int mode,bool windowsDark){return mode==2||mode!=1&&windowsDark;}
        internal static bool Dark {get{return Resolve(Mode,WindowsTheme.Read().Dark);}}
        internal static AppThemeState Read(){return Glass95.Dark?dark:light;}
        internal static void Toggle(){Preferences.Set(p=>p.UITheme=Dark?1:2);}
    }
    internal static class ReleaseFeatures
    {
        internal static readonly bool GlassUI=typeof(ReleaseFeatures).Assembly.GetName().Version>=new Version(9,5,0,0);
    }
}
