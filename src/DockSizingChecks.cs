using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace OneTaskBar
{
    internal static class DockSizingChecks
    {
        internal static async Task Run(ShellController shell,System.Windows.Forms.Screen screen)
        {
            int saved=Preferences.Current.DockScalePercent,edge=Preferences.Current.DockEdge,clockMode=Preferences.Current.DockClockMode;
            DockHost dock=null;
            try
            {
                foreach(int mode in new[]{0,1,2})foreach(int percent in new[]{60,100,140})foreach(int side in new[]{0,1,2,3})
                {
                    Preferences.Current.DockClockMode=mode;
                    Preferences.Current.DockScalePercent=percent;
                    Preferences.Current.DockEdge=side;
                    dock=new DockHost(shell,screen);dock.Show();
                    await Task.Delay(55);
                    var expected=Native.DropMonitorScale(screen.Bounds);
                    if(Math.Abs(dock.Scale-expected)>.01||Math.Abs(dock.ContentScale-dock.Scale*percent/100.0)>.001)
                        throw new Exception("Dock zoom changed the real monitor DPI");
                    Native.RectI actual;Native.GetWindowRect(new WindowInteropHelper(dock.Apps).Handle,out actual);
                    double thickness=(DockGeometry.Vertical(side)?actual.Right-actual.Left:actual.Bottom-actual.Top)/dock.Scale;
                    if(Math.Abs(thickness-44*percent/100.0)>1)
                        throw new Exception("Dock size does not follow its percentage on edge "+side);
                    if(actual.Left<screen.Bounds.Left||actual.Top<screen.Bounds.Top||actual.Right>screen.Bounds.Right||actual.Bottom>screen.Bounds.Bottom)
                        throw new Exception("Resized dock exceeds its monitor");
                    var button=dock.SearchButton;
                    var center=new Point(button.ActualWidth/2,button.ActualHeight/2);
                    var returned=button.PointFromScreen(button.PointToScreen(center));
                    if((returned-center).Length>1/dock.ContentScale)throw new Exception("Scaled dock input mapping exceeds one physical pixel: "+(returned-center).Length+" at "+percent+"% edge "+side);
                    var area=new Native.RectI {Left=screen.Bounds.Left,Top=screen.Bounds.Top,Right=screen.Bounds.Right,Bottom=screen.Bounds.Bottom};
                    var reserved=AppBarReservation.Reservation(screen.Bounds,dock.ContentScale,area,false,side);
                    double reservedSize=DockGeometry.Vertical(side)?reserved.Right-reserved.Left:reserved.Bottom-reserved.Top;
                    if(Math.Abs(reservedSize/dock.Scale-DockSizing.Reserved)>1)
                        throw new Exception("Dock work-area reservation uses the wrong percentage");
                    var grid=(System.Windows.Controls.Grid)dock.Status.Surface.Child;
                    foreach(UIElement child in grid.Children)
                    {
                        var item=child as System.Windows.Controls.Button;if(item==null)continue;
                        if(System.Windows.Automation.AutomationProperties.GetName(item)!="알림센터")continue;
                        var text=(System.Windows.Controls.TextBlock)item.Content;
                        if(text.DesiredSize.Height>item.ActualHeight+.1||text.DesiredSize.Width>item.ActualWidth+.5)
                            throw new Exception("Clock text exceeds the compact clock button at mode "+mode+" edge "+side);
                    }
                    dock.Close();dock=null;
                }
            }
            finally
            {
                if(dock!=null)dock.Close();
                Preferences.Current.DockScalePercent=saved;
                Preferences.Current.DockEdge=edge;
                Preferences.Current.DockClockMode=clockMode;
            }
        }
    }
}
