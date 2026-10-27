using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
                var models=new[]{"Galaxy Buds2 Pro","Galaxy Buds3 Pro","Jiho의 Buds4 Pro"};
                var expected=new[]{"ic_tws_left_dark.png","ic_buds3_left_dark.png","ic_buds4f_left_dark.png"};
                var review=new StackPanel {Orientation=Orientation.Horizontal,Background=Paint.Brush("#FF29342F")};
                for(int i=0;i<models.Length;i++)
                {
                    string model=models[i];
                    if(BudsDeviceData.SymbolFile(model)!=expected[i])throw new InvalidOperationException("Buds model chooses a different native asset family");
                    var source=await BackgroundWork.Run(()=>BudsDeviceData.ShortcutIcon(model));
                    var again=await BackgroundWork.Run(()=>BudsDeviceData.ShortcutIcon(model));
                    if(source!=again)throw new InvalidOperationException("Unchanged Buds model must reuse its decoded asset");
                    var cell=new StackPanel {Width=160,Margin=new Thickness(8,14,8,14)};
                    var icon=new Glyph("earbuds",42){Color=Paint.ControlWhite,HorizontalAlignment=HorizontalAlignment.Center};
                    icon.SetDeviceIcon(source);cell.Children.Add(icon);
                    var label=Paint.TextBlock(model,12,Paint.ControlWhite);label.HorizontalAlignment=HorizontalAlignment.Center;label.Margin=new Thickness(0,10,0,0);cell.Children.Add(label);review.Children.Add(cell);
                }
                Program.Save(review,528,100,Path.Combine(AppLog.Data,"buds-model-symbols.png"));
            }
            finally{if(dock!=null)dock.Close();Preferences.Current.DockStatusItems=saved;}
        }
    }
}
