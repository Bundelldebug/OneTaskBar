using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed class AppIconStudio : GlassWindow
    {
        readonly List<AppEntry> apps;
        readonly ListBox list = new ListBox
        {
            DisplayMemberPath = "Name",
            BorderThickness = new Thickness(0)
        };
        readonly Image before = new Image
        {
            Width = 96,
            Height = 96,
            Stretch = Stretch.Uniform
        };
        readonly Image after = new Image
        {
            Width = 96,
            Height = 96,
            Stretch = Stretch.Uniform
        };
        readonly TextBlock description = Paint.TextBlock("앱을 선택해 주세요", 13, AppSettingsPalette.Muted);
        readonly TextBlock status = Paint.TextBlock("", 13, AppSettingsPalette.Muted);
        readonly ComboBox mode = new ComboBox
        {
            Height = 37,
            ItemsSource = new[]
            {
                "자동",
                "원본 유지",
                "원본 그림으로 만들기",
                "사용자 이미지"
            }
        };
        readonly Slider scale = new Slider
        {
            Minimum = 40,
            Maximum = 100,
            Value = 72,
            TickFrequency = 1,
            IsSnapToTickEnabled = true
        };
        readonly TextBlock scaleLabel = Paint.TextBlock("그림 크기 72%", 13, AppSettingsPalette.Muted);
        AppEntry selected;
        ImageSource original, imported;
        AppIconOverride draft = new AppIconOverride();
        bool loading, closed;
        int selection;
        internal AppIconStudio(IEnumerable<AppEntry> entries)
        {
            Title = "One TaskBar · 아이콘 편집기";
            Width = 800;
            Height = 630;
            MinWidth = 690;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Dark = AppSettingsPalette.Dark;
            OpaqueSurface = true;
            SetMaterial(AppSettingsPalette.Surface, AppSettingsPalette.Surface);
            Surface.BorderBrush = AppSettingsPalette.Stroke;
            Surface.BorderThickness = new Thickness(1);
            apps = entries.GroupBy(AppIconTheme.Identity, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            var root = new DockPanel
            {
                Margin = new Thickness(24)
            };
            var heading = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 15)
            };
            var close = Paint.Action("닫기", Close);
            DockPanel.SetDock(close, Dock.Right);
            heading.Children.Add(close);
            var title = Paint.TextBlock("아이콘 편집기", 25, AppSettingsPalette.Text);
            title.FontWeight = FontWeights.SemiBold;
            title.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 1)
                    DragMove();
            };
            heading.Children.Add(title);
            DockPanel.SetDock(heading, Dock.Top);
            root.Children.Add(heading);
            var footer = new DockPanel
            {
                Margin = new Thickness(0, 15, 0, 0)
            };
            var save = Paint.Action("이 앱에 적용", Save);
            DockPanel.SetDock(save, Dock.Right);
            footer.Children.Add(save);
            status.TextWrapping = TextWrapping.Wrap;
            status.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(status);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(235) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(grid);
            var left = new DockPanel
            {
                Margin = new Thickness(0, 0, 20, 0)
            };
            var search = new TextBox
            {
                Height = 36,
                Margin = new Thickness(0, 0, 0, 8),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(search, "앱 검색");
            search.TextChanged += (s, e) => Filter(search.Text);
            DockPanel.SetDock(search, Dock.Top);
            left.Children.Add(search);
            var add = Paint.Action("목록에 없는 앱 선택", AddApp);
            add.Margin = new Thickness(0, 8, 0, 0);
            DockPanel.SetDock(add, Dock.Bottom);
            left.Children.Add(add);
            list.Background = AppSettingsPalette.Card;
            list.Foreground = AppSettingsPalette.Text;
            list.SelectionChanged += (s, e) => LoadSelection(list.SelectedItem as AppEntry);
            left.Children.Add(list);
            grid.Children.Add(left);
            var editor = new StackPanel();
            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);
            var enabled = new CheckBox
            {
                Content = "스퀘어클 앱 아이콘 사용",
                IsChecked = Preferences.Current.SquircleIcons,
                Margin = new Thickness(0, 0, 0, 17)
            };
            enabled.Checked += (s, e) => Preferences.SetAppIcons(true);
            enabled.Unchecked += (s, e) => Preferences.SetAppIcons(false);
            editor.Children.Add(enabled);
            var previews = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 2,
                Margin = new Thickness(0, 0, 0, 12)
            };
            previews.Children.Add(PreviewBox("원본", before));
            previews.Children.Add(PreviewBox("적용 미리보기", after));
            editor.Children.Add(previews);
            description.TextWrapping = TextWrapping.Wrap;
            description.Margin = new Thickness(0, 0, 0, 16);
            editor.Children.Add(description);
            AutomationProperties.SetName(mode, "아이콘 만드는 방식");
            editor.Children.Add(mode);
            mode.SelectionChanged += (s, e) =>
            {
                if (loading)
                    return;
                draft.Mode = Math.Max(0, mode.SelectedIndex);
                UpdatePreview();
            };
            var actions = new WrapPanel
            {
                Margin = new Thickness(0, 10, 0, 10)
            };
            actions.Children.Add(Paint.Action("이미지 선택", Import));
            actions.Children.Add(Paint.Action("바탕색", ChooseColor));
            actions.Children.Add(Paint.Action("자동 바탕색", () =>
            {
                draft.Color = null;
                UpdatePreview();
            }));
            editor.Children.Add(actions);
            editor.Children.Add(scaleLabel);
            scale.Margin = new Thickness(0, 8, 0, 15);
            AutomationProperties.SetName(scale, "그림 크기");
            scale.ValueChanged += (s, e) =>
            {
                if (loading)
                    return;
                draft.ArtworkPercent = (int)Math.Round(scale.Value);
                UpdatePreview();
            };
            editor.Children.Add(scale);
            var export = Paint.Action("PNG / ICO로 내보내기", Export);
            export.HorizontalAlignment = HorizontalAlignment.Left;
            editor.Children.Add(export);
            var note = Paint.TextBlock("자동 모드는 이미 스퀘어클인 아이콘을 그대로 유지합니다.", 12, AppSettingsPalette.Muted);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 15, 0, 0);
            editor.Children.Add(note);
            Surface.Child = root;
            AppSettingsPalette.Apply(root);
            Closed += (s, e) =>
            {
                closed = true;
                selection++;
                before.Source = after.Source = null;
                original = imported = null;
                apps.Clear();
                list.ItemsSource = null;
            };
            Filter("");
        }

        static UIElement PreviewBox(string name, Image image)
        {
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var body = new StackPanel
            {
                Margin = new Thickness(6, 15, 6, 12)
            };
            body.Children.Add(image);
            var text = Paint.TextBlock(name, 12, AppSettingsPalette.Muted);
            text.TextAlignment = TextAlignment.Center;
            text.Margin = new Thickness(0, 12, 0, 0);
            body.Children.Add(text);
            return new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = AppSettingsPalette.Card,
                Margin = new Thickness(0, 0, 8, 0),
                Child = body
            };
        }

        void Filter(string query)
        {
            list.ItemsSource = apps.Where(a => String.IsNullOrEmpty(query) || (a.Name ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0).ToArray();
            if (list.Items.Count > 0)
                list.SelectedIndex = 0;
        }

        async void LoadSelection(AppEntry app)
        {
            int version = ++selection;
            selected = app;
            original = imported = null;
            before.Source = after.Source = null;
            if (app == null)
                return;
            description.Text = "아이콘을 읽는 중…";
            try
            {
                ImageSource image = app.Icon ?? await BackgroundWork.Run(() => AppIcons.Get(app));
                if (closed || version != selection)
                    return;
                AppIconOverride rule = AppIconOverrides.Find(AppIconTheme.Identity(app)) ?? new AppIconOverride();
                ImageSource custom = rule.Mode == 3 ? await BackgroundWork.Run(() => AppIconOverrides.Image(rule)) : null;
                if (closed || version != selection)
                    return;
                original = image;
                draft = rule;
                imported = custom;
                loading = true;
                mode.SelectedIndex = draft.Mode;
                scale.Value = draft.ArtworkPercent;
                loading = false;
                UpdatePreview();
            }
            catch (Exception error)
            {
                if (!closed && version == selection)
                    status.Text = error.Message;
            }
        }

        void UpdatePreview()
        {
            scaleLabel.Text = "그림 크기 " + draft.ArtworkPercent + "%";
            bool custom = draft.Mode >= 2;
            scale.IsEnabled = custom;
            if (original == null && !(draft.Mode == 3 && imported != null))
            {
                description.Text = "이 앱의 원본 아이콘을 읽지 못했습니다. 이미지를 직접 선택할 수 있습니다.";
                return;
            }

            try
            {
                before.Source = original;
                after.Source = draft.Mode == 3 && imported != null ? AppIconShape.Tile(imported, draft.Color, draft.ArtworkPercent) : AppIconTheme.Preview(selected, original, draft);
                description.Text = AppIconTheme.Description(selected, original, draft);
            }
            catch (Exception error)
            {
                status.Text = "미리보기를 만들지 못했습니다: " + error.Message;
            }
        }

        void AddApp()
        {
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = "앱 또는 바로가기 선택",
                Filter = "앱과 바로가기|*.exe;*.lnk;*.url|모든 파일|*.*"
            };
            if (picker.ShowDialog(this) != true)
                return;
            var app = new AppEntry
            {
                Name = Path.GetFileNameWithoutExtension(picker.FileName),
                Target = picker.FileName
            };
            var existing = apps.FirstOrDefault(a => String.Equals(AppIconTheme.Identity(a), AppIconTheme.Identity(app), StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                apps.Add(app);
                existing = app;
            }

            list.ItemsSource = apps.ToArray();
            list.SelectedItem = existing;
        }

        async void Import()
        {
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = "아이콘 이미지 선택",
                Filter = "이미지|*.png;*.ico;*.jpg;*.jpeg;*.bmp"
            };
            if (picker.ShowDialog(this) != true)
                return;
            int version = selection;
            try
            {
                var image = await BackgroundWork.Run(() => AppIconFiles.Load(picker.FileName));
                if (closed || version != selection)
                    return;
                imported = image;
                draft.Mode = 3;
                mode.SelectedIndex = 3;
                UpdatePreview();
            }
            catch (Exception error)
            {
                status.Text = error.Message;
            }
        }

        void ChooseColor()
        {
            using (var picker = new Forms.ColorDialog())
            {
                picker.FullOpen = true;
                if (picker.ShowDialog() != Forms.DialogResult.OK)
                    return;
                draft.Color = "#" + picker.Color.R.ToString("X2") + picker.Color.G.ToString("X2") + picker.Color.B.ToString("X2");
                if (draft.Mode < 2)
                {
                    draft.Mode = 2;
                    mode.SelectedIndex = 2;
                }

                UpdatePreview();
            }
        }

        void Save()
        {
            if (selected == null)
                return;
            try
            {
                AppIconOverrides.Save(selected, draft, imported);
                draft = AppIconOverrides.Find(AppIconTheme.Identity(selected)) ?? new AppIconOverride();
                status.Text = Preferences.Current.SquircleIcons ? "저장했습니다." : "저장했습니다. 스퀘어클 앱 아이콘을 켜면 적용됩니다.";
            }
            catch (Exception error)
            {
                status.Text = "저장하지 못했습니다: " + error.Message;
            }
        }

        void Export()
        {
            if (after.Source == null)
                return;
            var picker = new Microsoft.Win32.SaveFileDialog
            {
                Title = "아이콘 내보내기",
                Filter = "PNG 이미지|*.png|Windows 아이콘|*.ico",
                FileName = "app-icon",
                DefaultExt = ".png",
                AddExtension = true
            };
            if (picker.ShowDialog(this) != true)
                return;
            try
            {
                AppIconFiles.Export(after.Source, picker.FileName);
                status.Text = "아이콘을 내보냈습니다.";
            }
            catch (Exception error)
            {
                status.Text = "내보내지 못했습니다: " + error.Message;
            }
        }
    }

    internal sealed partial class WelcomeWindow
    {
        void AppIconSetting(StackPanel parent)
        {
            Row(parent, "스퀘어클 앱 아이콘", "독·시작·앱·검색에 적용합니다. 이미 스퀘어클인 아이콘은 유지합니다", Preferences.Current.SquircleIcons, Preferences.SetAppIcons);
            var open = Paint.Action("아이콘 편집기", () =>
            {
                var editor = new AppIconStudio(shell.Apps.Concat(shell.Pins).Concat(shell.Running))
                {
                    Owner = this
                };
                editor.Show();
            });
            open.HorizontalAlignment = HorizontalAlignment.Left;
            open.Margin = new Thickness(15, 4, 15, 12);
            parent.Children.Add(open);
        }
    }

    internal static class AppIconCommand
    {
        internal static int Run(string[] args)
        {
            try
            {
                int index = Array.IndexOf(args, "--make-squircle-icon");
                if (index < 0 || index + 2 >= args.Length)
                    throw new ArgumentException("입력 이미지와 출력 PNG/ICO 경로가 필요합니다.");
                string input = Path.GetFullPath(args[index + 1]), output = Path.GetFullPath(args[index + 2]);
                if (String.Equals(input, output, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("원본과 다른 출력 경로를 지정해 주세요.");
                var original = AppIconFiles.Load(input);
                var result = AppIconShape.AlreadySquircle(original) ? original : AppIconShape.Automatic(original, true);
                AppIconFiles.Export(result, output);
                AppLog.Json("icon-tool-result.json", new { passed = true, file = output, originalPreserved = Object.ReferenceEquals(original, result) });
                return 0;
            }
            catch (Exception error)
            {
                AppLog.Json("icon-tool-result.json", new { passed = false, error = error.Message });
                return 1;
            }
        }
    }
}
