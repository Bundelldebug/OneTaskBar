using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class TrayFrameChecks
    {
        internal static async Task Run()
        {
            using (var window = new System.Windows.Forms.Form())
            {
                window.Text = "One TaskBar tray pixel fixture";
                window.ShowInTaskbar = false;
                window.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                window.ClientSize = new System.Drawing.Size(120, 120);
                window.BackColor = System.Drawing.Color.Red;
                window.Show();
                bool previous = TaskbarMask.Active;
                try
                {
                    TaskbarMask.Active = true;
                    TaskbarMask.Hide(window.Handle);
                    await Task.Delay(100);
                    var red = await Task.Run(() => TrayFrame.Read(window.Handle));
                    window.BackColor = System.Drawing.Color.Lime;
                    window.Refresh();
                    var green = await Task.Run(() => TrayFrame.Read(window.Handle));
                    bool updates = Match(red, 2) && Match(green, 1);
                    AppLog.Write("tray-pixel-check", "Masked client pixels update=" + updates);
                    if (!updates) throw new InvalidOperationException("Tray client capture did not follow its source");
                    await CheckIconUpdates(window);
                }
                finally
                {
                    window.Hide();
                    TaskbarMask.Restore(window.Handle);
                    TaskbarMask.Active = previous;
                }
            }
        }

        static async Task CheckIconUpdates(System.Windows.Forms.Form window)
        {
            bool online=false;
            var button=new System.Windows.Forms.Button {Text="",AccessibleName="fixture state",Left=20,Top=20,Width=60,Height=60};
            button.Paint+=(s,e)=>
            {
                using(var color=new System.Drawing.SolidBrush(online?System.Drawing.Color.Lime:System.Drawing.Color.Red))
                    e.Graphics.FillRectangle(color,16,16,28,28);
                e.Graphics.FillRectangle(System.Drawing.Brushes.White,25,25,10,10);
            };
            window.Controls.Add(button);
            button.Refresh();
            var item=new TrayIconItem {Name="fixture state",Element=System.Windows.Automation.AutomationElement.FromHandle(button.Handle)};
            using(var session=new TrayIconSession(window.Handle,new System.Collections.Generic.List<TrayIconItem> {item}))
            {
                session.Mask();
                var first=await Task.Run(()=>session.ReadUpdates());
                var unchanged=await Task.Run(()=>session.ReadUpdates());
                online=true;
                button.Invalidate();button.Update();
                var changed=await Task.Run(()=>session.ReadUpdates());
                if(first.Length!=1||first[0].Image==null||!first[0].Image.IsFrozen||
                    unchanged.Length!=0||changed.Length!=1||changed[0].Image==null)
                    throw new InvalidOperationException("Live tray icons must update only when their rendered status changes");
            }
        }

        static bool Match(TrayFrame frame, int channel)
        {
            if (frame == null || !frame.Image.IsFrozen) return false;
            var pixels = new FormatConvertedBitmap(frame.Image, PixelFormats.Bgra32, null, 0);
            var buffer = new byte[4 * pixels.PixelWidth * pixels.PixelHeight];
            pixels.CopyPixels(buffer, pixels.PixelWidth * 4, 0);
            int center = (pixels.PixelHeight / 2 * pixels.PixelWidth + pixels.PixelWidth / 2) * 4;
            return buffer[center + channel] > 220 && buffer[center + (channel == 1 ? 2 : 1)] < 20;
        }
    }
}
