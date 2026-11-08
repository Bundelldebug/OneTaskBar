using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // WPF paces its animation clock at 60 frames per second unless told otherwise, so every spring and flight in the
    // shell moved at half the rate a 120 Hz display can show. Raising the default lets them follow the display.
    // It has to happen before the first window exists.
    internal static class FrameRate
    {
        static bool raised;
        [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")]static extern int GetDeviceCaps(IntPtr dc,int index);
        internal static int Refresh()
        {
            var dc=GetDC(IntPtr.Zero);
            try{int hz=GetDeviceCaps(dc,116);return hz<=1?60:hz;}
            finally{ReleaseDC(IntPtr.Zero,dc);}
        }
        internal static int Target {get;private set;}
        internal static void Raise()
        {
            if(raised)return;
            raised=true;
            Target=60;
            try
            {
                // Animations follow the display rate, up to 120. The 60Hz mode in settings (and light effects) keeps them at 60.
                int hz=Math.Max(60,Math.Min(120,Refresh()));
                if(hz<=60||Performance.Light||Preferences.Current.Limit60Hz)return;
                Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline),new FrameworkPropertyMetadata(hz));
                Target=hz;
            }
            catch(Exception error){AppLog.Once("frame-rate",error.GetType().Name);}
        }
    }
}
