using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    // Action chips inside the settings window. Paint.Action turns a text button into a translucent glass card when
    // the glass interface is on; on the settings window's own light or dark card that card is nearly invisible and
    // the label loses its contrast, so settings chips take the window's own tint instead, with no glass rim.
    internal static class SettingsChip
    {
        internal static Button Action(string text, Action action)
        {
            var chip = Paint.Action(text, action);
            chip.Background = AppSettingsPalette.AccentLight;
            chip.BorderBrush = null;
            chip.BorderThickness = new Thickness(0);
            chip.Padding = new Thickness(16, 8, 16, 8);
            ((TextBlock)chip.Content).Foreground = AppSettingsPalette.Text;
            return chip;
        }
    }
}
