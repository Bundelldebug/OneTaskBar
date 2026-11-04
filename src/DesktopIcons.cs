using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace OneTaskBar
{
    // Reads the screen rectangles of Windows desktop icons through UI Automation.
    // It is read-only: Explorer's memory, icons and layout are never touched.
    internal static class DesktopIcons
    {
        const int Limit=1000;
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string kind,string name);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        internal static List<Rect> Read(IntPtr desktopView)
        {
            var result=new List<Rect>();
            IntPtr list=FindWindowEx(desktopView,IntPtr.Zero,"SysListView32",null);
            if(list==IntPtr.Zero||!IsWindowVisible(list))return result;
            var items=AutomationElement.FromHandle(list).FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem));
            foreach(AutomationElement item in items)
            {
                if(result.Count>=Limit)break;
                var bounds=item.Current.BoundingRectangle;
                if(!bounds.IsEmpty&&bounds.Width>0&&bounds.Height>0&&!Double.IsInfinity(bounds.X))result.Add(bounds);
            }
            return result;
        }
    }
}
