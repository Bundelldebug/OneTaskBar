using System;
using System.Threading.Tasks;

namespace OneTaskBar
{
    internal static class SideMediaChecks
    {
        internal static async Task Check(ShellController shell,System.Windows.Forms.Screen screen,double scale)
        {
            if(!ReleaseFeatures.GlassUI)return;
            int edge=Preferences.Current.DockEdge;
            bool side=Preferences.Current.NowBarSideCapsule,large=Preferences.Current.NowBarLargeCover;
            try
            {
                Preferences.Current.DockEdge=2;
                Preferences.Current.NowBarSideCapsule=true;
                Preferences.Current.NowBarLargeCover=false;
                var bar=new NowBar(shell,screen);
                try
                {
                    var state=new MediaState {Available=true,Active=true,Playing=true,CanPlay=true,CanSeek=true,Title="Side media",Artist="Fixture",Duration=210,Artwork=ArtworkPalette.Sample(System.Windows.Media.Colors.Crimson)};
                    bar.Update(state,scale,340,screen.Bounds.Height/scale-56,true);
                    await Task.Delay(350);
                    if(bar.Shape.Width!=44||bar.Shape.Height!=88)throw new Exception("Side capsule does not retain its vertical proportions");
                    bar.SetExpanded(true);
                    await Task.Delay(350);
                    if(bar.Shape.Height!=212)throw new Exception("Side capsule does not open its compact black player");
                    Program.Save(bar.Surface,(int)bar.Surface.ActualWidth,(int)bar.Surface.ActualHeight,System.IO.Path.Combine(AppLog.Data,"side-media-card.png"));
                    bar.SetExpanded(false);
                    await Task.Delay(350);
                    if(bar.Expanded||bar.Shape.Height!=88)throw new Exception("Side player does not return to its compact capsule");
                }
                finally{bar.Close();}
            }
            finally
            {
                Preferences.Current.DockEdge=edge;
                Preferences.Current.NowBarSideCapsule=side;
                Preferences.Current.NowBarLargeCover=large;
            }
        }
    }
}
