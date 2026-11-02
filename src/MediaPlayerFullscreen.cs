using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed partial class MediaPlayerView
    {
        bool fullscreenMode,fullscreenLayoutDirty;
        double fullscreenHeight;
        internal bool FullscreenPill {get;private set;}

        internal void FitFullscreen(double height,bool coverExpanded)
        {
            bool pill=!coverExpanded;
            bool changed=!fullscreenMode||FullscreenPill!=pill||Math.Abs(fullscreenHeight-height)>.1;
            fullscreenMode=true;
            FullscreenPill=pill;
            fullscreenHeight=height;
            FitPopup(height);
            if(changed){
                modeSet=false;
                fullscreenLayoutDirty=true;
                SeekBar.WaveDecoration=FullscreenPill;
                SeekBar.WaveMode=Modern||FullscreenPill;
                SeekBar.IsHitTestVisible=!FullscreenPill;
                SeekBar.VerticalAlignment=FullscreenPill?VerticalAlignment.Bottom:VerticalAlignment.Stretch;
                ((FrameworkElement)timeline.Children[0]).Visibility=FullscreenPill?Visibility.Collapsed:Visibility.Visible;
                PreviousButton.Visibility=FullscreenPill?Visibility.Collapsed:Visibility.Visible;
                NextButton.Margin=new Thickness(0);
                PlayButton.Background=FullscreenPill?Brushes.Transparent:Modern?controlGlass:Paint.ControlWhite;
                NextButton.Background=FullscreenPill?Brushes.Transparent:Modern?controlGlass:Brushes.Transparent;
                if(Modern){
                    ShuffleButton.Visibility=FullscreenPill?Visibility.Collapsed:Visibility.Visible;
                    transport.Margin=new Thickness(0);
                    if(modernState!=null)UpdateModern(modernState,device.Text);
                }
            }
            ApplyFullscreenLayout();
        }

        void ApplyFullscreenLayout()
        {
            if(!fullscreenMode)return;
            ArtworkButton.Visibility=Visibility.Collapsed;
            if(!fullscreenLayoutDirty)return;
            fullscreenLayoutDirty=false;
            if(!FullscreenPill){
                if(!Modern){
                    Height=fullscreenHeight-28;
                    Margin=new Thickness(18,14,18,14);
                    double[] heights={20,44,44,Math.Max(28,Height-108)};
                    for(int i=0;i<4;i++)RowDefinitions[i].Height=new GridLength(heights[i]);
                    header.ColumnDefinitions[0].Width=new GridLength(0);
                    header.ColumnDefinitions[2].Width=new GridLength(0);
                    route.Visibility=timeline.Visibility=controls.Visibility=Visibility.Visible;
                    controls.Margin=new Thickness(0);
                    header.Margin=new Thickness(0,2,0,0);
                    artist.Visibility=Visibility.Visible;
                    PlayButton.Margin=new Thickness(24,0,24,0);
                }
                else{
                    header.Margin=new Thickness(0,2,0,0);
                    Grid.SetRow(transport,3);
                }
                Grid.SetRow(timeline,2);
                timeline.VerticalAlignment=VerticalAlignment.Stretch;
                return;
            }
            Height=fullscreenHeight-1.2;
            Margin=new Thickness(0);
            double[] rows={16,27,Math.Max(32,Height-63),20};
            for(int i=0;i<4;i++)RowDefinitions[i].Height=new GridLength(rows[i]);
            route.Visibility=compactOutput.Visibility=Visibility.Collapsed;
            header.Margin=new Thickness(84,0,30,0);
            Grid.SetRow(header,1);
            Grid.SetRow(timeline,3);
            timeline.Margin=new Thickness(0);
            timeline.VerticalAlignment=VerticalAlignment.Bottom;
            SeekBar.Height=20;
            if(Modern){
                Grid.SetRow(transport,2);
                transport.Margin=new Thickness(84,0,30,0);
                transport.Visibility=Visibility.Visible;
                FitFullscreenPillControls();
            }
            else{
                Grid.SetRow(controls,2);
                controls.Margin=new Thickness(84,0,30,0);
                header.ColumnDefinitions[0].Width=new GridLength(0);
                header.ColumnDefinitions[2].Width=new GridLength(0);
                artist.Visibility=Visibility.Collapsed;
                FitFullscreenPillButtons();
            }
        }

        void FitFullscreenPillControls()
        {
            modernArtist.Visibility=Visibility.Collapsed;
            modernTitle.TextSize=14;
            modernTitle.Height=modernTitle.LineHeight;
            modernWords.Height=modernTitle.Height;
            titleLine.ColumnDefinitions[0].Width=new GridLength(0);
            double room=Math.Max(1,(header.ActualWidth>1?header.ActualWidth:ActualWidth-114)-8);
            modernTitle.Width=Math.Min(modernTitle.NaturalWidth,room);
            FitFullscreenPillButtons();
        }

        void FitFullscreenPillButtons()
        {
            playGlyph.Update(playGlyph.Kind,Paint.ControlWhite);
            PlayButton.Width=PlayButton.Height=NextButton.Width=NextButton.Height=38;
            PlayButton.Margin=new Thickness(0,0,16,0);
        }

        internal Rect FullscreenArtworkSlot
        {
            get{return new Rect(22,(fullscreenHeight-54)/2,54,54);}
        }

        internal void StopFullscreenMedia()
        {
            SeekBar.WavePlaying=false;
            if(modernTitle!=null)modernTitle.Running=false;
            if(modernArtist!=null)modernArtist.Running=false;
        }
    }
}
