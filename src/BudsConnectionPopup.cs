using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    internal sealed class BudsConnectionPopup : GlassWindow
    {
        readonly TextBlock name=Paint.TextBlock("",15,Paint.Text),detail=Paint.TextBlock("",12,Paint.Muted);
        readonly Image hero=new Image {Stretch=Stretch.Uniform,Margin=new Thickness(22,0,22,12)};
        readonly Button heroHit;
        readonly Image product=new Image {Width=62,Height=62,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,12,0)};readonly ColumnDefinition pictureColumn=new ColumnDefinition();readonly System.Windows.Controls.Primitives.UniformGrid buttons=new System.Windows.Controls.Primitives.UniformGrid();readonly Action<NotificationAction> invoke;
        readonly DispatcherTimer clock=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(16)};readonly Stopwatch watch=new Stopwatch();
        double progress,from,to;TaskCompletionSource<bool> finished;System.Drawing.Rectangle monitor;Rect bounds;
        internal BudsInfo Info;internal DateTime Expires;internal bool ClosedFlag;internal bool Animating {get{return clock.IsEnabled;}}
        internal BudsConnectionPopup(Action open,Action dismiss,Action<NotificationAction> onAction)
        {
            invoke=onAction;
            Title="One TaskBar · 버즈 연결";ShowActivated=false;Focusable=false;Opacity=0;Radius=31;StableSurface=true;Dark=true;Surface.CornerRadius=new CornerRadius(31);Surface.Background=QuickPanel.Glass;Surface.BorderBrush=Paint.Brush("#48FFFFFF");Surface.BorderThickness=new Thickness(.7);
            var content=new Grid();content.ColumnDefinitions.Add(pictureColumn);content.ColumnDefinitions.Add(new ColumnDefinition());content.Children.Add(product);
            var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};name.FontWeight=FontWeights.SemiBold;name.TextTrimming=TextTrimming.CharacterEllipsis;words.Children.Add(name);
            var state=new DockPanel {Margin=new Thickness(0,5,0,0)};var badge=new Border {Width=20,Height=20,CornerRadius=new CornerRadius(10),Background=Paint.Brush("#FFFFBC66"),Margin=new Thickness(0,0,6,0),Child=new Glyph("bluetooth",13){Color=Paint.OnLight}};DockPanel.SetDock(badge,Dock.Left);state.Children.Add(badge);detail.TextTrimming=TextTrimming.CharacterEllipsis;state.Children.Add(detail);words.Children.Add(state);Grid.SetColumn(words,1);content.Children.Add(words);
            var root=new Grid();var stack=new StackPanel();var hit=Paint.Button(content,open,"연결 정보 열기");hit.Background=Brushes.Transparent;hit.HorizontalContentAlignment=HorizontalAlignment.Stretch;hit.Padding=new Thickness(21,14,35,14);stack.Children.Add(hit);heroHit=Paint.Button(hero,open,"버즈 배터리 · 연결 정보 열기");heroHit.Background=Brushes.Transparent;heroHit.Padding=new Thickness(0);stack.Children.Add(heroHit);buttons.Margin=new Thickness(17,0,17,12);stack.Children.Add(buttons);root.Children.Add(stack);
            var close=Paint.Button(new Glyph("close",11){Color=Paint.Muted},dismiss,"연결 팝업 닫기");close.Width=close.Height=24;close.Padding=new Thickness(6);close.Background=Brushes.Transparent;close.HorizontalAlignment=HorizontalAlignment.Right;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new Thickness(0,6,7,0);root.Children.Add(close);Surface.Child=root;TransitionContent=root;
            SourceInitialized+=(s,e)=>{var handle=new WindowInteropHelper(this).Handle;Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x08000000);Native.BlurOpacity(this,0);};clock.Tick+=(s,e)=>Frame();Closed+=(s,e)=>{ClosedFlag=true;clock.Stop();if(finished!=null)finished.TrySetResult(false);};
        }
        internal int ActionCount {get{return buttons.Children.Count;}}internal Button ActionButton(int index){return buttons.Children[index] as Button;}
        internal void Update(BudsInfo info){Info=info;name.Text=info.Name;detail.Text=info.Detail;detail.ToolTip=info.Detail;bool rich=info.RichImage&&info.Image!=null;product.Source=rich?null:info.Image;product.Visibility=product.Source==null?Visibility.Collapsed:Visibility.Visible;pictureColumn.Width=new GridLength(product.Source==null?0:74);hero.Source=rich?info.Image:null;hero.Visibility=heroHit.Visibility=rich?Visibility.Visible:Visibility.Collapsed;buttons.Children.Clear();foreach(var item in info.Actions.Take(5)){var action=item;var label=Paint.TextBlock(action.Label,12,Paint.OnLight);label.TextTrimming=TextTrimming.CharacterEllipsis;label.TextAlignment=TextAlignment.Center;var button=Paint.Button(label,()=>invoke(action),action.Label);button.Background=Paint.ControlWhite;button.Margin=new Thickness(3,2,3,2);button.Padding=new Thickness(7);buttons.Children.Add(button);}buttons.Columns=Math.Max(1,Math.Min(3,buttons.Children.Count));buttons.Height=37*Math.Ceiling(buttons.Children.Count/3.0);buttons.Visibility=buttons.Children.Count==0?Visibility.Collapsed:Visibility.Visible;Expires=DateTime.UtcNow.AddSeconds(7);}
        internal static Rect Anchor(System.Drawing.Rectangle screen,System.Drawing.Rectangle work,double scale,double width,double height){return new Rect((work.Right-screen.Left)/scale-width-12,(work.Bottom-screen.Top)/scale-height-12,width,height);}
        internal void Position()
        {
            var screen=IsVisible?Forms.Screen.FromRectangle(monitor):Forms.Screen.FromHandle(Native.GetForegroundWindow());monitor=screen.Bounds;double scale=ShellController.MonitorScale(screen,this),width=Math.Min(356,screen.WorkingArea.Width/scale-24),height=(product.Source!=null?94:82)+(ActionCount>0?buttons.Height+12:0);if(hero.Source!=null){var bitmap=hero.Source as System.Windows.Media.Imaging.BitmapSource;hero.Height=Math.Min(180,Math.Max(76,(width-44)*(bitmap!=null?(double)bitmap.PixelHeight/bitmap.PixelWidth:.5)));height+=hero.Height+12;}bounds=Anchor(screen.Bounds,screen.WorkingArea,scale,width,height);
            if(!IsVisible){Native.BlurOpacity(this,0);ShowAt(monitor,bounds.X,bounds.Y,bounds.Width,bounds.Height);}Place();
        }
        internal void Appear(){Animate(1);}
        internal Task<bool> Disappear(){return Animate(0);}
        Task<bool> Animate(double value){if(finished!=null)finished.TrySetResult(false);finished=new TaskCompletionSource<bool>();var task=finished.Task;clock.Stop();from=progress;to=value;if(!Motion.Enabled){progress=to;Place();finished.TrySetResult(true);return task;}watch.Restart();clock.Start();return task;}
        void Frame(){double t=Math.Min(1,watch.Elapsed.TotalMilliseconds/(to==1?240:150));progress=from+(to-from)*(1-Math.Pow(1-t,3));Place();if(t>=1){clock.Stop();finished.TrySetResult(true);}}
        void Place(){Opacity=progress;Native.BlurOpacity(this,progress);Native.Place(this,monitor,bounds.X,bounds.Y+(1-progress)*12,bounds.Width,bounds.Height);}
    }
}
