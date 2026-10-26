using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    internal static class NotificationIcons
    {
        // Compare native pixel detail before any display scaling. Do not upscale
        // small notification logos and then mistake them for a sharper resource.
        internal static ImageSource Best(ImageSource logo,ImageSource shell)
        {
            if(logo==null)return shell;if(shell==null)return logo;var a=logo as BitmapSource;var b=shell as BitmapSource;
            return a!=null&&b!=null&&System.Math.Min(b.PixelWidth,b.PixelHeight)>System.Math.Min(a.PixelWidth,a.PixelHeight)?shell:logo;
        }
    }
}
