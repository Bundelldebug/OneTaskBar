using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    // Editing the tiles where they are shown, the way desktop widgets are edited. A right-click or
    // a long press starts it; then a click selects a tile, a drag moves it, and a bar beneath the
    // strip changes its size, order or removes it. A plus tile adds more. No settings page is needed.
    internal sealed partial class FullscreenTileStrip
    {
        const string AddKey="+add";
        const int ActionLimit=2;
        static readonly Brush PanelFill=Paint.Brush("#F2232A31"),PanelEdge=Paint.Brush("#35FFFFFF"),Soft=Paint.Brush("#33FFFFFF"),Chosen=Paint.Brush("#59FFFFFF"),Danger=Paint.Brush("#F0D64545");
        bool editing;
        string selectedKey;
        readonly List<UIElement> overlay=new List<UIElement>();
        Border chooser;
        DispatcherTimer hold;
        Entry pressed;
        Point pressAt;
        bool longPressed,dragging;
        TranslateTransform dragShift;
        // Asked to lay the strip out again, when editing adds or removes the plus tile.
        internal Action Changed;
        internal bool Editing {get{return editing;}}
        internal string SelectedKey {get{return selectedKey;}}
        internal bool ChooserOpen {get{return chooser!=null;}}
        internal int OverlayCount {get{return overlay.Count;}}

        void Hook(Entry entry)
        {
            var host=entry.Host;
            host.PreviewMouseLeftButtonDown+=(s,e)=>Press(entry,e);
            host.PreviewMouseMove+=(s,e)=>Drag(entry,e);
            host.PreviewMouseLeftButtonUp+=(s,e)=>Release(entry,e);
            host.MouseRightButtonUp+=(s,e)=>
            {
                if(entry.Key==AddKey)return;
                BeginEdit(entry.Key);
                e.Handled=true;
            };
        }
        void Press(Entry entry,MouseButtonEventArgs e)
        {
            if(entry.Key==AddKey)return;
            pressed=entry;
            pressAt=e.GetPosition(this);
            longPressed=dragging=false;
            if(editing)
            {
                e.Handled=true;
                Select(entry.Key);
                entry.Host.CaptureMouse();
                return;
            }
            if(hold==null)
            {
                hold=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(600)};
                hold.Tick+=(s,a)=>
                {
                    hold.Stop();
                    if(pressed==null)return;
                    longPressed=true;
                    BeginEdit(pressed.Key);
                };
            }
            hold.Start();
        }
        void CancelHold()
        {
            if(hold!=null)hold.Stop();
        }
        void Drag(Entry entry,MouseEventArgs e)
        {
            if(pressed!=entry)return;
            if(e.LeftButton!=MouseButtonState.Pressed)
            {
                CancelHold();
                pressed=null;
                return;
            }
            if(!editing)
            {
                var from=e.GetPosition(this);
                if(Math.Abs(from.X-pressAt.X)>12||Math.Abs(from.Y-pressAt.Y)>12)CancelHold();
                return;
            }
            var at=e.GetPosition(this);
            double dx=at.X-pressAt.X,dy=at.Y-pressAt.Y;
            if(!dragging)
            {
                if(Math.Abs(dx)<8&&Math.Abs(dy)<8)return;
                dragging=true;
                dragShift=new TranslateTransform();
                entry.Host.RenderTransform=dragShift;
                entry.Host.Opacity=.88;
                Panel.SetZIndex(entry.Host,20);
            }
            dragShift.X=dx;
            dragShift.Y=dy;
        }
        void Release(Entry entry,MouseButtonEventArgs e)
        {
            CancelHold();
            if(pressed!=entry)return;
            var host=entry.Host;
            if(host.IsMouseCaptured)host.ReleaseMouseCapture();
            bool moved=dragging,held=longPressed;
            pressed=null;
            dragging=longPressed=false;
            if(moved)
            {
                double cx=Canvas.GetLeft(host)+host.Width/2+dragShift.X,cy=Canvas.GetTop(host)+host.Height/2+dragShift.Y;
                host.RenderTransform=Transform.Identity;
                host.Opacity=1;
                Panel.SetZIndex(host,0);
                Drop(entry,cx,cy);
                e.Handled=true;
                return;
            }
            // A long press that opened editing must not also click the tile under the finger.
            if(editing||held)e.Handled=true;
        }
        // Dropped tiles go before or after the nearest other tile; shortcut buttons keep their place.
        void Drop(Entry moved,double centerX,double centerY)
        {
            if(moved.Tile==null)return;
            Entry nearest=null;
            double best=Double.MaxValue;
            foreach(var other in entries)
            {
                if(other==moved||other.Tile==null||other.Host.Visibility!=Visibility.Visible)continue;
                double x=Canvas.GetLeft(other.Host)+other.Host.Width/2,y=Canvas.GetTop(other.Host)+other.Host.Height/2;
                double distance=(x-centerX)*(x-centerX)+(y-centerY)*(y-centerY);
                if(distance>=best)continue;
                best=distance;
                nearest=other;
            }
            if(nearest==null)return;
            bool after=centerX>Canvas.GetLeft(nearest.Host)+nearest.Host.Width/2;
            Save(Reordered(Tiles(),moved.Key,nearest.Key,after));
        }

        internal static List<string> Reordered(IList<string> tiles,string key,string target,bool after)
        {
            var list=tiles.ToList();
            if(!list.Remove(key))return list;
            int index=list.IndexOf(target);
            if(index<0)list.Add(key);
            else list.Insert(after?index+1:index,key);
            return list;
        }
        internal static List<string> Moved(IList<string> tiles,string key,int delta)
        {
            var list=tiles.ToList();
            int from=list.IndexOf(key),to=from+delta;
            if(from<0||to<0||to>=list.Count)return list;
            list.RemoveAt(from);
            list.Insert(to,key);
            return list;
        }
        // The shown tiles fill the slots the shown tiles had. Tiles hidden for now, such as weather
        // without a place, stay exactly where they were, and come back in the same spot.
        internal static List<string> Merged(IList<string> saved,IList<string> oldShown,IList<string> shown)
        {
            var result=new List<string>();
            int next=0;
            foreach(string token in saved)
            {
                if(!oldShown.Contains(token))
                {
                    result.Add(token);
                    continue;
                }
                if(next<shown.Count)result.Add(shown[next++]);
            }
            while(next<shown.Count)result.Add(shown[next++]);
            return result;
        }
        static void Save(IList<string> shown)
        {
            var saved=(Preferences.Current.FullscreenTiles??new string[0]).Where(InfoTileCatalog.Known).ToList();
            var all=Merged(saved,Tiles(),shown).ToArray();
            Preferences.SetMediaAppearance(p=>p.FullscreenTiles=all);
        }
        static void SaveActions(IList<string> keys)
        {
            var all=keys.ToArray();
            Preferences.SetMediaAppearance(p=>p.FullscreenActions=all);
        }

        internal void BeginEdit(string key)
        {
            if(editing)
            {
                Select(key);
                return;
            }
            editing=true;
            selectedKey=key;
            Sync();
            if(Changed!=null)Changed();
        }
        internal void EndEdit(bool notify=true)
        {
            if(!editing)return;
            editing=false;
            selectedKey=null;
            CloseChooser();
            ClearOverlay();
            Sync();
            if(notify&&Changed!=null)Changed();
        }
        // Escape closes the chooser first, then editing, and only then the whole screen.
        internal bool ConsumeEscape()
        {
            if(chooser!=null)
            {
                CloseChooser();
                return true;
            }
            if(!editing)return false;
            EndEdit();
            return true;
        }
        internal void Select(string key)
        {
            selectedKey=key;
            CloseChooser();
            PlaceEditing();
        }
        internal void Remove(string key)
        {
            if(entries.Any(e=>e.Key==key&&e.Tile!=null))Save(Tiles().Where(k=>k!=key).ToList());
            else SaveActions(Actions().Where(k=>k!=key).ToList());
            selectedKey=null;
        }
        internal void Resize(string key,string token)
        {
            var list=Tiles().Select(k=>k==key?token:k).ToList();
            selectedKey=token;
            Save(list);
        }
        internal void Move(string key,int delta)
        {
            Save(Moved(Tiles(),key,delta));
        }
        internal void AddTile(InfoTileFamily family)
        {
            string token=family.Key+":"+(family.Spans.Contains("2x2")?"2x2":family.Spans[0]);
            var list=Tiles().ToList();
            list.Add(token);
            selectedKey=token;
            Save(list);
        }
        internal void AddAction(string key)
        {
            var list=Actions().ToList();
            if(list.Count>=ActionLimit||list.Contains(key))return;
            list.Add(key);
            selectedKey=key;
            SaveActions(list);
        }
        Button PlusButton()
        {
            var plus=Paint.TextBlock("+",34,scheme.Ink);
            plus.HorizontalAlignment=HorizontalAlignment.Center;
            var button=Paint.Button(plus,()=>OpenChooser(),"위젯 추가");
            button.Width=button.Height=InfoTile.Cell;
            button.Padding=new Thickness(0);
            button.Background=scheme.Surface;
            button.Resources["ButtonRadius"]=new CornerRadius(InfoTile.Cell/2);
            return button;
        }

        // The strip's own position on the media screen; edit controls live there, unscaled.
        Canvas Stage {get{return VisualTreeHelper.GetParent(this) as Canvas;}}
        Rect BoundsOf(Entry entry)
        {
            double left=GetLeft(this),top=GetTop(this);
            if(Double.IsNaN(left))left=0;
            if(Double.IsNaN(top))top=0;
            return new Rect(left+GetLeft(entry.Host)*Scale,top+GetTop(entry.Host)*Scale,entry.Host.Width*Scale,entry.Host.Height*Scale);
        }
        void ClearOverlay()
        {
            var stage=Stage;
            foreach(var item in overlay)
            {
                if(stage!=null)stage.Children.Remove(item);
            }
            overlay.Clear();
        }
        void CloseChooser()
        {
            if(chooser==null)return;
            var stage=Stage;
            if(stage!=null)stage.Children.Remove(chooser);
            chooser=null;
        }
        void Add(Canvas stage,UIElement item,double left,double top,int layer=10)
        {
            Canvas.SetLeft(item,left);
            Canvas.SetTop(item,top);
            Panel.SetZIndex(item,layer);
            stage.Children.Add(item);
            overlay.Add(item);
        }
        // Rings, remove badges and the bar, drawn after every layout while editing.
        internal void PlaceEditing()
        {
            ClearOverlay();
            var stage=Stage;
            if(!editing||stage==null||Visibility!=Visibility.Visible)return;
            Entry selected=null;
            foreach(var entry in entries)
            {
                if(entry.Host.Visibility!=Visibility.Visible||entry.Key==AddKey)continue;
                var box=BoundsOf(entry);
                bool on=entry.Key==selectedKey;
                if(on)selected=entry;
                double radius=entry.Tile!=null?InfoTile.Radius(entry.Columns,entry.Rows)*Scale:box.Width/2;
                var ring=new Border {Width=box.Width,Height=box.Height,CornerRadius=new CornerRadius(radius),BorderBrush=on?Paint.ControlWhite:Chosen,BorderThickness=new Thickness(on?2.5:1),IsHitTestVisible=false};
                Add(stage,ring,box.X,box.Y,9);
                string key=entry.Key;
                var badge=Paint.Button(new Glyph("close",11){Color=Paint.ControlWhite},()=>Remove(key),"위젯 제거");
                badge.Width=badge.Height=24;
                badge.Padding=new Thickness(0);
                badge.Background=Danger;
                badge.Resources["ButtonRadius"]=new CornerRadius(12);
                Add(stage,badge,box.X-8,box.Y-8,11);
            }
            var bar=Toolbar(selected);
            bar.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
            var area=Rect.Empty;
            foreach(var entry in entries)
            {
                if(entry.Host.Visibility==Visibility.Visible)area=Rect.Union(area,BoundsOf(entry));
            }
            double stageWidth=stage.ActualWidth>0?stage.ActualWidth:area.Right+40;
            double x=area.IsEmpty?0:area.X+(area.Width-bar.DesiredSize.Width)/2;
            Add(stage,bar,Math.Max(12,Math.Min(x,stageWidth-bar.DesiredSize.Width-12)),area.IsEmpty?0:area.Bottom+12,12);
            if(chooser!=null)Panel.SetZIndex(chooser,30);
        }
        Button Chip(string text,Action action,bool on,string hint=null)
        {
            var button=Paint.Button(Paint.TextBlock(text,13,Paint.ControlWhite),action,hint??text);
            button.Background=on?Chosen:Brushes.Transparent;
            button.Padding=new Thickness(12,7,12,7);
            button.Resources["ButtonRadius"]=new CornerRadius(16);
            return button;
        }
        Border Toolbar(Entry selected)
        {
            var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(6)};
            if(selected!=null&&selected.Tile!=null)
            {
                var family=InfoTileCatalog.Family(selected.Key);
                foreach(string span in family.Spans)
                {
                    string token=family.Key+":"+span;
                    row.Children.Add(Chip(InfoTileCatalog.SpanLabel(token),()=>Resize(selected.Key,token),token==selected.Key));
                }
                var visible=Tiles();
                int at=Array.IndexOf(visible,selected.Key);
                var left=Chip("←",()=>Move(selected.Key,-1),false,"앞으로");
                var right=Chip("→",()=>Move(selected.Key,1),false,"뒤로");
                left.IsEnabled=at>0;
                right.IsEnabled=at>=0&&at<visible.Length-1;
                row.Children.Add(left);
                row.Children.Add(right);
            }
            else if(selected==null)
            {
                var hint=Paint.TextBlock("위젯을 눌러 선택하고 끌어서 옮기세요",13,Paint.ControlWhite);
                hint.VerticalAlignment=VerticalAlignment.Center;
                hint.Margin=new Thickness(10,0,10,0);
                row.Children.Add(hint);
            }
            row.Children.Add(Chip("위젯 추가",()=>OpenChooser(),false));
            row.Children.Add(Chip("완료",()=>EndEdit(),false));
            return new Border {Background=PanelFill,BorderBrush=PanelEdge,BorderThickness=new Thickness(.6),CornerRadius=new CornerRadius(22),Child=row};
        }
        // Everything that can be added: tiles not shown yet, then shortcut buttons while one is free.
        internal void OpenChooser()
        {
            CloseChooser();
            var stage=Stage;
            if(stage==null)return;
            var list=new StackPanel {Margin=new Thickness(14)};
            var title=Paint.TextBlock("추가할 위젯",16,Paint.ControlWhite);
            title.FontWeight=FontWeights.SemiBold;
            title.Margin=new Thickness(4,0,0,10);
            list.Children.Add(title);
            var used=Tiles().Select(InfoTileCatalog.FamilyKey).ToList();
            var grid=new System.Windows.Controls.Primitives.UniformGrid {Columns=2};
            foreach(var family in InfoTileCatalog.Families.Where(f=>!used.Contains(f.Key)))
            {
                var picked=family;
                bool needsPlace=(family.Key=="weather"||family.Key=="rain")&&!WeatherService.Configured;
                var button=Chip(family.Title+(needsPlace?" · 위치 필요":""),()=>{CloseChooser();AddTile(picked);},false,family.Title+" 추가");
                button.HorizontalContentAlignment=HorizontalAlignment.Left;
                button.IsEnabled=!needsPlace;
                button.Margin=new Thickness(2);
                grid.Children.Add(button);
            }
            list.Children.Add(grid);
            var actions=Actions().ToList();
            if(actions.Count<ActionLimit)
            {
                var sub=Paint.TextBlock("빠른 실행 버튼",13,Paint.Brush("#B3FFFFFF"));
                sub.Margin=new Thickness(4,14,0,8);
                list.Children.Add(sub);
                var buttons=new System.Windows.Controls.Primitives.UniformGrid {Columns=2};
                foreach(var spec in QuickActionCatalog.Specs.Where(s=>!actions.Contains(s[0])))
                {
                    string key=spec[0];
                    var button=Chip(spec[2],()=>{CloseChooser();AddAction(key);},false);
                    button.HorizontalContentAlignment=HorizontalAlignment.Left;
                    button.Margin=new Thickness(2);
                    buttons.Children.Add(button);
                }
                list.Children.Add(buttons);
            }
            double height=Math.Min(460,Math.Max(260,stage.ActualHeight-160));
            var scroll=new SmoothScrollViewer {Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            chooser=new Border {Width=440,MaxHeight=height,Background=PanelFill,BorderBrush=PanelEdge,BorderThickness=new Thickness(.6),CornerRadius=new CornerRadius(28),Child=scroll};
            Canvas.SetLeft(chooser,Math.Max(0,(stage.ActualWidth-440)/2));
            Canvas.SetTop(chooser,Math.Max(0,(stage.ActualHeight-height)/3));
            Panel.SetZIndex(chooser,30);
            stage.Children.Add(chooser);
        }
    }
}
