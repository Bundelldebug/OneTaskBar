using System;
using System.Collections.Generic;
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
        readonly DispatcherTimer timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
        readonly Dictionary<TrayIconItem,Button> buttons=new Dictionary<TrayIconItem,Button>();
        readonly Grid icons=new Grid {Margin=new Thickness(12)};
        IDisposable outside;
        bool closing,closed,refreshing;
        readonly double cell=48,icon=26;
        internal int IconCount {get;private set;}
        internal bool Watching {get{return timer.IsEnabled;}}
        internal TrayOverflowWindow(TrayIconSession session,Action<TrayIconItem,bool> action)
        {
            this.session=session;invoke=action;Title="One TaskBar · 시스템 트레이";DockPalette=true;Dark=Glass95.DockDark;
            Radius=28;ShowActivated=false;Surface.CornerRadius=new CornerRadius(28);
            SetMaterial(QuickPanel.Glass,PanelMaterial.Solid(Glass95.DockDark));Surface.Child=icons;
            int count=session.Items.Count,columns=Math.Min(5,Math.Max(1,(int)Math.Ceiling(Math.Sqrt(count))));
            cell=48*TrayPanelSizing.Scale;icon=26*TrayPanelSizing.Scale;
            icons.Margin=new Thickness(12*TrayPanelSizing.Scale);
            for(int i=0;i<columns;i++)icons.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(cell)});
            for(int i=0;i<(count+columns-1)/columns;i++)icons.RowDefinitions.Add(new RowDefinition {Height=new GridLength(cell)});
            for(int i=0;i<count;i++){
                var item=session.Items[i];UIElement image=item.Icon==null?(UIElement)new Glyph(item.Fallback,23*TrayPanelSizing.Scale) {Color=Paint.Text}:new TrayIconVisual(item.Icon,Paint.Text,icon);
                var button=Paint.Button(image,()=>ActivateItem(item,false),item.Name);button.Margin=new Thickness(2);button.Padding=new Thickness(8*TrayPanelSizing.Scale);button.Background=Brushes.Transparent;button.Resources["ButtonRadius"]=new CornerRadius(18*TrayPanelSizing.Scale);
                System.Windows.Automation.AutomationProperties.SetName(button,item.Name);
                buttons.Add(item,button);
                button.PreviewMouseRightButtonUp+=(s,e)=>{e.Handled=true;ActivateItem(item,true);};
                Grid.SetColumn(button,i%columns);Grid.SetRow(button,i/columns);icons.Children.Add(button);IconCount++;
            }
            SourceInitialized+=(s,e)=>HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook((IntPtr h,int message,IntPtr wp,IntPtr lp,ref bool handled)=>{if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});
            timer.Tick+=(s,e)=>RefreshIcons();
            IsVisibleChanged+=(s,e)=>{if(!IsVisible&&!closing){closing=true;Close();}};
            Closing+=(s,e)=>{closing=true;timer.Stop();if(outside!=null){outside.Dispose();outside=null;}};
            Closed+=(s,e)=>{closed=true;session.Dispose();};
        }
        internal void Present(TrayPlacement placement,bool watch=true)
        {
            double width=icons.ColumnDefinitions.Count*cell+26*TrayPanelSizing.Scale,height=icons.RowDefinitions.Count*cell+26*TrayPanelSizing.Scale;
            var point=placement.Target((int)Math.Round(width*placement.Scale),(int)Math.Round(height*placement.Scale));
            ShowAt(placement.Monitor,(point.X-placement.Monitor.Left)/placement.Scale,(point.Y-placement.Monitor.Top)/placement.Scale,width,height);Pop();
            if(watch){timer.Start();outside=MenuDismiss.Watch(Contains,Dismiss);}
        }
        bool Contains(Native.PointI point){Native.RectI bounds;return Native.GetWindowRect(new WindowInteropHelper(this).Handle,out bounds)&&point.X>=bounds.Left&&point.X<bounds.Right&&point.Y>=bounds.Top&&point.Y<bounds.Bottom;}
        async void RefreshIcons()
        {
            if(closing||closed||refreshing)return;
            if(!session.SourceVisible){Dismiss();return;}
            refreshing=true;
            try
            {
                var updates=await Task.Run(()=>session.ReadUpdates());
                if(closing||closed)return;
                foreach(var update in updates)
                {
                    Button button;
                    if(!buttons.TryGetValue(update.Item,out button))continue;
                    if(update.Image!=null)
                    {
                        update.Item.Icon=update.Image;
                        button.Content=new TrayIconVisual(update.Image,Paint.Text,icon);
                    }
                    update.Item.Name=update.Name;
                    button.ToolTip=update.Name;
                    System.Windows.Automation.AutomationProperties.SetName(button,update.Name);
                }
            }
            catch(Exception error){AppLog.Once("tray-refresh",error.ToString());}
            finally{refreshing=false;}
        }
        async void ActivateItem(TrayIconItem item,bool context)
        {
            if(closing)return;closing=true;timer.Stop();HideImmediately();
            try{await Task.Run(()=>invoke(item,context));}
            catch(Exception error){AppLog.Once("tray-command",error.GetType().Name);}
            finally{if(!closed)Close();}
        }
        internal async void Dismiss(){if(closing)return;closing=true;timer.Stop();await DismissAsync();if(!closed)Close();}
    }
}
