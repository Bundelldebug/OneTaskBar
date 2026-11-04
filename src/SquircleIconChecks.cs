using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class SquircleIconChecks
    {
        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        static System.Collections.Generic.IEnumerable<T> Children<T>(DependencyObject root)
            where T : DependencyObject
        {
            var current = root as T;
            if (current != null)
                yield return current;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var item in Children<T>(VisualTreeHelper.GetChild(root, i)))
                    yield return item;
        }

        static ImageSource Shape(bool circle, bool wide = false)
        {
            var group = new DrawingGroup();
            using (var draw = group.Open())
            {
                if (wide)
                    draw.DrawRectangle(Brushes.CornflowerBlue, null, new Rect(0, 0, 100, 43));
                else if (circle)
                    draw.DrawEllipse(Brushes.CornflowerBlue, null, new Point(50, 50), 48, 48);
                else
                    draw.DrawGeometry(Brushes.CornflowerBlue, null, SquircleGeometry.Create(new Rect(2, 2, 96, 96)));
                draw.DrawEllipse(Brushes.White, null, new Point(50, wide ? 21 : 50), 12, 12);
            }

            group.Freeze();
            var image = new DrawingImage(group);
            image.Freeze();
            return AppIconShape.Raster(image, 128);
        }

        // A freeform glyph with transparent surroundings, like a logo that is not a filled shape.
        static ImageSource Mark(Brush body, Brush detail = null)
        {
            var group = new DrawingGroup();
            using (var draw = group.Open())
            {
                draw.DrawRectangle(body, null, new Rect(42, 10, 16, 80));
                draw.DrawRectangle(body, null, new Rect(10, 42, 80, 16));
                if (detail != null)
                    draw.DrawRectangle(detail, null, new Rect(42, 42, 16, 16));
            }

            group.Freeze();
            var image = new DrawingImage(group);
            image.Freeze();
            return AppIconShape.Raster(image, 128);
        }

        static FrameworkElement Card(string title, ImageSource image)
        {
            var stack = new StackPanel
            {
                Width = 128,
                Margin = new Thickness(8, 10, 8, 12)
            };
            stack.Children.Add(new Image { Source = image, Width = 72, Height = 72, Stretch = Stretch.Uniform });
            var label = Paint.TextBlock(title, 12, Paint.Brush("#FF253246"));
            label.TextAlignment = TextAlignment.Center;
            label.TextWrapping = TextWrapping.Wrap;
            label.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(label);
            return stack;
        }

        static bool UsesSource(Drawing drawing, ImageSource source)
        {
            var image = drawing as ImageDrawing;
            if (image != null)
                return Object.ReferenceEquals(image.ImageSource, source);
            var group = drawing as DrawingGroup;
            return group != null && group.Children.Any(child => UsesSource(child, source));
        }

        static void Capture(FrameworkElement content, string name, double width)
        {
            content.Measure(new Size(width, Double.PositiveInfinity));
            content.Arrange(new Rect(0, 0, width, content.DesiredSize.Height));
            content.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(AppLog.Data, name)))
                encoder.Save(stream);
        }

        internal static int Run(bool live = false)
        {
            var application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            Window window = null;
            int result = 1;
            bool enabled = Preferences.Current.SquircleIcons;
            application.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    Require(!new PreferenceData().SquircleIcons, "New installations must keep native icons by default");
                    var circle = Shape(true);
                    var squircle = Shape(false);
                    var wide = Shape(false, true);
                    Require(!AppIconShape.AlreadySquircle(circle), "Circle mistaken for squircle");
                    Require(AppIconShape.AlreadySquircle(squircle), "Continuous-corner tile not recognized");
                    Require(!AppIconShape.AlreadySquircle(wide), "Wide artwork mistaken for squircle");
                    int analyses = AppIconShape.AnalysisCount;
                    for (int repeat = 0; repeat < 8; repeat++)
                    {
                        AppIconShape.Automatic(circle, true);
                        AppIconShape.AlreadySquircle(circle);
                        AppIconShape.Background(circle);
                    }

                    Require(AppIconShape.AnalysisCount == analyses, "Preview redraw reanalyzed an immutable source icon");
                    Require(AppIconShape.Background(Mark(Brushes.CornflowerBlue)) == Colors.White, "Colored freeform artwork uses a white tile");
                    Require(AppIconShape.Background(Mark(Brushes.Black)) == Colors.White, "Dark freeform artwork uses a white tile");
                    Require(AppIconShape.Background(Mark(Brushes.White)) == Colors.Black, "White artwork that would vanish on white uses a black tile");
                    Require(AppIconShape.Background(Mark(Brushes.White, Brushes.CornflowerBlue)) == Colors.White, "White artwork with a visible colored part stays on white");
                    var mutable = new DrawingImage(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 64, 64))));
                    Require(!AppIconShape.AlreadySquircle(mutable), "Square recognized as squircle");
                    ((GeometryDrawing)mutable.Drawing).Geometry = SquircleGeometry.Create(new Rect(0, 0, 64, 64));
                    Require(AppIconShape.AlreadySquircle(mutable), "Mutable icon reused stale shape metadata");
                    var outline = AppIconShape.Pixels(AppIconShape.Raster(squircle, 128));
                    int edge = 0;
                    while (edge < 128 && outline[(16 * 128 + edge) * 4 + 3] < 128)
                        edge++;
                    Require(edge >= 9 && edge <= 16, "Outline differs from supplied Samsung Browser silhouette");
                    var app = new AppEntry
                    {
                        Name = "Unknown fixture",
                        AppID = "fixture.icon.unknown",
                        Icon = circle
                    };
                    var raw = app.Icon;
                    Preferences.Current.SquircleIcons = true;
                    AppIconTheme.NotifyChanged();
                    var transformed = AppIconTheme.Resolve(app, raw);
                    Require(transformed != null && !Object.ReferenceEquals(transformed, raw) && transformed.IsFrozen, "Unknown app conversion failed");
                    Require(AppIconShape.AlreadySquircle(transformed), "Automatic conversion did not produce a squircle");
                    Require(UsesSource(((DrawingImage)transformed).Drawing, raw), "Conversion replaced the original app artwork");
                    var nativeCenter = AppIconShape.Pixels(AppIconShape.Raster(raw, 128));
                    var themedCenter = AppIconShape.Pixels(AppIconShape.Raster(transformed, 128));
                    for (int i = 0; i < 4; i++)
                        Require(nativeCenter[(64 * 128 + 64) * 4 + i] == themedCenter[(64 * 128 + 64) * 4 + i], "Native center pixel changed");
                    Require(Object.ReferenceEquals(app.Icon, raw), "Theme mutated the native AppEntry icon");
                    Require(Object.ReferenceEquals(transformed, AppIconTheme.Resolve(app, raw)), "Unchanged refresh rebuilt artwork");
                    var samsung = new AppEntry
                    {
                        Name = "Samsung fixture",
                        AppID = "Samsung.fixture!App",
                        Icon = squircle
                    };
                    Require(Object.ReferenceEquals(squircle, AppIconTheme.Resolve(samsung, squircle)), "Existing squircle was reframed");
                    var highResolution = AppIconShape.Raster(circle, 512);
                    var highResolutionApp = new AppEntry
                    {
                        Name = "High resolution fixture",
                        AppID = "fixture.hires",
                        Icon = highResolution
                    };
                    var highResolutionTile = (DrawingImage)AppIconTheme.Resolve(highResolutionApp, highResolution);
                    Require(UsesSource(highResolutionTile.Drawing, highResolution) && highResolution.PixelWidth == 512, "Conversion replaced the full-resolution source with its analysis sample");
                    var spotify = new AppEntry
                    {
                        Name = "Spotify",
                        AppID = "SpotifyAB.SpotifyMusic_fixture!Spotify",
                        Icon = circle
                    };
                    var spotifyTile = (DrawingImage)AppIconTheme.Resolve(spotify, circle);
                    Require(UsesSource(spotifyTile.Drawing, circle), "Spotify lost its native circle artwork");
                    var spotifyPixels = AppIconShape.Pixels(AppIconShape.Raster(spotifyTile, 128));
                    int spotifyRim = (64 * 128 + 10) * 4;
                    Require(spotifyPixels[spotifyRim] < 3 && spotifyPixels[spotifyRim + 1] < 3 && spotifyPixels[spotifyRim + 2] < 3 && spotifyPixels[spotifyRim + 3] > 250, "Spotify must retain a black squircle outside its original circle");
                    var profiles = new[]
                    {
                        new AppEntry
                        {
                            Name = "Profile 1",
                            AppID = "Chrome.fixture1",
                            Executable = "chrome.exe",
                            Icon = circle
                        },
                        new AppEntry
                        {
                            Name = "Profile 2",
                            AppID = "Chrome.fixture2",
                            Executable = "chrome.exe",
                            Icon = wide
                        }
                    };
                    foreach (var profile in profiles)
                        Require(UsesSource(((DrawingImage)AppIconTheme.Resolve(profile, profile.Icon)).Drawing, profile.Icon), "Profile/PWA artwork was replaced");
                    Require(!String.Equals(AppIconTheme.Identity(profiles[0]), AppIconTheme.Identity(profiles[1]), StringComparison.OrdinalIgnoreCase), "Profile identities merged");
                    var unknownWide = new AppEntry
                    {
                        AppID = "fixture.wide",
                        Icon = wide
                    };
                    var convertedWide = AppIconTheme.Resolve(unknownWide, wide);
                    Require(AppIconShape.AlreadySquircle(convertedWide), "Wide artwork conversion failed");
                    var png = Path.Combine(AppLog.Data, "icon-export.png");
                    var ico = Path.Combine(AppLog.Data, "icon-export.ico");
                    AppIconFiles.Export(transformed, png);
                    AppIconFiles.Export(transformed, ico);
                    AppIconFiles.Export(transformed, png);
                    AppIconFiles.Export(transformed, ico);
                    var decoded = AppIconFiles.Load(png);
                    var decodedIco = AppIconFiles.Load(ico);
                    Require(decoded.PixelWidth == 512 && decoded.PixelHeight == 512 && decodedIco.PixelWidth == 256, "Export sizes are wrong");
                    using (var stream = File.OpenRead(ico))
                    using (var reader = new BinaryReader(stream))
                    {
                        Require(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 && reader.ReadUInt16() == 9, "ICO frame directory is invalid");
                    }

                    var kept = new AppIconOverride
                    {
                        Mode = 1
                    };
                    AppIconOverrides.Save(app, kept, null);
                    Require(Object.ReferenceEquals(AppIconTheme.Resolve(app, raw), raw), "Per-app original override ignored");
                    var custom = new AppIconOverride
                    {
                        Mode = 3,
                        ArtworkPercent = 68,
                        Color = "#183B62"
                    };
                    AppIconOverrides.Save(app, custom, wide);
                    Require(AppIconOverrides.Find(AppIconTheme.Identity(app)).Mode == 3 && AppIconOverrides.Image(AppIconOverrides.Find(AppIconTheme.Identity(app))) != null, "Custom icon was not persisted");
                    Require(AppIconShape.AlreadySquircle(AppIconTheme.Resolve(app, raw)), "Custom source conversion failed");
                    Require(AppIconTheme.Resolve(app, null) != null, "Custom icon without a native source did not render");
                    AppIconOverrides.Save(app, new AppIconOverride(), null);
                    Require(AppIconOverrides.Find(AppIconTheme.Identity(app)) == null, "Reset did not remove override");
                    var hostile = new AppIconOverride
                    {
                        Mode = 3,
                        ImageName = "..\\settings.json"
                    };
                    Require(AppIconOverrides.Image(hostile) == null, "Custom image escaped its directory");
                    var persisted = new JavaScriptSerializer().Deserialize<PreferenceData>(new JavaScriptSerializer().Serialize(new PreferenceData { SquircleIcons = true }));
                    Require(persisted.SquircleIcons, "Squircle option did not survive serialization");
                    var panel = new StackPanel();
                    panel.Children.Add(AppIcons.Visual(app, 48));
                    window = new Window
                    {
                        Width = 200,
                        Height = 140,
                        Content = panel,
                        ShowInTaskbar = false
                    };
                    window.Show();
                    await Task.Delay(80);
                    Require(Children<Image>(panel).Single().Source != raw, "Visible icon did not use the theme");
                    Preferences.Current.SquircleIcons = false;
                    AppIconTheme.NotifyChanged();
                    Require(Object.ReferenceEquals(Children<Image>(panel).Single().Source, raw), "Turning off did not restore the visible native icon");
                    Preferences.Current.SquircleIcons = true;
                    AppIconTheme.NotifyChanged();
                    Require(!Object.ReferenceEquals(Children<Image>(panel).Single().Source, raw), "Turning on did not update the visible icon");
                    window.Close();
                    window = null;
                    var editor = new AppIconStudio(new[] { app, samsung })
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    window = editor;
                    editor.Show();
                    await Task.Delay(80);
                    editor.Measure(new Size(800, 630));
                    editor.Arrange(new Rect(0, 0, 800, 630));
                    editor.UpdateLayout();
                    var editorList = Children<ListBox>(editor.Surface).SingleOrDefault();
                    Require(editorList != null, "Editor app list missing from layout");
                    editorList.SelectedItem = app;
                    var previews = Children<Image>(editor.Surface).ToArray();
                    Require(previews.Length == 2 && Object.ReferenceEquals(previews[0].Source, raw) && previews[1].Source != null, "Editor did not show the original and themed previews");
                    var editorMode = Children<ComboBox>(editor.Surface).SingleOrDefault();
                    var editorScale = Children<Slider>(editor.Surface).SingleOrDefault();
                    Require(editorMode != null && editorScale != null, "Editor mode or size control missing from layout");
                    editorMode.SelectedIndex = 2;
                    editorScale.Value = 68;
                    Require(previews[1].Source.IsFrozen, "Editor resize produced unfrozen artwork");
                    Capture(editor.Surface, "squircle-editor.png", 800);
                    editor.Close();
                    window = null;
                    for (int i = 0; i < 150; i++)
                        AppIconTheme.Resolve(new AppEntry { AppID = "fixture.eviction." + i }, raw);
                    Require(AppIconTheme.CachedCount <= AppIconTheme.CacheLimit, "Theme cache grew without a bound");
                    var comparisons = new WrapPanel
                    {
                        Width = 864,
                        Background = Paint.Brush("#FFF0F3F8")
                    };
                    comparisons.Children.Add(Card("원본 · 원형", circle));
                    comparisons.Children.Add(Card("자동 변환", transformed));
                    comparisons.Children.Add(Card("원본 · 가로형", wide));
                    comparisons.Children.Add(Card("자동 변환", convertedWide));
                    comparisons.Children.Add(Card("기존 스퀘어클", squircle));
                    comparisons.Children.Add(Card("원본 유지", squircle));
                    int nativePreserved = 0;
                    if (live)
                    {
                        var installedComparisons = new WrapPanel
                        {
                            Width = 864,
                            Background = Paint.Brush("#FFF0F3F8")
                        };
                        var installed = await BackgroundWork.Run(() => Catalog.Read(false));
                        var names = new[]
                        {
                            "삼성 브라우저",
                            "갤럭시 북 익스피리언스",
                            "삼성 전화",
                            "ChatGPT",
                            "Spotify",
                            "파일 탐색기",
                            "Microsoft Store",
                            "계산기",
                            "메모장",
                            "Discord",
                            "Chrome",
                            "그림판"
                        };
                        var entries = installed.Where(a => names.Contains(a.Name)).Take(16).ToArray();
                        foreach (var entry in entries)
                        {
                            var native = await BackgroundWork.Run(() => AppIcons.Get(entry));
                            if (native == null)
                                continue;
                            var themed = AppIconTheme.Resolve(entry, native);
                            Require(Object.ReferenceEquals(native, themed) || UsesSource(((DrawingImage)themed).Drawing, native), "Installed app artwork was replaced: " + entry.Name);
                            comparisons.Children.Add(Card(entry.Name + " · 원본", native));
                            comparisons.Children.Add(Card(entry.Name + " · 적용", themed));
                            installedComparisons.Children.Add(Card(entry.Name + " · 원본", native));
                            installedComparisons.Children.Add(Card(entry.Name + " · 적용", themed));
                            if (AppIconShape.AlreadySquircle(native))
                            {
                                Require(Object.ReferenceEquals(native, themed), "Native squircle changed: " + entry.Name);
                                nativePreserved++;
                            }
                        }

                        Capture(installedComparisons, "installed-squircle-preview.png", 864);
                    }

                    Capture(comparisons, "squircle-comparison.png", 864);
                    AppLog.Json("squircle-icon-probe.json", new { passed = true, nativeArtworkPreserved = true, sourcePixelsPreserved = highResolution.PixelWidth, spotifyNativeCircleOnBlack = true, referenceOutlineInset = edge, alreadySquirclePreserved = true, profileIdentityPreserved = true, nativeIconsPreserved = nativePreserved, liveToggleRestoresOriginal = true, pngPixels = 512, icoFrames = 9, cache = AppIconTheme.CachedCount, cacheLimit = AppIconTheme.CacheLimit, osInputSent = false });
                    result = 0;
                }
                catch (Exception error)
                {
                    AppLog.Json("squircle-icon-probe.json", new { passed = false, error = error.ToString() });
                }
                finally
                {
                    Preferences.Current.SquircleIcons = enabled;
                    AppIconTheme.NotifyChanged();
                    if (window != null)
                        window.Close();
                    application.Shutdown();
                }
            }));
            application.Run();
            return result;
        }
    }
}
