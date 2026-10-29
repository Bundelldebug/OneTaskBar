using System;
using System.Windows.Automation;

namespace OneTaskBar
{
    internal static class TrayButtonAction
    {
        internal static bool CanInvoke(AutomationElement element)
        {
            object pattern;
            return element.TryGetCurrentPattern(InvokePattern.Pattern,out pattern);
        }
        internal static bool Invoke(AutomationElement element,bool chevron=false)
        {
            if(element==null)return false;
            object pattern;
            if(element.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))
            {
                ((InvokePattern)pattern).Invoke();
                return true;
            }
            if(chevron&&element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern,out pattern))
            {
                var expand=(ExpandCollapsePattern)pattern;
                if(expand.Current.ExpandCollapseState==ExpandCollapseState.Collapsed)expand.Expand();
                else expand.Collapse();
                return true;
            }
            if(chevron&&element.TryGetCurrentPattern(TogglePattern.Pattern,out pattern))
            {
                ((TogglePattern)pattern).Toggle();
                return true;
            }
            return false;
        }
        internal static bool IsChevron(string id,string name)
        {
            id=id??"";name=name??"";
            return id.IndexOf("Chevron",StringComparison.OrdinalIgnoreCase)>=0||id.IndexOf("Overflow",StringComparison.OrdinalIgnoreCase)>=0
                ||name.Contains("숨겨진")||name.Contains("오버플로")||name.Contains("더 많은 아이콘")
                ||name.IndexOf("hidden",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("overflow",StringComparison.OrdinalIgnoreCase)>=0;
        }
    }
}
