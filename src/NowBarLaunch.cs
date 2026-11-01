using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal sealed partial class NowBar
    {
        TaskCompletionSource<bool> launchCollapse;

        internal Task<bool> CollapseForLaunchAsync()
        {
            SetExpanded(false);
            if (closed || !wanted) return Task.FromResult(false);
            if (!animating) return Task.FromResult(true);
            if (launchCollapse == null)
                launchCollapse = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return launchCollapse.Task;
        }

        void CompleteLaunchCollapse()
        {
            if (launchCollapse == null) return;
            var pending = launchCollapse;
            launchCollapse = null;
            // Present the collapsed content and blur before starting a reverse flight.
            Dispatcher.BeginInvoke(new Action(() => pending.TrySetResult(!closed && wanted && !expanded && !animating)),
                DispatcherPriority.ContextIdle);
        }
    }
}
