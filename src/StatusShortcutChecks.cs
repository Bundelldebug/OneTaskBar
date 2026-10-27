using System;
using System.IO;
using System.Threading.Tasks;

namespace OneTaskBar
{
    internal static class StatusShortcutChecks
    {
        internal static async Task Run(ShellController shell,System.Windows.Forms.Screen screen)
        {
            var saved=Preferences.Current.DockStatusItems;
            DockHost dock=null;
            try
            {
                Preferences.Current.DockStatusItems=new[]{"bluetooth","buds"};
                dock=new DockHost(shell,screen);
                dock.Show();
                await Task.Delay(180);
                var buds=dock.StatusShortcut("buds");
                var bluetooth=dock.StatusShortcut("bluetooth");
                if(buds==null||bluetooth==null||buds.Kind==bluetooth.Kind)throw new InvalidOperationException("Buds status shortcut must have its own earbud symbol");
                Program.Save(dock.Status.Surface,(int)dock.Status.Width,(int)dock.Status.Height,Path.Combine(AppLog.Data,"status-buds-shortcut.png"));
            }
            finally{if(dock!=null)dock.Close();Preferences.Current.DockStatusItems=saved;}
        }
    }
}
