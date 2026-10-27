using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OneTaskBar
{
    internal static class ToggleExpansionChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Toggle expansion: "+message);}
        internal static async Task Run(QuickWindow quick)
        {
            var grid=quick.GridUI;
            var board=grid.Board;
            var saved=board.Layout.Copy();
            try
            {
                board.Layout.Resize("toggles",4,2,false);
                board.Reflow(false);
                quick.UpdateLayout();
                var first=grid.ActionItem(grid.VisibleActionKeys.First()) as StackPanel;
                var button=(FrameworkElement)first.Children[0];
                double size=button.ActualHeight;
                Require(grid.VisibleActionKeys.All(key=>((TextBlock)((StackPanel)grid.ActionItem(key)).Children[1]).Visibility==Visibility.Collapsed),"multirow folded group never displays labels");
                quick.SetExpanded(true);
                await Task.Delay(70);
                Require(Math.Abs(button.ActualHeight-size)<.1&&board.Frames["toggles"].SizeScale.ScaleY==1,"expansion never scales circular icons");
                await Task.Delay(260);
                Require(grid.VisibleActionKeys.All(key=>Math.Abs(quick.FindToggle(key).ActualHeight-size)<.1),"folded and expanded icons have equal size");
                quick.SetExpanded(false);
                await Task.Delay(35);
                quick.SetExpanded(true);
                await Task.Delay(35);
                quick.SetExpanded(false);
                await Task.Delay(290);
                Require(!board.Expanded&&Double.IsNaN(board.Frames["toggles"].Height),"rapid reversal releases the temporary height");
                Require(grid.VisibleActionKeys.All(key=>((TextBlock)((StackPanel)grid.ActionItem(key)).Children[1]).Visibility==Visibility.Collapsed),"reversal leaves folded labels hidden");
                Program.Save(board.Frames["toggles"].Body,344,130,Path.Combine(AppLog.Data,"quick9-folded-two-rows.png"));
            }
            finally
            {
                grid.ExpandToggles(false,false);
                board.Frames["toggles"].StopExpansion();
                board.Layout.Restore(saved);
                board.Reflow(false);
            }
        }
    }
}
