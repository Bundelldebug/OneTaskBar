using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // The on/off switch of the Dex pages.
    internal sealed class DexSwitch : Border
    {
        static readonly Brush onTrack=Paint.Brush("#FF3E7BEA"),offTrack=Paint.Brush("#50FFFFFF");
        readonly Border knob=new Border {Width=16,Height=16,CornerRadius=new CornerRadius(8),Background=Paint.ControlWhite,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
        readonly TranslateTransform slide=new TranslateTransform();
        bool value,known;
        internal DexSwitch(Action press)
        {
            Width=38;Height=22;CornerRadius=new CornerRadius(11);Background=offTrack;Cursor=Cursors.Hand;
            knob.Margin=new Thickness(3,0,0,0);knob.RenderTransform=slide;Child=knob;
            MouseLeftButtonUp+=(s,e)=>{e.Handled=true;press();};
        }
        internal void Set(bool on)
        {
            if(known&&value==on)return;
            bool animate=known&&Motion.Enabled;known=true;value=on;Background=on?onTrack:offTrack;
            slide.BeginAnimation(TranslateTransform.XProperty,animate?Motion.Tween(slide.X,on?16:0,170):null);
            if(!animate)slide.X=on?16:0;
        }
    }

    // The white label that appears under a status capsule while the pointer rests on it.
    internal sealed class DexHint : Window
    {
        readonly TextBlock words=Paint.TextBlock("",12,Paint.Brush("#FF20262C"));
        readonly Border pill=new Border {CornerRadius=new CornerRadius(14),Background=Paint.Brush("#FAFFFFFF"),BorderBrush=Paint.Brush("#18000000"),BorderThickness=new Thickness(.7),Padding=new Thickness(13,5,13,5),Margin=new Thickness(8,2,8,10),Effect=Dex.SoftShadow};
        internal DexHint()
        {
            WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;SizeToContent=SizeToContent.WidthAndHeight;Title="One TaskBar · 안내";
            words.FontWeight=FontWeights.Medium;pill.Child=words;Content=pill;
            var handle=new WindowInteropHelper(this).EnsureHandle();
            Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x80|0x08000000|0x20);
        }
        internal void Present(string text,Native.RectI anchor)
        {
            bool dark=Glass95.Dark;
            pill.Background=Paint.Brush(dark?"#F2242A32":"#FAFFFFFF");
            pill.BorderBrush=Paint.Brush(dark?"#26FFFFFF":"#18000000");
            words.Foreground=Paint.Brush(dark?"#FFF4F6F8":"#FF20262C");
            words.Text=text;
            var handle=new WindowInteropHelper(this).Handle;
            bool fresh=!IsVisible;
            if(fresh){Opacity=0;Show();}
            UpdateLayout();
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            int width=(int)Math.Ceiling(ActualWidth*scale),height=(int)Math.Ceiling(ActualHeight*scale);
            int x=(anchor.Left+anchor.Right-width)/2,y=anchor.Bottom+(int)Math.Round(3*scale);
            var work=Forms.Screen.FromHandle(handle).Bounds;
            x=Math.Max(work.Left+4,Math.Min(work.Right-width-4,x));
            Native.SetWindowPos(handle,new IntPtr(-1),x,y,width,height,0x10);
            if(fresh)BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(90)));
        }
        internal void Dismiss()
        {
            BeginAnimation(OpacityProperty,null);
            if(IsVisible)Hide();
        }
    }

    // The layouts that open when the pointer rests on a window's maximize key.
    internal sealed class DexSnapMenu : Window
    {
        static readonly Brush lightInk=Paint.Brush("#FF2B3238"),lightFaint=Paint.Brush("#482B3238"),lightHover=Paint.Brush("#12000000");
        static readonly Brush darkInk=Paint.Brush("#FFF2F5F8"),darkFaint=Paint.Brush("#48F2F5F8"),darkHover=Paint.Brush("#1CFFFFFF");
        static readonly Rect[] Places=
        {
            new Rect(0,0,.5,.5),new Rect(0,0,1,.5),new Rect(.5,0,.5,.5),
            new Rect(0,0,.5,1),new Rect(.16,.1,.68,.8),new Rect(.5,0,.5,1),
            new Rect(0,.5,.5,.5),new Rect(0,.5,1,.5),new Rect(.5,.5,.5,.5)
        };
        readonly DexWindowKeys keys;readonly IntPtr target;readonly DispatcherTimer watch=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(120)};
        readonly FrameworkElement anchor;readonly bool dark=Glass95.Dark;int away;
        Brush Ink {get{return dark?darkInk:lightInk;}}
        Brush Faint {get{return dark?darkFaint:lightFaint;}}
        Brush HoverFill {get{return dark?darkHover:lightHover;}}
        internal DexSnapMenu(DexWindowKeys owner,IntPtr window,FrameworkElement key)
        {
            keys=owner;target=window;anchor=key;
            WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;SizeToContent=SizeToContent.WidthAndHeight;Title="One TaskBar · 창 배치";
            var grid=new UniformGrid {Columns=3,Rows=4};
            foreach(var place in Places)
            {
                var part=place;
                grid.Children.Add(Cell(new[]{part},true,()=>Choose(new[]{part})));
            }
            AddSplit(grid,new[]{new Rect(0,0,.5,1),new Rect(.5,0,.5,1)});
            AddSplit(grid,new[]{new Rect(0,0,1/3.0,1),new Rect(1/3.0,0,1/3.0,1),new Rect(2/3.0,0,1/3.0,1)});
            AddSplit(grid,new[]{new Rect(0,0,.5,.5),new Rect(.5,0,.5,.5),new Rect(0,.5,.5,.5),new Rect(.5,.5,.5,.5)});
            var stack=new StackPanel();stack.Children.Add(grid);
            var dotted=new System.Windows.Shapes.Line {X1=0,X2=164,Y1=.5,Y2=.5,Stroke=dark?Paint.Brush("#26FFFFFF"):Paint.Brush("#1E000000"),StrokeThickness=1,StrokeDashArray=new DoubleCollection {2,3},HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,6,0,6)};
            stack.Children.Add(dotted);
            var row=new StackPanel {Orientation=Orientation.Horizontal};
            row.Children.Add(new Glyph("dex-desktop",15){Color=Ink,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center});
            row.Children.Add(Paint.TextBlock("다른 디스플레이로 보내기",12,Ink));
            var send=Paint.Button(row,SendAway,"다른 디스플레이로 보내기");
            send.Background=Brushes.Transparent;send.Padding=new Thickness(10,7,10,7);send.HorizontalContentAlignment=HorizontalAlignment.Left;send.Resources["ButtonRadius"]=new CornerRadius(10);
            bool several=Forms.Screen.AllScreens.Length>1;send.IsEnabled=several;send.Opacity=several?1:.4;
            stack.Children.Add(send);
            Content=new Border {CornerRadius=new CornerRadius(20),Background=Paint.Brush(dark?"#F4222830":"#FAFFFFFF"),BorderBrush=Paint.Brush(dark?"#28FFFFFF":"#16000000"),BorderThickness=new Thickness(.8),Padding=new Thickness(10),Margin=new Thickness(10,4,10,12),Effect=Dex.SoftShadow,Child=stack};
            var handle=new WindowInteropHelper(this).EnsureHandle();
            Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x80|0x08000000);
            HwndSource.FromHwnd(handle).AddHook((IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});
            watch.Tick+=(s,e)=>Watch();
        }
        void AddSplit(UniformGrid grid,Rect[] parts)
        {
            bool enough=keys.WindowsInOrder(parts.Length).Count>=parts.Length;
            grid.Children.Add(Cell(parts,enough,()=>Choose(parts)));
        }
        Button Cell(Rect[] parts,bool enabled,Action press)
        {
            var canvas=new Canvas {Width=24,Height=18};
            canvas.Children.Add(new Border {Width=24,Height=18,CornerRadius=new CornerRadius(4),BorderBrush=enabled?Ink:Faint,BorderThickness=new Thickness(1.2)});
            foreach(var part in parts)
            {
                var fill=new Border {Width=Math.Max(2,part.Width*20-1),Height=Math.Max(2,part.Height*14-1),CornerRadius=new CornerRadius(1.5),Background=enabled?Ink:Faint};
                Canvas.SetLeft(fill,2+part.X*20+.5);Canvas.SetTop(fill,2+part.Y*14+.5);
                canvas.Children.Add(fill);
            }
            var button=Paint.Button(canvas,press,"창 배치");
            button.Width=54;button.Height=38;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;button.IsEnabled=enabled;button.Resources["ButtonRadius"]=new CornerRadius(10);
            if(enabled)
            {
                button.MouseEnter+=(s,e)=>button.Background=HoverFill;
                button.MouseLeave+=(s,e)=>button.Background=Brushes.Transparent;
            }
            return button;
        }
        void Choose(Rect[] parts)
        {
            var windows=keys.WindowsInOrder(parts.Length,target);
            var screen=Forms.Screen.FromHandle(target);
            for(int i=0;i<parts.Length&&i<windows.Count;i++)DexWindowKeys.PlaceWindow(windows[i],parts[i],screen);
            Dismiss();
        }
        void SendAway()
        {
            var screens=Forms.Screen.AllScreens;var here=Forms.Screen.FromHandle(target);
            int index=Array.FindIndex(screens,s=>s.DeviceName==here.DeviceName);
            DexWindowKeys.PlaceWindow(target,new Rect(.1,.08,.8,.84),screens[(index+1)%screens.Length]);
            Dismiss();
        }
        internal void Present()
        {
            var handle=new WindowInteropHelper(this).Handle;
            Opacity=0;Show();UpdateLayout();
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            var at=anchor.PointToScreen(new Point(anchor.ActualWidth/2,anchor.ActualHeight));
            int width=(int)Math.Ceiling(ActualWidth*scale),height=(int)Math.Ceiling(ActualHeight*scale);
            var work=Native.WorkArea(Forms.Screen.FromPoint(new System.Drawing.Point((int)at.X,(int)at.Y)));
            int x=Math.Max(work.Left+6,Math.Min(work.Right-width-6,(int)at.X-width/2)),y=Math.Min(work.Bottom-height-6,(int)at.Y+(int)Math.Round(6*scale));
            Native.SetWindowPos(handle,new IntPtr(-1),x,y,width,height,0x10);
            BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(110)));
            watch.Start();
        }
        void Watch()
        {
            if(!Native.IsWindow(target)||!anchor.IsVisible){Dismiss();return;}
            away=IsMouseOver||anchor.IsMouseOver?0:away+1;
            if(away>=3)Dismiss();
        }
        internal void Dismiss()
        {
            watch.Stop();
            keys.SnapClosed(this);
            Close();
        }
    }
}
