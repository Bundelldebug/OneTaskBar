using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // Covers the status icons without resizing their window or moving the tray button.
    internal sealed class TopBarCharge : Grid
    {
        const int Hold = 2250;
        const int End = 2590;
        readonly StackPanel normal;
        readonly Border capsule;
        readonly StackPanel label;
        readonly TextBlock percentage = Paint.TextBlock("", 13, Paint.ControlWhite);
        readonly ScaleTransform width = new ScaleTransform(.48, 1);
        readonly TranslateTransform sheen = new TranslateTransform(-.7, 0);
        bool preview;
        internal bool Active { get { return capsule.Visibility == Visibility.Visible; } }

        internal TopBarCharge(StackPanel normal)
        {
            this.normal = normal;
            Children.Add(normal);

            var paint = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5), RelativeTransform = sheen };
            paint.GradientStops.Add(new GradientStop(Color.FromRgb(25, 171, 231), 0));
            paint.GradientStops.Add(new GradientStop(Color.FromRgb(91, 120, 247), .38));
            paint.GradientStops.Add(new GradientStop(Color.FromRgb(63, 89, 214), 1));
            capsule = new Border
            {
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = paint,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(1, .5),
                RenderTransform = width,
                Visibility = Visibility.Collapsed
            };
            label = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            label.Children.Add(new Glyph("bolt", 13) { Color = Paint.ControlWhite, Margin = new Thickness(0, 0, 3, 0) });
            percentage.FontWeight = FontWeights.SemiBold;
            label.Children.Add(percentage);
            Children.Add(capsule);
            Children.Add(label);
        }

        internal void Play(PowerState power, bool preview)
        {
            Stop();
            // Charging belongs to the existing status bar. Never make a hidden
            // bar visible, or host this effect in a popup/standalone window.
            if (!IsVisible || !(Window.GetWindow(this) is TopStatusBar) ||
                !Motion.Enabled || power == null || !power.Known) return;
            this.preview = preview;
            SetLevel(power.Level);
            capsule.Visibility = label.Visibility = Visibility.Visible;
            capsule.BeginAnimation(OpacityProperty, Curve(0, 1, 210, Hold, End));
            label.BeginAnimation(OpacityProperty, Curve(0, 1, 280, Hold - 60, End - 90));
            width.BeginAnimation(ScaleTransform.ScaleXProperty, Curve(.48, 1, 270, Hold, End));
            sheen.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-.7, .35, TimeSpan.FromMilliseconds(End))
            {
                EasingFunction = SpringEase.Smooth
            });
            var fade = Curve(1, 0, 150, Hold + 25, End);
            fade.Completed += (s, e) => Stop();
            normal.BeginAnimation(OpacityProperty, fade);
        }

        internal void Update(PowerState power)
        {
            if (!Active) return;
            if (power == null || !power.Known || !preview && !power.Connected || !Motion.Enabled)
            {
                Stop();
                return;
            }
            SetLevel(power.Level);
        }

        void SetLevel(int level)
        {
            string text = level < 0 ? "–" : level.ToString(CultureInfo.InvariantCulture) + "%";
            if (percentage.Text != text) percentage.Text = text;
        }

        internal void Stop()
        {
            if (!Active) return;
            normal.BeginAnimation(OpacityProperty, null);
            capsule.BeginAnimation(OpacityProperty, null);
            label.BeginAnimation(OpacityProperty, null);
            width.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            sheen.BeginAnimation(TranslateTransform.XProperty, null);
            capsule.Visibility = label.Visibility = Visibility.Collapsed;
        }

        static DoubleAnimationUsingKeyFrames Curve(double rest, double peak, int arrive, int hold, int end)
        {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(rest, TimeSpan.Zero));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(peak, TimeSpan.FromMilliseconds(arrive), SpringEase.Smooth));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(peak, TimeSpan.FromMilliseconds(hold)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(rest, TimeSpan.FromMilliseconds(end), SpringEase.Smooth));
            return animation;
        }
    }
}

