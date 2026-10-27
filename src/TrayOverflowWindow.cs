using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal sealed class TrayOverflowWindow : GlassWindow
    {
        readonly TrayIconSession session;
        readonly Action<TrayIconItem,bool> invoke;
        readonly DispatcherTimer timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(200)};
        readonly Grid icons=new Grid {Margin=new Thickness(12)};
        IDisposable outside;
        bool closing;
        internal int IconCount {get;private set;}
        internal bool Watching {get{return timer.IsEnabled;}}
        internal TrayOverflowWindow(TrayIconSession session,Action<TrayIconItem,bool> action)
        {
            this.session=session;invoke=action;Title="One TaskBar · 시스템 트레이";DockPalette=true;Dark=Glass95.DockDark;
            Radius=28;ShowActivated=false;Surface.CornerRadius=new CornerRadius(28);
            SetMaterial(QuickPanel.Glass,PanelMaterial.Solid(Glass95.DockDark));Surface.Child=icons;
            int count=session.Items.Count,columns=Math.Min(5,Math.Max(1,(int)Math.Ceiling(Math.Sqrt(count))));
            for(int i=0;i<columns;i++)icons.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(48)});
            for(int i=0;i<(count+columns-1)/columns;i++)icons.RowDefinitions.Add(new RowDefinition {Height=new GridLength(48)});
            for(int i=0;i<count;i++){
                var item=session.Items[i];UIElement image=item.Icon==null?(UIElement)new Glyph(item.Fallback,23) {Color=Glass95.DockForeground}:new TrayIconVisual(item.Icon);
                var button=Paint.Button(image,()=>ActivateItem(item,false),item.Name);button.Margin=new Thickness(2);button.Padding=new Thickness(8);button.Background=Brushes.Transparent;button.Resources["ButtonRadius"]=new CornerRadius(18);
                System.Windows.Automation.AutomationProperties.SetName(button,item.Name);
                button.PreviewMouseRightButtonUp+=(s,e)=>{e.Handled=true;ActivateItem(item,true);};
                Grid.SetColumn(button,i%columns);Grid.SetRow(button,i/columns);icons.Children.Add(button);IconCount++;
            }
            SourceInitialized+=(s,e)=>HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook((IntPtr h,int message,IntPtr wp,IntPtr lp,ref bool handled)=>{if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});
            timer.Tick+=(s,e)=>{if(!session.SourceVisible)Dismiss();};
            IsVisibleChanged+=(s,e)=>{if(!IsVisible&&!closing){closing=true;Close();}};
            Closed+=(s,e)=>{timer.Stop();if(outside!=null){outside.Dispose();outside=null;}session.Dispose();};
        }
        internal void Present(TrayPlacement placement,bool watch=true)
        {
            double width=icons.ColumnDefinitions.Count*48+26,height=icons.RowDefinitions.Count*48+26;
            var point=placement.Target((int)Math.Round(width*placement.Scale),(int)Math.Round(height*placement.Scale));
            ShowAt(placement.Monitor,(point.X-placement.Monitor.Left)/placement.Scale,(point.Y-placement.Monitor.Top)/placement.Scale,width,height);Pop();
            if(watch){timer.Start();outside=MenuDismiss.Watch(Contains,Dismiss);}
        }
        bool Contains(Native.PointI point){Native.RectI bounds;return Native.GetWindowRect(new WindowInteropHelper(this).Handle,out bounds)&&point.X>=bounds.Left&&point.X<bounds.Right&&point.Y>=bounds.Top&&point.Y<bounds.Bottom;}
        async void ActivateItem(TrayIconItem item,bool context)
        {
            if(closing)return;closing=true;timer.Stop();HideImmediately();
            try{await Task.Run(()=>invoke(item,context));}
            catch(Exception error){AppLog.Once("tray-command",error.GetType().Name);}
            finally{Close();}
        }
        internal async void Dismiss(){if(closing)return;closing=true;timer.Stop();await DismissAsync();Close();}
    }
}
