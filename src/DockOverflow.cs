using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal sealed class DockOverflow : IDisposable
    {
        readonly ShellController shell;readonly DockHost dock;readonly Button anchor;
        readonly DispatcherTimer openDelay=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(220)},closeDelay=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(330)};
        readonly Action changed;List<AppEntry> entries=new List<AppEntry>();GlassWindow popup;IDisposable outside;string signature;bool disposed;
        internal bool IsOpen {get{return popup!=null&&popup.IsVisible;}}
        internal int Count {get{return entries.Count;}}
        internal GlassWindow Popup {get{return popup;}}
        internal DockOverflow(ShellController controller,DockHost owner,Button button,Action visibilityChanged)
        {
            shell=controller;dock=owner;anchor=button;changed=visibilityChanged;
            openDelay.Tick+=(s,e)=>{openDelay.Stop();Open();};closeDelay.Tick+=(s,e)=>{closeDelay.Stop();if(!DragReorder.Active&&!anchor.IsMouseOver&&popup!=null&&!popup.IsMouseOver&&!ContextOpen())Hide();};
            anchor.MouseEnter+=(s,e)=>{closeDelay.Stop();if(!IsOpen)openDelay.Start();};anchor.MouseLeave+=(s,e)=>{openDelay.Stop();ScheduleClose();};
        }
        bool ContextOpen(){return popup!=null&&Menus(popup.Surface).Any(m=>m.IsOpen);}
        static IEnumerable<ContextMenu> Menus(DependencyObject root){var button=root as Button;if(button!=null&&button.ContextMenu!=null)yield return button.ContextMenu;foreach(object child in LogicalTreeHelper.GetChildren(root)){var element=child as DependencyObject;if(element!=null)foreach(var menu in Menus(element))yield return menu;}}
        internal void Update(IEnumerable<AppEntry> hidden)
        {
            var next=hidden.ToList();string key=String.Join("|",next.Select(a=>a.Key+":"+String.Join(",",a.Windows)));entries=next;if(key==signature)return;signature=key;if(IsOpen){if(entries.Count==0)Hide();else Fill();}
        }
        internal void Toggle(){if(IsOpen)Hide();else Open();}
        internal void Open()
        {
            openDelay.Stop();closeDelay.Stop();if(disposed||entries.Count==0||!anchor.IsVisible||!dock.DockVisible)return;shell.Previews.Close();if(IsOpen)return;
            popup=new GlassWindow {Title="One TaskBar · 숨겨진 앱",ShowActivated=false,Dark=Glass95.DockDark,DockSurface=true,Radius=24};popup.Surface.CornerRadius=new CornerRadius(24);popup.SetMaterial(LightPanel.DockGlass,LightPanel.DockSolid);popup.Surface.BorderThickness=new Thickness(0);popup.MouseEnter+=(s,e)=>closeDelay.Stop();popup.MouseLeave+=(s,e)=>ScheduleClose();popup.PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape){e.Handled=true;Hide();}};Native.PrepareMonitor(popup,dock.Screen.Bounds);Fill();Glass95.DockInk(popup.Surface);
            var screen=dock.Screen.Bounds;double scale=dock.Scale;var physical=DockGeometry.ScreenRect(anchor);var reference=new Rect((physical.X-screen.Left)/scale,(physical.Y-screen.Top)/scale,physical.Width/scale,physical.Height/scale);var place=DockGeometry.Beside(reference,new Size(popup.Width,popup.Height),new Rect(0,0,screen.Width/scale,screen.Height/scale),dock.Edge);popup.ShowAt(screen,place.X,place.Y,popup.Width,popup.Height);popup.Pop();outside=MenuDismiss.Watch(Contains,Hide);if(changed!=null)changed();
        }
        void Fill()
        {
            if(popup==null)return;int columns=Math.Max(1,Math.Min(Math.Min(8,entries.Count),(int)((dock.Screen.Bounds.Width/dock.Scale-48)/50)));double width=columns*50+24,height=Math.Min(Math.Min(276,Math.Max(60,dock.Screen.Bounds.Height/dock.Scale-110)),Math.Ceiling(entries.Count/(double)columns)*52+24);var grid=new UniformGrid {Columns=columns,Margin=new Thickness(12)};
            foreach(var entry in entries){var item=entry;var content=new Grid {Width=42,Height=44};var icon=AppIcons.Visual(item,32);icon.VerticalAlignment=VerticalAlignment.Top;icon.Margin=new Thickness(0,2,0,0);content.Children.Add(icon);if(item.Windows.Count>0)content.Children.Add(new Border {Width=4,Height=4,Background=Paint.Ink,CornerRadius=new CornerRadius(2),VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center});Button button=null;button=Paint.Button(content,()=>{var modifiers=System.Windows.Input.Keyboard.Modifiers;if(item.Windows.Count>1&&!AppClick.Elevate(modifiers))shell.ActivateApp(item,button);else{Hide();shell.LaunchApp(item,modifiers);}},item.Name);button.Width=50;button.Height=52;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;dock.DragApps.Attach(button,()=>item);button.ContextMenu=WindowPicker.Menu(item,shell);button.ContextMenuOpening+=(s,e)=>closeDelay.Stop();button.ContextMenuClosing+=(s,e)=>ScheduleClose();grid.Children.Add(button);}
            popup.Width=width;popup.Height=height;popup.Surface.Child=new SmoothScrollViewer {Content=grid,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};popup.TransitionContent=(FrameworkElement)popup.Surface.Child;
        }
        bool Contains(Native.PointI point)
        {
            if(ContextOpen())return true;if(popup!=null&&popup.IsVisible){Native.RectI bounds;if(Native.GetWindowRect(new WindowInteropHelper(popup).Handle,out bounds)&&point.X>=bounds.Left&&point.X<bounds.Right&&point.Y>=bounds.Top&&point.Y<bounds.Bottom)return true;}if(anchor.IsVisible){Point pointA=anchor.PointToScreen(new Point()),pointB=anchor.PointToScreen(new Point(anchor.ActualWidth,anchor.ActualHeight));return point.X>=pointA.X&&point.X<pointB.X&&point.Y>=pointA.Y&&point.Y<pointB.Y;}return false;
        }
        void ScheduleClose(){if(IsOpen&&!DragReorder.Active&&!ContextOpen())closeDelay.Start();}
        internal void AfterDrag(){ScheduleClose();}
        internal async void Hide()
        {
            openDelay.Stop();closeDelay.Stop();if(outside!=null){outside.Dispose();outside=null;}var old=popup;popup=null;if(changed!=null)changed();if(old==null)return;await old.DismissAsync();old.Close();
        }
        public void Dispose(){disposed=true;openDelay.Stop();closeDelay.Stop();if(outside!=null)outside.Dispose();outside=null;if(popup!=null){popup.Close();popup=null;}entries.Clear();}
    }
}
