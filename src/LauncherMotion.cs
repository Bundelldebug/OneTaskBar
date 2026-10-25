using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace OneTaskBar
{
    // Cache only the moving launcher content, at native DPI, for the duration of a transition.
    // The live material/native window stays still; no cache survives hide or completion.
    internal static class LauncherMotion
    {
        sealed class State {internal int Token;internal bool Active,Opening;internal CacheMode Previous;internal BitmapCache Owned;internal TaskCompletionSource<bool> Ready;internal DispatcherTimer Deadline;internal DateTime DeadlineAt;}
        static readonly ConditionalWeakTable<FrameworkElement,State> states=new ConditionalWeakTable<FrameworkElement,State>();
        internal static bool Active(FrameworkElement element){State state;return element!=null&&states.TryGetValue(element,out state)&&state.Active;}
        internal static bool FitsCache(double width,double height,double dpi){return width>0&&height>0&&dpi>0&&width*dpi<=4096&&height*dpi<=4096&&width*height*dpi*dpi<=4000000;}
        static State Observe(FrameworkElement element){return states.GetValue(element,key=>{var state=new State();state.Deadline=new DispatcherTimer(DispatcherPriority.Background,key.Dispatcher);state.Deadline.Tick+=(s,e)=>{if(!state.Active){state.Deadline.Stop();return;}var remaining=state.DeadlineAt-DateTime.UtcNow;if(remaining.TotalMilliseconds>0){state.Deadline.Interval=TimeSpan.FromMilliseconds(Math.Max(1,remaining.TotalMilliseconds));return;}var window=Window.GetWindow(key) as GlassWindow;if(!state.Opening&&window!=null&&window.IsDismissing)window.HideImmediately();else Cancel(key);};key.IsVisibleChanged+=(s,e)=>{if(!key.IsVisible)Cancel(key);};key.Unloaded+=(s,e)=>Cancel(key);return state;});}
        static void ReleaseCache(FrameworkElement element,State state){if(state.Owned!=null&&element.CacheMode==state.Owned)element.CacheMode=state.Previous;state.Previous=null;state.Owned=null;element.InvalidateVisual();}
        static void Release(FrameworkElement element,State state){state.Deadline.Stop();ReleaseCache(element,state);state.Active=false;var ready=state.Ready;state.Ready=null;if(ready!=null)ready.TrySetResult(true);}
        internal static void InvalidateCache(DependencyObject child){for(var node=child;node!=null;node=VisualTreeHelper.GetParent(node)){var element=node as FrameworkElement;State state;if(element!=null&&states.TryGetValue(element,out state)&&state.Owned!=null)ReleaseCache(element,state);}}
        internal static Task WhenIdle(DependencyObject child){for(var node=child;node!=null;node=VisualTreeHelper.GetParent(node)){var element=node as FrameworkElement;State state;if(element!=null&&states.TryGetValue(element,out state)&&state.Active&&state.Ready!=null)return state.Ready.Task;}return Task.FromResult(true);}
        internal static void Cancel(FrameworkElement element)
        {
            State state;if(element==null||!states.TryGetValue(element,out state))return;++state.Token;Release(element,state);var group=element.RenderTransform as TransformGroup;if(group!=null)foreach(var transform in group.Children){var scale=transform as ScaleTransform;if(scale!=null){scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);}var shift=transform as TranslateTransform;if(shift!=null)shift.BeginAnimation(TranslateTransform.YProperty,null);}element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;element.RenderTransform=Transform.Identity;
        }
        internal static DoubleAnimation Run(FrameworkElement element,bool opening)
        {
            var state=Observe(element);var old=element.RenderTransform as TransformGroup;var oldScale=old==null?null:old.Children.OfType<ScaleTransform>().FirstOrDefault();var oldShift=old==null?null:old.Children.OfType<TranslateTransform>().FirstOrDefault();
            double dpi=VisualTreeHelper.GetDpi(element).DpiScaleX;bool cached=RenderCapability.Tier>=0x10000&&FitsCache(element.ActualWidth,element.ActualHeight,dpi);
            double fromScale=oldScale!=null?oldScale.ScaleX:opening&&cached?.985:1,fromY=oldShift!=null?oldShift.Y:opening?7:0,fromOpacity=old!=null?element.Opacity:opening?.15:element.Opacity;
            Cancel(element);int token=++state.Token;state.Active=true;state.Ready=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);if(cached){state.Previous=element.CacheMode;state.Owned=new BitmapCache {RenderAtScale=dpi,SnapsToDevicePixels=true};element.CacheMode=state.Owned;}
            int duration=opening?280:160;state.Opening=opening;state.DeadlineAt=DateTime.UtcNow.AddMilliseconds(duration+100);state.Deadline.Interval=TimeSpan.FromMilliseconds(duration+100);state.Deadline.Start();double targetScale=opening?1:cached?.99:1,targetY=opening?0:7;var scale=new ScaleTransform(fromScale,fromScale);var shift=new TranslateTransform(0,fromY);var group=new TransformGroup();group.Children.Add(scale);group.Children.Add(shift);element.RenderTransformOrigin=new Point(.5,1);element.RenderTransform=group;
            var end=Motion.PanelTween(fromScale,targetScale,duration);end.FillBehavior=FillBehavior.HoldEnd;end.Completed+=(s,e)=>{if(state.Token!=token)return;if(opening)Cancel(element);else Release(element,state);};var x=Motion.PanelTween(fromScale,targetScale,duration);x.FillBehavior=FillBehavior.HoldEnd;var y=Motion.PanelTween(fromY,targetY,duration);y.FillBehavior=FillBehavior.HoldEnd;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,x);scale.BeginAnimation(ScaleTransform.ScaleYProperty,end);shift.BeginAnimation(TranslateTransform.YProperty,y);
            element.Opacity=fromOpacity;var fade=Motion.Tween(fromOpacity,opening?1:0,opening?200:160);fade.FillBehavior=FillBehavior.HoldEnd;if(opening)element.BeginAnimation(UIElement.OpacityProperty,fade);return fade;
        }
        internal static void Page(FrameworkElement viewport){if(viewport==null||!viewport.IsVisible||!Motion.Enabled)return;for(var parent=VisualTreeHelper.GetParent(viewport);parent!=null;parent=VisualTreeHelper.GetParent(parent)){var element=parent as FrameworkElement;if(element!=null&&Active(element))return;}Run(viewport,true);}
    }
}
