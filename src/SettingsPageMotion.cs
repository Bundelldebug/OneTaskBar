using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    // Only the settings viewport moves. Its native frame and material stay fixed.
    internal sealed class SettingsPageMotion
    {
        internal readonly Grid View=new Grid {ClipToBounds=true};
        readonly FrameworkElement page;
        readonly Border underneath=new Border {CornerRadius=new CornerRadius(24),Margin=new Thickness(10,7,10,12),IsHitTestVisible=false,Visibility=Visibility.Collapsed};
        ScaleTransform scale;
        TranslateTransform shift;
        int revision;
        internal bool Active {get;private set;}
        internal SettingsPageMotion(FrameworkElement content)
        {
            page=content;View.Children.Add(underneath);View.Children.Add(content);
            View.Unloaded+=(s,e)=>Cancel();
            View.IsVisibleChanged+=(s,e)=>{if(!View.IsVisible)Cancel();};
        }
        internal void Cancel()
        {
            revision++;Active=false;
            if(scale!=null){scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);}
            if(shift!=null){shift.BeginAnimation(TranslateTransform.XProperty,null);shift.BeginAnimation(TranslateTransform.YProperty,null);}
            page.BeginAnimation(UIElement.OpacityProperty,null);page.Opacity=1;page.RenderTransform=Transform.Identity;
            underneath.Visibility=Visibility.Collapsed;scale=null;shift=null;
        }
        internal void Start(bool forward)
        {
            Cancel();
            if(!Motion.Enabled||!View.IsVisible)return;
            int token=revision;Active=true;
            underneath.Background=AppSettingsPalette.Card;underneath.Visibility=Visibility.Visible;
            scale=new ScaleTransform(1,1);shift=new TranslateTransform();
            var transform=new TransformGroup();transform.Children.Add(scale);transform.Children.Add(shift);
            page.RenderTransformOrigin=new Point(.5,0);page.RenderTransform=transform;
            var completion=Motion.PanelTween(forward?.975:.99,1,240);
            completion.Completed+=(s,e)=>{if(revision==token)Cancel();};
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,Motion.PanelTween(forward?.975:.99,1,240));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,completion);
            shift.BeginAnimation(TranslateTransform.XProperty,Motion.PanelTween(forward?16:-12,0,240));
            shift.BeginAnimation(TranslateTransform.YProperty,Motion.PanelTween(forward?10:5,0,240));
            page.BeginAnimation(UIElement.OpacityProperty,Motion.Tween(.58,1,180));
        }
    }
}
