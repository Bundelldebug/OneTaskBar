using System;
using System.Windows.Media;

namespace OneTaskBar
{
    // A tick that follows the display refresh, in place of a 16 ms timer that drifts against it and drops frames.
    // It only listens to the compositor while it is running.
    internal sealed class FrameClock
    {
        bool enabled;
        internal event EventHandler Tick;
        internal bool IsEnabled {get{return enabled;}}
        internal void Start()
        {
            if(enabled)return;
            enabled=true;
            CompositionTarget.Rendering+=Render;
        }
        internal void Stop()
        {
            if(!enabled)return;
            enabled=false;
            CompositionTarget.Rendering-=Render;
        }
        void Render(object sender,EventArgs args)
        {
            var handler=Tick;
            if(handler!=null)handler(this,EventArgs.Empty);
        }
    }
}
