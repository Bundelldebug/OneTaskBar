using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // The squircle island option, read from the owner's two One UI screen recordings:
    // - folded, a black squircle pill: a squircle tile on the left (album art, or a coloured tile with a white symbol)
    //   and the title on the right, with a thin rim tinted from what it shows;
    // - with a second activity, a small black squircle splits off to the left and keeps showing the first one;
    // - opening, the words leave first, the black shape grows into the card, and the card's content fades in after.
    // The safe area stays 26 DIP, so the pill keeps the compact height and only grows sideways.
    internal sealed partial class DynamicIsland
    {
        internal const double SatelliteGap=5;
        static readonly Brush IdleRim=Frozen(Color.FromArgb(0x2C,0xFF,0xFF,0xFF));
        static readonly Brush ChargeRim=Frozen(Color.FromArgb(0x90,0x34,0xC7,0x59));
        static readonly Brush ChargeTile=Frozen(Color.FromRgb(0x2F,0xB5,0x66));
        readonly System.Windows.Shapes.Path squircleRim=new System.Windows.Shapes.Path {StrokeThickness=2,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
        readonly Grid pill=new Grid {Visibility=Visibility.Collapsed};
        readonly Border pillArt=new Border {Width=18,Height=18,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Clip=IslandSquircleShape.Create(new Rect(0,0,18,18),9)};
        readonly TextBlock pillTitle=Paint.TextBlock("",12,Paint.ControlWhite);
        readonly Border chargeTile=new Border {Width=18,Height=18,Background=ChargeTile,Visibility=Visibility.Collapsed,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Clip=IslandSquircleShape.Create(new Rect(0,0,18,18),9)};
        // The second, smaller island that splits off to the left while something else uses the main one.
        readonly Border satellite=new Border {Background=Brushes.Black,Visibility=Visibility.Collapsed,IsHitTestVisible=false,RenderTransform=new TranslateTransform()};
        readonly Border satelliteArt=new Border {Width=16,Height=16,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Clip=IslandSquircleShape.Create(new Rect(0,0,16,16),8)};
        bool squircle,satelliteShown;
        Rect satelliteRect=Rect.Empty;
        Color rimColor;Brush rimBrush;
        internal bool SquircleActive {get{return squircle;}}
        internal bool SatelliteVisible {get{return satelliteShown;}}
        internal Rect SatelliteShape {get{return satelliteShown?satelliteRect:Rect.Empty;}}
        internal Brush SquircleRim {get{return squircleRim.Stroke;}}
        internal string PillTitle {get{return pillTitle.Text;}}

        static Brush Frozen(Color color){var brush=new SolidColorBrush(color);brush.Freeze();return brush;}

        void BuildSquircle()
        {
            pillTitle.FontWeight=FontWeights.SemiBold;
            pillTitle.VerticalAlignment=VerticalAlignment.Center;
            pillTitle.TextTrimming=TextTrimming.None;
            pillTitle.TextWrapping=TextWrapping.NoWrap;
            pillTitle.Margin=new Thickness(7,0,0,0);
            // A long title fades out at the right edge, as it does under the rim in the clips.
            var fade=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,0)};
            fade.GradientStops.Add(new GradientStop(Colors.Black,.82));
            fade.GradientStops.Add(new GradientStop(Colors.Transparent,1));
            fade.Freeze();
            var words=new Border {ClipToBounds=true,Child=pillTitle,OpacityMask=fade};
            pill.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            pill.ColumnDefinitions.Add(new ColumnDefinition());
            pill.Children.Add(pillArt);
            Grid.SetColumn(words,1);pill.Children.Add(words);
            Grid.SetColumnSpan(pill,3);
            compact.Children.Add(pill);
            feedback.Children.Insert(0,chargeTile);
            satellite.Child=satelliteArt;
            canvas.Children.Insert(0,satellite);
            var wrapper=Surface.Child as Grid;
            if(wrapper!=null)wrapper.Children.Add(squircleRim);
        }

        // Called with every presentation; changes nothing unless the preference moved.
        void SetSquircle(bool on)
        {
            state.Squircle=on;
            if(on==squircle)return;
            squircle=on;
            var classic=on?Visibility.Collapsed:Visibility.Visible;
            art.Visibility=activity.Visibility=classic;
            pill.Visibility=on?Visibility.Visible:Visibility.Collapsed;
            squircleRim.Visibility=on?Visibility.Visible:Visibility.Collapsed;
            compact.Margin=on?new Thickness(2,0,10,0):new Thickness(9,0,9,0);
            feedback.Margin=on?new Thickness(-4,0,12,0):new Thickness(14,0,14,0);
            feedbackGlyph.LayoutTransform=on?new ScaleTransform(.68,.68):null;
            if(!on){chargeTile.Visibility=Visibility.Collapsed;HideSatellite(false);Surface.Clip=clip;}
            // Quick, with a trace of give: the clips' pills reach their size in about a fifth of a second.
            if(!fluid){slide.Damping=on?.86:1;slide.Omega=on?34:38;}
            if(IsVisible&&!current.IsEmpty)PlaceShape();
        }

        // What the folded pill and the rim show for the current state.
        void DressSquircle(bool compactVisible,bool charge,bool open)
        {
            if(!squircle)return;
            if(compactVisible&&media!=null)
            {
                pillArt.Background=art.Background;
                string title=media.Title??"";
                if(pillTitle.Text!=title)pillTitle.Text=title;
            }
            chargeTile.Visibility=charge?Visibility.Visible:Visibility.Collapsed;
            if(charge){feedbackGlyph.Color=Paint.ControlWhite;feedbackGlyph.InvalidateVisual();}
            Brush rim=IdleRim;
            if(charge)rim=ChargeRim;
            else if(media!=null&&(compactVisible||open))rim=ArtworkRim(open?(byte)0x70:(byte)0xC8);
            if(!Object.ReferenceEquals(squircleRim.Stroke,rim))squircleRim.Stroke=rim;
            // Charging over music: the music moves into the small island on the left.
            bool second=charge&&media!=null&&Motion.Enabled,was=satelliteShown;
            if(second){satelliteArt.Background=art.Background;ShowSatellite();}
            else HideSatellite(Motion.Enabled);
            // The input area must take the small island in at once, even when the main shape does not move.
            if(was!=satelliteShown&&!current.IsEmpty)PlaceShape();
        }

        // The rim takes the main colour of the cover, as the dark red rim around the 50 Cent cover in the clip.
        Brush ArtworkRim(byte alpha)
        {
            var source=media==null?null:media.Artwork as BitmapSource;
            var color=source==null?Color.FromArgb(alpha,0x9A,0x9A,0xA0):ArtworkPalette.Read(source).Dominant;
            color.A=alpha;
            if(rimBrush!=null&&rimColor==color)return rimBrush;
            rimColor=color;
            rimBrush=Frozen(color);
            return rimBrush;
        }

        void ShowSatellite()
        {
            if(satelliteShown)return;
            satelliteShown=true;
            satellite.Visibility=Visibility.Visible;
            PlaceSatellite();
            if(!Motion.Enabled)return;
            // It splits out of the main pill: it starts tucked under the pill's left end and slides out with a little give.
            var move=(TranslateTransform)satellite.RenderTransform;
            move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(satelliteRect.Width+SatelliteGap,0,TimeSpan.FromMilliseconds(380)) {EasingFunction=new BackEase {Amplitude=.28,EasingMode=EasingMode.EaseOut}});
            satellite.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));
        }

        void HideSatellite(bool animated)
        {
            if(!satelliteShown)return;
            satelliteShown=false;
            var move=(TranslateTransform)satellite.RenderTransform;
            if(!animated)
            {
                move.BeginAnimation(TranslateTransform.XProperty,null);
                satellite.BeginAnimation(UIElement.OpacityProperty,null);
                satellite.Visibility=Visibility.Collapsed;
                satelliteRect=Rect.Empty;
                return;
            }
            // It slips back into the main pill and is gone.
            move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,satelliteRect.Width+SatelliteGap,TimeSpan.FromMilliseconds(260)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseIn}});
            var fade=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(260));
            fade.Completed+=(s,e)=>{if(!satelliteShown){satellite.Visibility=Visibility.Collapsed;satelliteRect=Rect.Empty;if(!closed&&IsVisible&&!current.IsEmpty)PlaceShape();}};
            satellite.BeginAnimation(UIElement.OpacityProperty,fade);
        }

        // Beside the main shape, the height of the folded pill and as wide as it is tall.
        void PlaceSatellite()
        {
            if(!squircle||satellite.Visibility!=Visibility.Visible||current.IsEmpty)return;
            double size=DynamicIslandLayout.CompactHeight;
            var place=new Rect(Math.Max(0,current.X-SatelliteGap-size),current.Y,size,size);
            if(place==satelliteRect)return;
            satelliteRect=place;
            if(satellite.Width!=size){satellite.Width=size;satellite.Height=size;satellite.Clip=IslandSquircleShape.Create(new Rect(0,0,size,size),size/2);}
            Canvas.SetLeft(satellite,place.X);
            Canvas.SetTop(satellite,place.Y);
        }

        // The squircle outline of the main shape, the rim on it, and an input area that also holds the small island.
        void PlaceSquircleShape(double radius)
        {
            var outline=IslandSquircleShape.Create(new Rect(0,0,current.Width,current.Height),radius);
            Surface.Clip=outline;
            squircleRim.Data=outline;
            PlaceSatellite();
            var area=InputArea();
            if(satellite.Visibility==Visibility.Visible&&!satelliteRect.IsEmpty)area.Union(satelliteRect);
            BackdropClip=area;
        }

        // The words wait until the black shape has mostly reached its new size, then fade in.
        void SquircleReveal(FrameworkElement content)
        {
            var fade=new DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)),new CubicEase {EasingMode=EasingMode.EaseOut}));
            content.BeginAnimation(UIElement.OpacityProperty,fade);
        }
    }
}
