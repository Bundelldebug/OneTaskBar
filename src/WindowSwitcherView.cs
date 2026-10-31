using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
namespace OneTaskBar
{
    // DWM previews use ordinary non-layered owned HWNDs; the surrounding glass remains WPF.
    internal sealed class SwitchThumbnail : Window
    {
        [StructLayout(LayoutKind.Sequential)]struct SizeI {internal int Width,Height;}
        [StructLayout(LayoutKind.Sequential)]struct Properties {internal uint Flags;internal Native.RectI Destination,Source;internal byte Opacity;[MarshalAs(UnmanagedType.Bool)]internal bool Visible;[MarshalAs(UnmanagedType.Bool)]internal bool ClientOnly;}
        [DllImport("dwmapi.dll")]static extern int DwmRegisterThumbnail(IntPtr target,IntPtr source,out IntPtr thumbnail);
        [DllImport("dwmapi.dll")]static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
        [DllImport("dwmapi.dll")]static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail,out SizeI size);
        [DllImport("dwmapi.dll")]static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref Properties properties);
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        readonly IntPtr source,owner;IntPtr thumbnail;bool closed;byte previewOpacity=255;
        int lastX=Int32.MinValue,lastY,lastWidth,lastHeight;
        internal int PositionChanges {get;private set;}
        internal bool ClearBackground {get;private set;}
        internal bool Registered {get{return thumbnail!=IntPtr.Zero;}}
        internal bool MotionPreview;
        internal void SetPreviewOpacity(byte value)
        {
            if(previewOpacity==value)return;previewOpacity=value;
            if(thumbnail==IntPtr.Zero)return;
            var properties=new Properties {Flags=4,Opacity=value};DwmUpdateThumbnailProperties(thumbnail,ref properties);
        }
        internal SwitchThumbnail(SwitchItem item,IntPtr parent,Action hover,Action click,Action<int> navigate)
        {
            source=item.Window;owner=parent;Title="One TaskBar · 전환 미리보기";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;AllowsTransparency=false;Background=Brushes.Transparent;Content=AppIcons.Visual(item.App,38);Cursor=Cursors.Hand;
            SourceInitialized+=(s,e)=>ClearBackground=Native.ClearPreviewBackground(this);
            SourceInitialized+=(s,e)=>{var handle=new WindowInteropHelper(this).Handle;new WindowInteropHelper(this).Owner=owner;Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x80|0x08000000)&~0x40000);Native.SetWindowLong(handle,-16,Native.GetWindowLong(handle,-16)|0x40000);int corner=2;DwmSetWindowAttribute(handle,33,ref corner,4);HwndSource.FromHwnd(handle).AddHook((IntPtr window,int message,IntPtr wp,IntPtr lp,ref bool handled)=>{if(message==0x83){handled=true;return IntPtr.Zero;}if(message==0x21){handled=true;return new IntPtr(3);}if(message==0x84){handled=true;return new IntPtr(1);}return IntPtr.Zero;});};
            MouseMove+=(s,e)=>hover();MouseLeftButtonUp+=(s,e)=>{e.Handled=true;click();};MouseWheel+=(s,e)=>{navigate(e.Delta>0?0x25:0x27);e.Handled=true;};Closed+=(s,e)=>{closed=true;Release();};
        }
        internal void Place(FrameworkElement anchor)
        {
            if(closed||anchor.ActualWidth<1||anchor.ActualHeight<1)return;
            var point=anchor.PointToScreen(new Point());var dpi=VisualTreeHelper.GetDpi(anchor).DpiScaleX;
            int x=(int)Math.Round(point.X),y=(int)Math.Round(point.Y),width=Math.Max(1,(int)Math.Round(anchor.ActualWidth*dpi)),height=Math.Max(1,(int)Math.Round(anchor.ActualHeight*dpi));
            PlacePixels(x,y,width,height);
        }
        internal void PlacePixels(int x,int y,int width,int height)
        {
            if(closed||width<1||height<1)return;
            if(IsVisible&&thumbnail!=IntPtr.Zero&&x==lastX&&y==lastY&&width==lastWidth&&height==lastHeight)return;
            bool resized=width!=lastWidth||height!=lastHeight;
            var handle=new WindowInteropHelper(this).EnsureHandle();
            if(MotionPreview){int disabled=1;DwmSetWindowAttribute(handle,3,ref disabled,4);}
            Native.SetWindowPos(handle,new IntPtr(-1),x,y,width,height,0x10);
            if(!IsVisible){Show();if(closed)return;Native.SetWindowPos(handle,new IntPtr(-1),x,y,width,height,0x10);}
            PositionChanges++;lastX=x;lastY=y;lastWidth=width;lastHeight=height;
            if(thumbnail==IntPtr.Zero&&DwmRegisterThumbnail(handle,source,out thumbnail)==0){Content=null;resized=true;}
            if(resized)Update(width,height);
        }
        void Update(int width,int height)
        {
            if(thumbnail==IntPtr.Zero)return;SizeI size;if(DwmQueryThumbnailSourceSize(thumbnail,out size)!=0||size.Width<=0||size.Height<=0)return;double scale=Math.Min(width/(double)size.Width,height/(double)size.Height),w=size.Width*scale,h=size.Height*scale;var properties=new Properties {Flags=1|4|8|16,Destination=new Native.RectI {Left=(int)Math.Round((width-w)/2),Top=(int)Math.Round((height-h)/2),Right=(int)Math.Round((width+w)/2),Bottom=(int)Math.Round((height+h)/2)},Opacity=previewOpacity,Visible=true,ClientOnly=!MotionPreview};DwmUpdateThumbnailProperties(thumbnail,ref properties);
        }
        void Release(){if(thumbnail!=IntPtr.Zero){DwmUnregisterThumbnail(thumbnail);thumbnail=IntPtr.Zero;}}
    }
    internal sealed class WindowSwitcherView : GlassWindow
    {
        sealed class Tile {internal int Index;internal IntPtr Window;internal Border Card,Image;internal StackPanel Labels;internal SwitchThumbnail Preview;}
        readonly List<Tile> tiles=new List<Tile>();readonly StackPanel row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(16,12,16,12)};readonly Action<int,bool> choose;readonly Action<int> move;SwitchSession session;SwitchLayout layout;int start=-1;bool closed;
        internal int RegisteredThumbnails {get{return tiles.Count(t=>t.Preview.Registered);}}
        internal int VisibleCards {get{return tiles.Count;}}
        internal int PageStart {get{return start;}}
        internal int Presentations {get;private set;}
        internal bool IsClosed {get{return closed;}}
        internal IEnumerable<Rect> PreviewBounds {get{return tiles.Select(t=>new Rect(t.Image.TranslatePoint(new Point(),this),t.Image.RenderSize));}}
        internal WindowSwitcherView(Action<int,bool> select,Action<int> navigate)
        {
            choose=select;move=navigate;Title="One TaskBar · 창 전환";ShowActivated=false;Radius=28;Dark=Glass95.Dark;Surface.CornerRadius=new CornerRadius(28);Surface.BorderThickness=new Thickness(.7);SetMaterial(QuickPanel.Glass,PanelMaterial.Solid(Dark));Surface.Child=row;
            SourceInitialized+=(s,e)=>{var handle=new WindowInteropHelper(this).Handle;Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x80|0x08000000)&~0x40000);HwndSource.FromHwnd(handle).AddHook((IntPtr window,int message,IntPtr wp,IntPtr lp,ref bool handled)=>{if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});};
            SizeChanged+=(s,e)=>PlacePreviews();LocationChanged+=(s,e)=>PlacePreviews();MouseWheel+=(s,e)=>{if(session==null)return;navigate(e.Delta>0?0x25:0x27);e.Handled=true;};Closed+=(s,e)=>{closed=true;ReleasePreviews();};
        }
        void ReleasePreviews(){foreach(var tile in tiles)tile.Preview.Close();tiles.Clear();row.Children.Clear();}
        internal void Present(SwitchSession value,SwitchLayout fit,System.Drawing.Rectangle screen)
        {
            Presentations++;session=value;bool reposition=!IsVisible||layout==null||layout.X!=fit.X||layout.Y!=fit.Y||layout.Width!=fit.Width||layout.Height!=fit.Height;int next=Math.Max(0,Math.Min(session.Selected-fit.Count/2,session.Items.Count-fit.Count));bool rebuild=layout==null||Math.Abs(layout.CardWidth-fit.CardWidth)>.1||Math.Abs(layout.PreviewHeight-fit.PreviewHeight)>.1||next!=start||tiles.Count!=Math.Min(fit.Count,session.Items.Count)||tiles.Any(t=>t.Index>=session.Items.Count||session.Items[t.Index].Window!=t.Window);layout=fit;
            if(rebuild){start=next;ReleasePreviews();var owner=new WindowInteropHelper(this).EnsureHandle();foreach(int index in Enumerable.Range(start,Math.Min(fit.Count,session.Items.Count-start))){int id=index;var item=session.Items[id];var body=new Grid();body.RowDefinitions.Add(new RowDefinition {Height=new GridLength(28)});body.RowDefinitions.Add(new RowDefinition {Height=new GridLength(fit.PreviewHeight)});body.RowDefinitions.Add(new RowDefinition {Height=new GridLength(32)});
                    var badge=AppIcons.Visual(item.App,26);badge.HorizontalAlignment=HorizontalAlignment.Center;badge.VerticalAlignment=VerticalAlignment.Top;body.Children.Add(badge);var image=new Border {Background=Paint.Brush("#32474F4F"),CornerRadius=new CornerRadius(9)};Grid.SetRow(image,1);body.Children.Add(image);var labels=new StackPanel {Margin=new Thickness(2,5,2,0),IsHitTestVisible=false};var name=Paint.TextBlock(item.App.Name,12,Paint.ControlWhite);name.FontWeight=FontWeights.SemiBold;name.TextAlignment=TextAlignment.Center;name.TextTrimming=TextTrimming.CharacterEllipsis;var title=Paint.TextBlock(item.Title,9,Paint.Brush("#E8EDF0EF"));title.TextAlignment=TextAlignment.Center;title.TextTrimming=TextTrimming.CharacterEllipsis;labels.Children.Add(name);labels.Children.Add(title);Grid.SetRow(labels,2);body.Children.Add(labels);
                    var card=new Border {Width=fit.CardWidth,Height=fit.PreviewHeight+76,CornerRadius=new CornerRadius(21),Padding=new Thickness(8),Child=body,Margin=new Thickness(0,0,id==start+Math.Min(fit.Count,session.Items.Count-start)-1?0:10,0),Background=Brushes.Transparent};card.ToolTip=item.App.Name+" · "+item.Title;card.MouseMove+=(s,e)=>choose(id,false);card.MouseLeftButtonUp+=(s,e)=>{e.Handled=true;choose(id,true);};row.Children.Add(card);tiles.Add(new Tile {Index=id,Window=item.Window,Card=card,Image=image,Labels=labels,Preview=new SwitchThumbnail(item,owner,()=>choose(id,false),()=>choose(id,true),move)});
                }}
            foreach(var tile in tiles){bool selected=tile.Index==session.Selected;Motion.Background(tile.Card,selected?"#8A59625F":"#00FFFFFF");tile.Labels.Visibility=selected?Visibility.Visible:Visibility.Hidden;}System.Windows.Automation.AutomationProperties.SetName(this,"창 전환 · "+session.Current.App.Name+" · "+session.Current.Title);
            if(reposition)ShowAt(screen,fit.X,fit.Y,fit.Width,fit.Height);
            if(rebuild||reposition){UpdateLayout();PlacePreviews();}
        }
        void PlacePreviews(){if(closed||!IsVisible)return;foreach(var tile in tiles)tile.Preview.Place(tile.Image);}
    }
}
