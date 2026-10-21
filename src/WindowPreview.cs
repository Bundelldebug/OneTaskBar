using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    internal sealed class WindowPreview : Window
    {
        [StructLayout(LayoutKind.Sequential)]struct SizeI {public int Width,Height;}
        [StructLayout(LayoutKind.Sequential)]struct ThumbnailProperties {public uint Flags;public Native.RectI Destination,Source;public byte Opacity;[MarshalAs(UnmanagedType.Bool)]public bool Visible;[MarshalAs(UnmanagedType.Bool)]public bool ClientOnly;}
        [DllImport("dwmapi.dll")]static extern int DwmRegisterThumbnail(IntPtr destination,IntPtr source,out IntPtr thumbnail);
        [DllImport("dwmapi.dll")]static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail,out SizeI size);
        [DllImport("dwmapi.dll")]static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref ThumbnailProperties properties);
        [DllImport("dwmapi.dll")]static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int bytes);
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr w,IntPtr l);
        sealed class Preview {internal IntPtr Source,Thumbnail;internal Border Image;internal Button Open;}
        readonly List<Preview> previews=new List<Preview>();readonly List<IntPtr> sources;readonly AppEntry app;readonly StackPanel tiles=new StackPanel {Orientation=Orientation.Horizontal};readonly TextBlock heading=Paint.TextBlock("",13,Paint.Text);readonly Button back,next;int page,pageSize=3;double tileWidth=300;bool closed;
        readonly DispatcherTimer refresh=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(700)};
        internal int RegisteredThumbnails {get{return previews.Count(p=>p.Thumbnail!=IntPtr.Zero);}}
        internal Button FirstPreview {get{return previews.Count>0?previews[0].Open:null;}}
        internal string AppKey {get{return app.Key;}}
        internal Size ThumbnailSize {get{return previews.Count==0?new Size():new Size(previews[0].Image.ActualWidth,previews[0].Image.ActualHeight);}}
        internal WindowPreview(AppEntry entry,IntPtr owner)
        {
            app=entry;sources=entry.Windows.Where(Native.IsWindow).Distinct().ToList();Title="One TaskBar · 창 미리보기";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;AllowsTransparency=false;Background=Paint.Brush("#FF505653");FontFamily=SystemFonts.MessageFontFamily;Height=260;
            var root=new DockPanel {Margin=new Thickness(12,9,12,12)};var titleRow=new Grid {Height=29,Margin=new Thickness(3,0,3,5)};heading.VerticalAlignment=VerticalAlignment.Center;heading.TextTrimming=TextTrimming.CharacterEllipsis;heading.Margin=new Thickness(0,0,80,0);titleRow.Children.Add(heading);var paging=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};back=Paint.Action("‹",()=>{page--;Rebuild();});next=Paint.Action("›",()=>{page++;Rebuild();});foreach(var button in new[]{back,next}){button.Width=30;button.Height=26;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;paging.Children.Add(button);}titleRow.Children.Add(paging);DockPanel.SetDock(titleRow,Dock.Top);root.Children.Add(titleRow);root.Children.Add(tiles);Content=root;
            SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;new WindowInteropHelper(this).Owner=owner;Native.SetWindowLong(h,-20,(Native.GetWindowLong(h,-20)|0x80|0x08000000)&~0x40000);Native.SetWindowLong(h,-16,Native.GetWindowLong(h,-16)|0x40000);int rounded=2;DwmSetWindowAttribute(h,33,ref rounded,4);HwndSource.FromHwnd(h).AddHook((IntPtr window,int message,IntPtr wp,IntPtr lp,ref bool handled)=>{if(message==0x83){handled=true;return IntPtr.Zero;}if(message==0x84){handled=true;return new IntPtr(1);}if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});};
            ContentRendered+=(s,e)=>Attach();SizeChanged+=(s,e)=>UpdateThumbnails();LocationChanged+=(s,e)=>UpdateThumbnails();Closed+=(s,e)=>{closed=true;refresh.Stop();Release();};refresh.Tick+=(s,e)=>{if(sources.Any(h=>!Native.IsWindow(h))){Rebuild();if(sources.Count==0)Close();}};IsVisibleChanged+=(s,e)=>{if(IsVisible)refresh.Start();else refresh.Stop();};Rebuild();
        }
        internal void Fit(double availableWidth){pageSize=Math.Max(1,Math.Min(3,(int)Math.Floor((availableWidth-24)/308)));tileWidth=Math.Max(100,Math.Min(300,availableWidth-32));Rebuild();}
        void Release(){foreach(var preview in previews)if(preview.Thumbnail!=IntPtr.Zero){DwmUnregisterThumbnail(preview.Thumbnail);preview.Thumbnail=IntPtr.Zero;}}
        void Rebuild()
        {
            Release();previews.Clear();tiles.Children.Clear();sources.RemoveAll(h=>!Native.IsWindow(h));int pages=Math.Max(1,(sources.Count+pageSize-1)/pageSize);page=Math.Max(0,Math.Min(page,pages-1));heading.Text=app.Name+" · "+sources.Count+"개 창";back.Visibility=next.Visibility=pages>1?Visibility.Visible:Visibility.Collapsed;back.IsEnabled=page>0;next.IsEnabled=page<pages-1;
            foreach(var source in sources.Skip(page*pageSize).Take(pageSize)){
                var row=new Grid {Width=tileWidth,Margin=new Thickness(4,0,4,0)};row.RowDefinitions.Add(new RowDefinition {Height=new GridLength(26)});row.RowDefinitions.Add(new RowDefinition());var text=new StringBuilder(512);Native.GetWindowText(source,text,text.Capacity);var title=Paint.TextBlock(text.Length>0?text.ToString():app.Name,11,Paint.Text);title.TextTrimming=TextTrimming.CharacterEllipsis;title.Margin=new Thickness(5,3,31,3);row.Children.Add(title);
                var close=Paint.Button(new Glyph("close",12),()=>CloseSource(source),"이 창 닫기");close.Width=close.Height=24;close.Padding=new Thickness(5);close.HorizontalAlignment=HorizontalAlignment.Right;close.Background=Brushes.Transparent;row.Children.Add(close);
                var image=new Border {Background=Paint.Brush("#FF383D3A"),Margin=new Thickness(4),CornerRadius=new CornerRadius(8),Child=app.Icon!=null?AppIcons.Visual(app.Icon,38):(UIElement)new Glyph("window",34)};var button=Paint.Button(image,()=>Open(source),title.Text);button.Padding=new Thickness(0);button.Background=Brushes.Transparent;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.VerticalContentAlignment=VerticalAlignment.Stretch;Grid.SetRow(button,1);row.Children.Add(button);tiles.Children.Add(row);previews.Add(new Preview {Source=source,Image=image,Open=button});
            }
            Width=24+Math.Max(1,previews.Count)*(tileWidth+8);if(IsVisible){UpdateLayout();Attach();}
        }
        void Attach(){if(closed)return;var target=new WindowInteropHelper(this).Handle;if(target==IntPtr.Zero)return;foreach(var preview in previews)if(preview.Thumbnail==IntPtr.Zero&&DwmRegisterThumbnail(target,preview.Source,out preview.Thumbnail)==0)preview.Image.Child=null;UpdateLayout();UpdateThumbnails();}
        void UpdateThumbnails()
        {
            if(closed||!IsVisible)return;var target=new WindowInteropHelper(this).Handle;if(target==IntPtr.Zero)return;double dpi=Math.Max(96,Native.GetDpiForWindow(target))/96.0;
            foreach(var preview in previews){if(preview.Thumbnail==IntPtr.Zero||preview.Image.ActualWidth<1)continue;SizeI size;if(DwmQueryThumbnailSourceSize(preview.Thumbnail,out size)!=0||size.Width<=0||size.Height<=0)continue;var p=preview.Image.TranslatePoint(new Point(),this);double width=preview.Image.ActualWidth,height=preview.Image.ActualHeight,factor=Math.Min(width/size.Width,height/size.Height),w=size.Width*factor,h=size.Height*factor;var bounds=new Native.RectI {Left=(int)Math.Round((p.X+(width-w)/2)*dpi),Top=(int)Math.Round((p.Y+(height-h)/2)*dpi),Right=(int)Math.Round((p.X+(width+w)/2)*dpi),Bottom=(int)Math.Round((p.Y+(height+h)/2)*dpi)};var properties=new ThumbnailProperties {Flags=1|4|8|16,Destination=bounds,Opacity=255,Visible=true,ClientOnly=true};DwmUpdateThumbnailProperties(preview.Thumbnail,ref properties);}
        }
        void Open(IntPtr source){if(Native.IsWindow(source))Native.Activate(source);Close();}
        async void CloseSource(IntPtr source){if(Native.IsWindow(source))PostMessage(source,0x10,IntPtr.Zero,IntPtr.Zero);await System.Threading.Tasks.Task.Delay(170);if(closed)return;Rebuild();if(sources.Count==0)Close();}
        protected override void OnPreviewKeyDown(KeyEventArgs e){if(e.Key==Key.Escape){Close();e.Handled=true;return;}base.OnPreviewKeyDown(e);}
    }
    internal sealed class WindowPreviewManager : IDisposable
    {
        readonly DispatcherTimer show=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(280)},hide=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(180)};FrameworkElement anchor;AppEntry entry;DockHost dock;WindowPreview popup;
        internal WindowPreview Popup {get{return popup;}}
        internal WindowPreviewManager(){show.Tick+=(s,e)=>{show.Stop();Open();};hide.Tick+=(s,e)=>{hide.Stop();if(popup==null||!popup.IsMouseOver)Close();};}
        internal void Enter(AppEntry value,FrameworkElement button,DockHost host)
        {
            hide.Stop();show.Stop();if(DragReorder.Active||value.Windows.Count==0){Close();return;}entry=value;anchor=button;dock=host;if(popup!=null&&popup.IsVisible&&popup.AppKey==value.Key)return;show.Start();
        }
        internal void Leave(){show.Stop();hide.Start();}
        void Open()
        {
            if(anchor==null||!anchor.IsMouseOver||!anchor.IsVisible||DragReorder.Active)return;ClosePopup();popup=new WindowPreview(entry,new WindowInteropHelper(dock.Apps).Handle);popup.MouseEnter+=(s,e)=>hide.Stop();popup.MouseLeave+=(s,e)=>Leave();var physical=anchor.PointToScreen(new Point(anchor.ActualWidth/2,0));var screen=Forms.Screen.FromPoint(new System.Drawing.Point((int)physical.X,(int)physical.Y));var handle=new WindowInteropHelper(popup).EnsureHandle();Native.SetWindowPos(handle,IntPtr.Zero,screen.Bounds.Left+12,screen.Bounds.Top+12,0,0,0x1|0x4|0x10);double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;popup.Fit((screen.WorkingArea.Width-24)/scale);double x=Math.Max(screen.WorkingArea.Left+12,Math.Min(screen.WorkingArea.Right-popup.Width*scale-12,physical.X-popup.Width*scale/2)),y=Math.Max(screen.WorkingArea.Top+12,physical.Y-popup.Height*scale-12);Native.SetWindowPos(handle,new IntPtr(-1),(int)Math.Round(x),(int)Math.Round(y),(int)Math.Round(popup.Width*scale),(int)Math.Round(popup.Height*scale),0x10);popup.Show();Native.SetWindowPos(handle,new IntPtr(-1),(int)Math.Round(x),(int)Math.Round(y),(int)Math.Round(popup.Width*scale),(int)Math.Round(popup.Height*scale),0x10);
        }
        void ClosePopup(){if(popup!=null){popup.Close();popup=null;}}
        internal void Close(){show.Stop();hide.Stop();ClosePopup();}
        public void Dispose(){Close();}
    }
}
