using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OneTaskBar
{
    // Only the settings viewport moves. Its native frame and material stay fixed.
    internal sealed class SettingsPageMotion
    {
        internal readonly Grid View=new Grid {ClipToBounds=true};
        readonly FrameworkElement page;
        readonly Image previous=new Image {Stretch=Stretch.Fill,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
        readonly TranslateTransform incoming=new TranslateTransform(),outgoing=new TranslateTransform();
        DispatcherOperation pending;
        bool prepared;
        int revision;
        internal bool Active {get;private set;}
        internal bool HoldsSnapshot {get{return previous.Source!=null;}}
        internal SettingsPageMotion(FrameworkElement content)
        {
            page=content;View.Children.Add(previous);View.Children.Add(content);
            View.Unloaded+=(s,e)=>Cancel();
            View.IsVisibleChanged+=(s,e)=>{if(!View.IsVisible)Cancel();};
            View.SizeChanged+=(s,e)=>{if(Active||prepared)Cancel();};
        }
        internal void Prepare()
        {
            BitmapSource snapshot=null;
            if(Motion.Enabled&&View.IsVisible&&View.ActualWidth>0&&View.ActualHeight>0)
            {
                var source=PresentationSource.FromVisual(View);
                var dpi=source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformToDevice:Matrix.Identity;
                int width=(int)Math.Ceiling(View.ActualWidth*dpi.M11),height=(int)Math.Ceiling(View.ActualHeight*dpi.M22);
                if(width>0&&height>0&&(long)width*height<=8000000)
                {
                    var bitmap=new RenderTargetBitmap(width,height,96*dpi.M11,96*dpi.M22,PixelFormats.Pbgra32);
                    bitmap.Render(View);
                    bitmap.Freeze();
                    snapshot=bitmap;
                }
            }
            Cancel();
            prepared=true;
            if(snapshot==null)return;
            previous.Source=snapshot;
            previous.Visibility=Visibility.Visible;
        }
        internal void Cancel()
        {
            revision++;Active=false;prepared=false;
            if(pending!=null){pending.Abort();pending=null;}
            incoming.BeginAnimation(TranslateTransform.XProperty,null);
            outgoing.BeginAnimation(TranslateTransform.XProperty,null);
            incoming.X=outgoing.X=0;
            page.BeginAnimation(UIElement.OpacityProperty,null);page.Opacity=1;page.RenderTransform=Transform.Identity;
            previous.BeginAnimation(UIElement.OpacityProperty,null);
            previous.Opacity=1;
            previous.RenderTransform=Transform.Identity;
            previous.Visibility=Visibility.Collapsed;
            previous.Source=null;
        }
        internal void Start(bool forward)
        {
            if(!prepared)Cancel();
            prepared=false;
            if(!Motion.Enabled||!View.IsVisible){Cancel();return;}
            int token=++revision;Active=true;
            page.Opacity=0;
            // ScrollViewer queues offset/extent changes. Keep the old frame visible
            // until the new content has completed that layout pass.
            pending=View.Dispatcher.BeginInvoke(new Action(()=>
            {
                pending=null;
                if(revision!=token)return;
                if(!View.IsVisible||!Motion.Enabled){Cancel();return;}
                page.UpdateLayout();
                if(revision!=token)return;
                page.Opacity=1;
                page.RenderTransform=incoming;
                var completion=Motion.PanelTween(forward?18:-18,0,220);
                completion.Completed+=(s,e)=>{if(revision==token)Cancel();};
                incoming.BeginAnimation(TranslateTransform.XProperty,completion);
                page.BeginAnimation(UIElement.OpacityProperty,Motion.Tween(0,1,180));
                if(previous.Source!=null)
                {
                    previous.RenderTransform=outgoing;
                    previous.Opacity=0;
                    outgoing.BeginAnimation(TranslateTransform.XProperty,Motion.PanelTween(0,forward?-12:12,220));
                    previous.BeginAnimation(UIElement.OpacityProperty,Motion.Tween(1,0,180));
                }
            }),DispatcherPriority.Loaded);
        }
    }
}
