using System;
using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    internal sealed partial class WelcomeWindow
    {
        // Interface language, on the software information page. A change reaches the open screens at once.
        void LanguageSetting(StackPanel parent)
        {
            var row = new StackPanel { Margin = new Thickness(15, 14, 15, 6) };
            row.Children.Add(Paint.TextBlock("언어", 15, AppSettingsPalette.Text));
            var note = Paint.TextBlock("인터페이스 표시 언어를 고릅니다. 바꾸면 열려 있는 화면에 바로 적용됩니다.", 12, AppSettingsPalette.Muted);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 4, 0, 0);
            row.Children.Add(note);
            var choices = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            string[] values = { "auto", "ko", "en", "ja", "zh" };
            string[] names = { "자동 (Windows 설정)", "한국어", "English", "日本語", "中文" };
            var buttons = new Button[values.Length];
            Action refresh = () =>
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    bool selected = Loc.Setting == values[i];
                    buttons[i].Background = selected ? AppSettingsPalette.Accent : AppSettingsPalette.AccentLight;
                    ((TextBlock)buttons[i].Content).Foreground = selected ? Paint.ControlWhite : AppSettingsPalette.Text;
                }
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                string value = values[i];
                buttons[i] = SettingsChip.Action(names[i], () =>
                {
                    Preferences.Set(p => p.Language = value);
                    Loc.Changed();
                    refresh();
                });
                buttons[i].Margin = new Thickness(0, 0, 8, 8);
                choices.Children.Add(buttons[i]);
            }

            refresh();
            row.Children.Add(choices);
            parent.Children.Add(row);
        }
    }
}
