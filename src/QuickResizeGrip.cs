using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OneTaskBar
{
    internal sealed class QuickResizeGrip : Thumb
    {
        static readonly Geometry curve=Curve();
        static readonly Pen stroke=Stroke(3),focusStroke=Stroke(1);
        bool keyboardFocus;
        internal QuickResizeGrip()
        {
            Width=Height=32;Focusable=true;Cursor=Cursors.SizeNWSE;
            HorizontalAlignment=HorizontalAlignment.Right;VerticalAlignment=VerticalAlignment.Bottom;
            var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);
            Template=new ControlTemplate(typeof(Thumb)){VisualTree=border};
            IsKeyboardFocusedChanged+=(s,e)=>InvalidateVisual();
        }
        static Geometry Curve(){var result=Geometry.Parse("M 7,28 C 17,26 26,17 28,7");result.Freeze();return result;}
        static Pen Stroke(double width){var result=new Pen(Paint.ControlWhite,width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};result.Freeze();return result;}
        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);drawing.DrawRectangle(Brushes.Transparent,null,new Rect(RenderSize));drawing.DrawGeometry(null,stroke,curve);
            if(IsKeyboardFocused&&keyboardFocus)drawing.DrawRoundedRectangle(null,focusStroke,new Rect(1,1,30,30),7,7);
        }
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            keyboardFocus=Keyboard.IsKeyDown(Key.Tab);base.OnGotKeyboardFocus(e);InvalidateVisual();
        }
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            keyboardFocus=false;InvalidateVisual();base.OnPreviewMouseLeftButtonDown(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            keyboardFocus=true;InvalidateVisual();if(e.Key==Key.Escape&&IsDragging){CancelDrag();e.Handled=true;}base.OnKeyDown(e);
        }
    }
}
