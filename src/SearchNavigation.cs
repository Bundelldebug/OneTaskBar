using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OneTaskBar
{
    // Navigation follows the rendered rows, including asynchronously added files.
    internal sealed class SearchNavigation
    {
        sealed class Item {internal Button Button;internal Action Open;internal Brush Background,Border;internal Thickness Thickness;}
        readonly FrameworkElement owner;readonly TextBox query;readonly List<Item> items=new List<Item>();Item selected;
        internal bool Composing {get;private set;}
        internal Button Selected {get{return selected==null?null:selected.Button;}}
        internal int Count {get{return items.Count;}}
        internal SearchNavigation(FrameworkElement root,TextBox input)
        {
            owner=root;query=input;owner.PreviewKeyDown+=KeyDown;
            TextCompositionManager.AddPreviewTextInputStartHandler(query,(s,e)=>Composing=true);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(query,(s,e)=>Composing=true);
            TextCompositionManager.AddPreviewTextInputHandler(query,(s,e)=>Composing=false);
            query.GotKeyboardFocus+=(s,e)=>Select(null,false);
            query.LostKeyboardFocus+=(s,e)=>Composing=false;
        }
        internal void Add(Button button,Action open)
        {
            button.BorderThickness=new Thickness(1);button.BorderBrush=Brushes.Transparent;
            var item=new Item {Button=button,Open=open,Background=button.Background,Border=button.BorderBrush,Thickness=button.BorderThickness};items.Add(item);
            button.GotKeyboardFocus+=(s,e)=>Select(item,false);
        }
        internal void Clear(){Select(null,false);items.Clear();}
        void Select(Item item,bool focus)
        {
            if(selected!=item){if(selected!=null){selected.Button.Background=selected.Background;selected.Button.BorderBrush=selected.Border;selected.Button.BorderThickness=selected.Thickness;}
                selected=item;if(item!=null){item.Button.Background=Paint.Brush("#35FFFFFF");item.Button.BorderBrush=Paint.Brush("#A0FFFFFF");item.Button.BorderThickness=new Thickness(1);}}
            if(item!=null&&focus){item.Button.Focus();item.Button.BringIntoView();}
        }
        Rect Bounds(Item item){return new Rect(item.Button.TranslatePoint(new Point(),owner),item.Button.RenderSize);}
        void KeyDown(object sender,KeyEventArgs e)
        {
            if(Composing||e.Key==Key.ImeProcessed||Keyboard.Modifiers!=ModifierKeys.None)return;
            bool inQuery=query.IsKeyboardFocusWithin;var current=items.FirstOrDefault(i=>i.Button.IsKeyboardFocusWithin);
            if(!inQuery&&current==null)return;
            var visible=items.Where(i=>i.Button.IsVisible&&i.Button.IsEnabled&&i.Button.ActualWidth>0).ToList();if(visible.Count==0)return;
            if(e.Key==Key.Enter){(inQuery?visible[0]:current).Open();e.Handled=true;return;}
            if(e.Key!=Key.Up&&e.Key!=Key.Down&&e.Key!=Key.Left&&e.Key!=Key.Right)return;
            if(inQuery){if(e.Key==Key.Down){Select(visible[0],true);e.Handled=true;}return;}
            Rect from=Bounds(current);double x=from.Left+from.Width/2,y=from.Top+from.Height/2;bool horizontal=e.Key==Key.Left||e.Key==Key.Right,positive=e.Key==Key.Right||e.Key==Key.Down;
            Item best=null;double score=Double.MaxValue;
            foreach(var item in visible){if(item==current)continue;Rect bounds=Bounds(item);double dx=bounds.Left+bounds.Width/2-x,dy=bounds.Top+bounds.Height/2-y,along=horizontal?dx:dy,across=Math.Abs(horizontal?dy:dx);if((positive?along:-along)<2)continue;
                if(horizontal&&Math.Abs(dy)>(from.Height+bounds.Height)*.35)continue;
                double distance=Math.Abs(along)+across*3;if(distance<score){score=distance;best=item;}}
            if(best!=null)Select(best,true);else if(e.Key==Key.Up){query.Focus();query.CaretIndex=query.Text.Length;}e.Handled=true;
        }
    }
}
