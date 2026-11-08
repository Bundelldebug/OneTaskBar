using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OneTaskBar
{
    // The search that opens out of the Dynamic Island: a field on top and a few results under it.
    // It lists apps first and always offers the typed text as a web search. The island owns the size and the motion.
    internal sealed class IslandSearchView : Grid
    {
        internal const double HeaderHeight=52,RowHeight=36,MaxRows=5;
        internal readonly TextBox Query=new TextBox();
        readonly StackPanel rows=new StackPanel {Margin=new Thickness(8,0,8,8)};
        readonly Border divider=new Border {Height=1,Background=Paint.Brush("#24FFFFFF"),Margin=new Thickness(14,0,14,0),VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
        readonly TextBlock hint=Paint.TextBlock("검색",16,Paint.Brush("#80FFFFFF"));
        readonly Func<string,List<AppEntry>> find;
        readonly Action<AppEntry> launch;
        readonly Action<string> web;
        readonly Action close;
        readonly List<Button> buttons=new List<Button>();
        readonly List<Action> actions=new List<Action>();
        int selected;
        internal Action Changed;
        internal int RowCount {get{return actions.Count;}}
        internal double DesiredHeight {get{return actions.Count==0?HeaderHeight:HeaderHeight+actions.Count*RowHeight+8;}}
        internal IslandSearchView(Func<string,List<AppEntry>> find,Action<AppEntry> launch,Action<string> web,Action close)
        {
            this.find=find;this.launch=launch;this.web=web;this.close=close;
            RowDefinitions.Add(new RowDefinition {Height=new GridLength(HeaderHeight)});
            RowDefinitions.Add(new RowDefinition());
            var header=new Grid {Margin=new Thickness(18,0,18,0)};
            header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(30)});
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.Children.Add(new Glyph("search",18){Color=Paint.ControlWhite,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center});
            Query.Background=Brushes.Transparent;Query.BorderThickness=new Thickness(0);Query.Foreground=Paint.ControlWhite;Query.CaretBrush=Paint.ControlWhite;
            Query.FontFamily=SystemFonts.MessageFontFamily;Query.FontSize=16;Query.VerticalContentAlignment=VerticalAlignment.Center;Query.VerticalAlignment=VerticalAlignment.Center;
            Query.Padding=new Thickness(0);Query.FocusVisualStyle=null;
            hint.IsHitTestVisible=false;hint.VerticalAlignment=VerticalAlignment.Center;
            Grid.SetColumn(Query,1);Grid.SetColumn(hint,1);header.Children.Add(Query);header.Children.Add(hint);
            Children.Add(header);Children.Add(divider);
            Grid.SetRow(rows,1);Children.Add(rows);
            Query.TextChanged+=(s,e)=>{hint.Visibility=Query.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Reload();};
            Query.PreviewKeyDown+=OnKey;
        }
        void OnKey(object sender,KeyEventArgs e)
        {
            if(e.Key==Key.Escape){close();e.Handled=true;}
            else if(e.Key==Key.Down&&actions.Count>0){Select((selected+1)%actions.Count);e.Handled=true;}
            else if(e.Key==Key.Up&&actions.Count>0){Select((selected+actions.Count-1)%actions.Count);e.Handled=true;}
            else if(e.Key==Key.Enter&&actions.Count>0){actions[Math.Min(selected,actions.Count-1)]();e.Handled=true;}
        }
        internal void Reset()
        {
            Query.Text="";hint.Visibility=Visibility.Visible;Reload();
        }
        internal void Focus(){Query.Focus();Keyboard.Focus(Query);}
        void Select(int index)
        {
            selected=index;
            for(int i=0;i<buttons.Count;i++)buttons[i].Background=i==selected?Paint.Brush("#C42F7BF6"):Brushes.Transparent;
            divider.Visibility=actions.Count>0?Visibility.Visible:Visibility.Collapsed;
            divider.VerticalAlignment=VerticalAlignment.Top;divider.Margin=new Thickness(14,HeaderHeight-1,14,0);
        }
        void Reload()
        {
            rows.Children.Clear();buttons.Clear();actions.Clear();selected=0;
            string text=Query.Text.Trim();
            var apps=find(text)??new List<AppEntry>();
            foreach(var entry in apps)
            {
                var app=entry;
                var line=new StackPanel {Orientation=Orientation.Horizontal};
                FrameworkElement icon=AppIcons.Visual(app,22);icon.Margin=new Thickness(0,0,11,0);icon.VerticalAlignment=VerticalAlignment.Center;
                var name=Paint.TextBlock(app.Name,14,Paint.ControlWhite);name.TextTrimming=TextTrimming.CharacterEllipsis;name.MaxWidth=250;
                line.Children.Add(icon);line.Children.Add(name);
                Add(Kinded(line,"앱"),()=>launch(app),app.Name);
            }
            if(text.Length>0)
            {
                var line=new StackPanel {Orientation=Orientation.Horizontal};
                line.Children.Add(new Glyph("search",18){Color=Paint.ControlWhite,Margin=new Thickness(2,0,11,0),VerticalAlignment=VerticalAlignment.Center});
                var label=Paint.TextBlock("웹에서 "+text+" 검색",14,Paint.ControlWhite);label.TextTrimming=TextTrimming.CharacterEllipsis;label.MaxWidth=250;
                line.Children.Add(label);
                Add(Kinded(line,"웹"),()=>web(text),"웹 검색");
            }
            Select(0);
            var changed=Changed;if(changed!=null)changed();
        }
        static UIElement Kinded(UIElement line,string kind)
        {
            var grid=new Grid {Width=312};
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            var label=Paint.TextBlock(kind,12,Paint.Brush("#99FFFFFF"));label.VerticalAlignment=VerticalAlignment.Center;
            Grid.SetColumn(label,1);grid.Children.Add(line);grid.Children.Add(label);
            return grid;
        }
        void Add(UIElement content,Action action,string name)
        {
            if(actions.Count>=MaxRows)return;
            var button=Paint.Button(content,action,name);
            button.Height=RowHeight;button.Padding=new Thickness(12,0,12,0);button.Background=Brushes.Transparent;
            button.HorizontalContentAlignment=HorizontalAlignment.Left;button.VerticalContentAlignment=VerticalAlignment.Center;
            button.Resources["ButtonRadius"]=new CornerRadius(12);
            button.Focusable=false;
            rows.Children.Add(button);buttons.Add(button);actions.Add(action);
        }
    }
}
