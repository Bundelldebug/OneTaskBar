using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace OneTaskBar
{
    internal static class TopBarChecks
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void BandGeometry()
        {
            foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0, 2.5 })
            foreach (int origin in new[] { -2560, 0, 2880 })
            foreach (bool bottom in new[] { false, true })
            {
                var monitor = new Drawing.Rectangle(origin, -900, 2560, 1440);
                int band = (int)Math.Ceiling(IslandSafeArea.Height * scale);
                var frame = new Native.RectI
                {
                    Left = monitor.Left, Right = monitor.Right,
                    Top = monitor.Top + (bottom ? 0 : band),
                    Bottom = monitor.Bottom - (bottom ? band : 0)
                };
                var sample = TopBarBand.SampleArea(monitor, frame, scale, bottom);
                Require(sample.Width == monitor.Width && sample.Top > frame.Top && sample.Bottom < frame.Bottom,
                    "The sampled strip stays inside the maximized window at each DPI and monitor origin");
                frame.Top += 100;
                frame.Bottom -= 100;
                Require(TopBarBand.SampleArea(monitor, frame, scale, bottom).IsEmpty,
                    "A normal window does not color the reserved band");
            }
            using (var bitmap = new Drawing.Bitmap(280, 6))
            using (var graphics = Drawing.Graphics.FromImage(bitmap))
            {
                graphics.Clear(Drawing.Color.White);
                graphics.FillRectangle(Drawing.Brushes.Black, 0, 0, 140, 6);
                var colors = TopBarBand.Sample(bitmap);
                Require(colors.Length == 7 && colors[0] == Colors.Black && colors[6] == Colors.White,
                    "The band's gradient retains the source window's edge colors");
                Require(TopBarBand.UseLightInk(colors[0]) && !TopBarBand.UseLightInk(colors[6]),
                    "The status text contrasts with the sampled color");
            }
        }

        static Native.RectI Bounds(Window window)
        {
            Native.RectI rect;
            Native.GetWindowRect(new WindowInteropHelper(window).Handle, out rect);
            return rect;
        }

        static bool Same(Native.RectI a, Native.RectI b)
        {
            return a.Left == b.Left && a.Right == b.Right && a.Top == b.Top && a.Bottom == b.Bottom;
        }

        internal static int Run(bool runtime)
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ShellController shell = null;
            TopStatusBar left = null, right = null;
            DockHost dock = null;
            IDisposable motion = null;
            int result = 1;
            application.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    Program.Inspectable = true;
                    motion = Motion.WithAnimationsForFixture();
                    Preferences.Current.Animations = true;
                    Preferences.Current.UIVersion = Fluid.Available ? 100 : 95;
                    Preferences.Current.TopStatusBar = true;
                    Preferences.Current.DynamicIsland = false;
                    BandGeometry();
                    shell = new ShellController(true);
                    shell.Power = new PowerState { Known = true, Level = 81, Connected = true };
                    var screen = Forms.Screen.PrimaryScreen;
                    left = new TopStatusBar(shell, TopStatusBar.Side.Left);
                    right = new TopStatusBar(shell, TopStatusBar.Side.Right);
                    double dpi = Native.PrepareMonitor(right, screen.Bounds);
                    left.SetMatchedBackground(Colors.DimGray);
                    right.SetMatchedBackground(Colors.DimGray);
                    left.Present(screen, dpi, false);
                    right.Present(screen, dpi, false);
                    await Task.Delay(350);
                    var before = Bounds(right);
                    right.PlayCharge(true);
                    await Task.Delay(350);
                    Require(right.ChargingEffect && Same(before, Bounds(right)),
                        "The charging capsule animates without moving the status window");
                    await Task.Delay(2600);
                    Require(!right.ChargingEffect && Same(before, Bounds(right)),
                        "Charging returns to the normal status icons at their original positions");
                    right.PlayCharge(false);
                    shell.Power.Connected = false;
                    right.Present(screen, dpi, false);
                    Require(!right.ChargingEffect, "Unplugging cancels the charging effect");
                    right.PlayCharge(true);
                    right.Conceal(true, true);
                    Require(!right.ChargingEffect, "Hiding the top bar releases the charging animation");
                    right.Conceal(false, false);
                    if (runtime)
                    {
                        // Exercise the idle topbar and dock pulse without desktop hooks, appbar reservations or hardware writes.
                        dock = new DockHost(shell, screen);
                        dock.Show();
                        dock.Refresh(shell.Power);
                        await Task.Delay(2000);
                        AppLog.Json("live-state.json", new { fixture = "idle-topbar-dock", desktop = false,
                            mediaActive = false, displays = 1, windowCount = 0, pulseMilliseconds = 750 });
                        for (int i = 0; i < 100; i++)
                        {
                            var windows = FullscreenVisibility.Capture();
                            FullscreenVisibility.Cover(screen.Bounds, windows);
                            FullscreenVisibility.BandCover(screen.Bounds, windows);
                            left.Present(screen, dpi, false);
                            right.Present(screen, dpi, false);
                            dock.Refresh(shell.Power);
                            await Task.Delay(750);
                        }
                    }
                    AppLog.Json("topbar-probe.json", new { passed = true, chargingKeepsGeometry = true,
                        chargingStopsAfterUnplug = true, sampledBandGeometry = true, dpi = dpi });
                    result = 0;
                }
                catch (Exception error)
                {
                    AppLog.Json("topbar-probe.json", new { passed = false, error = error.ToString() });
                }
                finally
                {
                    if (dock != null) dock.Close();
                    if (left != null) left.Close();
                    if (right != null) right.Close();
                    if (shell != null) shell.Dispose();
                    if (motion != null) motion.Dispose();
                    application.Shutdown(result);
                }
            }), DispatcherPriority.ApplicationIdle);
            return application.Run();
        }
    }
}
