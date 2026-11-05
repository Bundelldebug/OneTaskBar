using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OneTaskBar
{
    // Tiles below the clock of the collapsed fullscreen media screen.
    internal sealed partial class FullscreenTileStrip : Canvas
    {
        sealed class Entry
        {
            internal string Key;
            internal int Columns,Rows;
            internal FrameworkElement Host;
            internal InfoTile Tile;
        }
        readonly ShellController shell;
        readonly Action dismiss;
        readonly List<Entry> entries=new List<Entry>();
        string signature;
        TileScheme scheme=TileScheme.Unset;
        internal void Recolor(TileScheme next)
        {
            scheme=next;
            Sync();
        }
        internal int TileCount {get{return entries.Count;}}
        internal int VisibleCount {get{return entries.Count(e=>e.Host.Visibility==Visibility.Visible);}}
        internal double Scale {get;private set;}
        internal FullscreenTileStrip(ShellController controller,Action close)
        {
            shell=controller;
            dismiss=close;
            Scale=1;
        }
        static string[] Tiles()
        {
            return (Preferences.Current.FullscreenTiles??new string[0]).Where(InfoTileCatalog.Known).Where(k=>WeatherService.Configured||InfoTileCatalog.FamilyKey(k)!="weather"&&InfoTileCatalog.FamilyKey(k)!="rain").ToArray();
        }
        static string[] Actions()
        {
            return (Preferences.Current.FullscreenActions??new string[0]).Where(k=>QuickActionCatalog.Specs.Any(s=>s[0]==k)).ToArray();
        }
        // Rebuilds only when the chosen tiles change, so unrelated appearance changes keep their state.
        internal void Sync()
        {
            string next=scheme.Key+"|"+String.Join(",",Tiles())+"|"+String.Join(",",Actions())+"|"+editing;
            if(next==signature)return;
            signature=next;
            Retire();
            foreach(string key in Tiles())
            {
                var tile=InfoTileCatalog.Create(key,true,shell,scheme);
                var host=new Border {Background=scheme.Surface,CornerRadius=new CornerRadius(InfoTile.Radius(InfoTileCatalog.Columns(key),InfoTileCatalog.Rows(key))),Child=tile};
                Children.Add(host);
                var added=new Entry {Key=key,Columns=InfoTileCatalog.Columns(key),Rows=InfoTileCatalog.Rows(key),Host=host,Tile=tile};
                entries.Add(added);
                Hook(added);
            }
            foreach(string key in Actions())
            {
                var button=ActionButton(key);
                Children.Add(button);
                var action=new Entry {Key=key,Columns=1,Rows=1,Host=button};
                entries.Add(action);
                Hook(action);
            }
            if(editing)
            {
                var plus=PlusButton();
                Children.Add(plus);
                entries.Add(new Entry {Key=AddKey,Columns=1,Rows=1,Host=plus});
            }
        }
        Button ActionButton(string key)
        {
            var spec=QuickActionCatalog.Specs.First(s=>s[0]==key);
            var button=Paint.Button(new Glyph(spec[1],28){Color=scheme.Ink},()=>Invoke(key),spec[2]);
            button.Width=button.Height=InfoTile.Cell;
            button.Padding=new Thickness(0);
            button.Background=scheme.Surface;
            button.Resources["ButtonRadius"]=new CornerRadius(InfoTile.Cell/2);
            return button;
        }
        async void Invoke(string key)
        {
            try
            {
                if(QuickActionCatalog.Commands.Contains(key))dismiss();
                await shell.InvokeQuickAction(key);
            }
            catch(Exception e)
            {
                AppLog.Once("fullscreen-action",e.GetType().Name);
            }
        }
        // First-fit placement in two rows. A tile past the last column gets (-1,-1).
        internal static Point[] Place(int[] columns,int[] rows,int limit)
        {
            var used=new bool[Math.Max(0,limit),2];
            var result=new Point[columns.Length];
            for(int i=0;i<columns.Length;i++)
            {
                result[i]=new Point(-1,-1);
                double best=Double.MaxValue;
                for(int c=0;c+columns[i]<=limit;c++)
                {
                    for(int r=0;r+rows[i]<=2;r++)
                    {
                        // Prefer the top row, but fill a lower gap before opening a new column.
                        double rank=c+r*1.5;
                        if(rank>=best)continue;
                        bool free=true;
                        for(int x=c;x<c+columns[i];x++)
                        {
                            for(int y=r;y<r+rows[i];y++)free&=!used[x,y];
                        }
                        if(!free)continue;
                        best=rank;
                        result[i]=new Point(c,r);
                    }
                }
                if(result[i].X<0)continue;
                for(int x=(int)result[i].X;x<result[i].X+columns[i];x++)
                {
                    for(int y=(int)result[i].Y;y<result[i].Y+rows[i];y++)used[x,y]=true;
                }
            }
            // A one-row tile with nothing above or below it sits on the strip's center line.
            for(int i=0;i<columns.Length;i++)
            {
                if(result[i].X<0||rows[i]!=1)continue;
                int other=1-(int)result[i].Y;
                bool alone=true;
                for(int x=(int)result[i].X;x<result[i].X+columns[i];x++)alone&=!used[x,other];
                if(alone)result[i]=new Point(result[i].X,.5);
            }
            return result;
        }
        // Returns the scaled size, or an empty size when there is no usable room.
        internal Size Fit(double maxWidth,double maxHeight)
        {
            double full=InfoTile.Span(2);
            Scale=Math.Min(1,maxHeight/full);
            if(Scale<.6||entries.Count==0)return new Size(0,0);
            int limit=(int)((maxWidth/Scale+InfoTile.Gap)/(InfoTile.Cell+InfoTile.Gap));
            var cells=Place(entries.Select(e=>e.Columns).ToArray(),entries.Select(e=>e.Rows).ToArray(),limit);
            int width=0;
            for(int i=0;i<entries.Count;i++)
            {
                var entry=entries[i];
                bool fits=cells[i].X>=0;
                entry.Host.Visibility=fits?Visibility.Visible:Visibility.Collapsed;
                if(!fits)continue;
                entry.Host.Width=InfoTile.Span(entry.Columns);
                entry.Host.Height=InfoTile.Span(entry.Rows);
                SetLeft(entry.Host,cells[i].X*(InfoTile.Cell+InfoTile.Gap));
                SetTop(entry.Host,cells[i].Y*(InfoTile.Cell+InfoTile.Gap));
                width=Math.Max(width,(int)cells[i].X+entry.Columns);
            }
            Width=InfoTile.Span(Math.Max(1,width));
            Height=full;
            LayoutTransform=new ScaleTransform(Scale,Scale);
            return new Size(Width*Scale,full*Scale);
        }
        internal void Refresh(DateTime now)
        {
            foreach(var entry in entries)
            {
                if(entry.Tile!=null&&entry.Host.Visibility==Visibility.Visible)entry.Tile.Refresh(shell,now);
            }
        }
        internal InfoTile Find(string key)
        {
            var entry=entries.FirstOrDefault(e=>e.Key==key);
            return entry==null?null:entry.Tile;
        }
        internal void Retire()
        {
            CancelHold();
            pressed=null;
            foreach(var entry in entries)
            {
                if(entry.Tile!=null)entry.Tile.Retire();
            }
            entries.Clear();
            Children.Clear();
        }
    }

    // App shortcuts in the lower corners. Empty corners open the picker; right-click changes one.
    internal sealed class FullscreenAppCorners
    {
        internal const double Size=66;
        TileScheme scheme=TileScheme.Unset;
        internal void Recolor(TileScheme next)
        {
            scheme=next;
            Left.Background=Right.Background=next.Surface;
            leftShown=rightShown=null;
            appsShown=-1;
            Refresh();
        }
        readonly Canvas canvas;
        readonly ShellController shell;
        readonly Action dismiss;
        readonly Decorator leftIcon=new Decorator(),rightIcon=new Decorator();
        string leftShown,rightShown;
        int appsShown=-1;
        Border picker;
        StackPanel results;
        bool pickingLeft;
        internal readonly Button Left,Right;
        internal bool PickerOpen {get{return picker!=null;}}
        internal FullscreenAppCorners(Canvas surface,ShellController controller,Action close)
        {
            canvas=surface;
            shell=controller;
            dismiss=close;
            Left=Corner(leftIcon,true);
            Right=Corner(rightIcon,false);
        }
        Button Corner(Decorator icon,bool left)
        {
            var button=Paint.Button(icon,()=>Activate(left),"앱 바로가기 · 우클릭으로 변경");
            button.Width=button.Height=Size;
            button.Padding=new Thickness(0);
            button.Background=scheme.Surface;
            button.Resources["ButtonRadius"]=new CornerRadius(Size/2);
            button.MouseRightButtonUp+=(s,e)=>
            {
                OpenPicker(left);
                e.Handled=true;
            };
            canvas.Children.Add(button);
            return button;
        }
        static string Saved(bool left){return left?Preferences.Current.FullscreenAppLeft:Preferences.Current.FullscreenAppRight;}
        AppEntry Resolve(string key){return String.IsNullOrEmpty(key)?null:shell.Apps.FirstOrDefault(a=>a.Key==key);}
        void Activate(bool left)
        {
            var app=Resolve(Saved(left));
            if(app==null)
            {
                OpenPicker(left);
                return;
            }
            dismiss();
            shell.LaunchApp(app,ModifierKeys.None);
        }
        internal void Refresh()
        {
            string left=Saved(left:true),right=Saved(left:false);
            if(left==leftShown&&right==rightShown&&appsShown==shell.Apps.Count)return;
            leftShown=left;
            rightShown=right;
            appsShown=shell.Apps.Count;
            Show(leftIcon,Left,left);
            Show(rightIcon,Right,right);
        }
        void Show(Decorator icon,Button button,string key)
        {
            var app=Resolve(key);
            if(app!=null)icon.Child=AppIcons.Visual(app,38);
            else icon.Child=new Glyph("close",22){Color=scheme.Ink,RenderTransform=new RotateTransform(45,11,11)};
            string hint=app!=null?app.Name+" 열기 · 우클릭으로 변경":"바로가기 앱 선택";
            button.ToolTip=hint;
            System.Windows.Automation.AutomationProperties.SetName(button,hint);
        }
        internal void Place(double width,double height,bool visible)
        {
            var state=visible?Visibility.Visible:Visibility.Collapsed;
            Left.Visibility=Right.Visibility=state;
            if(!visible)
            {
                ClosePicker();
                return;
            }
            double top=height-24-MediaPlayerView.FullscreenPillHeight/2-Size/2;
            Canvas.SetLeft(Left,56);
            Canvas.SetTop(Left,top);
            Canvas.SetLeft(Right,width-56-Size);
            Canvas.SetTop(Right,top);
            Refresh();
            if(picker!=null)PlacePicker(width,height);
        }
        void PlacePicker(double width,double height)
        {
            picker.Height=Math.Min(520,Math.Max(240,height-120));
            Canvas.SetLeft(picker,Math.Max(0,(width-picker.Width)/2));
            Canvas.SetTop(picker,Math.Max(0,(height-picker.Height)/2));
        }
        internal void OpenPicker(bool left)
        {
            ClosePicker();
            pickingLeft=left;
            var root=new DockPanel {Margin=new Thickness(16)};
            var head=new Grid {Margin=new Thickness(0,0,0,10)};
            var title=Paint.TextBlock(left?"왼쪽 바로가기":"오른쪽 바로가기",16,Paint.ControlWhite);
            title.FontWeight=FontWeights.SemiBold;
            head.Children.Add(title);
            var close=Paint.Button(new Glyph("close",16){Color=Paint.ControlWhite},ClosePicker,"닫기");
            close.Width=close.Height=30;
            close.Padding=new Thickness(0);
            close.HorizontalAlignment=HorizontalAlignment.Right;
            close.Background=Brushes.Transparent;
            head.Children.Add(close);
            DockPanel.SetDock(head,Dock.Top);
            root.Children.Add(head);
            var search=new TextBox {Background=Paint.Brush("#30FFFFFF"),Foreground=Paint.ControlWhite,CaretBrush=Paint.ControlWhite,BorderThickness=new Thickness(0),FontSize=14,Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,0,10)};
            System.Windows.Automation.AutomationProperties.SetName(search,"앱 검색");
            search.TextChanged+=(s,e)=>ListApps(search.Text);
            DockPanel.SetDock(search,Dock.Top);
            root.Children.Add(search);
            var clear=Paint.Action("바로가기 비우기",()=>Choose(null));
            clear.Margin=new Thickness(0,10,0,0);
            clear.HorizontalAlignment=HorizontalAlignment.Left;
            DockPanel.SetDock(clear,Dock.Bottom);
            root.Children.Add(clear);
            results=new StackPanel();
            root.Children.Add(new SmoothScrollViewer {Content=results,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            picker=new Border {Width=380,CornerRadius=new CornerRadius(28),Background=Paint.Brush("#F2232A31"),BorderBrush=Paint.Brush("#35FFFFFF"),BorderThickness=new Thickness(.6),Child=root};
            picker.PreviewKeyDown+=(s,e)=>
            {
                if(e.Key!=Key.Escape)return;
                ClosePicker();
                e.Handled=true;
            };
            Panel.SetZIndex(picker,6);
            canvas.Children.Add(picker);
            PlacePicker(canvas.ActualWidth,canvas.ActualHeight);
            ListApps("");
            search.Focus();
        }
        void ListApps(string query)
        {
            if(results==null)return;
            results.Children.Clear();
            var apps=String.IsNullOrWhiteSpace(query)?shell.Apps.OrderBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).Take(200).ToList():LauncherSearch.Apps(shell.Apps,query.Trim());
            foreach(var app in apps)
            {
                var entry=app;
                var row=new DockPanel();
                var icon=AppIcons.Visual(entry,28);
                DockPanel.SetDock(icon,Dock.Left);
                row.Children.Add(icon);
                var name=Paint.TextBlock(entry.Name,13,Paint.ControlWhite);
                name.Margin=new Thickness(10,0,0,0);
                name.TextTrimming=TextTrimming.CharacterEllipsis;
                row.Children.Add(name);
                var button=Paint.Button(row,()=>Choose(entry.Key),entry.Name);
                button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
                button.Background=Brushes.Transparent;
                button.Padding=new Thickness(8,6,8,6);
                results.Children.Add(button);
            }
        }
        internal void Choose(string key)
        {
            bool left=pickingLeft;
            ClosePicker();
            Preferences.SetMediaAppearance(p=>
            {
                if(left)p.FullscreenAppLeft=key;
                else p.FullscreenAppRight=key;
            });
            Refresh();
        }
        internal void ClosePicker()
        {
            if(picker==null)return;
            canvas.Children.Remove(picker);
            picker=null;
            results=null;
        }
    }
}
