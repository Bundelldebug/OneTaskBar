using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class SettingsMotionChecks
    {
        internal static async Task Run()
        {
            var content=new Grid();
            for(int i=0;i<3;i++)content.RowDefinitions.Add(new RowDefinition());
            foreach(int row in new[]{0,2})
            {
                var card=new Border {Background=AppSettingsPalette.Card,CornerRadius=new CornerRadius(18),Margin=new Thickness(16,8,16,8)};
                Grid.SetRow(card,row);
                content.Children.Add(card);
            }
            var motion=new SettingsPageMotion(content);
            var fixture=new Window {Width=400,Height=350,Content=motion.View,ShowActivated=false,ShowInTaskbar=false};
            try
            {
                fixture.Show();
                await Task.Delay(90);
                motion.Prepare();
                if(!motion.HoldsSnapshot)throw new InvalidOperationException("Settings transition did not capture its outgoing frame");
                motion.Start(false);
                await Task.Delay(60);
                int width=(int)Math.Ceiling(motion.View.ActualWidth),height=(int)Math.Ceiling(motion.View.ActualHeight);
                var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
                image.Render(motion.View);
                var pixel=new byte[4];
                image.CopyPixels(new Int32Rect(width/2,height/2,1,1),pixel,4,0);
                if(pixel[3]>3)throw new InvalidOperationException("Settings return paints a blank card over the gaps between real cards");
                await Task.Delay(300);
                if(motion.Active||motion.HoldsSnapshot||content.Opacity!=1||!content.RenderTransform.Value.IsIdentity)
                    throw new InvalidOperationException("Settings return does not settle at its original layout");
                motion.Prepare();
                motion.Start(false);
                motion.Cancel();
                await Task.Delay(280);
                if(motion.Active||motion.HoldsSnapshot||content.Opacity!=1||!content.RenderTransform.Value.IsIdentity)
                    throw new InvalidOperationException("A queued settings transition restarts after cancellation");
            }
            finally
            {
                motion.Cancel();
                fixture.Close();
            }
        }
        internal static async Task Navigate(WelcomeWindow window)
        {
            window.SettingsHome();
            await Task.Delay(280);
            var viewport=window.SettingsViewport;
            viewport.ScrollToVerticalOffset(Math.Min(130,viewport.ScrollableHeight));
            await Task.Delay(60);
            double homeOffset=viewport.VerticalOffset;
            if(homeOffset<30)throw new InvalidOperationException("Settings scroll fixture has no home scroll range");
            window.OpenSettings("데스크톱");
            await Task.Delay(280);
            viewport.CheckWheel(-120);
            await Task.Delay(30);
            window.SettingsHome();
            await Task.Delay(60);
            double firstOffset=viewport.VerticalOffset;
            Program.Save(window.Surface,(int)window.Width,(int)window.Height,System.IO.Path.Combine(AppLog.Data,"settings-return-midpoint.png"));
            await Task.Delay(250);
            if(Math.Abs(firstOffset-homeOffset)>.5||Math.Abs(viewport.VerticalOffset-homeOffset)>.5)
                throw new InvalidOperationException("Returning from a scrolling settings page moves the home list after the transition begins");
            window.OpenSettings("화면");
            await Task.Delay(25);
            window.SearchSettings.Text="미디어";
            await Task.Delay(300);
            if(window.SettingsPage!=null||window.VisibleSettingsSections!=1||viewport.VerticalOffset>.5)
                throw new InvalidOperationException("Search interrupts a settings return with stale content or offset");
            window.SearchSettings.Clear();
            await Task.Delay(80);
            if(window.SettingsSnapshotHeld||viewport.Opacity!=1||!viewport.RenderTransform.Value.IsIdentity)
                throw new InvalidOperationException("Settings navigation retains its transition image or transform");
            bool animations=Preferences.Current.Animations;
            try
            {
                Preferences.Current.Animations=false;
                window.OpenSettings("화면");
                window.SettingsHome();
                if(window.SettingsAnimating||window.SettingsSnapshotHeld||viewport.Opacity!=1||!viewport.RenderTransform.Value.IsIdentity)
                    throw new InvalidOperationException("Settings navigation animates or retains a snapshot while motion is disabled");
            }
            finally{Preferences.Current.Animations=animations;}
        }
    }
}
