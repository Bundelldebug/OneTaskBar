using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    // The expanded fullscreen player puts the track on the cover itself: the source app's mark and the title
    // in large type, the artist under it, over a soft shade that keeps the white text readable on any art.
    internal sealed class FullscreenCaption : Grid
    {
        readonly MarqueeTitle title=new MarqueeTitle {Centered=true},artist=new MarqueeTitle {Centered=true,TextWeight=FontWeights.Normal};
        readonly Image icon=new Image {Stretch=Stretch.Uniform,Visibility=Visibility.Collapsed};
        readonly Border shade=new Border {VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};
        readonly Grid line=new Grid {HorizontalAlignment=HorizontalAlignment.Center};
        readonly StackPanel words=new StackPanel {VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Stretch,IsHitTestVisible=false};
        readonly RectangleGeometry clip=new RectangleGeometry();
        internal FullscreenCaption()
        {
            IsHitTestVisible=false;Visibility=Visibility.Collapsed;Clip=clip;
            var gradient=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};
            gradient.GradientStops.Add(new GradientStop(Colors.Transparent,0));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(120,0,0,0),.55));
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(190,0,0,0),1));
            gradient.Freeze();shade.Background=gradient;
            line.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            line.ColumnDefinitions.Add(new ColumnDefinition());
            line.Children.Add(icon);
            Grid.SetColumn(title,1);line.Children.Add(title);
            words.Children.Add(line);words.Children.Add(artist);
            Children.Add(shade);Children.Add(words);
        }
        internal void Set(string text,string by,ImageSource logo)
        {
            title.Text=text;artist.Text=by;
            title.Running=artist.Running=!String.IsNullOrEmpty(text);
            artist.Visibility=String.IsNullOrWhiteSpace(by)?Visibility.Collapsed:Visibility.Visible;
            if(icon.Source!=logo)icon.Source=logo;
            icon.Visibility=logo==null?Visibility.Collapsed:Visibility.Visible;
        }
        // Follows the cover as it moves and morphs; the text appears only once the cover is mostly open.
        internal void Place(Rect cover,double radius,double visible)
        {
            bool show=visible>.01&&cover.Width>120&&cover.Height>120;
            if(!show){if(Visibility!=Visibility.Collapsed)Visibility=Visibility.Collapsed;return;}
            Visibility=Visibility.Visible;Opacity=Math.Max(0,Math.Min(1,visible));
            Width=cover.Width;Height=cover.Height;Canvas.SetLeft(this,cover.X);Canvas.SetTop(this,cover.Y);
            clip.Rect=new Rect(0,0,cover.Width,cover.Height);clip.RadiusX=clip.RadiusY=radius;
            double size=Math.Max(16,Math.Min(36,cover.Width*.052)),room=Math.Max(40,cover.Width-48);
            title.TextSize=size;title.Height=title.LineHeight;artist.TextSize=Math.Max(12,size*.72);artist.Height=artist.LineHeight;
            double symbol=icon.Source==null?0:size+4;
            icon.Width=icon.Height=Math.Max(1,symbol-2);icon.Margin=new Thickness(0,0,symbol>0?8:0,0);line.ColumnDefinitions[0].Width=new GridLength(symbol>0?symbol+6:0);
            title.Width=Math.Min(title.NaturalWidth,Math.Max(1,room-symbol-8));artist.Width=room;
            words.Margin=new Thickness(24,0,24,Math.Max(14,cover.Height*.045));
            shade.Height=Math.Min(cover.Height,size*5.2+words.Margin.Bottom);
        }
    }
}
