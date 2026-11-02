using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal sealed class AppIconOverride
    {
        public string Identity, ImageName, Color;
        public int Mode, ArtworkPercent = 72;
        internal AppIconOverride Copy()
        {
            return (AppIconOverride)MemberwiseClone();
        }
    }

    internal static class AppIconOverrides
    {
        static readonly string directory = Path.Combine(AppLog.Data, "icon-overrides");
        static readonly string file = Path.Combine(directory, "icons.json");
        static Dictionary<string, AppIconOverride> values;
        static Dictionary<string, AppIconOverride> Values
        {
            get
            {
                if (values != null)
                    return values;
                values = new Dictionary<string, AppIconOverride>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (!File.Exists(file) || new FileInfo(file).Length > 1024 * 1024)
                        return values;
                    foreach (var item in new JavaScriptSerializer().Deserialize<List<AppIconOverride>>(File.ReadAllText(file)) ?? new List<AppIconOverride>())
                    {
                        if (item == null || String.IsNullOrEmpty(item.Identity) || item.Identity.Length > 4096 || values.Count >= 2048)
                            continue;
                        item.Mode = Math.Max(0, Math.Min(3, item.Mode));
                        item.ArtworkPercent = Math.Max(40, Math.Min(100, item.ArtworkPercent));
                        values[item.Identity] = item;
                    }
                }
                catch (Exception error)
                {
                    AppLog.Once("icon-overrides-read", error.Message);
                }

                return values;
            }
        }

        internal static AppIconOverride Find(string identity)
        {
            AppIconOverride value;
            return Values.TryGetValue(identity ?? "", out value) ? value.Copy() : null;
        }

        internal static ImageSource Image(AppIconOverride value)
        {
            if (value == null || String.IsNullOrEmpty(value.ImageName) || !System.Text.RegularExpressions.Regex.IsMatch(value.ImageName, @"\A[a-f0-9]{64}\.png\z"))
                return null;
            try
            {
                return AppIconFiles.Load(Path.Combine(directory, value.ImageName), 512);
            }
            catch (Exception error)
            {
                AppLog.Once("icon-override-image", error.Message);
                return null;
            }
        }

        internal static void Save(AppEntry app, AppIconOverride change, ImageSource imported)
        {
            string identity = AppIconTheme.Identity(app);
            if (String.IsNullOrEmpty(identity) || identity.Length > 4096)
                throw new InvalidOperationException("앱을 먼저 선택해 주세요.");
            if (change == null)
                throw new ArgumentNullException("change");
            var value = change.Copy();
            value.Identity = identity;
            value.Mode = Math.Max(0, Math.Min(3, value.Mode));
            value.ArtworkPercent = Math.Max(40, Math.Min(100, value.ArtworkPercent));
            Directory.CreateDirectory(directory);
            if (value.Mode == 3)
            {
                if (imported != null)
                {
                    string hash;
                    using (var sha = SHA256.Create())
                        hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
                    value.ImageName = hash + ".png";
                    AppIconFiles.Export(imported, Path.Combine(directory, value.ImageName));
                }
                else if (Image(value) == null)
                    throw new InvalidOperationException("사용할 이미지를 선택해 주세요.");
            }

            var next = new Dictionary<string, AppIconOverride>(Values, StringComparer.OrdinalIgnoreCase);
            if (value.Mode == 0)
                next.Remove(identity);
            else
                next[identity] = value;
            if (next.Count > 2048)
                throw new InvalidOperationException("저장할 수 있는 앱 수를 초과했습니다.");
            string temp = file + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(next.Values.ToArray()), new UTF8Encoding(false));
            if (File.Exists(file))
                File.Replace(temp, file, file + ".previous");
            else
                File.Move(temp, file);
            values = next;
            AppIconTheme.NotifyChanged();
        }
    }

    internal static class AppIconFiles
    {
        internal static BitmapSource Load(string file, int maximum = 512)
        {
            maximum = Math.Max(32, Math.Min(512, maximum));
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (!new[]
            {
                ".png",
                ".ico",
                ".jpg",
                ".jpeg",
                ".bmp"
            }.Contains(extension))
                throw new InvalidDataException("PNG, ICO, JPG 또는 BMP 이미지를 선택해 주세요.");
            var info = new FileInfo(file);
            if (!info.Exists || info.Length > 16 * 1024 * 1024)
                throw new InvalidDataException("16MB 이하의 이미지가 필요합니다.");
            using (var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
                if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || frame.PixelWidth > 8192 || frame.PixelHeight > 8192 || (long)frame.PixelWidth * frame.PixelHeight > 32000000)
                    throw new InvalidDataException("이미지 크기가 너무 큽니다. 8192픽셀 이하로 줄여 주세요.");
                if (extension == ".ico")
                {
                    var icon = new TransformedBitmap(frame, new ScaleTransform(Math.Min(1, maximum / (double)Math.Max(frame.PixelWidth, frame.PixelHeight)), Math.Min(1, maximum / (double)Math.Max(frame.PixelWidth, frame.PixelHeight))));
                    var copy = new WriteableBitmap(icon);
                    copy.Freeze();
                    return copy;
                }

                stream.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (frame.PixelWidth >= frame.PixelHeight)
                    image.DecodePixelWidth = Math.Min(maximum, frame.PixelWidth);
                else
                    image.DecodePixelHeight = Math.Min(maximum, frame.PixelHeight);
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        internal static void Export(ImageSource source, string file)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension != ".png" && extension != ".ico")
                throw new InvalidDataException("저장 형식은 PNG 또는 ICO입니다.");
            string temporary = file + ".tmp";
            try
            {
                using (var stream = File.Create(temporary))
                {
                    if (extension == ".ico")
                        WriteIco(source, stream);
                    else
                    {
                        var bytes = Png(source, 512);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }

                if (File.Exists(file))
                    Replace(temporary, file);
                else
                    File.Move(temporary, file);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        static void Replace(string temporary, string file)
        {
            // A file previewer or scanner can briefly hold a newly written icon.
            // Keep the old file intact and retry only transient sharing failures.
            for (int attempt = 0;; attempt++)
            {
                try
                {
                    File.Replace(temporary, file, null);
                    return;
                }
                catch (IOException error)
                {
                    int code = error.HResult & 65535;
                    if (attempt >= 3 || (code != 32 && code != 33 && code != 1175) || !File.Exists(temporary) || !File.Exists(file))
                        throw;
                    System.Threading.Thread.Sleep(50 * (attempt + 1));
                }
            }
        }

        static byte[] Png(ImageSource source, int size)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(AppIconShape.Raster(source, size)));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        static void WriteIco(ImageSource source, Stream stream)
        {
            int[] sizes =
            {
                16,
                20,
                24,
                32,
                40,
                48,
                64,
                128,
                256
            };
            var images = sizes.Select(size => Png(source, size)).ToArray();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((ushort)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)32);
                    writer.Write(images[i].Length);
                    writer.Write(offset);
                    offset += images[i].Length;
                }

                foreach (var bytes in images)
                    writer.Write(bytes);
            }
        }
    }
}
