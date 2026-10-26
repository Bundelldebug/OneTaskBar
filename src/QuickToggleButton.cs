using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace OneTaskBar
{
    internal sealed class QuickToggleButton : Button
    {
        internal bool Checked
        {
            get;
            private set;
        }
        bool initialized,lastAvailable,lastGlass,lastDark;
        internal void RefreshState(bool value,bool available)
        {
            bool glass=Glass95.Enabled,dark=Glass95.Dark;
            if(initialized&&Checked==value&&lastAvailable==available&&lastGlass==glass&&lastDark==dark)return;
            IsEnabled=available;
            Opacity=available?1:.38;
            AutomationProperties.SetItemStatus(this,available?(value?"켜짐":"꺼짐"):"지원 안 됨");
            bool redraw=!initialized||Checked!=value||lastGlass!=glass||lastDark!=dark;
            lastAvailable=available;
            lastGlass=glass;
            lastDark=dark;
            if(!redraw)return;
            bool before=Checked;
            Checked=value;
            initialized=true;
            var previous=Background as SolidColorBrush;
            var target=(SolidColorBrush)(glass?(value?Glass95.Selected:Glass95.Toggle):value?Paint.ControlWhite:Paint.Brush(QuickPanel.Inactive));
            var brush=new SolidColorBrush(target.Color);
            Background=brush;
            if(Glass95.Enabled)
            {
                BorderBrush=Brushes.Transparent;
                BorderThickness=new Thickness(0);
            }
            if(previous!=null&&Motion.Enabled&&IsVisible)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(previous.Color,target.Color,TimeSpan.FromMilliseconds(200))
            {
                FillBehavior=FillBehavior.Stop
            });
            var glyph=Content as Glyph;
            if(glyph!=null)
            {
                glyph.Color=Glass95.ToggleInk(value);
                glyph.InvalidateVisual();
            }
            var peer=UIElementAutomationPeer.FromElement(this) as TogglePeer;
            if(peer!=null)peer.Notify(before,value);
        }
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TogglePeer(this);
        }
        sealed class TogglePeer : ButtonAutomationPeer,IToggleProvider
        {
            readonly QuickToggleButton button;
            internal TogglePeer(QuickToggleButton owner):base(owner)
            {
                button=owner;
            }
            public override object GetPattern(PatternInterface pattern)
            {
                return pattern==PatternInterface.Toggle?this:base.GetPattern(pattern);
            }
            public ToggleState ToggleState
            {
                get
                {
                    return button.Checked?ToggleState.On:ToggleState.Off;
                }
            }
            public void Toggle()
            {
                if(!button.IsEnabled)throw new ElementNotEnabledException();
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            internal void Notify(bool before,bool after)
            {
                if(before!=after)RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,before?ToggleState.On:ToggleState.Off,after?ToggleState.On:ToggleState.Off);
            }
        }
    }
}
