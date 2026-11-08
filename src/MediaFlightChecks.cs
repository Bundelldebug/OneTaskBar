using System;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class MediaFlightChecks
    {
        static void Require(bool value,string message)
        {
            if(!value)throw new Exception(message);
        }
        internal static async Task CaptureRace(Window source)
        {
            var handle=new WindowInteropHelper(source).Handle;
            source.Activate();
            Native.Activate(handle);
            await Task.Delay(60);
            Native.RectI rect;
            Require(Native.GetWindowRect(handle,out rect),"capture fixture has a source window");
            var bounds=new Rect(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
            var completion=new TaskCompletionSource<BitmapSource>();
            using(var motion=new MediaWindowMotion(null,(window,area)=>completion.Task))
            {
                var pending=motion.Capture(handle,bounds);
                Native.ShowWindow(handle,6);
                await Task.Delay(80);
                Native.ShowWindow(handle,9);
                Native.Activate(handle);
                await Task.Delay(80);
                // Even if the HWND is visible again, a late desktop copy belongs
                // to an earlier transition and must never replace the app frame.
                completion.SetResult(ArtworkPalette.Sample(Colors.Coral));
                await pending;
                Require(motion.CapturedImage==null,"capture crossing minimize/restore is discarded");
            }
            var valid=ArtworkPalette.Sample(Colors.Turquoise);
            using(var motion=new MediaWindowMotion(null,(window,area)=>Task.FromResult(valid)))
            {
                await motion.Capture(handle,bounds);
                Require(Object.ReferenceEquals(motion.CapturedImage,valid),"an uninterrupted foreground capture is accepted");
            }
        }

        // The flight plane must be see-through everywhere except the window picture, and it must not be a layered window.
        internal static async Task PlaneTransparency()
        {
            using(var back=new System.Windows.Forms.Form {FormBorderStyle=System.Windows.Forms.FormBorderStyle.None,StartPosition=System.Windows.Forms.FormStartPosition.Manual,
                Bounds=new System.Drawing.Rectangle(1040,40,900,700),BackColor=System.Drawing.Color.FromArgb(255,0,255),ShowInTaskbar=false,TopMost=true})
            {
                back.Show();
                var picture=ArtworkPalette.Sample(Color.FromRgb(0,0,255));
                var initial=new Rect(1300,250,300,200);
                var plane=new MediaWindowPlane(picture,initial,new Rect(1120,120,44,44));
                try
                {
                    plane.Frame(initial,new Rect(1120,120,44,44),0);
                    plane.Present();
                    var handle=new WindowInteropHelper(plane).Handle;
                    Require((Native.GetWindowLong(handle,-20)&0x80000)==0,"flight plane is not a layered window");
                    await Task.Delay(400);
                    var inside=ScreenPixel(1450,350);
                    var outside=ScreenPixel(1110,110);
                    Require(inside.B>150&&inside.R<100,"flight plane shows the window picture");
                    Require(outside.R>200&&outside.B>200&&outside.G<80,"flight plane is transparent outside the picture: "+outside.R+","+outside.G+","+outside.B);
                }
                finally
                {
                    plane.Close();
                    back.Close();
                }
            }
        }
        static System.Drawing.Color ScreenPixel(int x,int y)
        {
            using(var bitmap=new System.Drawing.Bitmap(1,1))
            using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x,y,0,0,new System.Drawing.Size(1,1));
                return bitmap.GetPixel(0,0);
            }
        }

        internal static void Check()
        {
            var window=new Rect(100,90,1280,720);
            foreach(var resting in new[]{new Rect(200,200,44,44),new Rect(100,100,640,44),new Rect(100,100,44,640)})
            {
                var reserved=resting;
                double padding=MediaCapsulePose.HostPadding(resting);
                reserved.Inflate(padding,padding);
                for(int frame=0;frame<=240;frame++)
                    Require(reserved.Contains(MediaCapsulePose.At(resting,window,frame/240.0)),
                        "wide and vertical capsules fit their fixed native host at every handoff frame");
            }
            foreach(var capsule in new[]{new Rect(1400,40,44,88),new Rect(20,900,228,44),new Rect(20,40,44,44)})
            {
                var last=MediaWindowPose.At(window,capsule,0);
                Require(last.Width==window.Width&&last.Height==window.Height&&last.Opacity==1,"flight starts on the actual app bounds");
                for(int frame=1;frame<=240;frame++)
                {
                    var pose=MediaWindowPose.At(window,capsule,frame/240.0);
                    Require(pose.Width<=last.Width+.001&&pose.Height<=last.Height+.001,"neither flight axis swells before reaching the capsule");
                    Require(pose.Radius>=0&&pose.Radius<=Math.Min(pose.Width,pose.Height)/2+.001,"rounded mesh remains inside its bounds");
                    Require(pose.Opacity<=last.Opacity+.001&&pose.Opacity>=0,"window blends into the receiving capsule without reappearing");
                    Require(Math.Abs(pose.Tilt)<=15.001&&Math.Abs(pose.Turn)<=15.001,"perspective remains restrained");
                    var deformation=MediaCapsulePose.At(capsule,window,frame/240.0);
                    Require(deformation.Width>capsule.Width*.92&&deformation.Width<capsule.Width*1.08&&
                        deformation.Height>capsule.Height*.92&&deformation.Height<capsule.Height*1.08,"receiving capsule stays within its reserved deformation margin");
                    last=pose;
                }
                Require(Math.Abs(last.Width-capsule.Width)<.001&&Math.Abs(last.Height-capsule.Height)<.001&&
                    Math.Abs(last.Center.X-capsule.X-capsule.Width/2)<.001&&
                    Math.Abs(last.Center.Y-capsule.Y-capsule.Height/2)<.001&&last.Opacity==0,"window reaches the exact receiving shape");
                Require(MediaCapsulePose.At(capsule,window,0)==capsule&&MediaCapsulePose.At(capsule,window,1)==capsule,"capsule starts and finishes at rest");
                foreach(bool restoring in new[]{false,true})
                {
                    var first=MediaWindowPose.At(window,capsule,MediaFlightTiming.Position(0,restoring));
                    var firstStep=MediaWindowPose.At(window,capsule,MediaFlightTiming.Position(.001,restoring));
                    var end=MediaWindowPose.At(window,capsule,MediaFlightTiming.Position(1,restoring));
                    var endStep=MediaWindowPose.At(window,capsule,MediaFlightTiming.Position(.999,restoring));
                    Require((first.Center-firstStep.Center).Length<.15&&(end.Center-endStep.Center).Length<.15&&
                        Math.Abs(first.Width-firstStep.Width)<.15&&Math.Abs(end.Width-endStep.Width)<.15,"position and size hand off without endpoint velocity");
                }
            }
        }
    }
}
