using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    internal sealed class DesktopOverview : GlassWindow
    {
        sealed class Preview {internal Border Anchor,Card;internal SwitchThumbnail Window;}
        readonly Grid body=new Grid {Margin=new Thickness(28,18,28,24)};
        readonly StackPanel desktops=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        readonly Grid workspace=new Grid();
        readonly Canvas cards=new Canvas();
        readonly TextBlock empty=Paint.TextBlock("열려 있는 창이 없습니다",16,Paint.Text);
        readonly TextBlock caption=Paint.TextBlock("데스크톱",13,Paint.Text);
        readonly List<Preview> previews=new List<Preview>();
        readonly Action<Guid,IntPtr,int> action;
        readonly Action<IntPtr> closeWindow;
        DesktopSnapshot snapshot;
        int selected,pageStart;
        bool closing,closed,leaving;
        readonly Stopwatch transition=new Stopwatch();
        internal int PageIndex {get{return selected;}}
        internal int PreviewCount {get{return previews.Count;}}
        internal int RegisteredThumbnails {get{return previews.Count(p=>p.Window.Registered);}}
        internal bool EmptyVisible {get{return empty.Visibility==Visibility.Visible;}}
        internal bool TransitionActive {get{return transition.IsRunning;}}
        internal bool Retiring {get{return closing||closed;}}

        internal DesktopOverview(Action<Guid,IntPtr,int> execute,Action<IntPtr> close)
        {
            action=execute;closeWindow=close;Title="One TaskBar · 데스크톱 전환";
            Radius=0;Surface.CornerRadius=new CornerRadius(0);SetMaterial(QuickPanel.Glass,PanelMaterial.Solid(Glass95.Dark));
            body.RowDefinitions.Add(new RowDefinition {Height=new GridLength(104)});
            body.RowDefinitions.Add(new RowDefinition());
            body.RowDefinitions.Add(new RowDefinition {Height=new GridLength(48)});
            var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(64)});header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(64)});
            var back=Paint.Button(new Glyph("previous",18),()=>SelectPage(selected-1),"이전 데스크톱");back.Width=back.Height=40;header.Children.Add(back);
            var strip=new ScrollViewer {Content=desktops,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetColumn(strip,1);header.Children.Add(strip);
            var add=Paint.Button(Paint.TextBlock("+",25,Paint.Text),()=>Execute(1),"새 데스크톱");add.Width=add.Height=40;Grid.SetColumn(add,2);header.Children.Add(add);body.Children.Add(header);
            var space=new Border {CornerRadius=new CornerRadius(28),Background=QuickPanel.Card,Padding=new Thickness(16),Margin=new Thickness(0,8,0,12),Child=workspace};Grid.SetRow(space,1);body.Children.Add(space);
            workspace.Children.Add(cards);empty.HorizontalAlignment=HorizontalAlignment.Center;empty.VerticalAlignment=VerticalAlignment.Center;workspace.Children.Add(empty);
            caption.VerticalAlignment=VerticalAlignment.Top;caption.HorizontalAlignment=HorizontalAlignment.Left;workspace.Children.Add(caption);
            var pageButtons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top};
            pageButtons.Children.Add(Paint.Button(new Glyph("previous",14),()=>ChangeWindowPage(-1),"이전 창 목록"));pageButtons.Children.Add(Paint.Button(new Glyph("next",14),()=>ChangeWindowPage(1),"다음 창 목록"));workspace.Children.Add(pageButtons);
            var footer=new Grid();Grid.SetRow(footer,2);body.Children.Add(footer);
            var dismiss=Paint.Action("닫기",Dismiss);dismiss.HorizontalAlignment=HorizontalAlignment.Right;footer.Children.Add(dismiss);
            var switchButton=Paint.Action("이 데스크톱으로 전환",()=>Execute(0));switchButton.HorizontalAlignment=HorizontalAlignment.Center;footer.Children.Add(switchButton);
            var remove=Paint.Action("데스크톱 닫기",()=>Execute(2));remove.HorizontalAlignment=HorizontalAlignment.Left;remove.IsEnabled=false;remove.Tag="remove-desktop";footer.Children.Add(remove);
            Surface.Child=body;workspace.SizeChanged+=(s,e)=>ArrangePreviews();
            PreviewKeyDown+=(s,e)=>{
                if(e.Key==Key.Escape){e.Handled=true;Dismiss();}
                else if(e.Key==Key.Left||e.Key==Key.Right){e.Handled=true;SelectPage(selected+(e.Key==Key.Left?-1:1));}
                else if(e.Key==Key.Enter){e.Handled=true;Execute(0);}
            };
            PreviewMouseLeftButtonUp+=(s,e)=>{if(e.OriginalSource==cards||e.OriginalSource==workspace){e.Handled=true;Dismiss();}};
            Closed+=(s,e)=>{closed=true;StopTransition();ReleasePreviews();};
            IsVisibleChanged+=(s,e)=>{if(!IsVisible){StopTransition();ReleasePreviews();}};
        }
        internal void Present(DesktopSnapshot value,System.Drawing.Rectangle monitor)
        {
            empty.Text="열려 있는 창이 없습니다";
            snapshot=value;selected=value.CurrentIndex;pageStart=0;
            double dpi=Native.PrepareMonitor(this,monitor);
            ShowAt(monitor,0,0,monitor.Width/dpi,monitor.Height/dpi);
            foreach(var button in ((Grid)body.Children[2]).Children.OfType<Button>())if((string)button.Tag=="remove-desktop")button.IsEnabled=value.Pages.Count>1;
            RenderDesktops();RenderWindows();Activate();
        }
        internal void ShowLoading(System.Drawing.Rectangle monitor)
        {
            var placeholder=new DesktopSnapshot();placeholder.Pages.Add(new DesktopPage {Name="데스크톱",Id=Guid.Empty});
            Present(placeholder,monitor);empty.Text="데스크톱을 불러오는 중...";
        }
        void RenderDesktops()
        {
            desktops.Children.Clear();
            for(int i=0;i<snapshot.Pages.Count;i++){
                int index=i;var page=snapshot.Pages[i];
                var image=new Grid {Width=122,Height=54,Background=Paint.Brush("#2586979C")};
                int j=0;foreach(var item in page.Windows.Take(3)){
                    var mini=new Border {Width=50,Height=33,CornerRadius=new CornerRadius(3),Background=Paint.Brush("#C5ECF0EF"),Child=AppIcons.Visual(item.App,15),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(8+j*24,6+j*4,0,0)};image.Children.Add(mini);j++;
                }
                var content=new StackPanel();content.Children.Add(image);
                var label=Paint.TextBlock(page.Name,10,Paint.Text);label.TextAlignment=TextAlignment.Center;label.Margin=new Thickness(0,4,0,0);content.Children.Add(label);
                var button=Paint.Button(content,()=>SelectPage(index),page.Name);button.Padding=new Thickness(7);button.Margin=new Thickness(4,0,4,0);button.Background=Paint.Brush(index==selected?"#42FFFFFF":"#12FFFFFF");button.BorderBrush=Paint.Brush(index==selected?"#B0FFFFFF":"#00FFFFFF");button.BorderThickness=new Thickness(1);
                button.MouseDoubleClick+=(s,e)=>{e.Handled=true;SelectPage(index);Execute(0);};desktops.Children.Add(button);
            }
        }
        internal void SelectPage(int index)
        {
            if(closing||snapshot==null||index<0||index>=snapshot.Pages.Count||index==selected)return;
            selected=index;pageStart=0;RenderDesktops();RenderWindows();
        }
        void ReleasePreviews(){foreach(var preview in previews)preview.Window.Close();previews.Clear();cards.Children.Clear();}
        void RenderWindows()
        {
            if(closing||closed||!IsVisible)return;
            StopTransition();ReleasePreviews();
            var page=snapshot.Pages[selected];page.Windows.RemoveAll(w=>!Native.IsWindow(w.Window));
            caption.Text=page.Name+" · "+page.Windows.Count+"개 창";empty.Visibility=page.Windows.Count==0?Visibility.Visible:Visibility.Collapsed;
            var owner=new WindowInteropHelper(this).EnsureHandle();
            foreach(var item in page.Windows.Skip(pageStart).Take(12)){
                var entry=item;var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(30)});layout.RowDefinitions.Add(new RowDefinition());
                var title=new Grid();title.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(24)});title.ColumnDefinitions.Add(new ColumnDefinition());title.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(28)});
                title.Children.Add(AppIcons.Visual(item.App,19));var name=Paint.TextBlock(item.Title,11,Paint.Text);name.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(name,1);title.Children.Add(name);
                var close=Paint.Button(new Glyph("close",12),()=>{closeWindow(entry.Window);page.Windows.Remove(entry);RenderWindows();},"창 닫기");close.Padding=new Thickness(3);Grid.SetColumn(close,2);title.Children.Add(close);layout.Children.Add(title);
                var anchor=new Border {Background=Paint.Brush("#23000000"),CornerRadius=new CornerRadius(12)};Grid.SetRow(anchor,1);layout.Children.Add(anchor);
                var card=new Border {CornerRadius=new CornerRadius(18),Background=QuickPanel.Card,Padding=new Thickness(9),Child=layout};cards.Children.Add(card);
                card.MouseLeftButtonUp+=(s,e)=>{e.Handled=true;Execute(0,entry.Window);};
                var native=new SwitchThumbnail(item,owner,()=>{},()=>Execute(0,entry.Window),key=>ChangeWindowPage(key==0x25?-1:1));
                previews.Add(new Preview {Anchor=anchor,Card=card,Window=native});
            }
            UpdateLayout();ArrangePreviews();
            if(Motion.Enabled&&previews.Count>0){leaving=false;transition.Restart();CompositionTarget.Rendering+=Animate;Animate(null,EventArgs.Empty);}
        }
        void ChangeWindowPage(int direction)
        {
            if(snapshot==null||closing)return;int count=snapshot.Pages[selected].Windows.Count;
            int next=Math.Max(0,Math.Min(Math.Max(0,((count-1)/12)*12),pageStart+direction*12));
            if(next==pageStart)return;pageStart=next;RenderWindows();
        }
        void ArrangePreviews()
        {
            if(closed||!IsVisible||workspace.ActualWidth<50||workspace.ActualHeight<80)return;
            int count=previews.Count;if(count==0)return;
            double width=workspace.ActualWidth,height=workspace.ActualHeight-42;
            int columns=Math.Min(4,Math.Min(count,Math.Max(1,(int)(width/260))));int rows=(count+columns-1)/columns;
            double gap=22,cardWidth=Math.Min(460,(width-(columns-1)*gap)/columns),cardHeight=Math.Min(cardWidth*.64+42,(height-(rows-1)*gap)/rows);
            for(int i=0;i<count;i++){
                var preview=previews[i];double x=(width-(cardWidth*columns+gap*(columns-1)))/2+(i%columns)*(cardWidth+gap),y=30+(height-(cardHeight*rows+gap*(rows-1)))/2+(i/columns)*(cardHeight+gap);
                preview.Card.Width=cardWidth;preview.Card.Height=cardHeight;Canvas.SetLeft(preview.Card,x);Canvas.SetTop(preview.Card,y);
            }
            UpdateLayout();foreach(var preview in previews)preview.Window.Place(preview.Anchor);
        }
        void PlacePreviews(double progress)
        {
            double eased=1-Math.Pow(1-progress,3);
            if(leaving)eased=1-eased;
            foreach(var preview in previews){preview.Card.Opacity=eased;preview.Window.SetPreviewOpacity((byte)Math.Round(eased*255));}
            if(leaving){Surface.Opacity=eased;Native.BlurOpacity(this,eased);}
        }
        void Animate(object sender,EventArgs args){double progress=Math.Min(1,transition.Elapsed.TotalMilliseconds/220);PlacePreviews(progress);if(progress>=1)StopTransition();}
        void StopTransition(){transition.Stop();CompositionTarget.Rendering-=Animate;}
        void Execute(int operation,IntPtr window=default(IntPtr))
        {
            if(closing||snapshot==null)return;var id=snapshot.Pages[selected].Id;
            Dismiss();action(id,window,operation);
        }
        internal async void Dismiss()
        {
            if(closed||closing)return;closing=true;StopTransition();
            if(Motion.Enabled){leaving=true;transition.Restart();CompositionTarget.Rendering+=Animate;await Task.Delay(230);}
            if(!closed)Close();
        }
    }
}
