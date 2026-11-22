using System;
using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    // Plays the same BatteryPill charging morph as Windows mode inside the status cluster without resizing or moving the bar.
    internal sealed class TopBarCharge : Grid
    {
        const int Hold = 2150;
        const int End = 2600;
        readonly StackPanel normal;
        readonly BatteryPill battery;
        readonly UIElement[] siblings;
        BatteryAnimation animation;
        bool preview;
        internal bool Active { get { return animation != null && animation.Active; } }

        internal TopBarCharge(StackPanel normal, BatteryPill battery, params UIElement[] siblings)
        {
            this.normal = normal;
            this.battery = battery;
            this.siblings = siblings ?? new UIElement[0];
            ClipToBounds = false;
            normal.ClipToBounds = false;
            Children.Add(normal);
            Panel.SetZIndex(battery, 10);
            battery.MorphChanged = OnMorphChanged;
        }

        void OnMorphChanged(double value)
        {
            double opacity = Math.Max(0, 1 - value);
            foreach (var item in siblings)
                if (item != null) item.Opacity = opacity;
        }

        internal void Play(PowerState power, bool preview)
        {
            Stop();
            // Charging belongs to the existing status bar. Never make a hidden
            // bar visible, or host this effect in a popup/standalone window.
            if (!IsVisible || !(Window.GetWindow(this) is TopStatusBar) ||
                !Motion.Enabled || power == null || !power.Known) return;
            this.preview = preview;
            battery.Update(power);
            double clusterWidth = normal.ActualWidth > 0 ? normal.ActualWidth : 96;
            battery.ExpandedWidth = Math.Max(battery.ActualWidth > 0 ? battery.ActualWidth : battery.Width, clusterWidth);
            animation = new BatteryAnimation(battery, preview, Hold, End);
            animation.Completed = () => { animation = null; RestoreSiblings(); };
        }

        internal void Update(PowerState power)
        {
            if (!Active) return;
            if (power == null || !power.Known || !preview && !power.Connected || !Motion.Enabled)
            {
                Stop();
                return;
            }
            battery.Update(power);
        }

        internal void Stop()
        {
            var running = animation;
            animation = null;
            if (running != null)
            {
                running.Completed = null;
                running.Close();
            }
            RestoreSiblings();
        }

        void RestoreSiblings()
        {
            foreach (var item in siblings)
                if (item != null) item.Opacity = 1;
        }
    }
}

