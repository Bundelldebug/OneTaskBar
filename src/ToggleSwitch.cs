using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    internal sealed class ToggleSwitch : FrameworkElement
    {
        static readonly DependencyProperty ProgressProperty=DependencyProperty.Register("Progress",typeof(double),typeof(ToggleSwitch),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));
        internal bool Checked;internal Action<bool> Changed;
        internal ToggleSwitch(string name,bool value,Action<bool> changed)
        {
            Width=48;Height=28;Focusable=true;Cursor=Cursors.Hand;Checked=value;Changed=changed;SetValue(ProgressProperty,value?1.0:0.0);AutomationProperties.SetName(this,name);
        }
        internal void Set(bool value)
        {
            Checked=value;double target=value?1:0;
            double before=(double)GetValue(ProgressProperty);BeginAnimation(ProgressProperty,null);SetValue(ProgressProperty,target);if(Preferences.Current.Animations)BeginAnimation(ProgressProperty,new DoubleAnimation(before,target,TimeSpan.FromMilliseconds(170)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            if(Changed!=null)Changed(value);
        }
        internal void Refresh(bool value){if(Checked==value)return;Checked=value;BeginAnimation(ProgressProperty,null);SetValue(ProgressProperty,value?1.0:0.0);InvalidateVisual();}
        protected override void OnRender(DrawingContext dc)
        {
            double progress=(double)GetValue(ProgressProperty);
            dc.DrawRoundedRectangle(Paint.Brush(Checked?"#FF4085F4":"#FF718078"),null,new Rect(0,1,48,26),13,13);
            dc.DrawEllipse(Brushes.White,null,new Point(14+20*progress,14),10,10);
            if(IsKeyboardFocused)dc.DrawRoundedRectangle(null,new Pen(Paint.Brush("#FFE0EDFF"),1),new Rect(-2,-1,52,30),15,15);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){Focus();if(IsEnabled)Set(!Checked);e.Handled=true;}
        protected override void OnKeyDown(KeyEventArgs e){if(e.Key==Key.Space||e.Key==Key.Enter){Set(!Checked);e.Handled=true;}else base.OnKeyDown(e);}
        protected override AutomationPeer OnCreateAutomationPeer(){return new TogglePeer(this);}
        sealed class TogglePeer : FrameworkElementAutomationPeer,IToggleProvider
        {
            readonly ToggleSwitch control;
            internal TogglePeer(ToggleSwitch control):base(control){this.control=control;}
            protected override AutomationControlType GetAutomationControlTypeCore(){return AutomationControlType.CheckBox;}
            protected override string GetClassNameCore(){return "OneTaskBar.ToggleSwitch";}
            public override object GetPattern(PatternInterface pattern){return pattern==PatternInterface.Toggle?this:base.GetPattern(pattern);}
            public ToggleState ToggleState {get{return control.Checked?ToggleState.On:ToggleState.Off;}}
            public void Toggle(){if(!control.IsEnabled)throw new ElementNotEnabledException();control.Set(!control.Checked);}
        }
    }
}
